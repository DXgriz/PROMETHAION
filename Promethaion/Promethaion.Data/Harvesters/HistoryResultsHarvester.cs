using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Promethaion.Core.Entities;
using Promethaion.Core.Interfaces;
using Promethaion.Data.Repositories;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Promethaion.Data.Harvesters;

/// <summary>
/// Scrapes https://www.lotteryresults.co.za and normalises
/// every SA Lotto draw into a clean PatternEvent record.
/// Deduplicates by DrawDate — never by DrawNumber — to avoid
/// the sequence-reset bug where numbers collide across months.
/// </summary>
public class HistoryResultsHarvester : IDataHarvester
{
    #region Fields & Constructor
    private const string BaseUrl = "https://www.lotteryresults.co.za";
    private const string ArchiveUrl = "/lotto/archive";

    private readonly HttpClient _http;
    private readonly IPatterneventRepository _repo;
    private readonly ILogger<HistoryResultsHarvester> _log;
    private readonly PAionDbContext _context;

    public string SourceName => "lotteryresults.co.za";

    public HistoryResultsHarvester(
        HttpClient http,
        IPatterneventRepository repo,
        ILogger<HistoryResultsHarvester> log,
        PAionDbContext context)
    {
        _http = http;
        _repo = repo;
        _log = log;
        _context = context;
    }
    #endregion

    #region Public API

    public async Task<HarvestResult> SyncAsync(
        string gameName = "SA Lotto",
        CancellationToken ct = default)
    {
        _log.LogInformation("[Harvester] Full sync started from {Source}", SourceName);
        var monthUrls = await GetAllMonthUrlsAsync(ct);
        _log.LogInformation("[Harvester] Found {Count} monthly pages.", monthUrls.Count);
        return await ProcessMonthsAsync(monthUrls, gameName, ct);
    }


    public async Task<HarvestResult> SyncLatestAsync(
    string gameName = "SA Lotto",
    CancellationToken ct = default)
    {
        if (!(await _repo.GetAllAsync(gameName)).Any())
        {
            _log.LogInformation("[Harvester] DB empty → running full sync");
            return await SyncAsync(gameName, ct);
        }

        _log.LogInformation("[Harvester] Smart latest-sync started.");

        var now = DateTime.Now;

        var validUrls = new List<string>();
        int maxLookbackMonths = 6;

        for (int i = 0; i < maxLookbackMonths; i++)
        {
            var date = now.AddMonths(-i);
            var url = BuildMonthUrl(date.Year, date.Month);

            var html = await FetchHtmlAsync(url, ct);

            if (html != null && html.Length > 1000)
            {
                _log.LogInformation("[Harvester] Found valid page: {Url}", url);
                validUrls.Add(url);

                if (validUrls.Count >= 2)
                    break;
            }
            else
            {
                _log.LogWarning("[Harvester] Skipping invalid month: {Url}", url);
            }
        }

        if (validUrls.Count == 0)
        {
            _log.LogWarning("[Harvester] No valid recent months found.");
            return new HarvestResult(0, 0, 0, SourceName, DateTime.Now,
                new List<string> { "No valid months found" });
        }

        return await ProcessMonthsAsync(validUrls, gameName, ct);
    }
    #endregion

    #region Core pipeline

    private async Task<List<string>> GetAllMonthUrlsAsync(CancellationToken ct)
    {
        var html = await FetchHtmlAsync(BaseUrl + ArchiveUrl, ct);
        if (html is null) return [];

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var links = doc.DocumentNode
            .SelectNodes("//a[contains(@href,'/lotto/') and contains(@href,'-20')]")
            ?.Select(a => a.GetAttributeValue("href", ""))
            .Where(h => !string.IsNullOrWhiteSpace(h)
                     && !h.Contains("archive")
                     && !h.Contains("results-"))
            .Distinct()
            .Select(h => h.StartsWith("http") ? h : BaseUrl + h)
            .ToList() ?? [];

        // Sort oldest-first so draw numbers are assigned in chronological order.
        return links.OrderBy(u => u).ToList();
    }

    private async Task<HarvestResult> ProcessMonthsAsync(
    List<string> monthUrls,
    string gameName,
    CancellationToken ct)
    {
        int newRecords = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

        var existingEvents = await _repo.GetAllAsync(gameName);
        var existingDates = existingEvents
            .Select(e => e.DrawDate.Date)
            .ToHashSet();

        int drawSequence = existingEvents.Count > 0
            ? existingEvents.Max(e => e.DrawNumber)
            : 0;

        _log.LogInformation("[Harvester] DB has {Count} records. Starting from draw #{Seq}.",
            existingEvents.Count, drawSequence);

        foreach (var url in monthUrls)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // ✅ Unpack tuple — drawSequence carries forward across all months
                var (draws, updatedSequence) = await ScrapeMonthPageAsync(url, gameName, drawSequence, ct);
                drawSequence = updatedSequence;

                foreach (var draw in draws)
                {
                    if (existingDates.Contains(draw.DrawDate.Date))
                    {
                        skipped++;
                        continue;
                    }

                    await _repo.AddAsync(draw);
                    existingDates.Add(draw.DrawDate.Date);
                    newRecords++;

                    _log.LogDebug("[Harvester] Saved draw #{Num} — {Date}",
                        draw.DrawNumber, draw.DrawDate.ToString("yyyy-MM-dd"));
                }
            }
            catch (Exception ex)
            {
                failed++;
                var msg = $"Failed to process {url}: {ex.Message}";
                errors.Add(msg);
                _log.LogWarning(msg);
            }

            await Task.Delay(350, ct);
        }

        _log.LogInformation(
            "[Harvester] Done. New={N} Skipped={S} Failed={F}",
            newRecords, skipped, failed);

        return new HarvestResult(newRecords, skipped, failed, SourceName, DateTime.Now, errors);
    }

    /// <summary>
    /// Scrapes one monthly page.
    /// drawSequence is ref — it persists across all calls from ProcessMonthsAsync.
    /// </summary>
    private async Task<(List<PatternEvent> Events, int UpdatedSequence)> ScrapeMonthPageAsync(
    string url,
    string gameName,
    int drawSequence,
    CancellationToken ct)
    {
        var json = await FetchHtmlWithBrowser(url);

        if (string.IsNullOrWhiteSpace(json))
            return (new List<PatternEvent>(), drawSequence);

        var rawData = JsonSerializer.Deserialize<List<DrawDto>>(json);

        if (rawData == null || rawData.Count == 0)
        {
            _log.LogWarning("❌ No draw data found");
            return (new List<PatternEvent>(), drawSequence);
        }

        var results = new List<PatternEvent>();

        foreach (var draw in rawData)
        {
            try
            {
                if (draw.Numbers == null || draw.Numbers.Count < 7)
                    continue;

                if (string.IsNullOrWhiteSpace(draw.DateText))
                    continue;

                if (!TryParseDrawDate(draw.DateText, out var drawDate))
                {
                    _log.LogDebug("⏩ Invalid date format: {Date}", draw.DateText);
                    continue;
                }

                var values = draw.Numbers;

                if (values.Distinct().Count() < 5)
                    continue;

                if (values.Max() < 20)
                    continue;

                var mainBalls = values.Take(6).OrderBy(x => x).ToArray();
                int bonusBall = values[6];

                bool exists = await _context.DrawResults.AnyAsync(d =>
                    d.GameName == gameName &&
                    d.DrawDate.Date == drawDate.Date,
                    ct);

                if (exists)
                {
                    _log.LogDebug("⏩ Already exists: {Date}", drawDate);
                    continue;
                }

                drawSequence++;

                var evt = new PatternEvent
                {
                    DrawNumber = drawSequence,
                    DrawDate = drawDate,
                    Ball1 = mainBalls[0],
                    Ball2 = mainBalls[1],
                    Ball3 = mainBalls[2],
                    Ball4 = mainBalls[3],
                    Ball5 = mainBalls[4],
                    Ball6 = mainBalls[5],
                    BonusBall = bonusBall,
                    GameName = gameName
                };

                results.Add(evt);

                _log.LogInformation(
                    $"✅ REAL Draw #{drawSequence}: {string.Join(",", values)} ({drawDate:yyyy-MM-dd})");
            }
            catch (Exception ex)
            {
                _log.LogDebug("Skip draw error: {Err}", ex.Message);
            }
        }

        return (results, drawSequence);
    }
    #endregion

    #region Helpers

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Accept", "text/html,application/xhtml+xml");
            request.Headers.Add("Accept-Language", "en-ZA,en;q=0.9");

            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Harvester] Failed to fetch {Url}: {Err}", url, ex.Message);
            return null;
        }
    }


    private async Task<string?> FetchHtmlWithBrowser(string url)
    {
        using var playwright = await Playwright.CreateAsync();

        await using var browser = await playwright.Chromium.LaunchAsync(new()
        {
            Headless = true,
            Channel = "chrome",
            Args = new[]
            {
            "--disable-blink-features=AutomationControlled",
            "--no-sandbox",
            "--disable-dev-shm-usage"
        }
        });

        var context = await browser.NewContextAsync(new()
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36"
        });

        var page = await context.NewPageAsync();

        await page.GotoAsync(url, new()
        {
            WaitUntil = WaitUntilState.NetworkIdle
        });

        await page.WaitForSelectorAsync("body");

        await page.WaitForTimeoutAsync(2000);

        var data = await page.EvaluateAsync<object>(@"
                () => {
                        const draws = [];
                        const seen = new Set();

                        const blocks = document.querySelectorAll(""div"");

                        blocks.forEach(block => {
                            const text = block.innerText;

                            if (!text) return;

                            const dateMatch = text.match(/\b\d{1,2}\s+[A-Za-z]+\s+\d{4}\b/);
                            if (!dateMatch) return;

                            const dateText = dateMatch[0];

                            const matches = text.match(/\b\d{1,2}\b/g);
                            if (!matches || matches.length < 7) return;

                            const nums = matches
                                .map(n => parseInt(n))
                                .filter(n => n >= 1 && n <= 52);

                            if (nums.length < 7) return;

                            const drawKey = dateText + ""-"" + nums.slice(0,7).join("","");

                            if (seen.has(drawKey)) return;
                            seen.add(drawKey);

                            draws.push({
                                DateText: dateText,
                                Numbers: nums.slice(0, 7)
                            });
                        });

                        return draws;
                        }

        ");

        return JsonSerializer.Serialize(data);
    }


    private static bool TryParseDrawDate(string text, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Strip day-of-week: "Wednesday 29 January 2025" → "29 January 2025"
        var cleaned = Regex.Replace(text, @"^[A-Za-z]+\s+", "").Trim();

        var formats = new[]
        {
            "dd MMMM yyyy",
            "d MMMM yyyy",
            "dddd dd MMMM yyyy",
            "dddd d MMMM yyyy",
            "dd-MM-yyyy",
            "yyyy-MM-dd"
        };

        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(
                    cleaned,
                    fmt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out date))
                return true;

            if (DateTime.TryParseExact(
                    text,
                    fmt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out date))
                return true;
        }

        // Last resort: let the runtime try
        return DateTime.TryParse(cleaned, out date) || DateTime.TryParse(text, out date);
    }

    private static string BuildMonthUrl(int year, int month)
    {
        var name = new DateTime(year, month, 1)
            .ToString("MMMM")
            .ToLowerInvariant();
        return $"{BaseUrl}/lotto/{name}-{year}";
    }
    #endregion

    private class DrawDto
    {
        public string DateText { get; set; } = "";
        public List<int> Numbers { get; set; } = new();
    }
}


using Microsoft.Extensions.Logging;
using Promethaion.Core.Entities;
using Promethaion.Core.Interfaces;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace Promethaion.Data.Harvesters;
/// <summary>
/// Scrapes https://www.lotteryresults.co.za and normalises
/// every SA Lotto draw into a clean PatternEvent record.
///
/// Source structure (per monthly page):
///   Heading : "Wednesday 29 January 2025"
///   7 balls : first 6 = main balls, 7th = bonus ball
///
/// URL pattern:
///   Archive : /lotto/archive
///   Monthly : /lotto/{month-name}-{year}   e.g. /lotto/january-2025
/// </summary>
public class HistoryResultsHarvester : IDataHarvester
{
    private const string BaseUrl = "https://www.lotteryresults.co.za";
    private const string ArchiveUrl = "/lotto/archive";

    private readonly HttpClient _http;
    private readonly IPatterneventRepository _repo;
    private readonly ILogger<HistoryResultsHarvester> _log;

    public string SourceName => "lotteryresults.co.za";

    public HistoryResultsHarvester(
        HttpClient http,
        IPatterneventRepository repo,
        ILogger<HistoryResultsHarvester> log)
    {
        _http = http;
        _repo = repo;
        _log = log;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<HarvestResult> SyncAsync(
        string gameName = "SA Lotto",
        CancellationToken ct = default)
    {
        _log.LogInformation("[Harvester] Full sync started from {Source}", SourceName);

        var monthUrls = await GetAllMonthUrlsAsync(ct);
        _log.LogInformation("[Harvester] Found {Count} monthly pages to process.", monthUrls.Count);

        return await ProcessMonthsAsync(monthUrls, gameName, ct);
    }

    public async Task<HarvestResult> SyncLatestAsync(
        string gameName = "SA Lotto",
        CancellationToken ct = default)
    {
        _log.LogInformation("[Harvester] Latest-only sync started.");

        // Only fetch the current month and the previous month
        // in case we are near a month boundary.
        var now = DateTime.UtcNow;
        var urls = new List<string>
        {
            BuildMonthUrl(now.Year, now.Month),
            BuildMonthUrl(now.Year, now.Month == 1 ? 12 : now.Month - 1)
        };

        return await ProcessMonthsAsync(urls, gameName, ct);
    }

    // ── Internals ─────────────────────────────────────────────────────────────

    /// <summary>Reads the archive page and collects all monthly URLs.</summary>
    private async Task<List<string>> GetAllMonthUrlsAsync(CancellationToken ct)
    {
        var html = await FetchHtmlAsync(BaseUrl + ArchiveUrl, ct);
        if (html is null) return [];

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // All links inside the archive list that match /lotto/{month}-{year}
        var links = doc.DocumentNode
            .SelectNodes("//a[contains(@href,'/lotto/') and contains(@href,'-20')]")
            ?.Select(a => a.GetAttributeValue("href", ""))
            .Where(href => !string.IsNullOrWhiteSpace(href)
                        && !href.Contains("archive")
                        && !href.Contains("results-"))
            .Distinct()
            .Select(href => href.StartsWith("http") ? href : BaseUrl + href)
            .ToList() ?? [];

        return links;
    }

    /// <summary>Processes a list of monthly page URLs and persists new draws.</summary>
    private async Task<HarvestResult> ProcessMonthsAsync(
        List<string> monthUrls,
        string gameName,
        CancellationToken ct)
    {
        int newRecords = 0, skipped = 0, failed = 0;
        var errors = new List<string>();

        // Get the highest draw number already in the DB to avoid re-processing.
        int maxKnown = await _repo.GetMaxDrawNumberAsync(gameName);

        foreach (var url in monthUrls)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var draws = await ScrapeMonthPageAsync(url, gameName, ct);

                foreach (var draw in draws)
                {
                    // We assign draw numbers by date order — check if date already stored.
                    bool exists = (await _repo.GetByDrawNumberAsync(draw.DrawNumber, gameName)) is not null;
                    if (exists) { skipped++; continue; }

                    await _repo.AddAsync(draw);
                    newRecords++;

                    _log.LogDebug("[Harvester] Saved draw #{Num} on {Date}",
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

            // Be polite — small delay between page requests.
            await Task.Delay(400, ct);
        }

        _log.LogInformation(
            "[Harvester] Sync complete. New={New} Skipped={Skip} Failed={Fail}",
            newRecords, skipped, failed);

        return new HarvestResult(newRecords, skipped, failed, SourceName, DateTime.UtcNow, errors);
    }

    /// <summary>Scrapes a single monthly results page into PatternEvent records.</summary>
    private async Task<List<PatternEvent>> ScrapeMonthPageAsync(
        string url, string gameName, CancellationToken ct)
    {
        var html = await FetchHtmlAsync(url, ct);
        if (html is null) return [];

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var results = new List<PatternEvent>();

        // Each draw is represented as a heading (h2/h3) with date text
        // followed by a list of ball numbers.
        // lotteryresults.co.za renders them as:
        //   <h3><a href="/lotto/results-...">Wednesday 29 January 2025</a></h3>
        //   <ul class="balls"> <li>6</li><li>24</li>...<li>14</li> </ul>

        var drawHeadings = doc.DocumentNode
            .SelectNodes("//h2[contains(@class,'draw') or a[contains(@href,'results-')]] | //h3[a[contains(@href,'results-')]]");

        if (drawHeadings is null || drawHeadings.Count == 0)
        {
            // Fallback: try any heading containing a date-like link
            drawHeadings = doc.DocumentNode
                .SelectNodes("//*[self::h2 or self::h3][.//a[contains(@href,'/lotto/results-')]]");
        }

        if (drawHeadings is null) return results;

        int drawSequence = await _repo.GetMaxDrawNumberAsync(gameName);

        foreach (var heading in drawHeadings)
        {
            try
            {
                // Parse date from heading text e.g. "Wednesday 29 January 2025"
                var dateText = HtmlEntity.DeEntitize(heading.InnerText).Trim();
                if (!TryParseDrawDate(dateText, out var drawDate)) continue;

                // Ball numbers are in the next sibling node containing <li> elements
                var ballContainer = heading.SelectSingleNode(
                    "following-sibling::ul[1] | following-sibling::div[contains(@class,'ball')][1] | following-sibling::ol[1]");

                if (ballContainer is null)
                {
                    // Try parent's next sibling
                    ballContainer = heading.ParentNode?.SelectSingleNode(
                        "following-sibling::*[.//li or .//span[contains(@class,'ball')]][1]");
                }

                if (ballContainer is null) continue;

                var ballNodes = ballContainer.SelectNodes(".//li | .//span[contains(@class,'ball')]");
                if (ballNodes is null || ballNodes.Count < 7) continue;

                var allBalls = ballNodes
                    .Select(n => n.InnerText.Trim())
                    .Where(t => int.TryParse(t, out _))
                    .Select(int.Parse)
                    .ToArray();

                if (allBalls.Length < 7) continue;

                var mainBalls = allBalls.Take(6).OrderBy(x => x).ToArray();
                int bonusBall = allBalls[6];

                drawSequence++;

                results.Add(new PatternEvent
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
                });
            }
            catch (Exception ex)
            {
                _log.LogDebug("[Harvester] Skipping malformed draw entry: {Err}", ex.Message);
            }
        }

        // Sort oldest-first so draw numbers are assigned chronologically.
        return results.OrderBy(r => r.DrawDate).ToList();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string?> FetchHtmlAsync(string url, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[Harvester] Failed to fetch {Url}: {Err}", url, ex.Message);
            return null;
        }
    }

    private static bool TryParseDrawDate(string text, out DateTime date)
    {
        // Handles formats:
        //   "Wednesday 29 January 2025"
        //   "Saturday 11 March 2000"
        var formats = new[]
        {
            "dddd dd MMMM yyyy",
            "dddd d MMMM yyyy",
            "dd MMMM yyyy",
            "d MMMM yyyy"
        };

        // Strip day-of-week prefix if present (split on first space-digit boundary)
        var cleaned = System.Text.RegularExpressions.Regex.Replace(text, @"^[A-Za-z]+\s+", "").Trim();

        foreach (var fmt in new[] { "dd MMMM yyyy", "d MMMM yyyy" })
        {
            if (DateTime.TryParseExact(cleaned,
                    fmt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out date))
                return true;
        }

        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(text,
                    fmt,
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out date))
                return true;
        }

        date = default;
        return false;
    }

    private static string BuildMonthUrl(int year, int month)
    {
        var monthName = new DateTime(year, month, 1).ToString("MMMM").ToLowerInvariant();
        return $"{BaseUrl}/lotto/{monthName}-{year}";
    }
}
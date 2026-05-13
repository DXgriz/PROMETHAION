using Promethaion.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Promethaion.API.BackgroundServices;

/// <summary>
/// Hosted background service that:
///   1. On startup: runs a full historical sync if the DB has fewer than 50 records.
///   2. Every Wednesday and Saturday at 22:00 SAST: syncs the latest month
///      to pick up new draws automatically after they are published.
/// </summary>
public class HarvestBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HarvestBackgroundService> _log;

    // SA Lotto draws on Wednesdays and Saturdays.
    private static readonly DayOfWeek[] DrawDays = [DayOfWeek.Wednesday, DayOfWeek.Saturday];

    public HarvestBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<HarvestBackgroundService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Allow the web server to finish starting up first.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        await RunStartupSyncAsync(stoppingToken);

        // Poll every 30 minutes and check if it's a draw day past 22:00 SAST.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CheckScheduledSyncAsync(stoppingToken);
        }
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private async Task RunStartupSyncAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPatterneventRepository>();
        var harvester = scope.ServiceProvider.GetRequiredService<IDataHarvester>();

        int existing = await repo.CountAsync("SA Lotto");

        if (existing < 50)
        {
            _log.LogInformation(
                "[HarvestService] Only {Count} records in DB — running full historical sync.",
                existing);

            var result = await harvester.SyncAsync("SA Lotto", ct);
            LogResult(result);
        }
        else
        {
            _log.LogInformation(
                "[HarvestService] DB has {Count} records — skipping full sync, running latest only.",
                existing);

            var result = await harvester.SyncLatestAsync("SA Lotto", ct);
            LogResult(result);
        }
    }

    // ── Scheduled ─────────────────────────────────────────────────────────────

    private async Task CheckScheduledSyncAsync(CancellationToken ct)
    {
        // Convert UTC to SAST (UTC+2).
        var sast = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow,
            TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time"));

        bool isDrawDay = DrawDays.Contains(sast.DayOfWeek);
        bool isAfter22 = sast.Hour >= 22;

        if (!isDrawDay || !isAfter22) return;

        // Avoid double-syncing the same day — check if we already synced today.
        if (_lastSyncDate == sast.Date) return;

        _log.LogInformation(
            "[HarvestService] Scheduled sync triggered ({Day} {Time} SAST)",
            sast.DayOfWeek, sast.ToString("HH:mm"));

        using var scope = _scopeFactory.CreateScope();
        var harvester = scope.ServiceProvider.GetRequiredService<IDataHarvester>();

        var result = await harvester.SyncLatestAsync("SA Lotto", ct);
        LogResult(result);

        _lastSyncDate = sast.Date;
    }

    private DateTime _lastSyncDate = DateTime.MinValue.Date;

    private void LogResult(HarvestResult r) =>
        _log.LogInformation(
            "[HarvestService] Harvest complete — New: {New}, Skipped: {Skip}, Failed: {Fail}",
            r.NewRecords, r.SkippedDuplicates, r.FailedRows);
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promethaion.Core.Interfaces;
using Promethaion.Data.Repositories;

namespace Promethaion.Data.Harvesters;

/// <summary>
/// On startup: full sync if DB has fewer than 50 records, else latest-only.
/// On schedule: syncs every Wednesday and Saturday after 21:00 SAST,
/// then raises OnHarvestCompleted so TrainingBackgroundService retrains.
/// </summary>
public class HarvestBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HarvestBackgroundService> _log;

    private static readonly DayOfWeek[] DrawDays =
        [DayOfWeek.Wednesday, DayOfWeek.Saturday];

    private DateTime _lastSyncDate = DateTime.MinValue.Date;

    /// <summary>
    /// Raised after a successful harvest that produced new records.
    /// TrainingBackgroundService subscribes to this to trigger retraining.
    /// </summary>
    public event Func<int, Task>? OnHarvestCompleted;

    public HarvestBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<HarvestBackgroundService> log)
    {
        _scopeFactory = scopeFactory;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(8), ct);
        await RunStartupSyncAsync(ct);

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));
        while (await timer.WaitForNextTickAsync(ct))
            await CheckScheduledSyncAsync(ct);
    }

    private async Task RunStartupSyncAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IPatterneventRepository>();
        var harvester = scope.ServiceProvider.GetRequiredService<IDataHarvester>();

        int existing = await repo.CountAsync("SA Lotto");
        _log.LogInformation("[HarvestService] DB has {Count} records.", existing);

        var result = existing < 50
            ? await harvester.SyncAsync("SA Lotto", ct)
            : await harvester.SyncLatestAsync("SA Lotto", ct);

        LogResult(result);

        if (result.NewRecords > 0)
            await RaiseHarvestCompleted(result.NewRecords);
    }

    private async Task CheckScheduledSyncAsync(CancellationToken ct)
    {
        var sast = ToSast(DateTime.UtcNow);
        if (!DrawDays.Contains(sast.DayOfWeek)) return;
        if (sast.Hour < 21) return;
        if (_lastSyncDate == sast.Date) return;

        _log.LogInformation("[HarvestService] Scheduled sync — {Day} {Time} SAST",
            sast.DayOfWeek, sast.ToString("HH:mm"));

        using var scope = _scopeFactory.CreateScope();
        var harvester = scope.ServiceProvider.GetRequiredService<IDataHarvester>();
        var result = await harvester.SyncLatestAsync("SA Lotto", ct);

        LogResult(result);
        _lastSyncDate = sast.Date;

        if (result.NewRecords > 0)
            await RaiseHarvestCompleted(result.NewRecords);
    }

    private async Task RaiseHarvestCompleted(int newRecords)
    {
        if (OnHarvestCompleted is not null)
        {
            _log.LogInformation("[HarvestService] Signalling training — {N} new records.", newRecords);
            await OnHarvestCompleted(newRecords);
        }
    }

    private void LogResult(HarvestResult r) =>
        _log.LogInformation(
            "[HarvestService] New={N}  Skipped={S}  Failed={F}",
            r.NewRecords, r.SkippedDuplicates, r.FailedRows);

    private static DateTime ToSast(DateTime utc)
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(utc,
                TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time"));
        }
        catch { return utc.AddHours(2); }
    }
}

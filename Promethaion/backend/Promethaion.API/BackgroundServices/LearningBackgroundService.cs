using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promethaion.API.Hubs;
using Promethaion.Core.Interfaces;
using Promethaion.ML.Services;

namespace Promethaion.API.BackgroundServices;

/// <summary>
/// Handles all ML model training.
///
/// Triggered three ways:
///   1. Via OnHarvestCompleted event (wired in Program.cs) — fires after
///      new draw data is scraped, training immediately on fresh data.
///   2. Via hourly self-awareness check — retrains if rolling accuracy
///      has degraded below the configured threshold.
///   3. Via RunTrainingCycleAsync() called directly from the API controller
///      when the user clicks "Train Models" in the UI.
///
/// Broadcasts progress to the Blazor Intelligence page via SignalR
/// using IHubContext so messages reach clients from outside the hub.
/// </summary>
public class LearningBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LearningBackgroundService> _log;
    private readonly IHubContext<LearningHub> _hub;

    // Prevents two training runs from overlapping.
    // WaitAsync(0) = non-blocking — if already training, skip silently.
    private readonly SemaphoreSlim _trainLock = new(1, 1);

    public LearningBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<LearningBackgroundService> log,
        IHubContext<LearningHub> hub)
    {
        _scopeFactory = scopeFactory;
        _log = log;
        _hub = hub;
    }

    // ── IHostedService ────────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Hourly self-awareness check — retrains if accuracy has drifted.
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(ct))
            await CheckSelfAwarenessAsync("SA Lotto", ct);
    }

    // ── Public API (called from Program.cs event wire + API controller) ───────

    /// <summary>
    /// Runs a full training cycle across all three pipelines.
    /// Broadcasts progress to all connected Blazor clients via SignalR.
    /// </summary>
    public async Task RunTrainingCycleAsync(string gameName, CancellationToken ct)
    {
        // Non-blocking lock — if a training run is already in progress, skip.
        if (!await _trainLock.WaitAsync(0))
        {
            _log.LogWarning("[LearningService] Training already in progress — skipping duplicate request.");
            return;
        }

        try
        {
            _log.LogInformation("[LearningService] Training cycle started for {Game}.", gameName);

            // Notify all connected Blazor clients that training has started.
            await _hub.Clients.All.SendAsync("TrainingStarted", gameName, ct);

            using var scope = _scopeFactory.CreateScope();
            var draws = scope.ServiceProvider.GetRequiredService<IPatterneventRepository>();
            var trainer = scope.ServiceProvider.GetRequiredService<PipelineTrainer>();

            var history = await draws.GetAllAsync(gameName);

            if (history.Count < 20)
            {
                var msg = $"Only {history.Count} records in DB — need at least 20 to train.";
                _log.LogWarning("[LearningService] {Msg}", msg);
                await _hub.Clients.All.SendAsync("TrainingFailed", msg, ct);
                return;
            }

            _log.LogInformation("[LearningService] Training on {Count} draw records.", history.Count);

            await _hub.Clients.All.SendAsync(
                "PipelineProgress", "All", 5,
                $"Starting — {history.Count} records loaded.", ct);

            var metrics = await trainer.TrainAllAsync(history, ct);

            // Broadcast results to the Intelligence page.
            await _hub.Clients.All.SendAsync("TrainingCompleted",
                metrics.Select(m => new
                {
                    m.PipelineName,
                    m.ModelVersion,
                    m.RSquared,
                    m.MeanAbsoluteError,
                    m.RootMeanSquaredError,
                    m.IsBestVersion
                }), ct);

            _log.LogInformation(
                "[LearningService] Training complete — {Count} pipelines trained.", metrics.Count);
        }
        catch (OperationCanceledException)
        {
            _log.LogInformation("[LearningService] Training cancelled.");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[LearningService] Training failed.");
            await _hub.Clients.All.SendAsync("TrainingFailed", ex.Message, ct);
        }
        finally
        {
            _trainLock.Release();
        }
    }

    // ── Self-awareness check ──────────────────────────────────────────────────

    private async Task CheckSelfAwarenessAsync(string gameName, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var awareness = scope.ServiceProvider
            .GetRequiredService<IAdaptiveIntelligenceEngine>();

        bool shouldRetrain = await awareness.ShouldRetrainAsync();
        if (shouldRetrain)
        {
            _log.LogInformation(
                "[LearningService] Self-awareness engine triggered retrain for {Game}.", gameName);
            await RunTrainingCycleAsync(gameName, ct);
        }
    }
}

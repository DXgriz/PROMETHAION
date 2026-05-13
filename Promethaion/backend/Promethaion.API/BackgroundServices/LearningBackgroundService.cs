using Promethaion.Core.Interfaces;
using Promethaion.ML;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Promethaion.ML.Services;
using Promethaion.API.Hubs;

namespace Promethaion.API.BackgroundServices
{
    /// <summary>
    /// Hosted background service that:
    ///   1. Performs an initial training run on startup (if models not loaded).
    ///   2. Checks the self-awareness engine every hour to see if a retrain is needed.
    ///   3. Broadcasts progress via SignalR.
    /// </summary>
    public class LearningBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LearningBackgroundService> _logger;
        private readonly IHubContext<LearningHub> _hub;

        // How often to poll the self-awareness engine.
        private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

        public LearningBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<LearningBackgroundService> logger,
            IHubContext<LearningHub> hub)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Brief startup delay to let the web server initialise.
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            // Initial training on startup.
            await RunTrainingCycleAsync("SA Lotto", stoppingToken);

            // Periodic self-awareness check.
            using var timer = new PeriodicTimer(PollInterval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckAndRetrainAsync("SA Lotto", stoppingToken);
            }
        }

        /// <summary>Run a full training cycle unconditionally.</summary>
        public async Task RunTrainingCycleAsync(string gameName, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var draws = scope.ServiceProvider.GetRequiredService<IPatterneventRepository>();
            var trainer = scope.ServiceProvider.GetRequiredService<PipelineTrainer>();

            try
            {
                await _hub.Clients.Group(gameName)
                    .SendAsync("TrainingStarted", gameName, ct);

                var history = await draws.GetAllAsync(gameName);
                if (history.Count < 20)
                {
                    _logger.LogWarning("Not enough draws ({Count}) to train. Skipping.", history.Count);
                    return;
                }

                await _hub.Clients.Group(gameName)
                    .SendAsync("PipelineProgress", "All", 0, "Starting training…", ct);

                var metrics = await trainer.TrainAllAsync(history, ct);

                await _hub.Clients.Group(gameName)
                    .SendAsync("TrainingCompleted", metrics.Select(m => new
                    {
                        m.PipelineName,
                        m.ModelVersion,
                        m.RSquared,
                        m.MeanAbsoluteError,
                        m.IsBestVersion
                    }), ct);

                _logger.LogInformation("Training cycle completed for {Game}.", gameName);
            }
            catch (OperationCanceledException) { /* shutting down */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Training cycle failed for {Game}.", gameName);
                await _hub.Clients.Group(gameName)
                    .SendAsync("TrainingFailed", ex.Message, ct);
            }
        }

        private async Task CheckAndRetrainAsync(string gameName, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var awareness = scope.ServiceProvider.GetRequiredService<IAdaptiveIntelligenceEngine>();

            bool shouldRetrain = await awareness.ShouldRetrainAsync();
            if (shouldRetrain)
            {
                _logger.LogInformation("Self-awareness engine triggered a retrain for {Game}.", gameName);
                await RunTrainingCycleAsync(gameName, ct);
            }
        }
    }
}

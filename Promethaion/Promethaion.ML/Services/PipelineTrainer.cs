using Promethaion.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Promethaion.Core.Entities;

namespace Promethaion.ML.Services;

/// <summary>
/// Orchestrates training across all three pipelines.
/// Called by the background service whenever a retrain is triggered.
/// </summary>
public class PipelineTrainer
{
    private readonly IReadOnlyList<IAnalysisPipeline> _pipelines;
    private readonly ITrainingMetricsRepository _metricsRepo;
    private readonly ILogger<PipelineTrainer> _logger;

    public PipelineTrainer(
        IEnumerable<IAnalysisPipeline> pipelines,
        ITrainingMetricsRepository metricsRepo,
        ILogger<PipelineTrainer> logger)
    {
        _pipelines = pipelines.ToList();
        _metricsRepo = metricsRepo;
        _logger = logger;
    }

    /// <summary>
    /// Trains all pipelines and persists metrics.
    /// Returns the list of newly recorded ModelMetrics.
    /// </summary>
    public async Task<IReadOnlyList<TrainingMetrics>> TrainAllAsync(
        IReadOnlyList<PatternEvent> history,
        CancellationToken ct = default)
    {
        if (history.Count < 20)
            throw new InvalidOperationException(
                "At least 20 draw records are required to train models.");

        _logger.LogInformation("Starting training run on {Count} draws.", history.Count);
        var results = new List<TrainingMetrics>();

        foreach (var pipeline in _pipelines)
        {
            ct.ThrowIfCancellationRequested();
            _logger.LogInformation("Training {Pipeline}…", pipeline.PipelineName);

            try
            {
                var metrics = await pipeline.TrainAsync(history, ct);
                metrics.ModelVersion = $"{pipeline.PipelineName}-{DateTime.UtcNow:yyyyMMddHHmmss}";

                // Compare with current best.
                var best = await _metricsRepo.GetBestAsync(pipeline.PipelineName);
                bool isBetter = best is null || metrics.RSquared >= best.RSquared;
                metrics.IsBestVersion = isBetter;

                await _metricsRepo.AddAsync(metrics);
                if (isBetter)
                    await _metricsRepo.MarkAsBestAsync(metrics.Id, pipeline.PipelineName);

                _logger.LogInformation(
                    "{Pipeline} trained. MAE={MAE:F4} RMSE={RMSE:F4} R²={R2:F4} IsBest={IsBest}",
                    pipeline.PipelineName, metrics.MeanAbsoluteError,
                    metrics.RootMeanSquaredError, metrics.RSquared, isBetter);

                results.Add(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Training failed for {Pipeline}.", pipeline.PipelineName);
            }
        }

        return results;
    }
}

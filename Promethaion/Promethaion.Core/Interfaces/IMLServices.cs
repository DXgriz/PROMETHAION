using Promethaion.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.Core.Interfaces;

/// <summary>
/// Contract for any ML pipeline that can be trained on results history.
/// </summary>
/// 
public interface IAnalysisPipeline
{
    string PipelineName { get; }

    /// <summary>Train the model. Returns metrics for the training run.</summary>
    Task<TrainingMetrics> TrainAsync(IReadOnlyList<PatternEvent> history, CancellationToken ct = default);

    /// <summary>Predict probability scores for each ball number (1–52).</summary>
    Task<Dictionary<int, double>> ScoreBallsAsync(IReadOnlyList<PatternEvent> history, CancellationToken ct = default);

    bool IsModelLoaded { get; }
    Task LoadModelAsync(CancellationToken ct = default);
    Task SaveModelAsync(CancellationToken ct = default);
}

/// <summary>
/// Combines multiple pipelines into a single ensemble prediction.
/// </summary>
public interface IEnsembleEngine
{
    /// <summary>
    /// Produces a ranked list of (ballNumber → confidence) using
    /// weighted combination of all registered pipelines.
    /// </summary>
    Task<Dictionary<int, double>> GetEnsembleScoresAsync(
        IReadOnlyList<PatternEvent> history,
        CancellationToken ct = default);

    /// <summary>
    /// Selects the top-6 balls and returns a full Prediction entity.
    /// </summary>
    Task<PatternForecast> PredictNextDrawAsync(
        IReadOnlyList<PatternEvent> history,
        int targetDrawNumber,
        CancellationToken ct = default);
}

/// <summary>
/// Self-awareness: evaluates how well the system is actually doing
/// and triggers retraining when performance degrades.
/// </summary>
public interface IAdaptiveIntelligenceEngine
{
    /// <summary>
    /// Compares latest prediction against actual draw result.
    /// Stores match count and updates running accuracy.
    /// </summary>
    Task EvaluatePredictionAsync(PatternForecast prediction, PatternEvent actual);

    /// <summary>Running average match count over the last N predictions.</summary>
    Task<double> GetRollingAccuracyAsync(int windowSize = 20);

    /// <summary>
    /// Returns true if accuracy has degraded enough to warrant retraining.
    /// </summary>
    Task<bool> ShouldRetrainAsync();
}

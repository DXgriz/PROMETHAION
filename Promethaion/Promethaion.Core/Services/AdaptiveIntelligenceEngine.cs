using Promethaion.Core.Entities;
using Promethaion.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.Core.Services
{
    /// <summary>
    /// Evaluates prediction quality over time and signals when models need retraining.
    /// This is the "self-aware" component — it tracks how well the system models the lotto,
    /// computes rolling accuracy, and decides when a new training cycle is warranted.
    /// </summary>
    public class AdaptiveIntelligenceEngine : IAdaptiveIntelligenceEngine
    {
        private readonly IPatternForecastRepository _predictions;
        private readonly ITrainingMetricsRepository _metrics;

        // Minimum average match count before triggering a retrain signal.
        // 1.5 out of 6 is statistically above random — if we fall below this, retrain.
        private const double RetrainThreshold = 1.0;

        // Minimum number of evaluated predictions before making retrain decisions.
        private const int MinEvaluatedSamples = 5;

        public AdaptiveIntelligenceEngine(
            IPatternForecastRepository predictions,
            ITrainingMetricsRepository metrics)
        {
            _predictions = predictions;
            _metrics = metrics;
        }

        public async Task EvaluatePredictionAsync(PatternForecast prediction, PatternEvent actual)
        {
            var actualBalls = new HashSet<int>(actual.Balls);
            var matchCount = prediction.Balls.Count(b => actualBalls.Contains(b));

            prediction.ActualDrawResultId = actual.Id;
            prediction.MatchCount = matchCount;

            await _predictions.UpdateAsync(prediction);
        }

        public async Task<double> GetRollingAccuracyAsync(int windowSize = 20)
        {
            var evaluated = await _predictions.GetEvaluatedAsync();
            var window = evaluated
                .OrderByDescending(p => p.GeneratedAt)
                .Take(windowSize)
                .Where(p => p.MatchCount.HasValue)
                .ToList();

            if (!window.Any()) return 0;

            return window.Average(p => p.MatchCount!.Value);
        }

        public async Task<bool> ShouldRetrainAsync()
        {
            var evaluated = await _predictions.GetEvaluatedAsync();
            if (evaluated.Count < MinEvaluatedSamples) return false;

            var rolling = await GetRollingAccuracyAsync();
            return rolling < RetrainThreshold;
        }
    }
}

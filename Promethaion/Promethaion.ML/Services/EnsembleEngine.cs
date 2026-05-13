using Promethaion.Core.Entities;
using Promethaion.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Promethaion.ML.Services
{
    /// <summary>
    /// Combines the three pipelines using weighted average scoring,
    /// then selects the top-6 balls to form a Prediction.
    ///
    /// Pipeline weights can be tuned based on observed historical accuracy.
    /// The self-awareness engine will update these weights after each evaluation.
    /// </summary>
    public class EnsembleEngine : IEnsembleEngine
    {
        private readonly IReadOnlyList<IAnalysisPipeline> _pipelines;

        // Adjustable weights: Frequency · Sequence · Positional
        private double[] _weights;

        public EnsembleEngine(IEnumerable<IAnalysisPipeline> pipelines)
        {
            _pipelines = pipelines.ToList();
            _weights = Enumerable.Repeat(1.0 / _pipelines.Count, _pipelines.Count).ToArray();
        }

        /// <summary>Updates per-pipeline weights (e.g. from accuracy feedback).</summary>
        public void SetWeights(double[] weights)
        {
            if (weights.Length != _pipelines.Count)
                throw new ArgumentException("Weight array length must match pipeline count.");

            double sum = weights.Sum();
            _weights = weights.Select(w => w / sum).ToArray(); // normalise
        }

        public async Task<Dictionary<int, double>> GetEnsembleScoresAsync(
            IReadOnlyList<PatternEvent> history,
            CancellationToken ct = default)
        {
            var allScores = await Task.WhenAll(
                _pipelines.Select(p => p.ScoreBallsAsync(history, ct)));

            var ensemble = new Dictionary<int, double>();
            for (int ball = 1; ball <= 52; ball++)
            {
                double weighted = 0;
                for (int i = 0; i < _pipelines.Count; i++)
                    weighted += _weights[i] * allScores[i].GetValueOrDefault(ball);

                ensemble[ball] = weighted;
            }

            return ensemble;
        }

        public async Task<PatternForecast> PredictNextDrawAsync(
            IReadOnlyList<PatternEvent> history,
            int targetDrawNumber,
            CancellationToken ct = default)
        {
            var scores = await GetEnsembleScoresAsync(history, ct);

            // Sort all balls by score descending, pick top 6.
            var top6 = scores
                .OrderByDescending(kv => kv.Value)
                .Take(6)
                .Select(kv => kv.Key)
                .OrderBy(b => b)
                .ToArray();

            // Confidence = average score of the selected 6 balls normalised to [0,1].
            double overallConfidence = top6.Average(b => scores[b]);

            // Per-ball confidence as percentage of max possible score.
            double maxScore = scores.Values.Max();
            var perBall = top6.ToDictionary(
                b => b,
                b => maxScore > 0 ? Math.Round(scores[b] / maxScore, 4) : 0.0);

            // Bonus ball = top scored ball NOT in the main 6.
            int bonusBall = scores
                .Where(kv => !top6.Contains(kv.Key))
                .OrderByDescending(kv => kv.Value)
                .First().Key;

            return new PatternForecast
            {
                TargetDrawNumber = targetDrawNumber,
                Ball1 = top6[0],
                Ball2 = top6[1],
                Ball3 = top6[2],
                Ball4 = top6[3],
                Ball5 = top6[4],
                Ball6 = top6[5],
                BonusBall = bonusBall,
                ConfidenceScore = Math.Round(overallConfidence, 4),
                PerBallConfidenceJson = JsonSerializer.Serialize(perBall),
                ModelVersion = $"ensemble-{DateTime.UtcNow:yyyyMMdd}",
            };
        }
    }
}

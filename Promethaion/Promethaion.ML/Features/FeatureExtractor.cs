using Promethaion.Core.Entities;
using Promethaion.ML.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.ML.Features
{
    /// <summary>
    /// Converts a raw list of <see cref="PatternEvent"/> records into a rich
    /// feature matrix that ML.NET pipelines can consume.
    ///
    /// Each row in the output describes a (draw, ballNumber) pair and answers:
    ///   - How often has this ball appeared historically?
    ///   - How many draws has it been absent (gap / overdue)?
    ///   - What is its average position when it does appear?
    ///   - What is the rolling-window frequency over the last N draws?
    ///   - What day-of-week / week-of-year context does this draw have?
    /// </summary>
    internal class FeatureExtractor
    {
        public const int MinBall = 1;
        public const int MaxBall = 52;
        public const int BallsPerDraw = 6;

        /// <summary>
        /// Builds a feature row for every (draw × ball) combination.
        /// Used to train the frequency and positional pipelines.
        /// </summary>
        public static List<FeatureVector> BuildTrainingRows(IReadOnlyList<PatternEvent> history)
        {
            var rows = new List<FeatureVector>(history.Count * MaxBall);

            // Pre-compute global frequencies.
            var globalFreq = ComputeGlobalFrequency(history);

            for (int drawIdx = 0; drawIdx < history.Count; drawIdx++)
            {
                var draw = history[drawIdx];
                var drawnSet = new HashSet<int>(draw.Balls);
                var preceding = history.Take(drawIdx).ToList();

                // Rolling windows: last 10, 20, 52 draws.
                var last10 = preceding.TakeLast(10).ToList();
                var last20 = preceding.TakeLast(20).ToList();
                var last52 = preceding.TakeLast(52).ToList();

                // Per-slot averages.
                var slotAverages = ComputeSlotAverages(preceding);

                for (int ball = MinBall; ball <= MaxBall; ball++)
                {
                    int gapSinceSeen = ComputeGap(preceding, ball);

                    rows.Add(new FeatureVector
                    {
                        // Target: was this ball drawn?
                        Label = drawnSet.Contains(ball) ? 1f : 0f,

                        BallNumber = ball,
                        DrawIndex = drawIdx,
                        DayOfWeek = (float)draw.DrawDate.DayOfWeek,
                        WeekOfYear = (float)System.Globalization.ISOWeek.GetWeekOfYear(draw.DrawDate),
                        MonthOfYear = (float)draw.DrawDate.Month,

                        GlobalFrequency = (float)globalFreq.GetValueOrDefault(ball),
                        Freq10 = FreqInWindow(last10, ball),
                        Freq20 = FreqInWindow(last20, ball),
                        Freq52 = FreqInWindow(last52, ball),

                        GapSinceSeen = gapSinceSeen,
                        IsOverdue = gapSinceSeen > 10 ? 1f : 0f,

                        AvgPositionWhenDrawn = slotAverages.GetValueOrDefault(ball, 3.5f),

                        // Pair co-occurrence with last draw's numbers.
                        PairFreqWithPrev = preceding.Count == 0 ? 0f
                            : (float)ComputePairFreq(preceding, ball, preceding.Last().Balls),

                        // Distance from median of the last draw.
                        DistFromLastMedian = preceding.Count == 0 ? 0f
                            : Math.Abs(ball - (float)preceding.Last().Balls.Average()),
                    });
                }
            }

            return rows;
        }

        /// <summary>
        /// Builds a single inference row for a ball given the full known history.
        /// Used at prediction time (no Label needed).
        /// </summary>
        public static FeatureVector BuildInferenceRow(IReadOnlyList<PatternEvent> history, int ball)
        {
            var last10 = history.TakeLast(10).ToList();
            var last20 = history.TakeLast(20).ToList();
            var last52 = history.TakeLast(52).ToList();
            var latest = history.Last();
            var globalFreq = ComputeGlobalFrequency(history);

            return new FeatureVector
            {
                Label = 0f, // unknown
                BallNumber = ball,
                DrawIndex = history.Count,
                DayOfWeek = (float)DateTime.Today.AddDays(7 - (int)DateTime.Today.DayOfWeek).DayOfWeek,
                WeekOfYear = (float)System.Globalization.ISOWeek.GetWeekOfYear(DateTime.Today),
                MonthOfYear = (float)DateTime.Today.Month,
                GlobalFrequency = (float)globalFreq.GetValueOrDefault(ball),
                Freq10 = FreqInWindow(last10, ball),
                Freq20 = FreqInWindow(last20, ball),
                Freq52 = FreqInWindow(last52, ball),
                GapSinceSeen = ComputeGap(history.ToList(), ball),
                IsOverdue = ComputeGap(history.ToList(), ball) > 10 ? 1f : 0f,
                AvgPositionWhenDrawn = ComputeSlotAverages(history.ToList()).GetValueOrDefault(ball, 3.5f),
                PairFreqWithPrev = (float)ComputePairFreq(history.ToList(), ball, latest.Balls),
                DistFromLastMedian = Math.Abs(ball - (float)latest.Balls.Average()),
            };
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static Dictionary<int, double> ComputeGlobalFrequency(IReadOnlyList<PatternEvent> history)
        {
            var freq = new Dictionary<int, double>();
            int total = history.Count;
            if (total == 0) return freq;

            for (int b = MinBall; b <= MaxBall; b++)
                freq[b] = history.Count(d => d.Balls.Contains(b)) / (double)total;

            return freq;
        }

        private static float FreqInWindow(List<PatternEvent> window, int ball)
        {
            if (window.Count == 0) return 0f;
            return (float)window.Count(d => d.Balls.Contains(ball)) / window.Count;
        }

        private static int ComputeGap(List<PatternEvent> history, int ball)
        {
            for (int i = history.Count - 1; i >= 0; i--)
                if (history[i].Balls.Contains(ball))
                    return history.Count - 1 - i;
            return history.Count; // never seen
        }

        private static Dictionary<int, float> ComputeSlotAverages(List<PatternEvent> history)
        {
            var sums = new Dictionary<int, float>();
            var counts = new Dictionary<int, int>();

            foreach (var draw in history)
            {
                for (int pos = 0; pos < draw.Balls.Length; pos++)
                {
                    int b = draw.Balls[pos];
                    sums[b] = sums.GetValueOrDefault(b) + pos + 1;
                    counts[b] = counts.GetValueOrDefault(b) + 1;
                }
            }

            return sums.ToDictionary(kv => kv.Key, kv => kv.Value / counts[kv.Key]);
        }

        private static double ComputePairFreq(List<PatternEvent> history, int ball, int[] companions)
        {
            if (history.Count == 0) return 0;
            int coOccurrences = history.Count(d =>
                d.Balls.Contains(ball) && companions.Any(c => c != ball && d.Balls.Contains(c)));
            return (double)coOccurrences / history.Count;
        }
    }
}

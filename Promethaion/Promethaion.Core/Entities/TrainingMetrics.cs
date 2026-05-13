using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.Core.Entities
{
    /// <summary>
    /// Records the evaluation metrics for each training run.
    /// Enables the self-awareness engine to compare model generations.
    /// </summary>
    public class TrainingMetrics
    {
        public int Id { get; set; }
        public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
        public string ModelVersion { get; set; } = string.Empty;
        public string PipelineName { get; set; } = string.Empty;

        /// <summary>Number of draw records used for training.</summary>
        public int TrainingSetSize { get; set; }

        /// <summary>Number of records held out for evaluation.</summary>
        public int TestSetSize { get; set; }

        // Regression metrics (for number-scoring models).
        public double MeanAbsoluteError { get; set; }
        public double RootMeanSquaredError { get; set; }
        public double RSquared { get; set; }

        // Classification metrics (for ball-selection models).
        public double MacroAccuracy { get; set; }
        public double MicroAccuracy { get; set; }
        public double LogLoss { get; set; }

        /// <summary>Average number of correct balls predicted on the test set.</summary>
        public double AverageMatchCount { get; set; }

        /// <summary>Whether this version outperforms the previous best.</summary>
        public bool IsBestVersion { get; set; }

        /// <summary>Arbitrary JSON for extra pipeline-specific diagnostics.</summary>
        public string DiagnosticsJson { get; set; } = "{}";
    }
}

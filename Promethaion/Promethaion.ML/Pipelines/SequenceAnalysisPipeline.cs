using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.ML.Pipelines
{
    /// <summary>
    /// Pipeline 2 — Sequence model.
    /// Uses SDCA (Stochastic Dual Coordinate Ascent) binary logistic regression
    /// to model the sequential pattern of which balls tend to follow which.
    /// Logistic regression produces well-calibrated probabilities and is
    /// fast to retrain, making it ideal for the self-awareness loop.
    /// </summary>
    public class SequenceAnalysisPipeline : BaseAnalysisPipeline
    {
        public override string PipelineName => "SequenceModel";

        public SequenceAnalysisPipeline(string modelDirectory) : base(modelDirectory) { }

        protected override IEstimator<ITransformer> BuildTrainer()
        {
            // Binary classification: was this ball drawn (1) or not (0)?
            var calibratedTrainer = Mlc.BinaryClassification.Trainers.SdcaLogisticRegression(
                labelColumnName: "Label",
                featureColumnName: "Features",
                maximumNumberOfIterations: 100);

            // Wrap in a regression-compatible scorer so the base class can call
            // Regression.Evaluate and ScoreBallsAsync uniformly.
            return calibratedTrainer;
        }

        //public override async Task<Dictionary<int, double>> ScoreBallsAsync(
        //    Promethaion.Core.Entities.PatternEvent[] history,
        //    System.Threading.CancellationToken ct = default)
        //    => await base.ScoreBallsAsync(history, ct);
    }
}

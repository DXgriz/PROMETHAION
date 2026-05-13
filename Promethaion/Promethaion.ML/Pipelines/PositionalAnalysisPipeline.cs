using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.ML.Pipelines
{
    /// <summary>
    /// Pipeline 3 — Positional model.
    /// Uses LightGBM gradient boosting regression to score each ball based on
    /// positional bias (which slot it tends to appear in) and overdue detection.
    /// LightGBM handles high-cardinality features well and captures non-linear
    /// relationships between gap and draw probability.
    /// </summary>
    public class PositionalanalysisPipeline : BaseAnalysisPipeline
    {
        public override string PipelineName => "PositionalModel";

        public PositionalanalysisPipeline(string modelDirectory) : base(modelDirectory) { }

        protected override IEstimator<ITransformer> BuildTrainer() =>
            Mlc.Regression.Trainers.LightGbm(
                labelColumnName: "Label",
                featureColumnName: "Features",
                numberOfLeaves: 63,
                numberOfIterations: 200,
                learningRate: 0.05f,
                minimumExampleCountPerLeaf: 5);
    }

}

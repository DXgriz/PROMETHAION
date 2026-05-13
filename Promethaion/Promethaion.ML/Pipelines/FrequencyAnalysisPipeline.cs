using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ML.Trainers.FastTree;

namespace Promethaion.ML.Pipelines;

/// <summary>
/// Pipeline 1 — Frequency model.
/// Uses a FastForest regression tree to predict draw probability from
/// historical frequency, recency windows, and gap features.
/// FastForest is robust to outliers and requires no feature scaling.
/// </summary>
public class FrequencyAnalysisPipeline : BaseAnalysisPipeline
{
    public override string PipelineName => "FrequencyModel";

    public FrequencyAnalysisPipeline(string modelDirectory) : base(modelDirectory) { }

    protected override IEstimator<ITransformer> BuildTrainer() =>
        Mlc.Regression.Trainers.FastForest(
            labelColumnName: "Label",
            featureColumnName: "Features",
            numberOfLeaves: 31,
            numberOfTrees: 100,
            minimumExampleCountPerLeaf: 5);
}

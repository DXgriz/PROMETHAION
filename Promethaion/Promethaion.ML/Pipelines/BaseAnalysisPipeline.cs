using Microsoft.ML;
using Promethaion.Core.Entities;
using Promethaion.ML.Features;
using Promethaion.ML.Models;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.ML.Data;
using System.Text.Json;
using Promethaion.Core.Interfaces;


namespace Promethaion.ML.Pipelines;

/// <summary>
/// Shared scaffolding for all lotto ML pipelines.
/// Subclasses only need to implement <see cref="BuildTrainer"/> to plug in
/// their specific ML.NET trainer.
/// </summary>
public abstract class BaseAnalysisPipeline : IAnalysisPipeline
{
    protected readonly MLContext Mlc;
    protected readonly string ModelDirectory;
    protected ITransformer? _model;

    public abstract string PipelineName { get; }
    public bool IsModelLoaded => _model is not null;

    protected BaseAnalysisPipeline(string modelDirectory)
    {
        Mlc = new MLContext(seed: 42);
        ModelDirectory = modelDirectory;
        Directory.CreateDirectory(modelDirectory);
    }

    protected abstract IEstimator<ITransformer> BuildTrainer();

    // Feature columns fed into every pipeline.
    protected static readonly string[] FeatureColumns =
    [
        nameof(FeatureVector.BallNumber),
        nameof(FeatureVector.DayOfWeek),
        nameof(FeatureVector.WeekOfYear),
        nameof(FeatureVector.MonthOfYear),
        nameof(FeatureVector.GlobalFrequency),
        nameof(FeatureVector.Freq10),
        nameof(FeatureVector.Freq20),
        nameof(FeatureVector.Freq52),
        nameof(FeatureVector.GapSinceSeen),
        nameof(FeatureVector.IsOverdue),
        nameof(FeatureVector.AvgPositionWhenDrawn),
        nameof(FeatureVector.PairFreqWithPrev),
        nameof(FeatureVector.DistFromLastMedian),
    ];

    public virtual async Task<TrainingMetrics> TrainAsync(
        IReadOnlyList<PatternEvent> history,
        CancellationToken ct = default)
    {
        await Task.Yield(); // yield so caller's UI can update

        var rows = FeatureExtractor.BuildTrainingRows(history);
        var dataView = Mlc.Data.LoadFromEnumerable(rows);

        // 80 / 20 split.
        var split = Mlc.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: 42);

        var featurisePipeline = Mlc.Transforms.Concatenate("Features", FeatureColumns);
        var fullPipeline = featurisePipeline.Append(BuildTrainer());

        _model = fullPipeline.Fit(split.TrainSet);

        // Evaluate on test set.
        var predictions = _model.Transform(split.TestSet);

        var metrics = new TrainingMetrics
        {
            PipelineName = PipelineName,
            ModelVersion = $"v{DateTime.UtcNow:yyyyMMddHHmmss}",
            TrainingSetSize = (int)(rows.Count * 0.8),
            TestSetSize = (int)(rows.Count * 0.2),
        };

        try
        {
            var reg = Mlc.Regression.Evaluate(predictions, labelColumnName: "Label");
            metrics.MeanAbsoluteError = reg.MeanAbsoluteError;
            metrics.RootMeanSquaredError = reg.RootMeanSquaredError;
            metrics.RSquared = reg.RSquared;
            metrics.DiagnosticsJson = JsonSerializer.Serialize(new
            {
                reg.MeanAbsoluteError,
                reg.RootMeanSquaredError,
                reg.RSquared,
                Pipeline = PipelineName
            });
        }
        catch
        {
            // If the trainer is a classifier rather than regressor, skip regression eval.
        }

        await SaveModelAsync(ct);
        return metrics;
    }

    public virtual async Task<Dictionary<int, double>> ScoreBallsAsync(
        IReadOnlyList<PatternEvent> history,
        CancellationToken ct = default)
    {
        if (_model is null) await LoadModelAsync(ct);

        var scores = new Dictionary<int, double>();
        var engine = Mlc.Model.CreatePredictionEngine<FeatureVector, ScoredOutcome>(_model!);

        for (int ball = FeatureExtractor.MinBall; ball <= FeatureExtractor.MaxBall; ball++)
        {
            var row = FeatureExtractor.BuildInferenceRow(history, ball);
            var output = engine.Predict(row);
            // Clamp to [0,1] — raw regression scores may exceed bounds.
            scores[ball] = Math.Clamp(output.Score, 0.0, 1.0);
        }

        return scores;
    }

    public async Task SaveModelAsync(CancellationToken ct = default)
    {
        if (_model is null) return;
        var path = ModelPath();
        await Task.Run(() => Mlc.Model.Save(_model, null, path), ct);
    }

    public async Task LoadModelAsync(CancellationToken ct = default)
    {
        var path = ModelPath();
        if (!File.Exists(path)) return;
        await Task.Run(() =>
        {
            _model = Mlc.Model.Load(path, out _);
        }, ct);
    }

    private string ModelPath() =>
        Path.Combine(ModelDirectory, $"{PipelineName}.zip");
}

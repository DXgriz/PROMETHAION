using Promethaion.Core.Entities;
using Promethaion.Core.Interfaces;

namespace Promethaion.API.Services;

public class PredictionService
{
    private readonly IAnalysisPipeline _pipeline;
    private readonly IPatterneventRepository _repo;

    public PredictionService(
        IAnalysisPipeline pipeline,
        IPatterneventRepository repo)
    {
        _pipeline = pipeline;
        _repo = repo;
    }

    /// <summary>
    /// Returns full prediction including scores.
    /// </summary>
    public async Task<API.DTOs.DTOs.PredictionResult> PredictAsync(
        string gameName,
        CancellationToken ct = default)
    {
        var history = await _repo.GetAllAsync(gameName);

        if (history == null || history.Count == 0)
            throw new InvalidOperationException("No data available for prediction.");

        var scores = await _pipeline.ScoreBallsAsync(history, ct);

        var ordered = scores.OrderByDescending(x => x.Value).ToList();

        var mainBalls = ordered.Take(6).Select(x => x.Key).OrderBy(x => x).ToList();

        var bonusBall = ordered.Skip(6).First().Key;

        var confidence = mainBalls.Select(b => scores[b]).Average();

        return new API.DTOs.DTOs.PredictionResult
        {
            GameName = gameName,
            MainBalls = mainBalls,
            BonusBall = bonusBall,
            Confidence = confidence,
            GeneratedAt = DateTime.Now,

            Scores = scores
        };
    }
}
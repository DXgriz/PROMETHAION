using Promethaion.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Promethaion.Core.Interfaces;
public interface IPatterneventRepository
{
    Task<IReadOnlyList<PatternEvent>> GetAllAsync(string gameName = "SA Lotto");
    Task<PatternEvent?> GetByDrawNumberAsync(int drawNumber, string gameName = "SA Lotto");
    Task<IReadOnlyList<PatternEvent>> GetRecentAsync(int count, string gameName = "SA Lotto");
    Task<int> GetMaxDrawNumberAsync(string gameName = "SA Lotto");
    Task AddAsync(PatternEvent draw);
    Task AddRangeAsync(IEnumerable<PatternEvent> draws);
    Task<int> CountAsync(string gameName = "SA Lotto");
}

public interface IPatternForecastRepository
{
    Task<IReadOnlyList<PatternForecast>> GetAllAsync();
    Task<PatternForecast?> GetLatestAsync();
    Task<IReadOnlyList<PatternForecast>> GetEvaluatedAsync();
    Task AddAsync(PatternForecast prediction);
    Task UpdateAsync(PatternForecast prediction);
}

public interface ITrainingMetricsRepository
{
    Task<IReadOnlyList<TrainingMetrics>> GetAllAsync();
    Task<TrainingMetrics?> GetBestAsync(string pipelineName);
    Task<IReadOnlyList<TrainingMetrics>> GetHistoryAsync(string pipelineName);
    Task AddAsync(TrainingMetrics metrics);
    Task MarkAsBestAsync(int metricsId, string pipelineName);
}

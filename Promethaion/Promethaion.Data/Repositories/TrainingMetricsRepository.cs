using Microsoft.EntityFrameworkCore;
using Promethaion.Core.Entities;
using Promethaion.Core.Interfaces;

namespace Promethaion.Data.Repositories;

public class TrainingMetricsRepository : ITrainingMetricsRepository
{
    private readonly PAionDbContext _db;
    public TrainingMetricsRepository(PAionDbContext db) => _db = db;

    public async Task<IReadOnlyList<TrainingMetrics>> GetAllAsync() =>
        await _db.ModelMetrics
            .OrderByDescending(m => m.TrainedAt)
            .ToListAsync();

    public async Task<TrainingMetrics?> GetBestAsync(string pipelineName) =>
        await _db.ModelMetrics
            .Where(m => m.PipelineName == pipelineName && m.IsBestVersion)
            .OrderByDescending(m => m.TrainedAt)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<TrainingMetrics>> GetHistoryAsync(string pipelineName) =>
        await _db.ModelMetrics
            .Where(m => m.PipelineName == pipelineName)
            .OrderByDescending(m => m.TrainedAt)
            .ToListAsync();

    public async Task AddAsync(TrainingMetrics metrics)
    {
        _db.ModelMetrics.Add(metrics);
        await _db.SaveChangesAsync();
    }

    public async Task MarkAsBestAsync(int metricsId, string pipelineName)
    {
        var previous = await _db.ModelMetrics
            .Where(m => m.PipelineName == pipelineName && m.IsBestVersion)
            .ToListAsync();
        foreach (var p in previous) p.IsBestVersion = false;

        var current = await _db.ModelMetrics.FindAsync(metricsId);
        if (current != null) current.IsBestVersion = true;

        await _db.SaveChangesAsync();
    }
}
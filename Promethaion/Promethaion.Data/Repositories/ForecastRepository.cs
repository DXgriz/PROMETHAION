using System;
using System.Collections.Generic;
using System.Text;
using Promethaion.Core.Entities;
using Promethaion.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Promethaion.Data.Repositories;

public class ForecastRepository : IPatternForecastRepository
{
    private readonly PAionDbContext _db;
    public ForecastRepository(PAionDbContext db) => _db = db;

    public async Task<IReadOnlyList<PatternForecast>> GetAllAsync() =>
        await _db.Predictions
            .Include(p => p.ActualResult)
            .OrderByDescending(p => p.GeneratedAt)
            .ToListAsync();

    public async Task<PatternForecast?> GetLatestAsync() =>
        await _db.Predictions
            .OrderByDescending(p => p.GeneratedAt)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<PatternForecast>> GetEvaluatedAsync() =>
        await _db.Predictions
            .Include(p => p.ActualResult)
            .Where(p => p.MatchCount.HasValue)
            .OrderByDescending(p => p.GeneratedAt)
            .ToListAsync();

    public async Task AddAsync(PatternForecast prediction)
    {
        _db.Predictions.Add(prediction);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(PatternForecast prediction)
    {
        _db.Predictions.Update(prediction);
        await _db.SaveChangesAsync();
    }
}

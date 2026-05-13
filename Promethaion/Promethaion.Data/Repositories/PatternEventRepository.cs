using Promethaion.Core.Entities;
using Promethaion.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace Promethaion.Data.Repositories
{
    public class PatternEventRepository : IPatterneventRepository
    {
        private readonly PAionDbContext _db;
        public PatternEventRepository(PAionDbContext db) => _db = db;

        public async Task<IReadOnlyList<PatternEvent>> GetAllAsync(string gameName = "SA Lotto") =>
            await _db.DrawResults
                .Where(d => d.GameName == gameName)
                .OrderBy(d => d.DrawNumber)
                .ToListAsync();

        public async Task<PatternEvent?> GetByDrawNumberAsync(int drawNumber, string gameName = "SA Lotto") =>
            await _db.DrawResults
                .FirstOrDefaultAsync(d => d.DrawNumber == drawNumber && d.GameName == gameName);

        public async Task<IReadOnlyList<PatternEvent>> GetRecentAsync(int count, string gameName = "SA Lotto") =>
            await _db.DrawResults
                .Where(d => d.GameName == gameName)
                .OrderByDescending(d => d.DrawNumber)
                .Take(count)
                .OrderBy(d => d.DrawNumber)
                .ToListAsync();

        public async Task<int> GetMaxDrawNumberAsync(string gameName = "SA Lotto")
        {
            if (!await _db.DrawResults.AnyAsync(d => d.GameName == gameName)) return 0;
            return await _db.DrawResults
                .Where(d => d.GameName == gameName)
                .MaxAsync(d => d.DrawNumber);
        }

        public async Task AddAsync(PatternEvent draw)
        {
            _db.DrawResults.Add(draw);
            await _db.SaveChangesAsync();
        }

        public async Task AddRangeAsync(IEnumerable<PatternEvent> draws)
        {
            _db.DrawResults.AddRange(draws);
            await _db.SaveChangesAsync();
        }

        public async Task<int> CountAsync(string gameName = "SA Lotto") =>
            await _db.DrawResults.CountAsync(d => d.GameName == gameName);
    }
}

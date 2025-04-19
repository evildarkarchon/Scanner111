using Microsoft.EntityFrameworkCore;
using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Models;
using Scanner111.Infrastructure.Persistence;

namespace Scanner111.Infrastructure.Persistence.Repositories;

public class CrashLogRepository : RepositoryBase<CrashLog>, ICrashLogRepository
{
    public CrashLogRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IEnumerable<CrashLog>> GetByGameIdAsync(string gameId)
    {
        return await DbContext.CrashLogs
            .Where(c => c.GameId == gameId)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<CrashLog>> GetUnsolvedAsync()
    {
        return await DbContext.CrashLogs
            .Where(c => !c.IsSolved)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<CrashLog>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        return await DbContext.CrashLogs
            .Where(c => c.CrashTime >= start && c.CrashTime <= end)
            .ToListAsync();
    }
}
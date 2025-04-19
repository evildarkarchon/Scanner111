// Scanner111.Infrastructure/Persistence/Repositories/CrashLogRepository.cs
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
    
    public override async Task<CrashLog?> GetByIdAsync(string id)
    {
        return await DbContext.CrashLogs
            .Include(c => c.Plugins)
            .Include(c => c.CallStackEntries)
            .Include(c => c.DetectedIssues)
            .FirstOrDefaultAsync(c => c.Id == id);
    }
    
    public override async Task<IEnumerable<CrashLog>> GetAllAsync()
    {
        return await DbContext.CrashLogs
            .Include(c => c.Plugins)
            .Include(c => c.DetectedIssues)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<CrashLog>> GetByGameIdAsync(string gameId)
    {
        return await DbContext.CrashLogs
            .Include(c => c.Plugins)
            .Include(c => c.DetectedIssues)
            .Where(c => c.GameId == gameId)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<CrashLog>> GetUnsolvedAsync()
    {
        return await DbContext.CrashLogs
            .Include(c => c.Plugins)
            .Include(c => c.DetectedIssues)
            .Where(c => !c.IsSolved)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<CrashLog>> GetByDateRangeAsync(DateTime start, DateTime end)
    {
        return await DbContext.CrashLogs
            .Include(c => c.Plugins)
            .Include(c => c.DetectedIssues)
            .Where(c => c.CrashTime >= start && c.CrashTime <= end)
            .ToListAsync();
    }
    
    public override async Task AddAsync(CrashLog entity)
    {
        // First add the crash log
        await DbContext.CrashLogs.AddAsync(entity);
        await DbContext.SaveChangesAsync();
    }
    
    public override async Task UpdateAsync(CrashLog entity)
    {
        // Get existing entities to compare
        var existingCrashLog = await DbContext.CrashLogs
            .Include(c => c.Plugins)
            .Include(c => c.CallStackEntries)
            .Include(c => c.DetectedIssues)
            .FirstOrDefaultAsync(c => c.Id == entity.Id);
            
        if (existingCrashLog == null)
        {
            await AddAsync(entity);
            return;
        }
        
        // Update basic properties
        DbContext.Entry(existingCrashLog).CurrentValues.SetValues(entity);
        
        // Handle plugins
        UpdateCollection(existingCrashLog.Plugins, entity.Plugins, 
            (existing, updated) => existing.PluginName == updated.PluginName);
            
        // Handle call stack entries
        UpdateCollection(existingCrashLog.CallStackEntries, entity.CallStackEntries,
            (existing, updated) => existing.Order == updated.Order);
            
        // Handle detected issues
        UpdateCollection(existingCrashLog.DetectedIssues, entity.DetectedIssues,
            (existing, updated) => existing.Description == updated.Description);
            
        await DbContext.SaveChangesAsync();
    }
    
    private void UpdateCollection<T>(ICollection<T> existingItems, ICollection<T> newItems, Func<T, T, bool> comparer) 
        where T : class
    {
        // Remove items no longer present
        var itemsToRemove = existingItems.Where(existingItem => 
            !newItems.Any(newItem => comparer(existingItem, newItem))).ToList();
            
        foreach (var item in itemsToRemove)
        {
            existingItems.Remove(item);
        }
        
        // Add new items
        foreach (var newItem in newItems)
        {
            if (!existingItems.Any(existingItem => comparer(existingItem, newItem)))
            {
                existingItems.Add(newItem);
            }
        }
    }
}
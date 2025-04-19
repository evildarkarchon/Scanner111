using Microsoft.EntityFrameworkCore;
using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Models;
using Scanner111.Infrastructure.Persistence;

namespace Scanner111.Infrastructure.Persistence.Repositories;

public class PluginRepository : RepositoryBase<Plugin>, IPluginRepository
{
    public PluginRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<IEnumerable<Plugin>> GetByGameIdAsync(string gameId)
    {
        // In a real application, this would query a mapping table between games and plugins
        // For simplicity, we're returning all plugins here
        return await DbContext.Plugins.ToListAsync();
    }
    
    public async Task<IEnumerable<Plugin>> GetEnabledAsync(string gameId)
    {
        // In a real application, this would query a mapping table between games and plugins
        // For simplicity, we're returning all enabled plugins here
        return await DbContext.Plugins
            .Where(p => p.IsEnabled)
            .ToListAsync();
    }
    
    public async Task<IEnumerable<Plugin>> GetWithIssuesAsync(string gameId)
    {
        // In a real application, this would query a mapping table between games and plugins
        // For simplicity, we're returning all plugins with issues here
        return await DbContext.Plugins
            .Where(p => p.HasIssues)
            .ToListAsync();
    }
}
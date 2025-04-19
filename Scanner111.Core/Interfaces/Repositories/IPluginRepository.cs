using Scanner111.Core.Models;

namespace Scanner111.Core.Interfaces.Repositories;

public interface IPluginRepository : IRepository<Plugin>
{
    Task<IEnumerable<Plugin>> GetByGameIdAsync(string gameId);
    Task<IEnumerable<Plugin>> GetEnabledAsync(string gameId);
    Task<IEnumerable<Plugin>> GetWithIssuesAsync(string gameId);
}
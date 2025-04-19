using Scanner111.Core.Models;

namespace Scanner111.Core.Interfaces.Repositories;

public interface ICrashLogRepository : IRepository<CrashLog>
{
    Task<IEnumerable<CrashLog>> GetByGameIdAsync(string gameId);
    Task<IEnumerable<CrashLog>> GetUnsolvedAsync();
    Task<IEnumerable<CrashLog>> GetByDateRangeAsync(DateTime start, DateTime end);
}
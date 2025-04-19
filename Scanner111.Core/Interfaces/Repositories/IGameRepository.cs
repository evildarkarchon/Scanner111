using Scanner111.Core.Models;

namespace Scanner111.Core.Interfaces.Repositories;

public interface IGameRepository : IRepository<Game>
{
    Task<Game?> GetByExecutableNameAsync(string exeName);
}
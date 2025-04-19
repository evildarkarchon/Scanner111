using Microsoft.EntityFrameworkCore;
using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Models;
using Scanner111.Infrastructure.Persistence;

namespace Scanner111.Infrastructure.Persistence.Repositories;

public class GameRepository : RepositoryBase<Game>, IGameRepository
{
    public GameRepository(AppDbContext dbContext) : base(dbContext)
    {
    }
    
    public async Task<Game?> GetByExecutableNameAsync(string exeName)
    {
        return await DbContext.Games
            .FirstOrDefaultAsync(g => g.ExecutableName.Equals(exeName, StringComparison.OrdinalIgnoreCase));
    }
}
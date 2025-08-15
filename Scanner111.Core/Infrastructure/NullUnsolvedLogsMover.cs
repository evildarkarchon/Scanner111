using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Null implementation of IUnsolvedLogsMover for design-time support
/// </summary>
public class NullUnsolvedLogsMover : IUnsolvedLogsMover
{
    public Task<bool> MoveUnsolvedLogAsync(string crashLogPath, ApplicationSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(false);
    }
}
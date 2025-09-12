using Scanner111.Core.Analysis;

namespace Scanner111.CLI.Services;

/// <summary>
/// Registry for managing available analyzers.
/// </summary>
public interface IAnalyzerRegistry
{
    /// <summary>
    /// Gets all available analyzers.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of available analyzers.</returns>
    Task<IEnumerable<IAnalyzer>> GetAllAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets an analyzer by name.
    /// </summary>
    /// <param name="name">The analyzer name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The analyzer if found; otherwise, null.</returns>
    Task<IAnalyzer?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Registers an analyzer.
    /// </summary>
    /// <param name="analyzer">The analyzer to register.</param>
    void Register(IAnalyzer analyzer);
}
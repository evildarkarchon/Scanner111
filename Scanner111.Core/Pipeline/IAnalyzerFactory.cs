using Scanner111.Core.Analyzers;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Factory for creating analyzer instances
/// </summary>
public interface IAnalyzerFactory
{
    /// <summary>
    /// Create all available analyzers for the specified game
    /// </summary>
    IEnumerable<IAnalyzer> CreateAnalyzers(string game);
    
    /// <summary>
    /// Create a specific analyzer by name
    /// </summary>
    IAnalyzer? CreateAnalyzer(string name);
    
    /// <summary>
    /// Get available analyzer names
    /// </summary>
    IEnumerable<string> GetAvailableAnalyzers();
}
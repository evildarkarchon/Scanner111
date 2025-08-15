using Microsoft.Extensions.DependencyInjection;
using Scanner111.Core.Analyzers;

namespace Scanner111.Core.Pipeline;

/// <summary>
///     Default implementation of analyzer factory
/// </summary>
public class AnalyzerFactory : IAnalyzerFactory
{
    private readonly Dictionary<string, Type> _analyzerTypes;
    private readonly IServiceProvider _serviceProvider;

    public AnalyzerFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;

        // Register analyzer types
        _analyzerTypes = new Dictionary<string, Type>
        {
            ["FormId"] = typeof(FormIdAnalyzer),
            ["Plugin"] = typeof(PluginAnalyzer),
            ["Suspect"] = typeof(SuspectScanner),
            ["Settings"] = typeof(SettingsScanner),
            ["Record"] = typeof(RecordScanner),
            ["FileIntegrity"] = typeof(FileIntegrityAnalyzer),
            ["BuffoutVersion"] = typeof(BuffoutVersionAnalyzerV2)
        };
    }

    /// <summary>
    ///     Creates a collection of analyzers registered in the factory and orders them by their priority.
    /// </summary>
    /// <param name="game">The name of the game for which the analyzers are being created.</param>
    /// <returns>A collection of analyzers ordered by their priority.</returns>
    public IEnumerable<IAnalyzer> CreateAnalyzers(string game)
    {
        var analyzers = new List<IAnalyzer>();

        // Create all registered analyzers
        foreach (var (_, type) in _analyzerTypes)
        {
            var analyzer = CreateAnalyzerInstance(type);
            if (analyzer != null) analyzers.Add(analyzer);
        }

        return analyzers.OrderBy(a => a.Priority);
    }

    /// <summary>
    ///     Creates a new analyzer instance based on the provided name.
    /// </summary>
    /// <param name="name">The name of the analyzer to create.</param>
    /// <returns>An instance of the requested analyzer if found; otherwise, null.</returns>
    public IAnalyzer? CreateAnalyzer(string name)
    {
        return _analyzerTypes.TryGetValue(name, out var type) ? CreateAnalyzerInstance(type) : null;
    }

    /// <summary>
    ///     Retrieves the names of all available analyzers registered in the factory.
    /// </summary>
    /// <returns>A collection of registered analyzer names.</returns>
    public IEnumerable<string> GetAvailableAnalyzers()
    {
        return _analyzerTypes.Keys;
    }

    /// <summary>
    ///     Creates a new instance of an analyzer based on the specified analyzer type.
    /// </summary>
    /// <param name="analyzerType">The type of the analyzer to create.</param>
    /// <returns>An instance of the specified analyzer type if creation succeeds; otherwise, null.</returns>
    private IAnalyzer? CreateAnalyzerInstance(Type analyzerType)
    {
        try
        {
            return (IAnalyzer?)ActivatorUtilities.CreateInstance(_serviceProvider, analyzerType);
        }
        catch (Exception)
        {
            // Log error
            return null;
        }
    }
}
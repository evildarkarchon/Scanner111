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
            ["Record"] = typeof(RecordScanner)
        };
    }

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

    public IAnalyzer? CreateAnalyzer(string name)
    {
        return _analyzerTypes.TryGetValue(name, out var type) ? CreateAnalyzerInstance(type) : null;
    }

    public IEnumerable<string> GetAvailableAnalyzers()
    {
        return _analyzerTypes.Keys;
    }

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
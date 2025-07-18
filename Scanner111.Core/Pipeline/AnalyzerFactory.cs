using Microsoft.Extensions.DependencyInjection;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Default implementation of analyzer factory
/// </summary>
public class AnalyzerFactory : IAnalyzerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IYamlSettingsProvider _settingsProvider;
    private readonly Dictionary<string, Type> _analyzerTypes;

    public AnalyzerFactory(IServiceProvider serviceProvider, IYamlSettingsProvider settingsProvider)
    {
        _serviceProvider = serviceProvider;
        _settingsProvider = settingsProvider;
        
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
        foreach (var (name, type) in _analyzerTypes)
        {
            var analyzer = CreateAnalyzerInstance(type);
            if (analyzer != null)
            {
                analyzers.Add(analyzer);
            }
        }
        
        return analyzers.OrderBy(a => a.Priority);
    }

    public IAnalyzer? CreateAnalyzer(string name)
    {
        if (_analyzerTypes.TryGetValue(name, out var type))
        {
            return CreateAnalyzerInstance(type);
        }
        
        return null;
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
        catch (Exception ex)
        {
            // Log error
            return null;
        }
    }
}
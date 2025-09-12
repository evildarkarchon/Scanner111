using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;

namespace Scanner111.CLI.Services;

/// <summary>
/// Registry for managing available analyzers.
/// </summary>
public class AnalyzerRegistry : IAnalyzerRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalyzerRegistry> _logger;
    private readonly List<Type> _analyzerTypes;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyzerRegistry"/> class.
    /// </summary>
    public AnalyzerRegistry(IServiceProvider serviceProvider, ILogger<AnalyzerRegistry> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _analyzerTypes = new List<Type>
        {
            typeof(PluginAnalyzer),
            typeof(SettingsAnalyzer),
            typeof(FcxModeAnalyzer),
            typeof(PathValidationAnalyzer),
            typeof(GameIntegrityAnalyzer),
            typeof(DocumentsPathAnalyzer),
            typeof(FormIdAnalyzer),
            typeof(ModFileScanAnalyzer),
            typeof(RecordScannerAnalyzer),
            typeof(GpuAnalyzer),
            typeof(ModDetectionAnalyzer),
            typeof(SuspectScannerAnalyzer)
        };
    }
    
    /// <summary>
    /// Gets all available analyzers.
    /// </summary>
    public async Task<IEnumerable<IAnalyzer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var analyzers = new List<IAnalyzer>();
        
        foreach (var analyzerType in _analyzerTypes)
        {
            try
            {
                var analyzer = (IAnalyzer)_serviceProvider.GetRequiredService(analyzerType);
                analyzers.Add(analyzer);
                _logger.LogDebug("Loaded analyzer: {AnalyzerName}", analyzer.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load analyzer of type {AnalyzerType}", analyzerType.Name);
            }
        }
        
        return await Task.FromResult(analyzers);
    }
    
    /// <summary>
    /// Gets an analyzer by name.
    /// </summary>
    public async Task<IAnalyzer?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var analyzers = await GetAllAsync(cancellationToken);
        return analyzers.FirstOrDefault(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Registers an analyzer type.
    /// </summary>
    public void Register(IAnalyzer analyzer)
    {
        var type = analyzer.GetType();
        if (!_analyzerTypes.Contains(type))
        {
            _analyzerTypes.Add(type);
            _logger.LogInformation("Registered analyzer: {AnalyzerName}", analyzer.Name);
        }
    }
}
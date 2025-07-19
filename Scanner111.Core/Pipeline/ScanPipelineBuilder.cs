using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Pipeline;

/// <summary>
/// Builder for creating scan pipeline instances
/// </summary>
public class ScanPipelineBuilder
{
    private readonly IServiceCollection _services;
    private readonly List<Type> _analyzerTypes = new();
    private bool _enablePerformanceMonitoring;
    private bool _enableCaching = true;
    private bool _enableEnhancedErrorHandling = true;
    private int _maxConcurrency = Environment.ProcessorCount;

    public ScanPipelineBuilder()
    {
        _services = new ServiceCollection();
        ConfigureDefaultServices();
    }

    /// <summary>
    /// Add an analyzer to the pipeline
    /// </summary>
    public ScanPipelineBuilder AddAnalyzer<T>() where T : class, IAnalyzer
    {
        _analyzerTypes.Add(typeof(T));
        _services.AddTransient<T>();
        return this;
    }

    /// <summary>
    /// Add all default analyzers
    /// </summary>
    public ScanPipelineBuilder AddDefaultAnalyzers()
    {
        AddAnalyzer<FormIdAnalyzer>();
        AddAnalyzer<PluginAnalyzer>();
        AddAnalyzer<SuspectScanner>();
        AddAnalyzer<SettingsScanner>();
        AddAnalyzer<RecordScanner>();
        return this;
    }

    /// <summary>
    /// Configure the message handler
    /// </summary>
    public ScanPipelineBuilder WithMessageHandler(IMessageHandler messageHandler)
    {
        _services.AddSingleton(messageHandler);
        return this;
    }

    /// <summary>
    /// Enable performance monitoring
    /// </summary>
    public ScanPipelineBuilder WithPerformanceMonitoring(bool enable = true)
    {
        _enablePerformanceMonitoring = enable;
        return this;
    }

    /// <summary>
    /// Enable caching for analysis results and settings
    /// </summary>
    public ScanPipelineBuilder WithCaching(bool enable = true)
    {
        _enableCaching = enable;
        return this;
    }

    /// <summary>
    /// Enable enhanced error handling and resilience
    /// </summary>
    public ScanPipelineBuilder WithEnhancedErrorHandling(bool enable = true)
    {
        _enableEnhancedErrorHandling = enable;
        return this;
    }

    /// <summary>
    /// Set maximum concurrency for batch processing
    /// </summary>
    public ScanPipelineBuilder WithMaxConcurrency(int maxConcurrency)
    {
        _maxConcurrency = maxConcurrency;
        return this;
    }

    /// <summary>
    /// Configure logging
    /// </summary>
    public ScanPipelineBuilder WithLogging(Action<ILoggingBuilder> configure)
    {
        _services.AddLogging(configure);
        return this;
    }

    /// <summary>
    /// Build the scan pipeline
    /// </summary>
    public IScanPipeline Build()
    {
        // Build service provider
        var serviceProvider = _services.BuildServiceProvider();
        
        // Initialize static YamlSettingsCache with the service instance
        var yamlSettingsProvider = serviceProvider.GetRequiredService<IYamlSettingsProvider>();
        YamlSettingsCache.Initialize(yamlSettingsProvider);
        
        // Create analyzers
        var analyzers = _analyzerTypes
            .Select(type => (IAnalyzer)serviceProvider.GetRequiredService(type))
            .ToList();
        
        // Create base pipeline
        IScanPipeline pipeline;
        
        if (_enableCaching || _enableEnhancedErrorHandling)
        {
            // Use enhanced pipeline
            var enhancedLogger = serviceProvider.GetRequiredService<ILogger<EnhancedScanPipeline>>();
            var messageHandler = serviceProvider.GetRequiredService<IMessageHandler>();
            var settingsProvider = serviceProvider.GetRequiredService<IYamlSettingsProvider>();
            var cacheManager = serviceProvider.GetRequiredService<ICacheManager>();
            var resilientExecutor = serviceProvider.GetRequiredService<ResilientExecutor>();
            
            pipeline = new EnhancedScanPipeline(analyzers, enhancedLogger, messageHandler, 
                settingsProvider, cacheManager, resilientExecutor);
        }
        else
        {
            // Use basic pipeline
            var logger = serviceProvider.GetRequiredService<ILogger<ScanPipeline>>();
            var messageHandler = serviceProvider.GetRequiredService<IMessageHandler>();
            var settingsProvider = serviceProvider.GetRequiredService<IYamlSettingsProvider>();
            
            pipeline = new ScanPipeline(analyzers, logger, messageHandler, settingsProvider);
        }
        
        if (_enablePerformanceMonitoring)
        {
            var perfLogger = serviceProvider.GetRequiredService<ILogger>();
            return new PerformanceMonitoringPipeline(pipeline, perfLogger);
        }
        
        return pipeline;
    }

    private void ConfigureDefaultServices()
    {
        // Add logging
        _services.AddLogging(builder => builder.AddConsole());
        
        // Add infrastructure services
        _services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
        _services.AddSingleton<IAnalyzerFactory, AnalyzerFactory>();
        _services.AddSingleton<IFormIdDatabaseService, FormIdDatabaseService>();
        _services.AddSingleton<IReportWriter, ReportWriter>();
        
        // Add caching services if enabled
        if (_enableCaching)
        {
            _services.AddMemoryCache();
            _services.AddSingleton<ICacheManager, CacheManager>();
        }
        else
        {
            _services.AddSingleton<ICacheManager, NullCacheManager>();
        }
        
        // Add error handling services if enabled
        if (_enableEnhancedErrorHandling)
        {
            _services.AddSingleton<IErrorHandlingPolicy, DefaultErrorHandlingPolicy>();
            _services.AddSingleton<ResilientExecutor>();
        }
        else
        {
            _services.AddSingleton<ResilientExecutor>(provider => 
                new ResilientExecutor(new NoRetryErrorPolicy(), 
                    provider.GetRequiredService<ILogger<ResilientExecutor>>()));
        }
        
        // Add default message handler if not provided
        _services.TryAddSingleton<IMessageHandler, NullMessageHandler>();
        
        // ClassicScanLogsInfo removed - using IYamlSettingsProvider directly
    }
}

/// <summary>
/// Null message handler for when no UI is needed
/// </summary>
internal class NullMessageHandler : IMessageHandler
{
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowWarning(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowError(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowDebug(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info, MessageTarget target = MessageTarget.All) { }
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems) => new Progress<ProgressInfo>();
    public IProgressContext CreateProgressContext(string title, int totalItems) => new NullProgressContext();
}
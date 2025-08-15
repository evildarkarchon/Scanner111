using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Pipeline;

/// <summary>
///     Builder for constructing and configuring instances of scan pipelines.
/// </summary>
public class ScanPipelineBuilder
{
    private readonly List<Type> _analyzerTypes = new();
    private readonly IServiceCollection _services;
    private bool _enableCaching = true;
    private bool _enableEnhancedErrorHandling = true;
    private bool _enableFcx;
    private bool _enablePerformanceMonitoring;

    public ScanPipelineBuilder()
    {
        _services = new ServiceCollection();
        ConfigureDefaultServices();
    }

    /// <summary>
    ///     Adds an analyzer of the specified type to the pipeline.
    /// </summary>
    /// <typeparam name="T">The type of the analyzer to add. Must implement <see cref="IAnalyzer" />.</typeparam>
    /// <returns>The current instance of <see cref="ScanPipelineBuilder" /> for method chaining.</returns>
    public ScanPipelineBuilder AddAnalyzer<T>() where T : class, IAnalyzer
    {
        _analyzerTypes.Add(typeof(T));
        _services.AddTransient<T>();
        return this;
    }

    /// <summary>
    ///     Adds all default analyzers to the pipeline.
    /// </summary>
    /// <returns>The current instance of <see cref="ScanPipelineBuilder" /> for method chaining.</returns>
    public ScanPipelineBuilder AddDefaultAnalyzers()
    {
        AddAnalyzer<BuffoutVersionAnalyzerV2>();
        AddAnalyzer<FormIdAnalyzer>();
        AddAnalyzer<PluginAnalyzer>();
        AddAnalyzer<SuspectScanner>();
        AddAnalyzer<SettingsScanner>();
        AddAnalyzer<RecordScanner>();
        AddAnalyzer<FileIntegrityAnalyzer>();
        return this;
    }

    /// <summary>
    ///     Configures the specified message handler for the pipeline.
    /// </summary>
    /// <param name="messageHandler">
    ///     The message handler to be used in the pipeline. Must implement
    ///     <see cref="IMessageHandler" />.
    /// </param>
    /// <returns>An instance of <see cref="ScanPipelineBuilder" /> to allow method chaining.</returns>
    public ScanPipelineBuilder WithMessageHandler(IMessageHandler messageHandler)
    {
        _services.AddSingleton(messageHandler);
        return this;
    }

    /// <summary>
    ///     Enables or disables performance monitoring for the pipeline.
    /// </summary>
    /// <param name="enable">Indicates whether performance monitoring should be enabled. Defaults to <c>true</c>.</param>
    /// <returns>The current instance of <see cref="ScanPipelineBuilder" /> for method chaining.</returns>
    public ScanPipelineBuilder WithPerformanceMonitoring(bool enable = true)
    {
        _enablePerformanceMonitoring = enable;
        return this;
    }

    /// <summary>
    ///     Enables or disables caching for analysis results and settings within the pipeline.
    /// </summary>
    /// <param name="enable">A boolean value indicating whether caching should be enabled. Defaults to true.</param>
    /// <returns>The current instance of <see cref="ScanPipelineBuilder" /> for method chaining.</returns>
    public ScanPipelineBuilder WithCaching(bool enable = true)
    {
        _enableCaching = enable;
        return this;
    }

    /// <summary>
    ///     Enables or disables enhanced error handling and resilience for the pipeline.
    /// </summary>
    /// <param name="enable">A boolean indicating whether to enable enhanced error handling. Defaults to <c>true</c>.</param>
    /// <returns>The current instance of <see cref="ScanPipelineBuilder" /> to allow method chaining.</returns>
    public ScanPipelineBuilder WithEnhancedErrorHandling(bool enable = true)
    {
        _enableEnhancedErrorHandling = enable;
        return this;
    }

    /// <summary>
    ///     Enables or disables FCX (File Integrity Check) mode for the pipeline.
    ///     When enabled, the pipeline will perform file integrity checks before analyzing crash logs.
    /// </summary>
    /// <param name="enable">A boolean indicating whether to enable FCX mode. Defaults to <c>true</c>.</param>
    /// <returns>The current instance of <see cref="ScanPipelineBuilder" /> to allow method chaining.</returns>
    public ScanPipelineBuilder WithFcxMode(bool enable = true)
    {
        _enableFcx = enable;
        return this;
    }


    /// <summary>
    ///     Configures logging for the scan pipeline.
    /// </summary>
    /// <param name="configure">An action to configure the logging behavior using an <see cref="ILoggingBuilder" />.</param>
    /// <returns>The current instance of <see cref="ScanPipelineBuilder" /> to enable method chaining.</returns>
    public ScanPipelineBuilder WithLogging(Action<ILoggingBuilder> configure)
    {
        _services.AddLogging(configure);
        return this;
    }

    /// <summary>
    ///     Builds and returns the configured scan pipeline.
    /// </summary>
    /// <returns>An instance of <see cref="IScanPipeline" /> representing the fully constructed scan pipeline.</returns>
    public IScanPipeline Build()
    {
        // Build service provider
        var serviceProvider = _services.BuildServiceProvider();


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

        // Wrap with FCX pipeline if enabled
        if (_enableFcx)
        {
            var fcxLogger = serviceProvider.GetRequiredService<ILogger<FcxEnabledPipeline>>();
            var hashService = serviceProvider.GetRequiredService<IHashValidationService>();
            var settingsService = serviceProvider.GetRequiredService<IApplicationSettingsService>();
            var yamlSettings = serviceProvider.GetRequiredService<IYamlSettingsProvider>();
            var messageHandler = serviceProvider.GetRequiredService<IMessageHandler>();

            pipeline = new FcxEnabledPipeline(
                pipeline,
                settingsService,
                hashService,
                fcxLogger,
                messageHandler,
                yamlSettings);
        }

        // Wrap with performance monitoring if enabled
        if (_enablePerformanceMonitoring)
        {
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
            var perfLogger = loggerFactory.CreateLogger("PerformanceMonitoring");
            return new PerformanceMonitoringPipeline(pipeline, perfLogger);
        }

        return pipeline;
    }

    /// <summary>
    ///     Configures the default set of services required for the scan pipeline to operate.
    ///     This includes adding logging, caching, error handling, and core infrastructure services.
    /// </summary>
    private void ConfigureDefaultServices()
    {
        // Add logging
        _services.AddLogging(builder => builder.AddConsole());

        // Add infrastructure services
        _services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
        _services.AddSingleton<IAnalyzerFactory, AnalyzerFactory>();
        _services.AddSingleton<IFormIdDatabaseService, FormIdDatabaseService>();
        _services.AddSingleton<IReportWriter, ReportWriter>();
        _services.AddSingleton<IHashValidationService, HashValidationService>();
        _services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();

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
///     Provides a null implementation of the <see cref="IMessageHandler" /> interface,
///     primarily used when no message handling UI or mechanism is required.
/// </summary>
internal class NullMessageHandler : IMessageHandler
{
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
    }

    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
    }

    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
    }

    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
    }

    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
    }

    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
    }

    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
    }

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        return new Progress<ProgressInfo>();
    }

    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new NullProgressContext();
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Analysis.SignalProcessing;
using Scanner111.Core.Analysis.Validators;
using Scanner111.Core.Configuration;
using Scanner111.Core.Data;
using Scanner111.Core.Discovery;
using Scanner111.Core.IO;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Processing;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Core.DependencyInjection;

/// <summary>
///     Extension methods for configuring YAML settings services in dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds YAML settings services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">Optional action to configure YAML settings options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYamlSettings(
        this IServiceCollection services,
        Action<YamlSettingsOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
            services.Configure(configureOptions);
        else
            services.Configure<YamlSettingsOptions>(_ => { }); // Use defaults

        // Register core services
        services.AddSingleton<IFileIoCore, FileIoCore>();
        services.AddSingleton<IAsyncYamlSettingsCore, AsyncYamlSettingsCore>();
        // Note: IYamlSettingsCache is obsolete - removed from registration
        // services.AddSingleton<IYamlSettingsCache, YamlSettingsCache>();

        return services;
    }

    /// <summary>
    ///     Adds YAML settings services with custom implementations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="fileIoFactory">Factory for creating custom IFileIOCore implementation.</param>
    /// <param name="configureOptions">Optional action to configure YAML settings options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddYamlSettingsWithCustomIo(
        this IServiceCollection services,
        Func<IServiceProvider, IFileIoCore> fileIoFactory,
        Action<YamlSettingsOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
            services.Configure(configureOptions);
        else
            services.Configure<YamlSettingsOptions>(_ => { }); // Use defaults

        // Register custom file IO
        services.AddSingleton(fileIoFactory);

        // Register other services
        services.AddSingleton<IAsyncYamlSettingsCore, AsyncYamlSettingsCore>();
        // Note: IYamlSettingsCache is obsolete - removed from registration
        // services.AddSingleton<IYamlSettingsCache, YamlSettingsCache>();

        return services;
    }

    /// <summary>
    ///     Adds path discovery services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPathDiscoveryServices(this IServiceCollection services)
    {
        // Register path validation service as singleton (thread-safe with caching)
        services.AddSingleton<IPathValidationService, PathValidationService>();

        // Register discovery services as singletons (thread-safe with caching)
        services.AddSingleton<IGamePathDiscoveryService, GamePathDiscoveryService>();
        services.AddSingleton<IDocumentsPathDiscoveryService, DocumentsPathDiscoveryService>();

        return services;
    }

    /// <summary>
    ///     Adds messaging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMessagingServices(this IServiceCollection services)
    {
        // Register message service as singleton (thread-safe)
        services.AddSingleton<IMessageService, MessageService>();

        return services;
    }

    /// <summary>
    ///     Adds FormID database services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureOptions">Optional action to configure FormID database options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFormIdDatabase(
        this IServiceCollection services,
        Action<FormIdDatabaseOptions>? configureOptions = null)
    {
        // Configure options
        if (configureOptions != null)
            services.Configure(configureOptions);
        else
            // Default configuration
            services.Configure<FormIdDatabaseOptions>(options =>
            {
                options.MaxConnections = 5;
                options.CacheExpirationMinutes = 30;
                options.EnableFormIdLookups = true;
                // Database paths will need to be configured based on game
            });

        // Add memory cache if not already registered
        services.AddMemoryCache();

        // Register FormID database as singleton (thread-safe with connection pooling)
        services.AddSingleton<IFormIdDatabase, FormIdDatabasePool>();

        return services;
    }

    /// <summary>
    ///     Adds the FormID analyzer to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="showFormIdValues">Whether to show FormID values from database lookups.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFormIdAnalyzer(
        this IServiceCollection services,
        bool showFormIdValues = true)
    {
        // Register FormID analyzer as transient (new instance per analysis)
        services.AddTransient(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<FormIdAnalyzer>>();
            var formIdDatabase = provider.GetService<IFormIdDatabase>();

            return new FormIdAnalyzer(logger, formIdDatabase, showFormIdValues);
        });

        return services;
    }

    /// <summary>
    ///     Adds plugin loading and analysis services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="crashGenName">Optional name for the crash generator (defaults to "Scanner111").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPluginServices(
        this IServiceCollection services,
        string? crashGenName = null)
    {
        // Register plugin loader as singleton (thread-safe with caching)
        services.AddSingleton<IPluginLoader, PluginLoader>();

        // Register plugin analyzer as transient (new instance per analysis)
        services.AddTransient(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<PluginAnalyzer>>();
            var pluginLoader = provider.GetRequiredService<IPluginLoader>();
            var yamlCore = provider.GetRequiredService<IAsyncYamlSettingsCore>();

            return new PluginAnalyzer(logger, pluginLoader, yamlCore, crashGenName);
        });

        return services;
    }

    /// <summary>
    ///     Adds settings analysis services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSettingsAnalysisServices(this IServiceCollection services)
    {
        // Register validators as singletons (thread-safe)
        services.AddSingleton<MemoryManagementValidator>();
        services.AddSingleton<Buffout4SettingsValidator>();
        services.AddSingleton<ModSettingsCompatibilityValidator>();
        services.AddSingleton<VersionAwareSettingsValidator>();

        // Register settings service as singleton (thread-safe with caching)
        services.AddSingleton<ISettingsService, SettingsService>();

        // Register settings analyzer as transient (new instance per analysis)
        // Wire up all validators
        services.AddTransient(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SettingsAnalyzer>>();
            var settingsService = provider.GetRequiredService<ISettingsService>();
            var memoryValidator = provider.GetRequiredService<MemoryManagementValidator>();
            var buffout4Validator = provider.GetService<Buffout4SettingsValidator>();
            var modCompatValidator = provider.GetService<ModSettingsCompatibilityValidator>();
            var versionValidator = provider.GetService<VersionAwareSettingsValidator>();

            return new SettingsAnalyzer(
                logger,
                settingsService,
                memoryValidator,
                buffout4Validator,
                modCompatValidator,
                versionValidator);
        });

        // Register FCX mode analyzer as transient (new instance per analysis)
        services.AddTransient<FcxModeAnalyzer>();

        return services;
    }

    /// <summary>
    ///     Adds mod detection services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModDetectionServices(this IServiceCollection services)
    {
        // Register mod database as singleton (thread-safe with caching)
        services.AddSingleton<IModDatabase, ModDatabase>();

        // Register mod detection analyzer as transient (new instance per analysis)
        services.AddTransient<IModDetectionAnalyzer, ModDetectionAnalyzer>();

        return services;
    }

    /// <summary>
    ///     Adds mod file scanning services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddModFileScanServices(this IServiceCollection services)
    {
        // Register mod file scanner as singleton (thread-safe with semaphore management)
        services.AddSingleton<IModFileScanner, ModFileScanner>();

        // Register crash generator checker as singleton (thread-safe with caching)
        services.AddSingleton<ICrashGenChecker, CrashGenChecker>();

        // Register XSE plugin checker as singleton (thread-safe)
        services.AddSingleton<IXsePluginChecker, XsePluginChecker>();

        // Register mod file scan analyzer as transient (new instance per analysis)
        services.AddTransient<ModFileScanAnalyzer>();

        return services;
    }

    /// <summary>
    ///     Adds high-performance infrastructure services.
    ///     Optimized for C# without Python's GIL limitations.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHighPerformanceInfrastructure(this IServiceCollection services)
    {
        // Register memory-mapped file handler
        services.AddSingleton<MemoryMappedFileHandler>();

        // Register optimized database operations
        services.AddSingleton<OptimizedDatabaseOperations>();
        
        // Register high-performance file I/O
        services.AddSingleton<HighPerformanceFileIO>();
        
        // Register dataflow pipeline orchestrator
        services.AddTransient<DataflowPipelineOrchestrator>();
        
        // Register channel-based batch processor as factory
        services.AddTransient(typeof(ChannelBasedBatchProcessor<,>));

        return services;
    }

    /// <summary>
    ///     Adds GPU detection services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGpuDetectionServices(this IServiceCollection services)
    {
        // Register GPU detector as singleton (thread-safe, stateless)
        services.AddSingleton<IGpuDetector, GpuDetector>();

        // Register GPU analyzer as transient (new instance per analysis)
        services.AddTransient<GpuAnalyzer>();

        return services;
    }

    /// <summary>
    ///     Adds record scanning services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="crashGenName">Optional name for the crash generator (defaults to "Scanner111").</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddRecordScanServices(
        this IServiceCollection services,
        string? crashGenName = null)
    {
        // Register record scanner analyzer as transient (new instance per analysis)
        services.AddTransient(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<RecordScannerAnalyzer>>();
            var yamlCore = provider.GetRequiredService<IAsyncYamlSettingsCore>();

            return new RecordScannerAnalyzer(logger, yamlCore, crashGenName);
        });

        return services;
    }

    /// <summary>
    ///     Adds advanced signal processing services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSignalProcessingServices(this IServiceCollection services)
    {
        // Register signal processing components as singletons (thread-safe, stateless)
        services.AddSingleton<SignalProcessor>();
        services.AddSingleton<SeverityCalculator>();
        services.AddSingleton<CallStackAnalyzer>();

        // Register enhanced suspect scanner analyzer as transient
        services.AddTransient(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SuspectScannerAnalyzer>>();
            var yamlCore = provider.GetRequiredService<IAsyncYamlSettingsCore>();
            var signalProcessor = provider.GetRequiredService<SignalProcessor>();
            var severityCalculator = provider.GetRequiredService<SeverityCalculator>();
            var callStackAnalyzer = provider.GetRequiredService<CallStackAnalyzer>();

            return new SuspectScannerAnalyzer(
                yamlCore, 
                logger, 
                signalProcessor, 
                severityCalculator, 
                callStackAnalyzer);
        });

        return services;
    }

    /// <summary>
    ///     Adds all Scanner111 analyzer services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureFormIdOptions">Optional action to configure FormID database options.</param>
    /// <param name="showFormIdValues">Whether to show FormID values from database lookups.</param>
    /// <param name="crashGenName">Optional name for the crash generator.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAnalyzerServices(
        this IServiceCollection services,
        Action<FormIdDatabaseOptions>? configureFormIdOptions = null,
        bool showFormIdValues = true,
        string? crashGenName = null)
    {
        // Add FormID database and analyzer
        services.AddFormIdDatabase(configureFormIdOptions);
        services.AddFormIdAnalyzer(showFormIdValues);

        // Add plugin services and analyzer
        services.AddPluginServices(crashGenName);

        // Add settings analysis services
        services.AddSettingsAnalysisServices();

        // Add mod detection services
        services.AddModDetectionServices();

        // Add mod file scanning services
        services.AddModFileScanServices();

        // Add critical analyzers (Phase 1)
        services.AddGpuDetectionServices();
        services.AddRecordScanServices(crashGenName);

        // Add high-performance infrastructure (Phase 2)
        services.AddHighPerformanceInfrastructure();

        // Add advanced analysis features (Phase 3)
        services.AddSignalProcessingServices();

        return services;
    }

    /// <summary>
    ///     Adds all Scanner111 core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureYamlOptions">Optional action to configure YAML settings options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScanner111Core(
        this IServiceCollection services,
        Action<YamlSettingsOptions>? configureYamlOptions = null)
    {
        // Add YAML settings services
        services.AddYamlSettings(configureYamlOptions);

        // Add path discovery services
        services.AddPathDiscoveryServices();

        // Add messaging services
        services.AddMessagingServices();

        return services;
    }

    /// <summary>
    ///     Adds all Scanner111 services including analyzers to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureYamlOptions">Optional action to configure YAML settings options.</param>
    /// <param name="configureFormIdOptions">Optional action to configure FormID database options.</param>
    /// <param name="showFormIdValues">Whether to show FormID values from database lookups.</param>
    /// <param name="crashGenName">Optional name for the crash generator.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScanner111Complete(
        this IServiceCollection services,
        Action<YamlSettingsOptions>? configureYamlOptions = null,
        Action<FormIdDatabaseOptions>? configureFormIdOptions = null,
        bool showFormIdValues = true,
        string? crashGenName = null)
    {
        // Add core services
        services.AddScanner111Core(configureYamlOptions);

        // Add all analyzer services
        services.AddAnalyzerServices(configureFormIdOptions, showFormIdValues, crashGenName);

        return services;
    }
}
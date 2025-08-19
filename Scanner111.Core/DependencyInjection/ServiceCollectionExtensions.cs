using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Abstractions;
using Scanner111.Core.Analyzers;
using Scanner111.Core.FCX;
using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.ModManagers;
using Scanner111.Core.ModManagers.MO2;
using Scanner111.Core.ModManagers.Vortex;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;

namespace Scanner111.Core.DependencyInjection;

/// <summary>
/// Extension methods for configuring Scanner111 core services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Scanner111 core services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddScanner111Core(this IServiceCollection services)
    {
        // Add abstractions
        services.AddAbstractions();
        
        // Add infrastructure services
        services.AddInfrastructureServices();
        
        // Add analyzers
        services.AddAnalyzers();
        
        // Add pipeline services
        services.AddPipelineServices();
        
        // Add game scanning services
        services.AddGameScanningServices();
        
        // Add application services
        services.AddApplicationServices();
        
        // Add mod manager services
        services.AddModManagerServices();
        
        // Add FCX services
        services.AddFcxServices();
        
        return services;
    }
    
    /// <summary>
    /// Adds file system and path abstractions to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddAbstractions(this IServiceCollection services)
    {
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IFileVersionInfoProvider, FileVersionInfoProvider>();
        services.AddSingleton<IEnvironmentPathProvider, EnvironmentPathProvider>();
        services.AddSingleton<IPathService, PathService>();
        
        return services;
    }
    
    /// <summary>
    /// Adds infrastructure services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsHelper, SettingsHelper>();
        services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
        services.AddSingleton<IFormIdDatabaseService, FormIdDatabaseService>();
        services.AddSingleton<IReportWriter, ReportWriter>();
        services.AddSingleton<IHashValidationService, HashValidationService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IConsoleService, ConsoleService>();
        services.AddSingleton<IUnsolvedLogsMover, UnsolvedLogsMover>();
        services.AddSingleton<ICrashLogParser, CrashLogParser>();
        services.AddSingleton<IGamePathDetection, GamePathDetection>();
        // Note: IGameVersionDetection and ICrashLogDirectoryManager need interfaces created
        // services.AddSingleton<IGameVersionDetection, GameVersionDetection>();
        // services.AddSingleton<ICrashLogDirectoryManager, CrashLogDirectoryManager>();
        
        // Add caching
        services.AddMemoryCache();
        services.AddSingleton<ICacheManager, CacheManager>();
        
        // Add error handling
        services.AddSingleton<IErrorHandlingPolicy, DefaultErrorHandlingPolicy>();
        services.AddSingleton<ResilientExecutor>();
        
        return services;
    }
    
    /// <summary>
    /// Adds analyzer services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddAnalyzers(this IServiceCollection services)
    {
        services.AddTransient<BuffoutVersionAnalyzerV2>();
        services.AddTransient<FormIdAnalyzer>();
        services.AddTransient<PluginAnalyzer>();
        services.AddTransient<SuspectScanner>();
        services.AddTransient<SettingsScanner>();
        services.AddTransient<RecordScanner>();
        services.AddTransient<FileIntegrityAnalyzer>();
        services.AddTransient<DocumentsValidationAnalyzer>();
        services.AddTransient<GpuDetectionAnalyzer>();
        
        return services;
    }
    
    /// <summary>
    /// Adds pipeline services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddPipelineServices(this IServiceCollection services)
    {
        services.AddSingleton<IAnalyzerFactory, AnalyzerFactory>();
        
        // Note: IScanPipeline should be created using ScanPipelineBuilder
        // This is typically done in the application startup rather than here
        
        return services;
    }
    
    /// <summary>
    /// Adds game scanning services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddGameScanningServices(this IServiceCollection services)
    {
        services.AddSingleton<IGameScannerService, GameScannerService>();
        services.AddSingleton<ICrashGenChecker, CrashGenChecker>();
        services.AddSingleton<IModIniScanner, ModIniScanner>();
        services.AddSingleton<IWryeBashChecker, WryeBashChecker>();
        services.AddSingleton<IXsePluginValidator, XsePluginValidator>();
        
        return services;
    }
    
    /// <summary>
    /// Adds application services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<IPapyrusMonitorService, PapyrusMonitorService>();
        services.AddSingleton<IPastebinService, PastebinService>();
        services.AddSingleton<IRecentItemsService, RecentItemsService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IAudioNotificationService, AudioNotificationService>();
        services.AddSingleton<IModManagerService, ModManagerService>();
        
        return services;
    }
    
    /// <summary>
    /// Adds mod manager services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddModManagerServices(this IServiceCollection services)
    {
        services.AddSingleton<IModManagerDetector, ModManagerDetector>();
        // Note: The following need interfaces created
        // services.AddSingleton<IMO2ProfileReader, MO2ProfileReader>();
        // services.AddSingleton<IMO2ModListParser, MO2ModListParser>();
        services.AddSingleton<IModManager, ModOrganizer2Manager>();
        // services.AddSingleton<IVortexConfigReader, VortexConfigReader>();
        services.AddSingleton<IModManager, VortexManager>();
        
        return services;
    }
    
    /// <summary>
    /// Adds FCX (File Check eXtended) services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddFcxServices(this IServiceCollection services)
    {
        services.AddSingleton<IModScanner, ModScanner>();
        services.AddSingleton<IModCompatibilityService, ModCompatibilityService>();
        services.AddTransient<ModConflictAnalyzer>();
        services.AddTransient<VersionAnalyzer>();
        
        return services;
    }
    
    /// <summary>
    /// Adds logging services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional action to configure logging.</param>
    /// <returns>The configured service collection for method chaining.</returns>
    public static IServiceCollection AddScanner111Logging(this IServiceCollection services, 
        Action<ILoggingBuilder>? configure = null)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
            configure?.Invoke(builder);
        });
        
        return services;
    }
}
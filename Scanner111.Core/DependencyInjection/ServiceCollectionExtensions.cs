using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Data;
using Scanner111.Core.Discovery;
using Scanner111.Core.IO;
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
        {
            // Default configuration
            services.Configure<FormIdDatabaseOptions>(options =>
            {
                options.MaxConnections = 5;
                options.CacheExpirationMinutes = 30;
                options.EnableFormIdLookups = true;
                // Database paths will need to be configured based on game
            });
        }

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
}
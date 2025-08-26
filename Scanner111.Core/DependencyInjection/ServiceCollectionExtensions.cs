using Microsoft.Extensions.DependencyInjection;
using Scanner111.Core.Configuration;
using Scanner111.Core.Discovery;
using Scanner111.Core.IO;

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
        services.AddSingleton<IYamlSettingsCache, YamlSettingsCache>();

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
        services.AddSingleton<IYamlSettingsCache, YamlSettingsCache>();

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

        return services;
    }
}
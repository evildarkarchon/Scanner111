using Microsoft.Extensions.DependencyInjection;
using Scanner111.Core.Configuration;
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
}
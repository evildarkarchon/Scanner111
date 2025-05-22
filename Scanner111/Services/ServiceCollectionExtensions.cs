using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.ClassicLib.Papyrus.Services;
using Scanner111.ClassicLib.ScanLog.Services;

namespace Scanner111.Services;

/// <summary>
///     Extension methods for registering services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds the game context service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddGameContextService(this IServiceCollection services)
    {
        services.AddSingleton<IGameContextService, GameContextService>();
        return services;
    }

    /// <summary>
    ///     Adds the YAML settings cache service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddYamlSettingsCache(this IServiceCollection services)
    {
        // Register the singleton instance factory
        services.AddSingleton<IYamlSettingsCache>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<YamlSettingsCache>>();
            var gameContextService = provider.GetRequiredService<IGameContextService>();

            // Initialize the singleton and return it
            return YamlSettingsCache.Initialize(logger, gameContextService);
        });

        return services;
    }

    /// <summary>
    ///     Adds the Papyrus log service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddPapyrusLogService(this IServiceCollection services)
    {
        services.AddTransient<IPapyrusLogService, PapyrusLogService>();
        return services;
    }

    /// <summary>
    ///     Adds the file service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddFileService(this IServiceCollection services)
    {
        services.AddSingleton<IFileService, FileService>();
        return services;
    }

    /// <summary>
    ///     Adds the game scan service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddGameScanService(this IServiceCollection services)
    {
        services.AddSingleton<IGameScanService, GameScanService>();
        return services;
    }

    /// <summary>
    ///     Adds all application services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add logging
        services.AddLogging(configure => { configure.AddConsole(); });

        // Add game context service (must be added before YAML settings cache)
        services.AddGameContextService();
        // Add YAML settings cache
        services.AddYamlSettingsCache();
        // Add crash log scanning services
        services.AddCrashLogScanServices();

        // Add Papyrus log service
        services.AddPapyrusLogService();

        // Add file service
        services.AddFileService();

        // Add game scan service
        services.AddGameScanService();

        // Add other services as needed

        return services;
    }
}
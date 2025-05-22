using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Scanner111.Services;

/// <summary>
/// Extension methods for registering services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the game context service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddGameContextService(this IServiceCollection services)
    {
        services.AddSingleton<IGameContextService, GameContextService>();
        
        // Also register with the GlobalRegistry for backward compatibility
        services.AddSingleton<IServiceProvider>(sp => 
        {
            var gameContextService = sp.GetRequiredService<IGameContextService>();
            GlobalRegistry.Register(GlobalRegistry.Keys.CurrentGame, gameContextService.GetCurrentGame());
            return sp;
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds the YAML settings cache service to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddYamlSettingsCache(this IServiceCollection services)
    {
        services.AddSingleton<IYamlSettingsCache, YamlSettingsCache>();
        
        // Also register with the GlobalRegistry for backward compatibility
        services.AddSingleton<IServiceProvider>(sp => 
        {
            var yamlCache = sp.GetRequiredService<IYamlSettingsCache>();
            GlobalRegistry.Register(GlobalRegistry.Keys.YamlCache, yamlCache);
            return sp;
        });
        
        return services;
    }
    
    /// <summary>
    /// Adds all application services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add logging
        services.AddLogging(configure => 
        {
            configure.AddConsole();
        });
        
        // Add game context service (must be added before YAML settings cache)
        services.AddGameContextService();
        
        // Add YAML settings cache
        services.AddYamlSettingsCache();
        
        // Add other services as needed
        
        return services;
    }
    
    /// <summary>
    /// Adds backward compatibility with GlobalRegistry pattern.
    /// This is a transitional feature that should be removed once all code has been migrated to DI.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddGlobalRegistryCompatibility(this IServiceCollection services)
    {
        services.AddSingleton<IServiceProvider>(sp => 
        {
            // Register the service provider itself
            GlobalRegistry.Register("ServiceProvider", sp);
            
            // Register other core services
            var yamlCache = sp.GetRequiredService<IYamlSettingsCache>();
            GlobalRegistry.Register(GlobalRegistry.Keys.YamlCache, yamlCache);
            
            var gameContextService = sp.GetRequiredService<IGameContextService>();
            GlobalRegistry.Register(GlobalRegistry.Keys.CurrentGame, gameContextService.GetCurrentGame());
            
            return sp;
        });
        
        return services;
    }
}
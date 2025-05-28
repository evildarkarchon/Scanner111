using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Scanner111.Services.Configuration;

/// <summary>
/// Extension methods for registering configuration services
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Adds the YAML configuration service to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddYamlConfiguration(this IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, YamlConfigurationService>();
        return services;
    }

    /// <summary>
    /// Initializes the configuration service with default files and preloading
    /// </summary>
    /// <param name="serviceProvider">The service provider</param>
    /// <returns>A task representing the initialization</returns>
    public static async Task InitializeConfigurationAsync(this IServiceProvider serviceProvider)
    {
        var configService = serviceProvider.GetRequiredService<IConfigurationService>();
        var logger = serviceProvider.GetRequiredService<ILogger<YamlConfigurationService>>();

        try
        {
            await configService.EnsureDefaultFilesAsync();
            await configService.PreloadStaticFilesAsync();
            logger.LogInformation("Configuration service initialized successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize configuration service");
            throw;
        }
    }
}
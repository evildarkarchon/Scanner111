using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.Services.Interfaces;
using Scanner111.ViewModels;

namespace Scanner111.Services;

/// <summary>
///     Extension methods for setting up services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds all application services to the service collection
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Add update services
        services.AddUpdateServices();

        // Register other application services here
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IGameScanService, GameScanService>();

        return services;
    }

    /// <summary>
    ///     Adds the update service and related components to the service collection
    /// </summary>
    public static IServiceCollection AddUpdateServices(this IServiceCollection services)
    {
        // Register the YamlSettingsAdapter as implementation of IYamlSettingsService
        services.AddSingleton<IYamlSettingsService, YamlSettingsAdapter>();

        // Register the GameContextService
        services.AddSingleton<IGameContextService, GameContextService>();

        // Register the service as a singleton since it doesn't maintain mutable state
        services.AddSingleton<IUpdateService, UpdateService>();

        // Register the ViewModel
        services.AddTransient<UpdateViewModel>();

        // Register a shared HttpClient for the service
        services.AddSingleton<HttpClient>();

        return services;
    }
}
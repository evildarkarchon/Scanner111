using System;
using System.Net.Http;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        // Register services with appropriate lifetimes
        services.AddSingleton<ICrashLogFileService, CrashLogFileService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IModDetectionService, ModDetectionService>();
        services.AddSingleton<IReportWriterService, ReportWriterService>();

        // Register the main scan service with its dependencies
        services.AddSingleton<ICrashLogScanService, CrashLogScanService>();

        // Register other application services here
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFileService, FileService>();
        services.AddSingleton<IGameScanService, GameScanService>();

        // Register YamlSettingsCache as a singleton
        services.AddSingleton<IYamlSettingsCache>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<YamlSettingsCache>>();
            var gameContextService = sp.GetRequiredService<IGameContextService>();

            // Create the singleton instance using reflection since the constructor is private
            var instance = Activator.CreateInstance(
                typeof(YamlSettingsCache),
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                [logger, gameContextService],
                null) as YamlSettingsCache;

            // Set the singleton instance
            if (instance != null)
                YamlSettingsCache.SetInstance(instance);

            return instance!;
        });

        return services;
    }
}
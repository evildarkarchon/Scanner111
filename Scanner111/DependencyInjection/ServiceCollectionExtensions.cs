using Microsoft.Extensions.DependencyInjection;
using Scanner111.ClassicLib;

namespace Scanner111.DependencyInjection;

/// <summary>
/// Extension methods for configuring services in the application.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all services required by the Scanner111 application.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScanner111Services(this IServiceCollection services)
    {
        // Register core services
        RegisterCoreServices(services);

        // Register view models
        RegisterViewModels(services);

        // Register application services
        RegisterApplicationServices(services);

        return services;
    }

    private static void RegisterCoreServices(IServiceCollection services)
    {
        // Register ClassicLib services
        services.AddSingleton<IFileSystem, FileSystem>();
        services.AddSingleton<IGameRegistry, GameRegistry>();
        services.AddSingleton<YamlSettingsCache>();
        services.AddSingleton<YamlSettings>();
        services.AddSingleton<ClassicSettings>();

        // Configure game options
        services.Configure<GameOptions>(options =>
        {
            options.Game = "Fallout4"; // Default game
            options.VR = ""; // Default VR setting
        });
    }

    private static void RegisterViewModels(IServiceCollection services)
    {
        // TODO: Register your view models
        // Example: services.AddTransient<MainWindowViewModel>();
    }

    private static void RegisterApplicationServices(IServiceCollection services)
    {
        // TODO: Register application-specific services
        // Example: services.AddSingleton<ISettingsManager, YamlSettingsManager>();
    }
}
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection; // Added for DI
using Scanner111.Models;
using Scanner111.ViewModels;
using Scanner111.Views;
using Scanner111.Services; // Added for ServiceCollectionExtensions

namespace Scanner111;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var serviceProvider = ConfigureServices();

        BuildAvaloniaApp(serviceProvider)
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider serviceProvider) =>
        AppBuilder.Configure(() => new App(serviceProvider))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Register AppSettings - placeholder for actual loading logic
        services.AddSingleton(new AppSettings());

        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();

        // Call extension method to register other services
        services.AddCustomServices();

        // Register other services (e.g., file service, API clients) here if not using extensions
        // services.AddSingleton<IFileService, FileService>();

        return services.BuildServiceProvider();
    }
}

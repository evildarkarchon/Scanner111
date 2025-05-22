using System;
using System.Threading.Tasks;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.Models;
using Scanner111.Services;
using Scanner111.ViewModels;

// Added for DI

// Added for ServiceCollectionExtensions

namespace Scanner111;

internal class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting application...");
            Console.WriteLine("Configuring services...");
            var serviceProvider = ConfigureServices();
            Console.WriteLine("Services configured successfully.");

            // Initialize paths asynchronously and wait for completion
            try
            {
                Console.WriteLine("Initializing application paths...");
                InitializeApplicationPathsAsync(serviceProvider).Wait();
                Console.WriteLine("Application paths initialized successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing paths: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
                // Continue anyway to show the UI
            }

            Console.WriteLine("Starting Avalonia UI...");
            var appBuilder = BuildAvaloniaApp(serviceProvider);
            Console.WriteLine("Avalonia app builder created successfully.");
            Console.WriteLine("Starting with classic desktop lifetime...");
            appBuilder.StartWithClassicDesktopLifetime(args);
            Console.WriteLine(
                "Application completed successfully."); // This may not be reached as UI runs on main thread
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fatal error in application: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");

            // Wait for user input before closing
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    private static async Task InitializeApplicationPathsAsync(IServiceProvider serviceProvider)
    {
        var directoryService = serviceProvider.GetRequiredService<IGameDirectoryService>();
        await directoryService.InitializePathsAsync();
    }

    public static AppBuilder BuildAvaloniaApp(IServiceProvider serviceProvider)
    {
        try
        {
            Console.WriteLine("Building Avalonia app...");

            // Create the App instance with the service provider
            var app = new App(serviceProvider);
            Console.WriteLine("App instance created successfully.");

            var builder = AppBuilder.Configure(() => app)
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

            Console.WriteLine("Avalonia app built successfully.");
            return builder;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error building Avalonia app: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

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
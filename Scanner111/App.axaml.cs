using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.ViewModels;
using Scanner111.Views;
using Scanner111.Services;
using Scanner111.Services.Configuration;
using System;
using Scanner111.Services.CrashLog;
using Scanner111.ViewModels.Tabs;
using System.Threading.Tasks;

namespace Scanner111;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            // Set up dependency injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // Initialize configuration service in a safe way - we can use Task.Run to prevent deadlocks
            InitializeConfigurationSafe().ConfigureAwait(false);

            // Create main window with dependency injection
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                desktop.MainWindow = mainWindow;

                // Handle application exit
                desktop.ShutdownRequested += OnShutdownRequested;
            }
            else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            {
                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                singleViewPlatform.MainView = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur during initialization
            var logger = _serviceProvider?.GetService<ILogger<App>>();
            logger?.LogError(ex, "Unhandled exception in OnFrameworkInitializationCompleted");
            // Still call base to ensure the app can continue if possible
            base.OnFrameworkInitializationCompleted();
        }
    }

    private async Task InitializeConfigurationSafe()
    {
        try
        {
            await _serviceProvider!.InitializeConfigurationAsync();
        }
        catch (Exception ex)
        {
            // Log error but continue - app can still run with defaults
            var logger = _serviceProvider?.GetService<ILogger<App>>();
            logger?.LogError(ex, "Failed to initialize configuration service");
        }
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        try
        {
            _serviceProvider?.Dispose();
        }
        catch (Exception ex)
        {
            // Log cleanup errors but don't prevent shutdown
            var logger = _serviceProvider?.GetService<ILogger<App>>();
            logger?.LogError(ex, "Error during application shutdown cleanup");
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add logging with console provider
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
#if DEBUG
            builder.SetMinimumLevel(LogLevel.Debug);
#endif
        });

        // Add configuration service
        services.AddYamlConfiguration();

        // Add other services
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IEnhancedDialogService, EnhancedDialogService>();
        services.AddSingleton<GameConfigurationHelper>();
        services.AddSingleton<ICrashLogValidationService, CrashLogValidationService>();

        // Add ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<MainTabViewModel>();
        services.AddTransient<SettingsTabViewModel>();
        services.AddTransient<ArticlesTabViewModel>();
        services.AddTransient<BackupsTabViewModel>();

        // Add Views
        services.AddTransient<MainWindow>();
    }
}
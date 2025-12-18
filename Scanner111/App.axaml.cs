using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Common.DependencyInjection;
using Scanner111.Common.Services.Logging;
using Scanner111.Common.Services.Updates;
using Scanner111.Services;
using Scanner111.ViewModels;
using Scanner111.Views;
using Serilog;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Scanner111;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure Serilog early before services are built
        var logPathProvider = new LogPathProvider();
        ConfigureSerilog(logPathProvider);

        var services = new ServiceCollection();
        ConfigureServices(services, logPathProvider);
        Services = services.BuildServiceProvider();

        // Log application startup
        var logger = Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Scanner111 application started");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
            desktop.ShutdownRequested += (_, _) =>
            {
                logger.LogInformation("Scanner111 application shutting down");
                Log.CloseAndFlush();
            };

            // Perform startup update check if enabled (non-blocking)
            _ = PerformStartupUpdateCheckAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureSerilog(ILogPathProvider logPathProvider)
    {
        var logDirectory = logPathProvider.GetLogDirectory();
        Directory.CreateDirectory(logDirectory);

        var logPath = logPathProvider.GetLogFilePath();

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.File(
                path: logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static void ConfigureServices(IServiceCollection services, ILogPathProvider logPathProvider)
    {
        // Register shared Scanner111.Common services
        services.AddScanner111CommonServices(logPathProvider);

        // Add Serilog for file-based logging (GUI-specific)
        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        // GUI-specific services
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IScanResultsService, ScanResultsService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IBackupService, BackupService>();

        // ViewModels (transient - new instance per navigation)
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HomePageViewModel>();
        services.AddTransient<BackupsViewModel>();
        services.AddTransient<ArticlesViewModel>();
        services.AddTransient<PapyrusMonitorViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<ResultsViewModel>();

        // ViewModel factory delegates for navigation
        services.AddTransient<Func<SettingsViewModel>>(sp => () => sp.GetRequiredService<SettingsViewModel>());
        services.AddTransient<Func<HomePageViewModel>>(sp => () => sp.GetRequiredService<HomePageViewModel>());
        services.AddTransient<Func<ResultsViewModel>>(sp => () => sp.GetRequiredService<ResultsViewModel>());
        services.AddTransient<Func<BackupsViewModel>>(sp => () => sp.GetRequiredService<BackupsViewModel>());
        services.AddTransient<Func<PapyrusMonitorViewModel>>(sp => () => sp.GetRequiredService<PapyrusMonitorViewModel>());
        services.AddTransient<Func<AboutViewModel>>(sp => () => sp.GetRequiredService<AboutViewModel>());

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();
    }

    private static async Task PerformStartupUpdateCheckAsync()
    {
        try
        {
            var settingsService = Services.GetRequiredService<ISettingsService>();
            if (!settingsService.CheckForUpdatesOnStartup)
            {
                return;
            }

            var updateService = Services.GetRequiredService<IUpdateService>();
            var result = await updateService.CheckForUpdatesAsync(settingsService.IncludePrereleases);

            if (result.Success && result.IsUpdateAvailable && result.LatestRelease is not null)
            {
                var logger = Services.GetRequiredService<ILogger<App>>();
                logger.LogInformation(
                    "Update available: {CurrentVersion} -> {LatestVersion}",
                    result.CurrentVersion,
                    result.LatestRelease.Version);
            }
        }
        catch (Exception ex)
        {
            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogWarning(ex, "Failed to check for updates on startup");
        }
    }
}
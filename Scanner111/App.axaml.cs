using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.Common.Services.Analysis;
using Scanner111.Common.Services.Configuration;
using Scanner111.Common.Services.Database;
using Scanner111.Common.Services.FileIO;
using Scanner111.Common.Services.Orchestration;
using Scanner111.Common.Services.Parsing;
using Scanner111.Common.Services.PathValidation;
using Scanner111.Common.Services.Reporting;
using Scanner111.Common.Services.DocsPath;
using Scanner111.Common.Services.ScanGame;
using Scanner111.Common.Services.Settings;
using Scanner111.ViewModels;
using Scanner111.Views;
using System; // Required for IServiceProvider
using Scanner111.Services; // Required for IDialogService and DialogService

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
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register common services
        services.AddSingleton<IFileIOService, FileIOService>();
        services.AddSingleton<ILogParser, LogParser>();
        services.AddSingleton<IPluginAnalyzer, PluginAnalyzer>();
        services.AddSingleton<ISuspectScanner, SuspectScanner>();
        services.AddSingleton<ISettingsScanner, SettingsScanner>(); // Assuming implementation exists
        services.AddSingleton<IReportWriter, ReportWriter>();
        services.AddSingleton<IYamlConfigLoader, YamlConfigLoader>();
        services.AddSingleton<IConfigurationCache, ConfigurationCache>(); // Assuming implementation exists

        // Path validation and user settings services
        services.AddSingleton<IUserSettingsService, UserSettingsService>();
        services.AddSingleton<IPathValidator, PathValidator>();
        services.AddSingleton<IDatabaseConnectionFactory>(provider =>
            new SqliteDatabaseConnectionFactory("path_to_your_db.sqlite")); // TODO: Get actual path from config
        services.AddSingleton<IFormIdAnalyzer, FormIdAnalyzer>();

        // ScanGame services
        services.AddSingleton<IIniValidator, IniValidator>();
        services.AddSingleton<IDocsPathDetector, DocsPathDetector>();

        // Orchestration services (often transient or scoped if stateful per-request)
        // LogOrchestrator can be transient if it's processing one log per instance
        services.AddTransient<ILogOrchestrator, LogOrchestrator>();

        // Register factory delegate for LogOrchestrator (used by ScanExecutor)
        services.AddSingleton<Func<ILogOrchestrator>>(sp => () => sp.GetRequiredService<ILogOrchestrator>());
        services.AddSingleton<IScanExecutor, ScanExecutor>();

        // Register ViewModels as transient, as new instances are typically created for each view
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<HomePageViewModel>();
        services.AddTransient<BackupsViewModel>();
        services.AddTransient<ArticlesViewModel>();

        // Register factory delegates for ViewModel navigation
        services.AddTransient<Func<SettingsViewModel>>(sp => () => sp.GetRequiredService<SettingsViewModel>());
        services.AddTransient<Func<HomePageViewModel>>(sp => () => sp.GetRequiredService<HomePageViewModel>());
        services.AddTransient<Func<ResultsViewModel>>(sp => () => sp.GetRequiredService<ResultsViewModel>());
        services.AddTransient<Func<BackupsViewModel>>(sp => () => sp.GetRequiredService<BackupsViewModel>());

        // Register Views (MainWindow needs to resolve its ViewModel)
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>(); // Register SettingsWindow

        // Register DialogService
        services.AddSingleton<IDialogService, DialogService>();

        // Register Scan Results Service (shared state for results)
        services.AddSingleton<IScanResultsService, ScanResultsService>();

        // Register Settings Service (shared settings state)
        services.AddSingleton<ISettingsService, SettingsService>();

        // Register Backup Service
        services.AddSingleton<IBackupService, BackupService>();

        // Register ResultsViewModel (uses shared results service)
        services.AddTransient<ResultsViewModel>();
    }
}
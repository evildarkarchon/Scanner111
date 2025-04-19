// Scanner111.UI/App.axaml.cs
using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Application.Interfaces.Services;
using Scanner111.Application.Services;
using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Infrastructure.Persistence;
using Scanner111.Infrastructure.Persistence.Extensions;
using Scanner111.Infrastructure.Persistence.Repositories;
using Scanner111.Infrastructure.Services;
using Scanner111.Plugins.Fallout4;
using Scanner111.Plugins.Interface.Services;
using Scanner111.UI.Services;
using Scanner111.UI.ViewModels;
using Scanner111.UI.Views;

namespace Scanner111.UI;

public partial class App : Avalonia.Application
{
    private ServiceProvider _serviceProvider = null!;
    private ILogger<App>? _logger;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        
        // Get logger
        _logger = _serviceProvider.GetService<ILogger<App>>();
        _logger?.LogInformation("Application starting...");

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Create main window
            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
            };
            
            // Initialize the database in the background
            Task.Run(async () => await InitializeDatabaseAsync());
            
            // Initialize the main view model
            if (desktop.MainWindow.DataContext is MainWindowViewModel mainViewModel)
            {
                mainViewModel.Initialize();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add logging
        services.AddLogging(configure => configure
            .AddConsole()
            .AddDebug()
            .SetMinimumLevel(LogLevel.Information));

        // Database configuration
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scanner111",
            "scanner111.db");
            
        // Create the directory if it doesn't exist
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath}"));

        // Register repositories
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<ICrashLogRepository, CrashLogRepository>();
        services.AddScoped<IPluginRepository, PluginRepository>();

        // Register services
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<ILogAnalyzerService, LogAnalyzerService>();
        services.AddSingleton<IGameDetectionService, GameDetectionService>();
        services.AddSingleton<IPluginSystemService, PluginSystemService>();
        services.AddScoped<IPluginService, PluginService>();

        // Register application services
        services.AddScoped<GameService>();
        services.AddScoped<CrashLogService>();
        services.AddScoped<PluginAnalysisService>();

        // Register plugin host
        services.AddSingleton<IPluginHost, PluginHost>();

        // Register plugins
        services.AddSingleton<Fallout4Plugin>();
        services.AddSingleton<IYamlCompatibilityService, ClassicYamlCompatibilityService>();

        // Register view models
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<CrashLogListViewModel>();
        services.AddTransient<CrashLogDetailViewModel>();
        services.AddTransient<GameListViewModel>();
        services.AddTransient<GameDetailViewModel>();
        services.AddTransient<PluginAnalysisViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
    
    private async Task InitializeDatabaseAsync()
    {
        try
        {
            _logger?.LogInformation("Initializing database...");
            
            // Ensure database and migrations
            await _serviceProvider.EnsureDatabaseCreatedAndMigratedAsync(_logger);
            
            // Seed initial data
            await _serviceProvider.SeedInitialDataAsync(_logger);
            
            // Register plugins
            await InitializePluginsAsync();
            
            _logger?.LogInformation("Database initialization completed successfully.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize database.");
        }
    }
    
    private async Task InitializePluginsAsync()
    {
        try
        {
            _logger?.LogInformation("Initializing plugins...");
            
            var pluginSystemService = _serviceProvider.GetRequiredService<IPluginSystemService>();
            var fallout4Plugin = _serviceProvider.GetRequiredService<Fallout4Plugin>();
            
            // Register the Fallout 4 plugin
            await pluginSystemService.RegisterPluginAsync(fallout4Plugin);
            
            // Initialize the plugin system
            await pluginSystemService.InitializeAsync();
            
            _logger?.LogInformation("Plugins initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize plugins.");
        }
    }
}
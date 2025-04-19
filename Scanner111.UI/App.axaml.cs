// Scanner111.UI/App.axaml.cs

using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.Application.Services;
using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Infrastructure.Persistence;
using Scanner111.Infrastructure.Persistence.Repositories;
using Scanner111.Infrastructure.Services;
using Scanner111.Plugins.Fallout4;
using Scanner111.Plugins.Interface.Services;
using Scanner111.UI.Services;
using Scanner111.UI.ViewModels;
using Scanner111.UI.Views;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Scanner111.Application.Interfaces.Services;

namespace Scanner111.UI;

public partial class App : Avalonia.Application
{
    private ServiceProvider _serviceProvider = null!;

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

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Initialize the database
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            dbContext.Database.Migrate();

            // Register the Fallout 4 plugin
            var pluginSystemService = scope.ServiceProvider.GetRequiredService<IPluginSystemService>();
            var fallout4Plugin = scope.ServiceProvider.GetRequiredService<Fallout4Plugin>();
            pluginSystemService.RegisterPluginAsync(fallout4Plugin).GetAwaiter().GetResult();
            
            // Initialize the plugin system
            pluginSystemService.InitializeAsync().GetAwaiter().GetResult();

            // Create main window
            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
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
}
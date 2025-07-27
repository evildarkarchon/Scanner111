using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;
using Scanner111.GUI.Services;
using Scanner111.GUI.ViewModels;
using Scanner111.GUI.Views;

namespace Scanner111.GUI;

public class App : Application
{
    private IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the Avalonia application by loading its XAML resources.
    /// </summary>
    /// <remarks>
    /// This method is overridden from the base <see cref="Application"/> class to
    /// load the XAML resources associated with the application. It ensures that
    /// all styles, templates, and other UI components defined in XAML are properly
    /// initialized before the application starts running.
    /// </remarks>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Called when the framework has completed its initialization process.
    /// </summary>
    /// <remarks>
    /// This method is overridden to provide additional setup for the application's
    /// startup sequence. It configures dependency injection and ensures that the main window
    /// is created and assigned to the application's lifetime. If the application is configured
    /// to use a <see cref="IClassicDesktopStyleApplicationLifetime"/>, the main window
    /// will be set to an instance of <c>MainWindow</c> with proper DI integration.
    /// After performing the custom initialization, the method calls the base implementation
    /// to complete the default framework setup process.
    /// </remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        // Setup dependency injection
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Create main window with dependency injection
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Configures the dependency injection container with all required services.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    private static void ConfigureServices(IServiceCollection services)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Add memory cache for CacheManager
        services.AddMemoryCache();

        // Register Core services
        services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<FormIdDatabaseService>();
        services.AddTransient<IReportWriter, ReportWriter>();
        services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
        services.AddSingleton<ICacheManager, CacheManager>();
        services.AddSingleton<IHashValidationService, HashValidationService>();
        services.AddSingleton<IUnsolvedLogsMover, UnsolvedLogsMover>();

        // Register Pipeline services
        services.AddTransient<IScanPipeline, ScanPipeline>();
        services.AddTransient<ScanPipelineBuilder>();

        // Register Analyzers
        services.AddTransient<FormIdAnalyzer>();
        services.AddTransient<PluginAnalyzer>();
        services.AddTransient<RecordScanner>();
        services.AddTransient<SettingsScanner>();
        services.AddTransient<SuspectScanner>();
        services.AddTransient<FileIntegrityAnalyzer>();
        services.AddTransient<BuffoutVersionAnalyzerV2>();

        // Register GUI services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<GuiMessageHandlerService>();
        services.AddSingleton<IMessageHandler>(provider => provider.GetRequiredService<GuiMessageHandlerService>());

        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsWindowViewModel>();

        // Register Views
        services.AddTransient<MainWindow>();
        services.AddTransient<SettingsWindow>();
    }
}
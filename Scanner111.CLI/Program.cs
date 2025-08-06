using System.Text;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;
using Scanner111.Core.Analyzers;
using Scanner111.Core.FCX;
using Scanner111.Core.Pipeline;

// Ensure UTF-8 encoding for Windows console
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
}

// Parse command line arguments to check for legacy progress option early
var useLegacyProgress = args.Contains("--legacy-progress");

// Setup dependency injection
var services = new ServiceCollection();
ConfigureServices(services, useLegacyProgress);
var serviceProvider = services.BuildServiceProvider();

// Perform startup update check
await PerformStartupUpdateCheckAsync(serviceProvider);

// Check if running in interactive mode (no arguments provided)
if (args.Length == 0 && Environment.UserInteractive)
{
    var uiService = serviceProvider.GetRequiredService<ITerminalUIService>();
    return await uiService.RunInteractiveMode();
}

// Parse command line arguments
var parser = new Parser(with => with.HelpWriter = Console.Error);
var result = parser.ParseArguments<Scanner111.CLI.Models.ScanOptions, DemoOptions, ConfigOptions, AboutOptions, FcxOptions, InteractiveOptions>(args);

return await result.MapResult(
    async (Scanner111.CLI.Models.ScanOptions opts) => await serviceProvider.GetRequiredService<ScanCommand>().ExecuteAsync(opts),
    async (DemoOptions opts) => await serviceProvider.GetRequiredService<DemoCommand>().ExecuteAsync(opts),
    async (ConfigOptions opts) => await serviceProvider.GetRequiredService<ConfigCommand>().ExecuteAsync(opts),
    async (AboutOptions opts) => await serviceProvider.GetRequiredService<AboutCommand>().ExecuteAsync(opts),
    async (FcxOptions opts) => await serviceProvider.GetRequiredService<FcxCommand>().ExecuteAsync(opts),
    async (InteractiveOptions opts) => await serviceProvider.GetRequiredService<InteractiveCommand>().ExecuteAsync(opts),
    async errs => await Task.FromResult(1));

static void ConfigureServices(IServiceCollection services, bool useLegacyProgress = false)
{
    // Configure logging
    services.AddLogging(builder =>
    {
        builder.AddConsole();
        builder.SetMinimumLevel(LogLevel.Information);
    });

    // Register Core services
    services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
    services.AddSingleton<IUpdateService, UpdateService>();
    services.AddSingleton<ICacheManager, CacheManager>();
    services.AddSingleton<IUnsolvedLogsMover, UnsolvedLogsMover>();

    // Register CLI services
    services.AddSingleton<ICliSettingsService, CliSettingsService>();
    services.AddSingleton<IFileScanService, FileScanService>();
    services.AddSingleton<IScanResultProcessor, ScanResultProcessor>();
    
    // Use enhanced message handler by default, legacy if specified
    if (useLegacyProgress)
    {
        services.AddSingleton<IMessageHandler, SpectreMessageHandler>();
    }
    else
    {
        services.AddSingleton<IMessageHandler, EnhancedSpectreMessageHandler>();
    }
    
    services.AddSingleton<ITerminalUIService, SpectreTerminalUIService>();

    // Register FCX services
    services.AddSingleton<IHashValidationService, HashValidationService>();
    services.AddSingleton<IBackupService, BackupService>();
    services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
    services.AddSingleton<IModScanner, ModScanner>();
    services.AddSingleton<IModCompatibilityService, ModCompatibilityService>();
    
    // Register commands
    services.AddTransient<ScanCommand>();
    services.AddTransient<DemoCommand>();
    services.AddTransient<ConfigCommand>();
    services.AddTransient<AboutCommand>();
    services.AddTransient<FcxCommand>();
    services.AddTransient<InteractiveCommand>();
    
    // Register ICommand interfaces for injection
    services.AddTransient<ICommand<Scanner111.CLI.Models.ScanOptions>, ScanCommand>();
    services.AddTransient<ICommand<DemoOptions>, DemoCommand>();
    services.AddTransient<ICommand<ConfigOptions>, ConfigCommand>();
    services.AddTransient<ICommand<AboutOptions>, AboutCommand>();
    services.AddTransient<ICommand<FcxOptions>, FcxCommand>();
    services.AddTransient<ICommand<InteractiveOptions>, InteractiveCommand>();
}

static async Task PerformStartupUpdateCheckAsync(IServiceProvider serviceProvider)
{
    try
    {
        var settingsService = serviceProvider.GetRequiredService<IApplicationSettingsService>();
        var settings = await settingsService.LoadSettingsAsync();
        
        if (settings.EnableUpdateCheck)
        {
            var updateService = serviceProvider.GetRequiredService<IUpdateService>();
            await updateService.IsLatestVersionAsync(quiet: false);
        }
    }
    catch (Exception ex)
    {
        var messageHandler = serviceProvider.GetRequiredService<IMessageHandler>();
        messageHandler.ShowDebug($"Update check failed during startup: {ex.Message}");
        // Don't fail the application if update check fails
    }
}
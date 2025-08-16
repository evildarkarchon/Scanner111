using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.FCX;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;
using ScanOptions = Scanner111.CLI.Models.ScanOptions;

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
var result =
    parser
        .ParseArguments<ScanOptions, DemoOptions, ConfigOptions, AboutOptions, FcxOptions, InteractiveOptions,
            WatchOptions, StatsOptions, PapyrusOptions, PastebinOptions>(args);

return await result.MapResult(
    async (ScanOptions opts) => await serviceProvider.GetRequiredService<ScanCommand>().ExecuteAsync(opts),
    async (DemoOptions opts) => await serviceProvider.GetRequiredService<DemoCommand>().ExecuteAsync(opts),
    async (ConfigOptions opts) => await serviceProvider.GetRequiredService<ConfigCommand>().ExecuteAsync(opts),
    async (AboutOptions opts) => await serviceProvider.GetRequiredService<AboutCommand>().ExecuteAsync(opts),
    async (FcxOptions opts) => await serviceProvider.GetRequiredService<FcxCommand>().ExecuteAsync(opts),
    async (InteractiveOptions opts) =>
        await serviceProvider.GetRequiredService<InteractiveCommand>().ExecuteAsync(opts),
    async (WatchOptions opts) => await serviceProvider.GetRequiredService<WatchCommand>().ExecuteAsync(opts),
    async (StatsOptions opts) => await serviceProvider.GetRequiredService<StatsCommand>().ExecuteAsync(opts),
    async (PapyrusOptions opts) => await serviceProvider.GetRequiredService<PapyrusCommand>().ExecuteAsync(opts),
    async (PastebinOptions opts) => await serviceProvider.GetRequiredService<PastebinCommand>().ExecuteAsync(opts),
    async errs => await Task.FromResult(1));

static void ConfigureServices(IServiceCollection services, bool useLegacyProgress = false)
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
    services.AddSingleton<ICacheManager, CacheManager>();
    services.AddSingleton<IUnsolvedLogsMover, UnsolvedLogsMover>();
    services.AddSingleton<IAudioNotificationService, AudioNotificationService>();
    services.AddSingleton<IStatisticsService, StatisticsService>();
    services.AddSingleton<IRecentItemsService, RecentItemsService>();

    // Register Core infrastructure
    services.AddSingleton<IReportWriter, ReportWriter>();
    services.AddSingleton<IScanPipeline, ScanPipeline>();

    // Register CLI services
    services.AddSingleton<ICliSettingsService, CliSettingsService>();
    services.AddSingleton<IFileScanService, FileScanService>();
    services.AddSingleton<IScanResultProcessor, ScanResultProcessor>();

    // Use enhanced message handler by default, legacy if specified
    if (useLegacyProgress)
        services.AddSingleton<IMessageHandler, SpectreMessageHandler>();
    else
        services.AddSingleton<IMessageHandler, EnhancedSpectreMessageHandler>();

    services.AddSingleton<ITerminalUIService, SpectreTerminalUIService>();

    // Register FCX services
    services.AddSingleton<IHashValidationService, HashValidationService>();
    services.AddSingleton<IBackupService, BackupService>();
    services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
    services.AddSingleton<IModScanner, ModScanner>();
    services.AddSingleton<IModCompatibilityService, ModCompatibilityService>();
    services.AddSingleton<IConsoleService, ConsoleService>();

    // Register Mod Manager services
    services.AddSingleton<IModManagerDetector, ModManagerDetector>();
    services.AddSingleton<IModManagerService, ModManagerService>();

    // Register Papyrus Monitoring service
    services.AddSingleton<IPapyrusMonitorService, PapyrusMonitorService>();

    // Register Pastebin service
    services.AddSingleton<IPastebinService, PastebinService>();

    // Register commands
    services.AddTransient<ScanCommand>();
    services.AddTransient<DemoCommand>();
    services.AddTransient<ConfigCommand>();
    services.AddTransient<AboutCommand>();
    services.AddTransient<FcxCommand>();
    services.AddTransient<InteractiveCommand>();
    services.AddTransient<WatchCommand>();
    services.AddTransient<StatsCommand>();
    services.AddTransient<PapyrusCommand>();
    services.AddTransient<PastebinCommand>();

    // Register ICommand interfaces for injection
    services.AddTransient<ICommand<ScanOptions>, ScanCommand>();
    services.AddTransient<ICommand<DemoOptions>, DemoCommand>();
    services.AddTransient<ICommand<ConfigOptions>, ConfigCommand>();
    services.AddTransient<ICommand<AboutOptions>, AboutCommand>();
    services.AddTransient<ICommand<FcxOptions>, FcxCommand>();
    services.AddTransient<ICommand<InteractiveOptions>, InteractiveCommand>();
    services.AddTransient<ICommand<WatchOptions>, WatchCommand>();
    services.AddTransient<ICommand<StatsOptions>, StatsCommand>();
    services.AddTransient<ICommand<PapyrusOptions>, PapyrusCommand>();
    services.AddTransient<ICommand<PastebinOptions>, PastebinCommand>();
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
            // Use quiet mode during startup to avoid display issues before UI is ready
            await updateService.IsLatestVersionAsync(quiet: true);
        }
    }
    catch
    {
        // Silently ignore update check failures during startup
        // The user can manually check for updates later if needed
    }
}
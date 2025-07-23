using System.Text;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;

// Ensure UTF-8 encoding for Windows console
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = Encoding.UTF8;
    Console.InputEncoding = Encoding.UTF8;
}

// Setup dependency injection
var services = new ServiceCollection();
ConfigureServices(services);
var serviceProvider = services.BuildServiceProvider();

// Perform startup update check
await PerformStartupUpdateCheckAsync(serviceProvider);

// Parse command line arguments
var parser = new Parser(with => with.HelpWriter = Console.Error);
var result = parser.ParseArguments<ScanOptions, DemoOptions, ConfigOptions, AboutOptions>(args);

return await result.MapResult(
    async (ScanOptions opts) => await serviceProvider.GetRequiredService<ScanCommand>().ExecuteAsync(opts),
    async (DemoOptions opts) => await serviceProvider.GetRequiredService<DemoCommand>().ExecuteAsync(opts),
    async (ConfigOptions opts) => await serviceProvider.GetRequiredService<ConfigCommand>().ExecuteAsync(opts),
    async (AboutOptions opts) => await serviceProvider.GetRequiredService<AboutCommand>().ExecuteAsync(opts),
    async errs => await Task.FromResult(1));

static void ConfigureServices(IServiceCollection services)
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

    // Register CLI services
    services.AddSingleton<ICliSettingsService, CliSettingsService>();
    services.AddSingleton<IFileScanService, FileScanService>();
    services.AddSingleton<IScanResultProcessor, ScanResultProcessor>();
    services.AddSingleton<IMessageHandler, CliMessageHandler>();

    // Register commands
    services.AddTransient<ScanCommand>();
    services.AddTransient<DemoCommand>();
    services.AddTransient<ConfigCommand>();
    services.AddTransient<AboutCommand>();
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
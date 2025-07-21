using System.Text;
using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;

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
    // Register services
    services.AddSingleton<ICliSettingsService, CliSettingsService>();
    services.AddSingleton<IFileScanService, FileScanService>();
    services.AddSingleton<IScanResultProcessor, ScanResultProcessor>();

    // Register commands
    services.AddTransient<ScanCommand>();
    services.AddTransient<DemoCommand>();
    services.AddTransient<ConfigCommand>();
    services.AddTransient<AboutCommand>();
}
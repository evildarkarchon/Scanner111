using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Cli.Commands;
using Scanner111.Common.DependencyInjection;

namespace Scanner111.Cli;

/// <summary>
/// CLI entry point for Scanner111 crash log scanner.
/// </summary>
public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        var serviceProvider = services.BuildServiceProvider();

        // Build and invoke root command
        var rootCommand = ScanCommand.Create(serviceProvider);
        return await rootCommand.InvokeAsync(args).ConfigureAwait(false);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Add console logging (warnings and errors only to avoid cluttering output)
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Warning);
        });

        // Add Scanner111.Common services
        services.AddScanner111CommonServices();
    }
}

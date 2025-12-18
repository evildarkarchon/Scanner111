using System.CommandLine;

namespace Scanner111.Cli.Commands;

/// <summary>
/// Defines the root scan command and its options.
/// </summary>
public static class ScanCommand
{
    public static RootCommand Create(IServiceProvider serviceProvider)
    {
        var rootCommand = new RootCommand("Scanner111 - Crash Log Auto Scanner for Bethesda Games");

        // Define options
        var scanPathOption = new Option<DirectoryInfo?>(
            name: "--scan-path",
            description: "Path to scan for crash logs (auto-detects if not specified)");

        var fcxModeOption = new Option<bool>(
            name: "--fcx-mode",
            description: "Enable FCX (Fallout Crash Xtra) mode");

        var showFidValuesOption = new Option<bool>(
            name: "--show-fid-values",
            description: "Show FormID values in reports");

        var statLoggingOption = new Option<bool>(
            name: "--stat-logging",
            description: "Enable statistical logging");

        var moveUnsolvedOption = new Option<bool>(
            name: "--move-unsolved",
            description: "Move unsolved logs to separate directory");

        var simplifyLogsOption = new Option<bool>(
            name: "--simplify-logs",
            description: "Simplify logs by removing unnecessary sections");

        var iniPathOption = new Option<DirectoryInfo?>(
            name: "--ini-path",
            description: "Custom path to INI files");

        var modsFolderPathOption = new Option<DirectoryInfo?>(
            name: "--mods-folder-path",
            description: "Custom path to mods folder");

        var concurrencyOption = new Option<int>(
            name: "--concurrency",
            getDefaultValue: () => 50,
            description: "Maximum concurrent log processing tasks (1-100)");

        var quietOption = new Option<bool>(
            name: "--quiet",
            description: "Suppress progress output");
        quietOption.AddAlias("-q");

        // Add options to command
        rootCommand.AddOption(scanPathOption);
        rootCommand.AddOption(fcxModeOption);
        rootCommand.AddOption(showFidValuesOption);
        rootCommand.AddOption(statLoggingOption);
        rootCommand.AddOption(moveUnsolvedOption);
        rootCommand.AddOption(simplifyLogsOption);
        rootCommand.AddOption(iniPathOption);
        rootCommand.AddOption(modsFolderPathOption);
        rootCommand.AddOption(concurrencyOption);
        rootCommand.AddOption(quietOption);

        // Set handler
        rootCommand.SetHandler(async (context) =>
        {
            var scanPath = context.ParseResult.GetValueForOption(scanPathOption);
            var fcxMode = context.ParseResult.GetValueForOption(fcxModeOption);
            var showFidValues = context.ParseResult.GetValueForOption(showFidValuesOption);
            var statLogging = context.ParseResult.GetValueForOption(statLoggingOption);
            var moveUnsolved = context.ParseResult.GetValueForOption(moveUnsolvedOption);
            var simplifyLogs = context.ParseResult.GetValueForOption(simplifyLogsOption);
            var iniPath = context.ParseResult.GetValueForOption(iniPathOption);
            var modsFolderPath = context.ParseResult.GetValueForOption(modsFolderPathOption);
            var concurrency = context.ParseResult.GetValueForOption(concurrencyOption);
            var quiet = context.ParseResult.GetValueForOption(quietOption);

            // Validate concurrency
            if (concurrency < 1 || concurrency > 100)
            {
                Console.Error.WriteLine("Error: --concurrency must be between 1 and 100");
                context.ExitCode = ExitCodes.InvalidArguments;
                return;
            }

            var handler = new ScanCommandHandler(serviceProvider);
            context.ExitCode = await handler.ExecuteAsync(
                scanPath,
                fcxMode,
                showFidValues,
                statLogging,
                moveUnsolved,
                simplifyLogs,
                iniPath,
                modsFolderPath,
                concurrency,
                quiet,
                context.GetCancellationToken());
        });

        return rootCommand;
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Cli.Progress;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Services.DocsPath;
using Scanner111.Common.Services.Orchestration;

namespace Scanner111.Cli.Commands;

/// <summary>
/// Handles the execution of the scan command.
/// </summary>
public class ScanCommandHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ScanCommandHandler> _logger;

    public ScanCommandHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetRequiredService<ILogger<ScanCommandHandler>>();
    }

    public async Task<int> ExecuteAsync(
        DirectoryInfo? scanPath,
        bool fcxMode,
        bool showFidValues,
        bool statLogging,
        bool moveUnsolved,
        bool simplifyLogs,
        DirectoryInfo? iniPath,
        DirectoryInfo? modsFolderPath,
        int concurrency,
        bool quiet,
        CancellationToken ct)
    {
        try
        {
            // Resolve scan path (auto-detect if not provided)
            var resolvedScanPath = await ResolveScanPathAsync(scanPath, ct).ConfigureAwait(false);
            if (resolvedScanPath is null)
            {
                ConsoleOutput.WriteError("Could not determine crash log location. Use --scan-path to specify.");
                return ExitCodes.ScanPathNotFound;
            }

            if (!Directory.Exists(resolvedScanPath))
            {
                ConsoleOutput.WriteError($"Scan path does not exist: {resolvedScanPath}");
                return ExitCodes.ScanPathNotFound;
            }

            // Build ScanConfig
            var config = BuildScanConfig(
                resolvedScanPath,
                fcxMode,
                showFidValues,
                moveUnsolved,
                simplifyLogs,
                iniPath,
                modsFolderPath,
                concurrency);

            // Display header
            if (!quiet)
            {
                ConsoleOutput.WriteHeader();
                ConsoleOutput.WriteInfo($"Scanning: {resolvedScanPath}");
                Console.WriteLine();
            }

            // Create progress reporter
            IProgress<ScanProgress>? progress = quiet
                ? null
                : new ConsoleProgressReporter();

            // Execute scan
            var executor = _serviceProvider.GetRequiredService<IScanExecutor>();
            var result = await executor.ExecuteScanAsync(config, progress, ct).ConfigureAwait(false);

            // Display summary
            if (!quiet)
            {
                Console.WriteLine();
            }
            ConsoleOutput.WriteSummary(result);

            // Always return success - failed logs are just statistics
            return ExitCodes.Success;
        }
        catch (OperationCanceledException)
        {
            ConsoleOutput.WriteWarning("Scan cancelled by user.");
            return ExitCodes.Cancelled;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scan failed");
            ConsoleOutput.WriteError($"Scan failed: {ex.Message}");
            return ExitCodes.UnexpectedError;
        }
    }

    private async Task<string?> ResolveScanPathAsync(DirectoryInfo? scanPath, CancellationToken ct)
    {
        if (scanPath is not null)
        {
            return scanPath.FullName;
        }

        // Auto-detect using DocsPathDetector
        var detector = _serviceProvider.GetRequiredService<IDocsPathDetector>();
        var result = await detector.DetectDocsPathAsync(GameType.Fallout4, ct).ConfigureAwait(false);

        if (!result.Found || result.DocsPath is null)
        {
            return null;
        }

        // Default crash log location for Fallout 4 (F4SE folder under game documents)
        return Path.Combine(result.DocsPath, "F4SE");
    }

    private static ScanConfig BuildScanConfig(
        string scanPath,
        bool fcxMode,
        bool showFidValues,
        bool moveUnsolved,
        bool simplifyLogs,
        DirectoryInfo? iniPath,
        DirectoryInfo? modsFolderPath,
        int concurrency)
    {
        var customPaths = new Dictionary<string, string>();

        if (iniPath is not null)
        {
            customPaths["ini_path"] = iniPath.FullName;
        }

        if (modsFolderPath is not null)
        {
            customPaths["mods_folder_path"] = modsFolderPath.FullName;
        }

        return new ScanConfig
        {
            ScanPath = scanPath,
            FcxMode = fcxMode,
            ShowFormIdValues = showFidValues,
            MoveUnsolvedLogs = moveUnsolved,
            SimplifyLogs = simplifyLogs,
            MaxConcurrent = concurrency,
            CustomPaths = customPaths
        };
    }
}

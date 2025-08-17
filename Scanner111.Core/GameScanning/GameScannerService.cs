using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.GameScanning;

/// <summary>
///     Main service for comprehensive game scanning functionality.
///     Orchestrates all individual game scanners.
/// </summary>
public class GameScannerService : IGameScannerService
{
    private readonly ICrashGenChecker _crashGenChecker;
    private readonly ILogger<GameScannerService> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly IModIniScanner _modIniScanner;
    private readonly IWryeBashChecker _wryeBashChecker;
    private readonly IXsePluginValidator _xsePluginValidator;

    public GameScannerService(
        ICrashGenChecker crashGenChecker,
        IXsePluginValidator xsePluginValidator,
        IModIniScanner modIniScanner,
        IWryeBashChecker wryeBashChecker,
        IMessageHandler messageHandler,
        ILogger<GameScannerService> logger)
    {
        _crashGenChecker = crashGenChecker;
        _xsePluginValidator = xsePluginValidator;
        _modIniScanner = modIniScanner;
        _wryeBashChecker = wryeBashChecker;
        _messageHandler = messageHandler;
        _logger = logger;
    }

    public async Task<GameScanResult> ScanGameAsync(CancellationToken cancellationToken = default)
    {
        var result = new GameScanResult();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Starting comprehensive game scan");
        _messageHandler.ShowInfo("Starting comprehensive game scan...");

        try
        {
            // Run all scanners in parallel for better performance
            var tasks = new List<Task<(string type, string output)>>
            {
                RunScannerAsync("CrashGen", () => _crashGenChecker.CheckAsync(), cancellationToken),
                RunScannerAsync("XsePlugins", () => _xsePluginValidator.ValidateAsync(), cancellationToken),
                RunScannerAsync("ModInis", () => _modIniScanner.ScanAsync(), cancellationToken),
                RunScannerAsync("WryeBash", () => _wryeBashChecker.AnalyzeAsync(), cancellationToken)
            };

            var results = await Task.WhenAll(tasks);

            // Process results
            foreach (var (type, output) in results) ProcessScanResult(result, type, output);

            // Analyze for critical issues and warnings
            AnalyzeResults(result);

            stopwatch.Stop();
            _logger.LogInformation($"Game scan completed in {stopwatch.ElapsedMilliseconds}ms");
            if (result.HasIssues)
                _messageHandler.ShowWarning(
                    $"Game scan completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds with issues found");
            else
                _messageHandler.ShowSuccess($"Game scan completed in {stopwatch.Elapsed.TotalSeconds:F1} seconds");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Game scan was cancelled");
            _messageHandler.ShowWarning("Game scan cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during game scan");
            _messageHandler.ShowError($"Game scan failed: {ex.Message}");
            result.CriticalIssues.Add($"Scan failed: {ex.Message}");
            result.HasIssues = true;
        }

        return result;
    }

    public async Task<string> CheckCrashGenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _messageHandler.ShowInfo("Checking Crash Generator configuration...");
            var result = await _crashGenChecker.CheckAsync();
            _messageHandler.ShowSuccess("Crash Generator check completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Crash Generator");
            _messageHandler.ShowError($"Crash Generator check failed: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ValidateXsePluginsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _messageHandler.ShowInfo("Validating XSE plugins...");
            var result = await _xsePluginValidator.ValidateAsync();
            _messageHandler.ShowSuccess("XSE plugin validation completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating XSE plugins");
            _messageHandler.ShowError($"XSE plugin validation failed: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> ScanModInisAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _messageHandler.ShowInfo("Scanning mod INI files...");
            var result = await _modIniScanner.ScanAsync();
            _messageHandler.ShowSuccess("Mod INI scan completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning mod INIs");
            _messageHandler.ShowError($"Mod INI scan failed: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    public async Task<string> CheckWryeBashAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            _messageHandler.ShowInfo("Analyzing Wrye Bash report...");
            var result = await _wryeBashChecker.AnalyzeAsync();
            _messageHandler.ShowSuccess("Wrye Bash analysis completed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking Wrye Bash");
            _messageHandler.ShowError($"Wrye Bash check failed: {ex.Message}");
            return $"Error: {ex.Message}";
        }
    }

    private async Task<(string type, string output)> RunScannerAsync(
        string scannerType,
        Func<Task<string>> scannerFunc,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = await scannerFunc();
            return (scannerType, output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error in {scannerType} scanner");
            return (scannerType, $"Error: {ex.Message}");
        }
    }

    private void ProcessScanResult(GameScanResult result, string type, string output)
    {
        switch (type)
        {
            case "CrashGen":
                result.CrashGenResults = output;
                break;
            case "XsePlugins":
                result.XsePluginResults = output;
                break;
            case "ModInis":
                result.ModIniResults = output;
                break;
            case "WryeBash":
                result.WryeBashResults = output;
                break;
        }
    }

    private void AnalyzeResults(GameScanResult result)
    {
        // Check for critical issues (marked with ❌)
        var allOutput =
            $"{result.CrashGenResults}\n{result.XsePluginResults}\n{result.ModIniResults}\n{result.WryeBashResults}";

        if (allOutput.Contains("❌"))
        {
            result.HasIssues = true;

            // Extract critical issues
            var lines = allOutput.Split('\n');
            foreach (var line in lines.Where(l => l.Contains("❌")))
            {
                var cleanLine = line.Replace("❌", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanLine) && cleanLine.Length > 5) result.CriticalIssues.Add(cleanLine);
            }
        }

        // Check for warnings (marked with ⚠️)
        if (allOutput.Contains("⚠️"))
        {
            result.HasIssues = true;

            // Extract warnings
            var lines = allOutput.Split('\n');
            foreach (var line in lines.Where(l => l.Contains("⚠️")))
            {
                var cleanLine = line.Replace("⚠️", "").Trim();
                if (!string.IsNullOrWhiteSpace(cleanLine) && cleanLine.Length > 5) result.Warnings.Add(cleanLine);
            }
        }

        // Log summary
        _logger.LogInformation(
            $"Scan complete: {result.CriticalIssues.Count} critical issues, {result.Warnings.Count} warnings");
    }
}
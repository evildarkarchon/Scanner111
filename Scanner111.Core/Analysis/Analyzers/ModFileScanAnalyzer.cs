using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
///     Analyzer for comprehensive mod file scanning including unpacked files, BA2 archives,
///     crash generator configuration, and XSE plugin validation.
///     Integrates Python ScanGame functionality with the C# orchestration system.
/// </summary>
public sealed class ModFileScanAnalyzer : AnalyzerBase
{
    private readonly IModFileScanner _modFileScanner;
    private readonly ICrashGenChecker _crashGenChecker;
    private readonly IXsePluginChecker _xsePluginChecker;

    public ModFileScanAnalyzer(
        ILogger<ModFileScanAnalyzer> logger,
        IModFileScanner modFileScanner,
        ICrashGenChecker crashGenChecker,
        IXsePluginChecker xsePluginChecker) : base(logger)
    {
        _modFileScanner = modFileScanner ?? throw new ArgumentNullException(nameof(modFileScanner));
        _crashGenChecker = crashGenChecker ?? throw new ArgumentNullException(nameof(crashGenChecker));
        _xsePluginChecker = xsePluginChecker ?? throw new ArgumentNullException(nameof(xsePluginChecker));
    }

    public override string Name => "ModFileScan";

    public override string DisplayName => "Mod File Scanning Analysis";

    public override int Priority => 15; // Run after basic analysis but before mod detection

    public override TimeSpan Timeout => TimeSpan.FromMinutes(10); // Mod scanning can take a while

    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting comprehensive mod file scanning analysis");

        try
        {
            var scanResults = new List<ModFileScanResult>();
            var fragments = new List<ReportFragment>();

            // Get mod paths from context or settings
            var modPaths = await GetModPathsAsync(context, cancellationToken).ConfigureAwait(false);

            if (modPaths.ModsPath != null)
            {
                // Perform unpacked mod scanning
                var unpackedResult = await ScanUnpackedModsAsync(modPaths.ModsPath, cancellationToken).ConfigureAwait(false);
                scanResults.Add(unpackedResult);
                
                if (!string.IsNullOrWhiteSpace(unpackedResult.Report))
                {
                    fragments.Add(CreateScanFragment("Unpacked Mod Files", unpackedResult.Report, unpackedResult.IssueCount));
                }

                // Perform archived mod scanning if BSArch is available
                if (!string.IsNullOrEmpty(modPaths.BsArchPath))
                {
                    var archivedResult = await ScanArchivedModsAsync(modPaths.ModsPath, modPaths.BsArchPath, cancellationToken).ConfigureAwait(false);
                    scanResults.Add(archivedResult);
                    
                    if (!string.IsNullOrWhiteSpace(archivedResult.Report))
                    {
                        fragments.Add(CreateScanFragment("Archived Mod Files (BA2)", archivedResult.Report, archivedResult.IssueCount));
                    }
                }

                // Check log files for errors if logs path is available
                if (!string.IsNullOrEmpty(modPaths.LogsPath))
                {
                    var logResult = await CheckLogErrorsAsync(modPaths.LogsPath, cancellationToken).ConfigureAwait(false);
                    scanResults.Add(logResult);
                    
                    if (!string.IsNullOrWhiteSpace(logResult.Report))
                    {
                        fragments.Add(CreateScanFragment("Log File Analysis", logResult.Report, logResult.IssueCount));
                    }
                }
            }

            // Perform crash generator settings check
            var crashGenResult = await CheckCrashGenSettingsAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(crashGenResult.Report))
            {
                fragments.Add(CreateConfigFragment("Crash Generator Configuration", crashGenResult.Report));
            }

            // Perform XSE plugin check
            var xsePluginResult = await CheckXsePluginsAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(xsePluginResult.Report))
            {
                fragments.Add(CreateConfigFragment("Script Extender Plugins", xsePluginResult.Report));
            }

            // Store scan results in shared context for other analyzers
            context.SetSharedData("ModFileScanResults", scanResults);

            // Determine overall severity and create combined fragment
            var overallSeverity = DetermineOverallSeverity(scanResults);
            ReportFragment combinedFragment;

            if (fragments.Count > 0)
            {
                combinedFragment = ReportFragment.CreateWithChildren(
                    "Mod File Scanning Analysis", 
                    fragments, 
                    15);
            }
            else
            {
                combinedFragment = ReportFragment.CreateInfo(
                    "Mod File Scanning Analysis",
                    "Mod file scanning completed successfully with no issues detected.",
                    100);
            }

            var result = new AnalysisResult(Name)
            {
                Success = true,
                Fragment = combinedFragment,
                Severity = overallSeverity
            };

            // Add metadata
            result.AddMetadata("UnpackedScanned", (modPaths.ModsPath != null).ToString());
            result.AddMetadata("ArchivedScanned", (!string.IsNullOrEmpty(modPaths.BsArchPath)).ToString());
            result.AddMetadata("LogsScanned", (!string.IsNullOrEmpty(modPaths.LogsPath)).ToString());
            result.AddMetadata("CrashGenChecked", (!string.IsNullOrWhiteSpace(crashGenResult.Report)).ToString());
            result.AddMetadata("XsePluginsChecked", (!string.IsNullOrWhiteSpace(xsePluginResult.Report)).ToString());

            // Calculate total issue counts
            var totalIssues = scanResults.Sum(r => r.IssueCount);
            result.AddMetadata("TotalIssues", totalIssues.ToString());

            LogInformation("Mod file scanning analysis completed with severity: {Severity}, Total issues: {TotalIssues}", 
                overallSeverity, totalIssues);

            return result;
        }
        catch (OperationCanceledException)
        {
            // Re-throw cancellation exceptions so they propagate correctly
            throw;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to perform mod file scanning analysis");
            return AnalysisResult.CreateFailure(Name, $"Mod file scanning analysis failed: {ex.Message}");
        }
    }

    public override async Task<bool> CanAnalyzeAsync(AnalysisContext context)
    {
        // This analyzer can run on any analysis type as it provides general mod file scanning
        await Task.CompletedTask.ConfigureAwait(false);
        return context != null;
    }

    #region Private Helper Methods

    private async Task<ModPaths> GetModPathsAsync(AnalysisContext context, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // Try to get paths from context first
            if (context.TryGetSharedData<string>("ModsPath", out var modsPathFromContext))
            {
                return new ModPaths(modsPathFromContext, null, null);
            }

            // These would normally come from YAML settings
            // Using fallback values for now
            var modsPath = @"C:\Games\Fallout4\Data"; // Would be from settings
            var bsArchPath = @"C:\Scanner111\Data\BSArch.exe"; // Would be from local data directory
            var logsPath = @"C:\Users\Documents\My Games\Fallout4\Logs"; // Would be from documents discovery

            await Task.CompletedTask.ConfigureAwait(false);
            return new ModPaths(modsPath, bsArchPath, logsPath);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            LogWarning("Failed to get mod paths from configuration: {Error}", ex.Message);
            return new ModPaths(null, null, null);
        }
    }

    private async Task<ModFileScanResult> ScanUnpackedModsAsync(string modsPath, CancellationToken cancellationToken)
    {
        try
        {
            LogDebug("Starting unpacked mod scan at: {ModsPath}", modsPath);
            var report = await _modFileScanner.ScanModsUnpackedAsync(modsPath, cancellationToken).ConfigureAwait(false);
            var issueCount = CountReportedIssues(report);
            
            return new ModFileScanResult("Unpacked", true, report, issueCount);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            LogWarning("Unpacked mod scan failed: {Error}", ex.Message);
            return new ModFileScanResult("Unpacked", false, $"Failed to scan unpacked mods: {ex.Message}", 0);
        }
    }

    private async Task<ModFileScanResult> ScanArchivedModsAsync(string modsPath, string bsArchPath, CancellationToken cancellationToken)
    {
        try
        {
            LogDebug("Starting archived mod scan at: {ModsPath}", modsPath);
            var report = await _modFileScanner.ScanModsArchivedAsync(modsPath, bsArchPath, cancellationToken).ConfigureAwait(false);
            var issueCount = CountReportedIssues(report);
            
            return new ModFileScanResult("Archived", true, report, issueCount);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            LogWarning("Archived mod scan failed: {Error}", ex.Message);
            return new ModFileScanResult("Archived", false, $"Failed to scan archived mods: {ex.Message}", 0);
        }
    }

    private async Task<ModFileScanResult> CheckLogErrorsAsync(string logsPath, CancellationToken cancellationToken)
    {
        try
        {
            LogDebug("Starting log error check at: {LogsPath}", logsPath);
            var report = await _modFileScanner.CheckLogErrorsAsync(logsPath, cancellationToken).ConfigureAwait(false);
            var issueCount = CountReportedIssues(report);
            
            return new ModFileScanResult("LogErrors", true, report, issueCount);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            LogWarning("Log error check failed: {Error}", ex.Message);
            return new ModFileScanResult("LogErrors", false, $"Failed to check log errors: {ex.Message}", 0);
        }
    }

    private async Task<ModFileScanResult> CheckCrashGenSettingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogDebug("Starting crash generator settings check");
            var report = await _crashGenChecker.CheckCrashGenSettingsAsync(cancellationToken).ConfigureAwait(false);
            var issueCount = CountReportedIssues(report);
            
            return new ModFileScanResult("CrashGen", true, report, issueCount);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            LogWarning("Crash generator settings check failed: {Error}", ex.Message);
            return new ModFileScanResult("CrashGen", false, $"Failed to check crash generator settings: {ex.Message}", 0);
        }
    }

    private async Task<ModFileScanResult> CheckXsePluginsAsync(CancellationToken cancellationToken)
    {
        try
        {
            LogDebug("Starting XSE plugins check");
            var report = await _xsePluginChecker.CheckXsePluginsAsync(cancellationToken).ConfigureAwait(false);
            var issueCount = CountReportedIssues(report);
            
            return new ModFileScanResult("XsePlugins", true, report, issueCount);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            LogWarning("XSE plugins check failed: {Error}", ex.Message);
            return new ModFileScanResult("XsePlugins", false, $"Failed to check XSE plugins: {ex.Message}", 0);
        }
    }

    private static int CountReportedIssues(string report)
    {
        if (string.IsNullOrWhiteSpace(report))
            return 0;

        // Count various issue indicators in the report
        var issueCount = 0;
        
        // Count error and warning markers
        issueCount += CountOccurrences(report, "❌");
        issueCount += CountOccurrences(report, "⚠️");
        issueCount += CountOccurrences(report, "❓");
        
        // Count "CAUTION" and "ERROR" messages
        issueCount += CountOccurrences(report, "CAUTION");
        issueCount += CountOccurrences(report, "ERROR");
        
        return issueCount;
    }

    private static int CountOccurrences(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
            return 0;

        var count = 0;
        var index = 0;
        
        while ((index = text.IndexOf(pattern, index, StringComparison.OrdinalIgnoreCase)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        
        return count;
    }

    private static ReportFragment CreateScanFragment(string title, string content, int issueCount)
    {
        var priority = issueCount switch
        {
            0 => 50,
            <= 5 => 25,
            _ => 15
        };

        var severity = issueCount switch
        {
            0 => AnalysisSeverity.Info,
            <= 3 => AnalysisSeverity.Warning,
            _ => AnalysisSeverity.Error
        };

        return severity switch
        {
            AnalysisSeverity.Error => ReportFragment.CreateError(title, content, priority),
            AnalysisSeverity.Warning => ReportFragment.CreateWarning(title, content, priority),
            _ => ReportFragment.CreateInfo(title, content, priority)
        };
    }

    private static ReportFragment CreateConfigFragment(string title, string content)
    {
        // Configuration checks are usually informational or warnings
        if (content.Contains("❌") || content.Contains("ERROR"))
            return ReportFragment.CreateError(title, content, 20);
        else if (content.Contains("⚠️") || content.Contains("CAUTION"))
            return ReportFragment.CreateWarning(title, content, 25);
        else
            return ReportFragment.CreateInfo(title, content, 30);
    }

    private static AnalysisSeverity DetermineOverallSeverity(IList<ModFileScanResult> scanResults)
    {
        var hasErrors = scanResults.Any(r => !r.Success || r.IssueCount > 5);
        var hasWarnings = scanResults.Any(r => r.IssueCount > 0);

        if (hasErrors)
            return AnalysisSeverity.Error;
        else if (hasWarnings)
            return AnalysisSeverity.Warning;
        else
            return AnalysisSeverity.Info;
    }

    #endregion

    #region Helper Classes

    private sealed record ModPaths(string? ModsPath, string? BsArchPath, string? LogsPath);

    private sealed class ModFileScanResult
    {
        public ModFileScanResult(string scanType, bool success, string report, int issueCount)
        {
            ScanType = scanType ?? throw new ArgumentNullException(nameof(scanType));
            Success = success;
            Report = report ?? string.Empty;
            IssueCount = issueCount;
        }

        public string ScanType { get; }
        public bool Success { get; }
        public string Report { get; }
        public int IssueCount { get; }
    }

    #endregion
}
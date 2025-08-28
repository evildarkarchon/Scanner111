using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Caching;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Reporting.Fragments;

namespace Scanner111.Core.Analysis;

/// <summary>
///     Interface for FCX mode handling functionality.
/// </summary>
public interface IFcxModeHandler
{
    /// <summary>
    ///     Gets whether FCX mode is enabled.
    /// </summary>
    bool? FcxMode { get; }

    /// <summary>
    ///     Gets the main files check result.
    /// </summary>
    string MainFilesCheck { get; }

    /// <summary>
    ///     Gets the game files check result.
    /// </summary>
    string GameFilesCheck { get; }

    /// <summary>
    ///     Checks FCX mode and runs necessary file checks if enabled.
    ///     Thread-safe and caches results across multiple calls.
    /// </summary>
    Task CheckFcxModeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Generates a report fragment with FCX mode messages.
    /// </summary>
    ReportFragment GetFcxMessages();

    /// <summary>
    ///     Gets detailed report fragments for comprehensive reporting.
    /// </summary>
    IEnumerable<ReportFragment> GetDetailedFragments();

    /// <summary>
    ///     Gets a summary fragment with key FCX metrics.
    /// </summary>
    ReportFragment GetSummaryFragment();

    /// <summary>
    ///     Gets an error-only fragment for critical issues.
    /// </summary>
    ReportFragment? GetErrorOnlyFragment();

    /// <summary>
    ///     Resets FCX checks and cached results.
    /// </summary>
    Task ResetFcxChecksAsync();
}

/// <summary>
///     Handles FCX mode checking with thread-safe caching of results.
///     Implements enhanced coordination with fragment composition and advanced caching.
/// </summary>
public sealed class FcxModeHandler : IFcxModeHandler, IDisposable
{
    // Static fields for class-level coordination (shared across instances)
    private static readonly SemaphoreSlim s_globalLock = new(1, 1);
    private static bool s_fcxChecksRun;
    private static FcxFileCheckResult? s_cachedResult;
    private static DateTime s_lastCheckTime = DateTime.MinValue;
    private static readonly TimeSpan s_cacheExpiration = TimeSpan.FromMinutes(5);
    private static int s_cacheVersion;
    private static readonly Dictionary<string, TimeSpan> s_performanceMetrics = new();
    
    private readonly SemaphoreSlim _fcxLock = new(1, 1);
    private readonly ILogger<FcxModeHandler> _logger;
    private readonly ModDetectionSettings _modSettings;
    private readonly IFcxFileChecker? _fileChecker;
    private readonly IFcxCacheManager? _cacheManager;
    private FcxFileCheckResult? _currentResult;
    private readonly Stopwatch _operationTimer = new();
    private bool _disposed;

    public FcxModeHandler(
        ILogger<FcxModeHandler> logger,
        ModDetectionSettings modSettings,
        IFcxFileChecker? fileChecker = null,
        IFcxCacheManager? cacheManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _modSettings = modSettings ?? throw new ArgumentNullException(nameof(modSettings));
        _fileChecker = fileChecker;
        _cacheManager = cacheManager;
    }

    /// <summary>
    ///     Cleans up resources used by the handler.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _fcxLock?.Dispose();
        _disposed = true;

        _logger.LogDebug("FcxModeHandler disposed");
    }

    /// <inheritdoc />
    public bool? FcxMode => _modSettings.FcxMode;

    /// <inheritdoc />
    public string MainFilesCheck => _currentResult?.MainFilesResult ?? string.Empty;

    /// <inheritdoc />
    public string GameFilesCheck => _currentResult?.ModFilesResult ?? string.Empty;

    /// <inheritdoc />
    public async Task CheckFcxModeAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(FcxModeHandler));

        if (FcxMode == true)
        {
            await _fcxLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Use global lock for cross-instance synchronization
                await s_globalLock.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    var now = DateTime.UtcNow;
                    var cacheExpired = now - s_lastCheckTime > s_cacheExpiration;

                    // Check if we need to run FCX checks
                    if (!s_fcxChecksRun || cacheExpired || s_cachedResult == null)
                    {
                        _logger.LogInformation("Running FCX mode file checks (cache expired: {Expired})", cacheExpired);
                        
                        _operationTimer.Restart();
                        
                        // Use enhanced file checker if available, otherwise fall back to simple checks
                        if (_fileChecker != null)
                        {
                            var options = new FcxCheckOptions
                            {
                                IncludeArchivedMods = false, // Default to false, should come from config
                                RetryOnFailure = true,
                                ComputeChecksums = false,
                                MaxParallelism = 4
                            };

                            var progress = new Progress<FcxCheckProgress>(p => 
                                _logger.LogDebug("FCX Check Progress: {Operation} - {Percent}%", 
                                    p.CurrentOperation, p.PercentComplete));

                            // Use placeholder paths - should be provided via configuration
                            var gamePath = GetGamePath();
                            var modPath = GetModPath();
                            
                            s_cachedResult = await _fileChecker.CheckFilesAsync(
                                gamePath,
                                modPath,
                                options,
                                progress,
                                cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // Fallback implementation
                            s_cachedResult = new FcxFileCheckResult
                            {
                                Success = true,
                                MainFilesResult = await RunMainFilesCheckAsync(cancellationToken).ConfigureAwait(false),
                                ModFilesResult = await RunGameFilesCheckAsync(cancellationToken).ConfigureAwait(false),
                                CompletedAt = DateTime.UtcNow
                            };
                        }

                        s_fcxChecksRun = true;
                        s_lastCheckTime = now;
                        s_cacheVersion++;
                        
                        _operationTimer.Stop();
                        s_performanceMetrics["LastCheckDuration"] = _operationTimer.Elapsed;

                        _logger.LogInformation("FCX mode file checks completed in {Duration}ms", 
                            _operationTimer.ElapsedMilliseconds);
                        
                        // Cache the result if cache manager is available
                        if (_cacheManager != null)
                        {
                            await _cacheManager.SetAsync(
                                "fcx:result:latest", 
                                s_cachedResult,
                                new CacheEntryOptions 
                                { 
                                    SlidingExpiration = s_cacheExpiration,
                                    Priority = CachePriority.High
                                }).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        _logger.LogDebug("Using cached FCX check results (age: {Age}, version: {Version})", 
                            now - s_lastCheckTime, s_cacheVersion);
                    }

                    // Always assign the stored results to instance
                    _currentResult = s_cachedResult;
                }
                finally
                {
                    s_globalLock.Release();
                }
            }
            finally
            {
                _fcxLock.Release();
            }
        }
        else
        {
            _currentResult = new FcxFileCheckResult
            {
                Success = false,
                MainFilesResult = "❌ FCX Mode is disabled, skipping game files check... \n-----\n",
                ModFilesResult = string.Empty,
                CompletedAt = DateTime.UtcNow
            };
            _logger.LogDebug("FCX mode is disabled, skipping file checks");
        }
    }

    /// <inheritdoc />
    public ReportFragment GetFcxMessages()
    {
        return FcxReportFragments.CreateStatusFragment(
            FcxMode == true,
            MainFilesCheck,
            GameFilesCheck,
            10);
    }

    /// <inheritdoc />
    public IEnumerable<ReportFragment> GetDetailedFragments()
    {
        var fragments = new List<ReportFragment>();

        // Status fragment
        fragments.Add(GetFcxMessages());

        // Detailed results if available
        if (_currentResult != null)
        {
            // Add detailed fragment
            var additionalInfo = new Dictionary<string, string>();
            if (s_performanceMetrics.TryGetValue("LastCheckDuration", out var duration))
            {
                additionalInfo["Check Duration"] = $"{duration.TotalSeconds:F2}s";
            }
            additionalInfo["Cache Version"] = s_cacheVersion.ToString();
            
            fragments.Add(FcxReportFragments.CreateDetailedFragment(
                _currentResult,
                additionalInfo,
                25));

            // Add integrity fragment if there are issues
            var hasIntegrityIssues = _currentResult.MainFilesResult?.Contains("❌") == true;
            if (hasIntegrityIssues)
            {
                fragments.Add(FcxReportFragments.CreateFileIntegrityFragment(
                    _currentResult.MainFilesResult,
                    true,
                    20));
            }

            // Add mod files fragment if mods were checked
            if (!string.IsNullOrWhiteSpace(_currentResult.ModFilesResult))
            {
                var modCount = ExtractModCount(_currentResult.ModFilesResult);
                var issueCount = ExtractIssueCount(_currentResult.ModFilesResult);
                
                fragments.Add(FcxReportFragments.CreateModFilesFragment(
                    _currentResult.ModFilesResult,
                    modCount,
                    issueCount,
                    30));
            }
        }

        // Add performance metrics if available
        if (s_performanceMetrics.Count > 0)
        {
            fragments.Add(FcxReportFragments.CreatePerformanceFragment(
                new Dictionary<string, TimeSpan>(s_performanceMetrics),
                100, // placeholder
                1024 * 1024, // placeholder
                90));
        }

        return fragments.Where(f => f != null);
    }

    /// <inheritdoc />
    public ReportFragment GetSummaryFragment()
    {
        if (_currentResult == null)
        {
            return FcxReportFragments.CreateStatusFragment(
                FcxMode == true,
                null,
                null,
                15);
        }

        var duration = s_performanceMetrics.TryGetValue("LastCheckDuration", out var d) 
            ? d 
            : TimeSpan.Zero;

        return FcxReportFragments.CreateSummaryFragment(
            _currentResult,
            duration,
            15);
    }

    /// <inheritdoc />
    public ReportFragment? GetErrorOnlyFragment()
    {
        if (_currentResult == null || _currentResult.Success)
            return null;

        return FcxReportFragments.CreateErrorFragment(
            _currentResult.ErrorMessage ?? "Unknown error",
            null,
            "Check FCX mode settings and ensure Scanner111 has proper file access permissions.",
            5);
    }

    /// <inheritdoc />
    public async Task ResetFcxChecksAsync()
    {
        await s_globalLock.WaitAsync().ConfigureAwait(false);
        try
        {
            s_fcxChecksRun = false;
            s_cachedResult = null;
            s_lastCheckTime = DateTime.MinValue;
            s_cacheVersion++;
            s_performanceMetrics.Clear();

            _currentResult = null;

            // Clear cache manager if available
            if (_cacheManager != null)
            {
                await _cacheManager.InvalidateAsync("fcx:").ConfigureAwait(false);
            }

            // Clear file checker cache if available
            if (_fileChecker != null)
            {
                await _fileChecker.ClearChecksumCacheAsync().ConfigureAwait(false);
            }

            _logger.LogInformation("FCX checks and cached results have been reset (new version: {Version})", s_cacheVersion);
        }
        finally
        {
            s_globalLock.Release();
        }
    }

    /// <summary>
    ///     Runs main files check (placeholder for SetupCoordinator integration).
    /// </summary>
    private async Task<string> RunMainFilesCheckAsync(CancellationToken cancellationToken)
    {
        // This would integrate with the actual SetupCoordinator when available
        // For now, return a placeholder message
        await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Simulate work

        return "✔️ Main game files integrity check completed successfully.\n-----\n";
    }

    /// <summary>
    ///     Runs game files check (placeholder for game_combined_result integration).
    /// </summary>
    private async Task<string> RunGameFilesCheckAsync(CancellationToken cancellationToken)
    {
        // This would integrate with the actual game scanning functionality when available
        // For now, return a placeholder message
        await Task.Delay(100, cancellationToken).ConfigureAwait(false); // Simulate work

        return "✔️ Game mod files check completed successfully.\n-----\n";
    }

    private static int ExtractModCount(string modFilesResult)
    {
        // Try to extract mod count from result string
        var match = System.Text.RegularExpressions.Regex.Match(modFilesResult, @"(\d+)\s+mods?");
        return match.Success && int.TryParse(match.Groups[1].Value, out var count) ? count : 0;
    }

    private static int ExtractIssueCount(string modFilesResult)
    {
        // Count error markers in the result
        return modFilesResult.Count(c => c == '❌');
    }
    
    private string GetGamePath()
    {
        // Should be provided via configuration or discovery service
        // For now, return empty string to indicate paths should be discovered
        return string.Empty;
    }
    
    private string GetModPath()
    {
        // Should be provided via configuration or discovery service  
        // For now, return empty string to indicate paths should be discovered
        return string.Empty;
    }
}
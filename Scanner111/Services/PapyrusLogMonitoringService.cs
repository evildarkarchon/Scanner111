using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for monitoring and analyzing Papyrus log files.
///     Provides functionality to extract statistics and monitor for changes.
/// </summary>
public class PapyrusLogMonitoringService : IPapyrusLogMonitoringService, IDisposable
{
    private readonly AppSettings _appSettings;
    private readonly TimeSpan _cacheValidityPeriod = TimeSpan.FromSeconds(30);
    private readonly ILogger<PapyrusLogMonitoringService>? _logger;
    private readonly IYamlSettingsCacheService _yamlSettingsCacheService;
    private Action<PapyrusLogAnalysis>? _changeCallback;

    private FileSystemWatcher? _fileWatcher;

    // Cache for the last analysis to avoid redundant processing
    private PapyrusLogAnalysis? _lastAnalysis;
    private DateTime _lastAnalysisTime = DateTime.MinValue;
    private CancellationTokenSource? _monitoringCts;

    public PapyrusLogMonitoringService(
        IYamlSettingsCacheService yamlSettingsCacheService,
        AppSettings appSettings,
        ILogger<PapyrusLogMonitoringService>? logger = null)
    {
        _yamlSettingsCacheService = yamlSettingsCacheService ??
                                    throw new ArgumentNullException(nameof(yamlSettingsCacheService));
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _logger = logger;
    }

    /// <summary>
    ///     Dispose method to clean up resources
    /// </summary>
    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Gets the path to the Papyrus log file from settings
    /// </summary>
    public string? GetPapyrusLogPath()
    {
        try
        {
            // Check if the game name contains "VR" to determine if it's a VR version
            var vrSuffix = _appSettings.GameName.Contains("VR", StringComparison.OrdinalIgnoreCase) ? "_VR" : "";

            // Use the same pattern as in the Python code
            return _yamlSettingsCacheService.GetSetting<string>(Yaml.GameLocal,
                $"Game{vrSuffix}_Info.Docs_File_PapyrusLog");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting Papyrus log path");
            return null;
        }
    }

    /// <summary>
    ///     Analyzes the Papyrus log file and extracts statistics
    /// </summary>
    /// <param name="progress">Optional progress reporter to receive progress updates.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>PapyrusLogAnalysis object with analysis results</returns>
    public async Task<PapyrusLogAnalysis> AnalyzePapyrusLogAsync(IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting Papyrus log analysis");
        progress?.Report(new ScanProgress(0, "Starting Papyrus log analysis"));

        // Check if we have a valid cached result
        if (_lastAnalysis != null && DateTime.Now - _lastAnalysisTime < _cacheValidityPeriod)
        {
            _logger?.LogInformation("Returning cached Papyrus log analysis");
            progress?.Report(new ScanProgress(100, "Retrieved cached Papyrus log analysis"));
            return _lastAnalysis;
        }

        return await Task.Run(async () =>
        {
            try
            {
                var papyrusPath = GetPapyrusLogPath();
                progress?.Report(new ScanProgress(10, "Retrieved Papyrus log path", papyrusPath));

                var analysis = new PapyrusLogAnalysis
                {
                    LogFilePath = papyrusPath,
                    AnalysisTime = DateTime.Now
                };

                if (papyrusPath == null)
                {
                    _logger?.LogWarning("Papyrus log path not found in settings");
                    progress?.Report(new ScanProgress(100, "Papyrus log path not found"));
                    return analysis;
                }

                if (!File.Exists(papyrusPath))
                {
                    _logger?.LogWarning("Papyrus log file not found at path: {Path}", papyrusPath);
                    progress?.Report(new ScanProgress(100, "Papyrus log file not found"));
                    return analysis;
                }

                progress?.Report(new ScanProgress(20, "Papyrus log file found, reading contents"));

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // In C#, we can use StreamReader with encoding detection or UTF8
                    // For proper encoding detection like Python's chardet, we'd need a library
                    // or we can make a best effort with the .NET encoding detection
                    var papyrusData = await TryReadFileWithEncodingDetectionAsync(papyrusPath, cancellationToken)
                        .ConfigureAwait(false);

                    progress?.Report(new ScanProgress(50, "Analyzing Papyrus log content"));

                    var lineCount = papyrusData.Length;
                    var processedLines = 0;
                    var reportInterval = Math.Max(1, lineCount / 10); // Report progress every 10%

                    foreach (var line in papyrusData)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (line.Contains("Dumping Stacks"))
                            analysis.DumpCount++;
                        else if (line.Contains("Dumping Stack"))
                            analysis.StackCount++;
                        else if (line.Contains(" warning: "))
                            analysis.WarningCount++;
                        else if (line.Contains(" error: ")) analysis.ErrorCount++;

                        // Update progress periodically
                        processedLines++;
                        if (processedLines % reportInterval != 0 && processedLines != lineCount) continue;
                        var percentComplete = 50 + (int)((float)processedLines / lineCount * 50);
                        progress?.Report(new ScanProgress(percentComplete, "Analyzing Papyrus log content",
                            $"Processed {processedLines} of {lineCount} lines"));
                    }

                    // Cache the result
                    _lastAnalysis = analysis;
                    _lastAnalysisTime = DateTime.Now;

                    _logger?.LogInformation(
                        "Papyrus log analysis completed. Found {Errors} errors, {Warnings} warnings",
                        analysis.ErrorCount, analysis.WarningCount);
                    progress?.Report(new ScanProgress(100, "Papyrus log analysis completed"));
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error analyzing Papyrus log");
                    progress?.Report(new ScanProgress(100, $"Error analyzing Papyrus log: {ex.Message}"));
                    // For now we'll leave the counts at their defaults (0)
                    // and let the view handle the error message
                }

                return analysis;
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Papyrus log analysis was cancelled");
                progress?.Report(new ScanProgress(0, "Analysis cancelled"));
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error during Papyrus log analysis");
                progress?.Report(new ScanProgress(0, $"Error: {ex.Message}"));
                throw;
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    ///     Starts monitoring the Papyrus log file for changes
    /// </summary>
    public async Task StartMonitoringAsync(Action<PapyrusLogAnalysis> callback, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Starting Papyrus log monitoring");

        if (_fileWatcher != null)
        {
            _logger?.LogInformation("Papyrus log monitoring already active");
            // Already monitoring
            return;
        }

        _changeCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        var papyrusPath = GetPapyrusLogPath();

        if (papyrusPath == null || !File.Exists(papyrusPath))
        {
            _logger?.LogWarning("Cannot monitor Papyrus log: file not found at {PapyrusPath}", papyrusPath);

            // Even if the file doesn't exist, we should send an initial analysis
            var emptyAnalysis = new PapyrusLogAnalysis
            {
                LogFilePath = papyrusPath,
                AnalysisTime = DateTime.Now
            };
            callback(emptyAnalysis);
            return;
        }

        // Initialize the monitoring cancellation token source
        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Set up file system watcher
            var directoryPath = Path.GetDirectoryName(papyrusPath);
            var fileName = Path.GetFileName(papyrusPath);

            if (directoryPath == null)
            {
                _logger?.LogError("Invalid Papyrus log path: {PapyrusPath}", papyrusPath);
                return;
            }

            _fileWatcher = new FileSystemWatcher(directoryPath, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            // Use a debounce mechanism to avoid triggering on rapid file changes
            var lastChangeTime = DateTime.MinValue;
            var debounceTimeSpan = TimeSpan.FromSeconds(2);

            _fileWatcher.Changed += async (sender, e) =>
            {
                // Simple debounce mechanism
                var now = DateTime.Now;
                if (now - lastChangeTime < debounceTimeSpan) return;
                lastChangeTime = now;

                try
                {
                    // Wait briefly to let file writes complete
                    await Task.Delay(500, _monitoringCts?.Token ?? default).ConfigureAwait(false);

                    // If we've been cancelled, bail out
                    if (_monitoringCts?.IsCancellationRequested == true) return;

                    // Clear the cache to force a fresh analysis
                    _lastAnalysis = null;

                    // Analyze the log file
                    var analysis = await AnalyzePapyrusLogAsync(cancellationToken: _monitoringCts?.Token ?? default)
                        .ConfigureAwait(false);

                    // Invoke the callback with results
                    _changeCallback?.Invoke(analysis);

                    _logger?.LogDebug("Papyrus log file changed, updated analysis");
                }
                catch (OperationCanceledException)
                {
                    // Monitoring was cancelled, just exit
                    _logger?.LogInformation("Papyrus log file change handling was cancelled");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error in file change handler");
                }
            };

            // Initial analysis
            var initialAnalysis = await AnalyzePapyrusLogAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            callback(initialAnalysis);

            _logger?.LogInformation("Papyrus log monitoring started successfully");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to set up Papyrus log monitoring");
            StopMonitoring();
        }
    }

    /// <summary>
    ///     Stops monitoring the Papyrus log file
    /// </summary>
    public void StopMonitoring()
    {
        _logger?.LogInformation("Stopping Papyrus log monitoring");

        // Cancel any ongoing monitoring tasks
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;

        // Clean up the file watcher
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }

        _changeCallback = null;

        _logger?.LogInformation("Papyrus log monitoring stopped");
    }

    /// <summary>
    ///     Attempts to read a file with encoding detection
    /// </summary>
    private async Task<string[]> TryReadFileWithEncodingDetectionAsync(string filePath,
        CancellationToken cancellationToken = default)
    {
        // Try to detect encoding from byte order marks
        Encoding encoding;
        using (var reader = new StreamReader(filePath, Encoding.Default, true))
        {
            // Read a small portion to let the StreamReader detect the encoding from BOM
            var buffer = new char[4096];
            await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            encoding = reader.CurrentEncoding;
        }

        // Now read the whole file with the detected encoding
        return await File.ReadAllLinesAsync(filePath, encoding, cancellationToken).ConfigureAwait(false);
    }
}
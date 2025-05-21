using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Models;

namespace Scanner111.Services
{    /// <summary>
     /// Service for monitoring and analyzing Papyrus log files.
     /// Provides functionality to extract statistics and monitor for changes.
     /// </summary>
    public class PapyrusLogMonitoringService : IPapyrusLogMonitoringService, IDisposable
    {
        private readonly IYamlSettingsCacheService _yamlSettingsCacheService;
        private readonly AppSettings _appSettings;
        private readonly ILogger<PapyrusLogMonitoringService>? _logger;

        private FileSystemWatcher? _fileWatcher;
        private Action<PapyrusLogAnalysis>? _changeCallback;
        private CancellationTokenSource? _monitoringCts;

        public PapyrusLogMonitoringService(
            IYamlSettingsCacheService yamlSettingsCacheService,
            AppSettings appSettings,
            ILogger<PapyrusLogMonitoringService>? logger = null)
        {
            _yamlSettingsCacheService = yamlSettingsCacheService ?? throw new ArgumentNullException(nameof(yamlSettingsCacheService));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            _logger = logger;
        }

        /// <summary>
        /// Gets the path to the Papyrus log file from settings
        /// </summary>
        public string? GetPapyrusLogPath()
        {
            try
            {
                // Check if the game name contains "VR" to determine if it's a VR version
                string vrSuffix = _appSettings.GameName.Contains("VR", StringComparison.OrdinalIgnoreCase) ? "_VR" : "";

                // Use the same pattern as in the Python code
                return _yamlSettingsCacheService.GetSetting<string>(YAML.Game_Local, $"Game{vrSuffix}_Info.Docs_File_PapyrusLog");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting Papyrus log path");
                return null;
            }
        }

        /// <summary>
        /// Analyzes the Papyrus log file and extracts statistics
        /// </summary>
        public async Task<PapyrusLogAnalysis> AnalyzePapyrusLogAsync()
        {
            string? papyrusPath = GetPapyrusLogPath();

            var analysis = new PapyrusLogAnalysis
            {
                LogFilePath = papyrusPath,
                AnalysisTime = DateTime.Now
            };

            if (papyrusPath != null && File.Exists(papyrusPath))
            {
                try
                {
                    // In C#, we can use StreamReader with encoding detection or UTF8
                    // For proper encoding detection like Python's chardet, we'd need a library
                    // or we can make a best effort with the .NET encoding detection
                    string[] papyrusData = await TryReadFileWithEncodingDetectionAsync(papyrusPath);

                    foreach (var line in papyrusData)
                    {
                        if (line.Contains("Dumping Stacks"))
                        {
                            analysis.DumpCount++;
                        }
                        else if (line.Contains("Dumping Stack"))
                        {
                            analysis.StackCount++;
                        }
                        else if (line.Contains(" warning: "))
                        {
                            analysis.WarningCount++;
                        }
                        else if (line.Contains(" error: "))
                        {
                            analysis.ErrorCount++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error analyzing Papyrus log");
                    // For now we'll leave the counts at their defaults (0)
                    // and let the view handle the error message
                }
            }

            return analysis;
        }

        /// <summary>
        /// Attempts to read a file with encoding detection
        /// </summary>
        private async Task<string[]> TryReadFileWithEncodingDetectionAsync(string filePath)
        {
            // Try to detect encoding from byte order marks
            Encoding encoding;
            using (var reader = new StreamReader(filePath, Encoding.Default, true))
            {
                // Read a small portion to let the StreamReader detect the encoding from BOM
                await reader.ReadAsync(new char[4096], 0, 4096);
                encoding = reader.CurrentEncoding;
            }

            // Now read the whole file with the detected encoding
            return await File.ReadAllLinesAsync(filePath, encoding);
        }

        /// <summary>
        /// Starts monitoring the Papyrus log file for changes
        /// </summary>
        public async Task StartMonitoringAsync(Action<PapyrusLogAnalysis> callback, CancellationToken cancellationToken)
        {
            if (_fileWatcher != null)
            {
                // Already monitoring
                return;
            }

            _changeCallback = callback ?? throw new ArgumentNullException(nameof(callback));
            string? papyrusPath = GetPapyrusLogPath();

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
                    if (now - lastChangeTime < debounceTimeSpan)
                    {
                        return;
                    }
                    lastChangeTime = now;

                    try
                    {
                        // Wait briefly to let file writes complete
                        await Task.Delay(500, _monitoringCts?.Token ?? default);

                        // If we've been cancelled, bail out
                        if (_monitoringCts?.IsCancellationRequested == true)
                        {
                            return;
                        }

                        var analysis = await AnalyzePapyrusLogAsync();

                        // Invoke the callback with results
                        _changeCallback?.Invoke(analysis);
                    }
                    catch (OperationCanceledException)
                    {
                        // Monitoring was cancelled, just exit
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error in file change handler");
                    }
                };

                // Initial analysis
                var initialAnalysis = await AnalyzePapyrusLogAsync();
                callback(initialAnalysis);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to set up Papyrus log monitoring");
                StopMonitoring();
            }
        }

        /// <summary>
        /// Stops monitoring the Papyrus log file
        /// </summary>
        public void StopMonitoring()
        {
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
        }

        /// <summary>
        /// Dispose method to clean up resources
        /// </summary>
        public void Dispose()
        {
            StopMonitoring();
            GC.SuppressFinalize(this);
        }
    }
}

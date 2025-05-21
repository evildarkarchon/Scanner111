using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Scanner111.Models;
using Scanner111.Services;

namespace Scanner111.Services
{
    /// <summary>
    /// Main service that coordinates scanning of log files
    /// </summary>    
    public class ScanLogService
    {
        private readonly AppSettings _appSettings;
        private readonly WarningDatabase _warningDatabase;
        private readonly CrashLogParserService _parserService;
        private readonly PluginDetectionService _pluginDetection;
        private readonly CrashAnalysisService _crashAnalysis; private readonly IYamlSettingsCacheService _yamlSettingsCache;
        private readonly CrashStackAnalysis _crashStackAnalysis;
        private readonly CrashLogFormattingService _formattingService;
        private readonly ModDetectionService _modDetection;
        private readonly SpecializedSettingsCheckService _specializedSettingsCheck;
        private readonly CrashReportGenerator _reportGenerator;

        public ScanLogService(
            AppSettings appSettings,
            WarningDatabase warningDatabase,
            CrashLogParserService parserService,
            PluginDetectionService pluginDetection,
            CrashAnalysisService crashAnalysis,
            IYamlSettingsCacheService yamlSettingsCache,
            CrashStackAnalysis crashStackAnalysis,
            CrashLogFormattingService formattingService,
            ModDetectionService modDetection,
            SpecializedSettingsCheckService specializedSettingsCheck,
            CrashReportGenerator reportGenerator)
        {
            _appSettings = appSettings;
            _warningDatabase = warningDatabase;
            _parserService = parserService;
            _pluginDetection = pluginDetection;
            _crashAnalysis = crashAnalysis;
            _yamlSettingsCache = yamlSettingsCache;
            _crashStackAnalysis = crashStackAnalysis;
            _formattingService = formattingService;
            _modDetection = modDetection;
            _specializedSettingsCheck = specializedSettingsCheck;
            _reportGenerator = reportGenerator;
        }

        /// <summary>
        /// Preprocesses and formats crash log files before scanning them
        /// </summary>
        /// <param name="logFilePaths">File paths to preprocess</param>
        /// <returns>Number of successfully preprocessed files</returns>
        public async Task<int> PreprocessCrashLogsAsync(IEnumerable<string> logFilePaths)
        {
            // Apply formatting to crash logs (normalize plugin load order, optionally remove noise)
            return await _formattingService.ReformatCrashLogsAsync(
                logFilePaths,
                _appSettings.SimplifyRemoveStrings
            );
        }

        /// <summary>
        /// Scans a single crash log file
        /// </summary>
        public async Task<List<LogIssue>> ScanLogFileAsync(string logFilePath)
        {
            var issues = new List<LogIssue>();
            if (!File.Exists(logFilePath))
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(logFilePath),
                    Title = "File Not Found",
                    Message = $"The specified crash log file was not found: {logFilePath}",
                    Severity = SeverityLevel.Error
                });
                return issues;
            }

            // Format the crash log before parsing if needed
            await PreprocessCrashLogsAsync(new[] { logFilePath });

            // 1. Parse the crash log content
            var parsedLog = await _parserService.ParseCrashLogContentAsync(logFilePath);
            if (!parsedLog.Lines.Any()) // Check if parsing failed or file was empty/not found by parser
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(logFilePath),
                    Title = "Log Parsing Failed",
                    Message = $"Could not parse or read content from: {logFilePath}",
                    Severity = SeverityLevel.Error
                });
                return issues;
            }            // 2. Perform various checks based on parsedLog and settings/warningDatabase
            _pluginDetection.DetectModIssues(parsedLog, issues);
            _crashAnalysis.AnalyzeCrashLog(parsedLog, issues);

            // 3. Perform enhanced mod detection checks
            _modDetection.DetectSingleMods(parsedLog, issues);
            _modDetection.DetectModConflicts(parsedLog, issues);
            _modDetection.DetectImportantMods(parsedLog, issues);
            _modDetection.CheckPluginLimits(parsedLog, issues);

            // 4. Check specialized settings
            _specializedSettingsCheck.CheckAllSettings(parsedLog, issues);

            // Fallback for unhandled exceptions during development of this service
            if (!issues.Any() && parsedLog.Lines.Any()) // If no specific issues found yet, but log was parsed
            {
                // This is just a placeholder to show the service was called.
                // Remove this once actual checks are implemented.
                bool unhandledExceptionFound = false;
                foreach (var line in parsedLog.Lines)
                {
                    if (line.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
                    {
                        unhandledExceptionFound = true;
                        break;
                    }
                }

                if (unhandledExceptionFound)
                {
                    issues.Add(new LogIssue
                    {
                        FileName = Path.GetFileName(logFilePath),
                        Title = "Generic Unhandled Exception",
                        Message = "An unhandled exception was mentioned in the log.",
                        Recommendation = "Review the log details for more information.",
                        Severity = SeverityLevel.Warning,
                        Source = "BasicScan"
                    });
                }
            }

            if (!issues.Any() && parsedLog.Lines.Any())
            {
                issues.Add(new LogIssue
                {
                    FileName = Path.GetFileName(logFilePath),
                    Title = "Scan Complete (No Specific Issues Identified)",
                    Message = "The log file was scanned, but no specific issues covered by current rules were found.",
                    Severity = SeverityLevel.Information,
                    Source = "ScanLogService"
                });
            }

            return issues;
        }

        /// <summary>
        /// Scans multiple crash log files.
        /// </summary>
        /// <param name="logFilePaths">A list of paths to crash log files.</param>
        /// <returns>A list of identified issues from all files.</returns>
        public async Task<List<LogIssue>> ScanMultipleLogFilesAsync(IEnumerable<string> logFilePaths)
        {
            // First preprocess all crash logs
            await PreprocessCrashLogsAsync(logFilePaths);

            // Then scan them
            var allIssues = new List<LogIssue>();
            foreach (var filePath in logFilePaths)
            {
                var fileIssues = await ScanLogFileAsync(filePath);
                allIssues.AddRange(fileIssues);
            }
            return allIssues;
        }

        /// <summary>
        /// Scans a crash log file and generates a formatted report
        /// </summary>
        /// <param name="logFilePath">Path to the crash log file</param>
        /// <param name="reportsDirectory">Directory where reports should be saved, null to use default</param>
        /// <returns>The report content as a string</returns>
        public async Task<string> ScanAndGenerateReportAsync(string logFilePath, string? reportsDirectory = null)
        {
            // Scan the log file
            var issues = await ScanLogFileAsync(logFilePath);

            // Generate the report
            string report = _reportGenerator.GenerateReport(logFilePath, issues);

            // Save the report if a directory is specified
            if (!string.IsNullOrEmpty(reportsDirectory))
            {
                _reportGenerator.SaveReportToFile(report, logFilePath, reportsDirectory);
            }

            return report;
        }

        /// <summary>
        /// Processes a batch of crash logs, determines if any are unsolved, and moves them if needed
        /// </summary>
        /// <param name="logFilePaths">List of log file paths to process</param>
        /// <param name="reportsDirectory">Directory where reports should be saved</param>
        /// <param name="unsolvedDirectory">Directory where unsolved logs should be moved</param>
        /// <param name="moveUnsolved">Whether to move unsolved logs</param>
        /// <returns>Number of logs processed</returns>
        public async Task<int> ProcessCrashLogsWithReportingAsync(
            IEnumerable<string> logFilePaths,
            string reportsDirectory,
            string unsolvedDirectory,
            bool moveUnsolved = false)
        {
            int processedCount = 0;

            foreach (var logFilePath in logFilePaths)
            {
                try
                {
                    // Scan the log file
                    var issues = await ScanLogFileAsync(logFilePath);

                    // Generate and save the report
                    string report = _reportGenerator.GenerateReport(logFilePath, issues);
                    _reportGenerator.SaveReportToFile(report, logFilePath, reportsDirectory);

                    // Check if this is an "unsolved" log
                    bool isUnsolved = !issues.Any(i => i.Severity == SeverityLevel.Critical || i.Severity == SeverityLevel.Error);

                    // Move the log if it's unsolved and moveUnsolved is true
                    if (isUnsolved && moveUnsolved)
                    {
                        _reportGenerator.MoveUnsolvedLog(logFilePath, unsolvedDirectory);
                    }

                    processedCount++;
                }
                catch (Exception ex)
                {
                    // Log the exception but continue processing other logs
                    // In a real app, you would want to log this properly
                    Console.WriteLine($"Error processing {logFilePath}: {ex.Message}");
                }
            }

            return processedCount;
        }
    }
}

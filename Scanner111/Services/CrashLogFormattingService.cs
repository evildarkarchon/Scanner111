using Scanner111.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Scanner111.Services
{
    /// <summary>
    /// Service for reformatting crash log files 
    /// Ported from Python's crashlogs_reformat functionality
    /// </summary>
    public class CrashLogFormattingService
    {
        private readonly AppSettings _appSettings;
        // Regex for plugin entries with format like: [FE:  0] or [FE: 12]
        private static readonly Regex PluginEntryRegex = new Regex(@"(\[)([A-Za-z0-9]+:[ ]+[0-9]+)(\])", RegexOptions.Compiled);

        public CrashLogFormattingService(AppSettings appSettings)
        {
            _appSettings = appSettings;
        }

        /// <summary>
        /// Processes and reformats crash log files by optionally removing certain lines
        /// and normalizing plugin load order formatting.
        /// </summary>
        /// <param name="crashLogFiles">List of crash log file paths to process</param>
        /// <param name="removeStrings">List of strings that trigger line removal when SimplifyLogs is enabled</param>
        /// <returns>Number of files successfully processed</returns>
        public async Task<int> ReformatCrashLogsAsync(IEnumerable<string> crashLogFiles, IEnumerable<string> removeStrings)
        {
            if (crashLogFiles == null)
                throw new ArgumentNullException(nameof(crashLogFiles));

            int processedCount = 0;
            int failedCount = 0;
            bool simplifyLogs = _appSettings.SimplifyLogs;
            var processingErrors = new List<string>();

            foreach (var filePath in crashLogFiles)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(filePath))
                    {
                        processingErrors.Add("Skipped empty file path");
                        continue;
                    }

                    if (!File.Exists(filePath))
                    {
                        processingErrors.Add($"File not found: {filePath}");
                        continue;
                    }

                    // Check if file is readable and not locked
                    try
                    {
                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    }
                    catch (IOException ex)
                    {
                        processingErrors.Add($"File access error: {filePath} - {ex.Message}");
                        failedCount++;
                        continue;
                    }

                    // Read all lines from the file
                    string[] originalLines = await File.ReadAllLinesAsync(filePath);

                    // Process the content
                    var processedContent = FormatCrashLogContent(
                        string.Join(Environment.NewLine, originalLines),
                        removeStrings,
                        simplifyLogs);

                    // Write the processed content back to the file
                    await File.WriteAllTextAsync(filePath, processedContent);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    processingErrors.Add($"Error reformatting crash log {filePath}: {ex.Message}");
                    failedCount++;
                }
            }

            // Store errors for diagnostic purposes
            _appSettings.LastProcessingErrors = processingErrors;

            return processedCount;
        }

        /// <summary>
        /// Creates a formatted version of a crash log in memory without modifying the original file
        /// </summary>
        /// <param name="originalContent">Original crash log content</param>
        /// <param name="removeStrings">Strings to remove when simplifyLogs is true</param>
        /// <param name="simplifyLogs">Whether to remove lines containing removeStrings</param>
        /// <returns>Formatted crash log content</returns>
        public string FormatCrashLogContent(string originalContent, IEnumerable<string> removeStrings, bool simplifyLogs)
        {
            if (string.IsNullOrEmpty(originalContent))
                return originalContent;

            // Handle null removeStrings
            removeStrings = removeStrings ?? Array.Empty<string>();

            var originalLines = originalContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var processedLinesReversed = new List<string>();
            bool inPluginsSection = true;

            // Process the file from bottom to top
            for (int i = originalLines.Length - 1; i >= 0; i--)
            {
                var line = originalLines[i];

                // Check if we're exiting the PLUGINS section (when reading from bottom to top)
                if (inPluginsSection && line.StartsWith("PLUGINS:"))
                {
                    inPluginsSection = false;  // Exited the PLUGINS section
                }

                // Skip lines that should be removed when SimplifyLogs is enabled
                if (simplifyLogs && removeStrings.Any(s => !string.IsNullOrEmpty(s) &&
                                                   line.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;  // Skip this line
                }

                // Reformat plugin load order lines using regex for better accuracy
                if (inPluginsSection && line.Contains("["))
                {
                    try
                    {
                        // Use regex to find and replace plugin entries
                        string modifiedLine = PluginEntryRegex.Replace(line, match =>
                        {
                            var prefix = match.Groups[1].Value; // "["
                            var content = match.Groups[2].Value; // "FE:  0"
                            var suffix = match.Groups[3].Value; // "]"

                            // Replace spaces with zeros
                            var modifiedContent = content.Replace(" ", "0");

                            return $"{prefix}{modifiedContent}{suffix}";
                        });

                        processedLinesReversed.Add(modifiedLine);
                    }
                    catch
                    {
                        // If any error occurs during parsing, keep the original line
                        processedLinesReversed.Add(line);
                    }
                }
                else
                {
                    // Not a plugin line or not in PLUGINS section, keep as is
                    processedLinesReversed.Add(line);
                }
            }

            // Reverse the lines back to their original order
            processedLinesReversed.Reverse();

            // Combine the lines into a single string
            return string.Join(Environment.NewLine, processedLinesReversed);
        }

        /// <summary>
        /// Validates if a file appears to be a crash log based on content
        /// </summary>
        /// <param name="filePath">Path to the file to check</param>
        /// <returns>True if the file appears to be a crash log, otherwise false</returns>
        public async Task<bool> IsCrashLogAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                // Read the first few KB of the file to check for crash log indicators
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new StreamReader(fileStream);

                // Read up to 8KB to check for crash log indicators
                char[] buffer = new char[8192];
                int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                string content = new string(buffer, 0, bytesRead);

                // Check for common crash log indicators
                return content.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase) ||
                       content.Contains("PLUGINS:", StringComparison.OrdinalIgnoreCase) ||
                       content.Contains("CRASH REASON:", StringComparison.OrdinalIgnoreCase) ||
                       content.Contains("STACK:", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false; // If there's an error reading the file, it's not a valid crash log
            }
        }
    }
}

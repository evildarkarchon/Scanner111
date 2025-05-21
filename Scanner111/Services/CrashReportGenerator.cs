using Scanner111.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Scanner111.Services
{
    /// <summary>
    /// Service for generating formatted crash reports based on detected issues
    /// Similar to the autoscan_report function in the Python implementation
    /// </summary>
    public class CrashReportGenerator
    {
        private readonly AppSettings _appSettings;
        private readonly YamlSettingsCacheService _yamlSettingsCache;

        public CrashReportGenerator(
            AppSettings appSettings,
            YamlSettingsCacheService yamlSettingsCache)
        {
            _appSettings = appSettings;
            _yamlSettingsCache = yamlSettingsCache;
        }

        /// <summary>
        /// Generates a formatted report from the list of detected issues
        /// </summary>
        public string GenerateReport(string crashLogPath, List<LogIssue> issues)
        {
            if (issues == null || !issues.Any())
                return "No issues found in the crash log.";

            var reportBuilder = new StringBuilder();

            // Get the current time for the report header
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // Add header information
            reportBuilder.AppendLine("==================================================================");
            reportBuilder.AppendLine($"CLASSIC AUTOSCAN REPORT | {timestamp}");
            reportBuilder.AppendLine("==================================================================");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine($"LOG FILE: {Path.GetFileName(crashLogPath)}");
            reportBuilder.AppendLine();

            // Generate a random hint if available
            string randomHint = GetRandomHint();
            if (!string.IsNullOrEmpty(randomHint))
            {
                reportBuilder.AppendLine(randomHint);
                reportBuilder.AppendLine();
            }

            // Get critical issues first
            var criticalIssues = issues.Where(i => i.Severity == SeverityLevel.Critical).ToList();
            if (criticalIssues.Any())
            {
                reportBuilder.AppendLine("CRITICAL ISSUES");
                reportBuilder.AppendLine("==================================================================");
                foreach (var issue in criticalIssues)
                {
                    AppendIssue(reportBuilder, issue);
                }
                reportBuilder.AppendLine();
            }

            // Get error issues
            var errorIssues = issues.Where(i => i.Severity == SeverityLevel.Error).ToList();
            if (errorIssues.Any())
            {
                reportBuilder.AppendLine("ERRORS");
                reportBuilder.AppendLine("==================================================================");
                foreach (var issue in errorIssues)
                {
                    AppendIssue(reportBuilder, issue);
                }
                reportBuilder.AppendLine();
            }

            // Get warning issues
            var warningIssues = issues.Where(i => i.Severity == SeverityLevel.Warning).ToList();
            if (warningIssues.Any())
            {
                reportBuilder.AppendLine("WARNINGS");
                reportBuilder.AppendLine("==================================================================");
                foreach (var issue in warningIssues)
                {
                    AppendIssue(reportBuilder, issue);
                }
                reportBuilder.AppendLine();
            }

            // Get information issues
            var infoIssues = issues.Where(i => i.Severity == SeverityLevel.Information).ToList();
            if (infoIssues.Any())
            {
                reportBuilder.AppendLine("INFORMATION");
                reportBuilder.AppendLine("==================================================================");
                foreach (var issue in infoIssues)
                {
                    AppendIssue(reportBuilder, issue);
                }
                reportBuilder.AppendLine();
            }

            // Summary
            reportBuilder.AppendLine("==================================================================");
            reportBuilder.AppendLine("SUMMARY");
            reportBuilder.AppendLine("==================================================================");
            reportBuilder.AppendLine($"Critical Issues: {criticalIssues.Count}");
            reportBuilder.AppendLine($"Errors: {errorIssues.Count}");
            reportBuilder.AppendLine($"Warnings: {warningIssues.Count}");
            reportBuilder.AppendLine($"Information: {infoIssues.Count}");
            reportBuilder.AppendLine();
            reportBuilder.AppendLine("==================================================================");
            reportBuilder.AppendLine("END OF REPORT");
            reportBuilder.AppendLine("==================================================================");

            return reportBuilder.ToString();
        }

        /// <summary>
        /// Formats and appends a single issue to the report
        /// </summary>
        private void AppendIssue(StringBuilder reportBuilder, LogIssue issue)
        {
            reportBuilder.AppendLine($"[{issue.Title}]");
            reportBuilder.AppendLine($"{issue.Message}");

            if (!string.IsNullOrEmpty(issue.Details))
            {
                reportBuilder.AppendLine($"Details: {issue.Details}");
            }

            if (!string.IsNullOrEmpty(issue.Recommendation))
            {
                reportBuilder.AppendLine($"Recommendation: {issue.Recommendation}");
            }

            reportBuilder.AppendLine("------------------------------------------------------------------");
        }

        /// <summary>
        /// Gets a random hint from the hints list in the YAML database
        /// </summary>
        private string GetRandomHint()
        {
            try
            {
                var gameYamlNode = _yamlSettingsCache.GetYamlNode(YAML.Game);
                if (gameYamlNode == null)
                    return string.Empty;

                var hintsNode = _yamlSettingsCache.GetNodeByPath(gameYamlNode, "Game_Hints");
                if (hintsNode == null)
                    return string.Empty;

                // Convert the YAML node to a list of hints
                var hints = _yamlSettingsCache.GetChildrenAsList(hintsNode);
                if (hints == null || !hints.Any())
                    return string.Empty;

                // Select a random hint
                var random = new Random();
                int index = random.Next(hints.Count);
                return hints[index];
            }
            catch (Exception)
            {
                // If anything goes wrong, return an empty string
                return string.Empty;
            }
        }

        /// <summary>
        /// Saves the crash report to a file
        /// </summary>
        public void SaveReportToFile(string reportContent, string crashLogPath, string outputDirectory)
        {
            try
            {
                // Create output directory if it doesn't exist
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Generate a filename based on the original crash log
                string baseFilename = Path.GetFileNameWithoutExtension(crashLogPath);
                string reportFilename = $"{baseFilename}_report_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string reportFilePath = Path.Combine(outputDirectory, reportFilename);

                // Write the report content to the file
                File.WriteAllText(reportFilePath, reportContent);
            }
            catch (Exception ex)
            {
                // In a real app, you would handle this error appropriately
                Console.WriteLine($"Error saving report: {ex.Message}");
            }
        }

        /// <summary>
        /// Moves unsolved logs to a separate directory
        /// </summary>
        public void MoveUnsolvedLog(string crashLogPath, string unsolvedDirectory)
        {
            try
            {
                // Create unsolved directory if it doesn't exist
                if (!Directory.Exists(unsolvedDirectory))
                {
                    Directory.CreateDirectory(unsolvedDirectory);
                }

                // Generate a filename for the unsolved log
                string filename = Path.GetFileName(crashLogPath);
                string unsolvedFilePath = Path.Combine(unsolvedDirectory, filename);

                // If a file with this name already exists, append a timestamp
                if (File.Exists(unsolvedFilePath))
                {
                    string baseFilename = Path.GetFileNameWithoutExtension(filename);
                    string extension = Path.GetExtension(filename);
                    unsolvedFilePath = Path.Combine(unsolvedDirectory, $"{baseFilename}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
                }

                // Move the file
                File.Move(crashLogPath, unsolvedFilePath);
            }
            catch (Exception ex)
            {
                // In a real app, you would handle this error appropriately
                Console.WriteLine($"Error moving unsolved log: {ex.Message}");
            }
        }
    }
}

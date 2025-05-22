using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services
{
    /// <summary>
    /// Service for scanning and analyzing Wrye Bash report data
    /// </summary>
    public class ScanWryeCheckService : IScanWryeCheckService
    {
        private readonly IYamlSettingsCacheService _yamlSettingsCache;
        private readonly bool _testMode;

        public ScanWryeCheckService(IYamlSettingsCacheService yamlSettingsCache, bool testMode = false)
        {
            _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
            _testMode = testMode;
        }

        /// <summary>
        /// Analyzes Wrye Bash reports for potential issues.
        /// </summary>
        /// <returns>A detailed analysis report from Wrye Bash data.</returns>
        public async Task<string> ScanWryeCheckAsync()
        {
            var results = new StringBuilder();
            results.AppendLine("================ WRYE BASH CHECK ANALYSIS ================\n");

            var gameDir = GetSetting<string>(YAML.Game, "game_dir");
            if (string.IsNullOrEmpty(gameDir))
            {
                results.AppendLine("❌ ERROR : Game directory not configured in settings");
                return results.ToString();
            }

            // Get reports directory
            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var gameName = GetSetting<string>(YAML.Game, "game_name") ?? "Fallout4";
            var reportsPath = Path.Combine(docsPath, "My Games", gameName, "Bash Patches");

            if (!Directory.Exists(reportsPath) && !_testMode)
            {
                results.AppendLine("⚠️ WARNING : Wrye Bash reports directory not found!");
                results.AppendLine($"     Expected path: {reportsPath}");
                return results.ToString();
            }

            // Find the latest report file
            var reportFile = await FindLatestReportAsync(reportsPath);

            if (string.IsNullOrEmpty(reportFile))
            {
                results.AppendLine("⚠️ WARNING : No Wrye Bash report files found");
                results.AppendLine("     Run 'Build Patch' in Wrye Bash to generate a report");
                return results.ToString();
            }

            // Parse and analyze the report
            var reportIssues = await AnalyzeReportAsync(reportFile);

            if (reportIssues.Count == 0)
            {
                results.AppendLine($"✔️ No issues found in the Wrye Bash report: {Path.GetFileName(reportFile)}");
            }
            else
            {
                results.AppendLine(
                    $"Found {reportIssues.Count} issues in Wrye Bash report: {Path.GetFileName(reportFile)}\n");

                foreach (var issue in reportIssues)
                {
                    results.AppendLine($"⚠️ {issue.IssueType}: {issue.PluginName}");
                    results.AppendLine($"    {issue.Description}");
                    results.AppendLine();
                }
            }

            return results.ToString();
        }

        /// <summary>
        /// Finds the most recent Wrye Bash report file
        /// </summary>
        private async Task<string> FindLatestReportAsync(string reportsPath)
        {
            if (_testMode)
            {
                return "test_report.txt";
            }

            string latestReport = "";
            DateTime latestDate = DateTime.MinValue;

            await Task.Run(() =>
            {
                try
                {
                    var reportFiles = Directory.GetFiles(reportsPath, "*.csv")
                        .Concat(Directory.GetFiles(reportsPath, "*.txt"))
                        .Where(f => Path.GetFileName(f).Contains("Report", StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var file in reportFiles)
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime > latestDate)
                        {
                            latestDate = fileInfo.LastWriteTime;
                            latestReport = file;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error finding Wrye Bash report: {ex.Message}");
                }
            });

            return latestReport;
        }

        /// <summary>
        /// Represents an issue found in a Wrye Bash report
        /// </summary>
        private class ReportIssue
        {
            public string IssueType { get; set; } = "";
            public string PluginName { get; set; } = "";
            public string Description { get; set; } = "";
        }

        /// <summary>
        /// Analyzes a Wrye Bash report file
        /// </summary>
        private async Task<List<ReportIssue>> AnalyzeReportAsync(string reportPath)
        {
            var issues = new List<ReportIssue>();

            if (_testMode)
            {
                return issues;
            }

            try
            {
                var reportContent = await File.ReadAllTextAsync(reportPath);

                // Analyze ITM Records (Identical to Master records)
                await AnalyzeItmRecordsAsync(reportContent, issues);

                // Analyze UDR Records (Undeleted References)
                await AnalyzeUdrRecordsAsync(reportContent, issues);

                // Analyze Wild Edits
                await AnalyzeWildEditsAsync(reportContent, issues);

                // Analyze Form Version errors (only for Skyrim)
                if (reportContent.Contains("FormID Versions", StringComparison.OrdinalIgnoreCase))
                {
                    await AnalyzeFormVersionsAsync(reportContent, issues);
                }
            }
            catch (Exception ex)
            {
                issues.Add(new ReportIssue
                {
                    IssueType = "Report Parse Error",
                    PluginName = Path.GetFileName(reportPath),
                    Description = $"Failed to parse report: {ex.Message}"
                });
            }

            return issues;
        }

        /// <summary>
        /// Analyzes ITM (Identical to Master) records in the report
        /// </summary>
        private async Task AnalyzeItmRecordsAsync(string reportContent, List<ReportIssue> issues)
        {
            await Task.Run(() =>
            {
                var itmPattern = new Regex(@"ITM, Identical to Master\s*\((\d+)\):\s*(.+?)(?:\r?\n|$)");
                var matches = itmPattern.Matches(reportContent);

                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count >= 3)
                    {
                        var count = match.Groups[1].Value;
                        var plugin = match.Groups[2].Value.Trim();

                        if (int.Parse(count) > 0)
                        {
                            issues.Add(new ReportIssue
                            {
                                IssueType = "ITM Records",
                                PluginName = plugin,
                                Description = $"Contains {count} records identical to master that should be cleaned"
                            });
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Analyzes UDR (Undeleted References) records in the report
        /// </summary>
        private async Task AnalyzeUdrRecordsAsync(string reportContent, List<ReportIssue> issues)
        {
            await Task.Run(() =>
            {
                var udrPattern = new Regex(@"UDR, Undeleted References\s*\((\d+)\):\s*(.+?)(?:\r?\n|$)");
                var matches = udrPattern.Matches(reportContent);

                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count >= 3)
                    {
                        var count = match.Groups[1].Value;
                        var plugin = match.Groups[2].Value.Trim();

                        if (int.Parse(count) > 0)
                        {
                            issues.Add(new ReportIssue
                            {
                                IssueType = "Undeleted References",
                                PluginName = plugin,
                                Description = $"Contains {count} undeleted references that should be cleaned"
                            });
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Analyzes wild edits in the report
        /// </summary>
        private async Task AnalyzeWildEditsAsync(string reportContent, List<ReportIssue> issues)
        {
            await Task.Run(() =>
            {
                var wildEditPattern = new Regex(@"Wild Edits\s*\((\d+)\):\s*(.+?)(?:\r?\n|$)");
                var matches = wildEditPattern.Matches(reportContent);

                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count >= 3)
                    {
                        var count = match.Groups[1].Value;
                        var plugin = match.Groups[2].Value.Trim();

                        if (int.Parse(count) > 0)
                        {
                            issues.Add(new ReportIssue
                            {
                                IssueType = "Wild Edits",
                                PluginName = plugin,
                                Description = $"Contains {count} wild edits that may cause compatibility issues"
                            });
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Analyzes FormID version errors (Skyrim-specific)
        /// </summary>
        private async Task AnalyzeFormVersionsAsync(string reportContent, List<ReportIssue> issues)
        {
            await Task.Run(() =>
            {
                var formVersionPattern = new Regex(@"FormID Versions\s*\((\d+)\):\s*(.+?)(?:\r?\n|$)");
                var matches = formVersionPattern.Matches(reportContent);

                foreach (Match match in matches)
                {
                    if (match.Success && match.Groups.Count >= 3)
                    {
                        var count = match.Groups[1].Value;
                        var plugin = match.Groups[2].Value.Trim();

                        if (int.Parse(count) > 0)
                        {
                            issues.Add(new ReportIssue
                            {
                                IssueType = "FormID Version Errors",
                                PluginName = plugin,
                                Description = $"Contains {count} FormID version errors that may cause issues"
                            });
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Gets a setting from the YAML settings cache
        /// </summary>
        private T? GetSetting<T>(YAML yamlType, string key) where T : class
        {
            try
            {
                return _yamlSettingsCache.GetSetting<T>(yamlType, key);
            }
            catch
            {
                return null;
            }
        }
    }
}


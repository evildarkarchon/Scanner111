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
    /// Service for scanning mod INI files for potential issues
    /// </summary>
    public class ScanModInisService : IScanModInisService
    {
        private readonly YamlSettingsCacheService _yamlSettingsCache;
        private readonly bool _testMode;

        public ScanModInisService(YamlSettingsCacheService yamlSettingsCache, bool testMode = false)
        {
            _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
            _testMode = testMode;
        }

        /// <summary>
        /// Scans mod INI files for potential issues or incompatibilities.
        /// </summary>
        /// <returns>A detailed report of the mod INI file analysis.</returns>
        public async Task<string> ScanModInisAsync()
        {
            var results = new StringBuilder();
            results.AppendLine("================= MOD INI FILES SCAN =================\n");

            var gameDir = GetSetting<string>(YAML.Game, "game_dir");
            if (string.IsNullOrEmpty(gameDir))
            {
                results.AppendLine("❌ ERROR : Game directory not configured in settings");
                return results.ToString();
            }

            // Get scan configuration
            var problematicSettings = GetSettingAsList<Dictionary<string, string>>(YAML.Main, "problematic_ini_settings");
            var excludedInis = GetSettingAsList<string>(YAML.Main, "exclude_inis");

            if (problematicSettings.Count == 0)
            {
                results.AppendLine("ℹ️ No problematic INI settings defined in configuration");
                return results.ToString();
            }

            // Find INI files in Data directory
            var dataPath = Path.Combine(gameDir, "Data");
            var iniFiles = await GetIniFilesAsync(dataPath, excludedInis);

            if (iniFiles.Count == 0)
            {
                results.AppendLine("ℹ️ No INI files found to scan");
                return results.ToString();
            }

            results.AppendLine($"Found {iniFiles.Count} INI files to scan\n");
            int issueCount = 0;

            // Scan each INI file
            foreach (var iniFile in iniFiles)
            {
                var fileIssues = await ScanIniFileAsync(iniFile, problematicSettings);

                if (fileIssues.Count > 0)
                {
                    var relativePath = iniFile.Replace(dataPath, "Data").TrimStart('\\', '/');
                    results.AppendLine($"⚠️ Issues found in {relativePath}:");

                    foreach (var issue in fileIssues)
                    {
                        results.AppendLine($"  - Line {issue.LineNumber}: {issue.Line}");
                        results.AppendLine($"    {issue.Reason}");
                    }

                    results.AppendLine();
                    issueCount += fileIssues.Count;
                }
            }

            results.AppendLine($"Total INI issues found: {issueCount}");

            return results.ToString();
        }

        /// <summary>
        /// Finds all INI files in the given directory and subdirectories
        /// </summary>
        private async Task<List<string>> GetIniFilesAsync(string rootPath, List<string> excludedInis)
        {
            var iniFiles = new List<string>();

            // In test mode, return an empty list to skip the actual file system operations
            if (_testMode)
            {
                return iniFiles;
            }

            await Task.Run(() =>
            {
                try
                {
                    var allIniFiles = Directory.GetFiles(rootPath, "*.ini", SearchOption.AllDirectories);

                    // Filter out excluded INIs
                    foreach (var file in allIniFiles)
                    {
                        bool excluded = false;
                        foreach (var pattern in excludedInis)
                        {
                            if (file.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                excluded = true;
                                break;
                            }
                        }

                        if (!excluded)
                        {
                            iniFiles.Add(file);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error finding INI files: {ex.Message}");
                }
            });

            return iniFiles;
        }

        /// <summary>
        /// Represents an issue found in an INI file
        /// </summary>
        private class IniIssue
        {
            public int LineNumber { get; set; }
            public string Line { get; set; } = "";
            public string Reason { get; set; } = "";
        }

        /// <summary>
        /// Scans a single INI file for problematic settings
        /// </summary>
        private async Task<List<IniIssue>> ScanIniFileAsync(string iniPath, List<Dictionary<string, string>> problematicSettings)
        {
            var issues = new List<IniIssue>();

            // In test mode, return empty results
            if (_testMode)
            {
                return issues;
            }

            try
            {
                var lines = await File.ReadAllLinesAsync(iniPath);

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();

                    // Skip comments and empty lines
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";") || line.StartsWith("#"))
                    {
                        continue;
                    }

                    // Check against problematic settings
                    foreach (var setting in problematicSettings)
                    {
                        if (!setting.TryGetValue("pattern", out var pattern) || string.IsNullOrEmpty(pattern))
                        {
                            continue;
                        }

                        setting.TryGetValue("reason", out var reason);
                        reason ??= "This setting may cause issues";

                        try
                        {
                            if (Regex.IsMatch(line, pattern, RegexOptions.IgnoreCase))
                            {
                                issues.Add(new IniIssue
                                {
                                    LineNumber = i + 1,
                                    Line = line,
                                    Reason = reason
                                });
                                break; // Only report one issue per line
                            }
                        }
                        catch (RegexParseException)
                        {
                            // If regex is invalid, try a simple string contains check
                            if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                            {
                                issues.Add(new IniIssue
                                {
                                    LineNumber = i + 1,
                                    Line = line,
                                    Reason = reason
                                });
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                issues.Add(new IniIssue
                {
                    LineNumber = 0,
                    Line = "Error scanning file",
                    Reason = ex.Message
                });
            }

            return issues;
        }

        /// <summary>
        /// Gets a setting from the YAML settings cache
        /// </summary>
        private T? GetSetting<T>(YAML yamlType, string key) where T : class
        {
            try
            {
                var storeType = YamlTypeToStoreType(yamlType);
                return _yamlSettingsCache.GetSetting<T>(storeType, key);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Gets a list setting from the YAML settings cache
        /// </summary>
        private List<T> GetSettingAsList<T>(YAML yamlType, string key)
        {
            try
            {
                var storeType = YamlTypeToStoreType(yamlType);
                var setting = _yamlSettingsCache.GetSetting<List<T>>(storeType, key);
                return setting ?? new List<T>();
            }
            catch
            {
                return new List<T>();
            }
        }

        /// <summary>
        /// Maps YAML enum to YamlStoreType enum
        /// </summary>
        private YamlStoreType YamlTypeToStoreType(YAML yamlType)
        {
            return yamlType switch
            {
                YAML.Main => YamlStoreType.Main,
                YAML.Game => YamlStoreType.Game,
                YAML.Game_Local => YamlStoreType.GameLocal,
                _ => YamlStoreType.Main // Default case
            };
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services
{
    /// <summary>
    /// Implementation for checking Buffout/Crashgen settings
    /// </summary>
    public class CheckCrashgenSettingsService : ICheckCrashgenSettingsService
    {
        private readonly YamlSettingsCacheService _yamlSettingsCache;

        public CheckCrashgenSettingsService(YamlSettingsCacheService yamlSettingsCache)
        {
            _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
        }

        /// <summary>
        /// Checks Buffout/Crashgen settings for potential issues or optimizations.
        /// </summary>
        /// <returns>A detailed report of settings analysis.</returns>
        public async Task<string> CheckCrashgenSettingsAsync()
        {
            var results = new StringBuilder();
            results.AppendLine("============= BUFFOUT/CRASHGEN SETTINGS =============\n");

            var gameDir = GetSetting<string>(YAML.Game, "game_dir");
            if (string.IsNullOrEmpty(gameDir))
            {
                results.AppendLine("❌ ERROR : Game directory not configured in settings");
                return results.ToString();
            }

            // Look for Buffout4.toml or BuffoutAE.toml or equivalent based on game
            var buffoutPaths = new List<string>
            {
                Path.Combine(gameDir, "Data", "F4SE", "Plugins", "Buffout4.toml"),
                Path.Combine(gameDir, "Data", "SKSE", "Plugins", "CrashLogger.toml"),
                Path.Combine(gameDir, "Data", "SKSE", "Plugins", "CrashLoggerSSE.toml"),
                Path.Combine(gameDir, "Data", "SKSE", "Plugins", "EngineFixes.toml")
            };

            bool foundAnyConfig = false;

            foreach (var configPath in buffoutPaths)
            {
                if (!File.Exists(configPath))
                {
                    continue;
                }

                foundAnyConfig = true;
                results.AppendLine($"✔️ Analyzing settings file: {Path.GetFileName(configPath)}\n");

                try
                {
                    var configLines = await File.ReadAllLinesAsync(configPath);
                    var settings = ParseTomlSettings(configLines);

                    // Check for recommended settings based on config type
                    if (configPath.Contains("Buffout4"))
                    {
                        CheckBuffout4Settings(settings, results);
                    }
                    else if (configPath.Contains("CrashLogger"))
                    {
                        CheckCrashLoggerSettings(settings, results);
                    }
                    else if (configPath.Contains("EngineFixes"))
                    {
                        CheckEngineFixesSettings(settings, results);
                    }
                }
                catch (Exception ex)
                {
                    results.AppendLine($"❌ ERROR: Failed to parse {Path.GetFileName(configPath)}: {ex.Message}");
                }
            }

            if (!foundAnyConfig)
            {
                results.AppendLine("⚠️ WARNING: No Buffout/Crashgen configuration files were found.");
                results.AppendLine("Consider installing Buffout4 (for Fallout 4) or Crash Logger (for Skyrim)");
                results.AppendLine("to improve crash diagnostics and game stability.");
            }

            return results.ToString();
        }

        /// <summary>
        /// Parses TOML configuration into settings dictionary
        /// </summary>
        private Dictionary<string, string> ParseTomlSettings(string[] lines)
        {
            var settings = new Dictionary<string, string>();
            string currentSection = "";

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("#") || trimmedLine.StartsWith(";"))
                {
                    continue;
                }

                // Extract section headers [Section]
                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2).Trim();
                    continue;
                }

                // Extract key = value pairs
                var parts = trimmedLine.Split('=', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim().Trim('"', '\'');

                    // Add with section prefix if we're in a section
                    var fullKey = !string.IsNullOrEmpty(currentSection) ? $"{currentSection}.{key}" : key;
                    settings[fullKey] = value;
                }
            }

            return settings;
        }

        /// <summary>
        /// Checks Buffout4 settings against recommended values
        /// </summary>
        private void CheckBuffout4Settings(Dictionary<string, string> settings, StringBuilder results)
        {
            // Define recommended Buffout4 settings
            var recommended = new Dictionary<string, (string Value, string Reason)>
            {
                // Critical settings for crash logging
                {"Fixes.MemoryManager", ("true", "Required for proper crash logging")},
                {"Fixes.ActorIsHostileToActor", ("true", "Prevents common crash")},
                {"Fixes.Achievements", ("true", "Recommended for mod compatibility")},
                {"Fixes.BSMTAManager", ("true", "Improves game stability")},
                
                // Recommended for crash logging
                {"Logging.ModNames", ("true", "Helps identify problematic mods in crash logs")},
                {"Logging.Plugins", ("true", "Essential for crash diagnostics")},
                {"Logging.Crashes", ("true", "Required for generating useful crash logs")},
                {"Logging.StockGames", ("true", "Provides better crash logs")}
            };

            foreach (var (key, (recommendedValue, reason)) in recommended)
            {
                if (settings.TryGetValue(key, out var actualValue))
                {
                    if (string.Equals(actualValue.ToLower(), recommendedValue.ToLower()))
                    {
                        results.AppendLine($"✔️ {key} = {actualValue} (Correct)");
                    }
                    else
                    {
                        results.AppendLine($"⚠️ {key} = {actualValue} (Recommended: {recommendedValue})");
                        results.AppendLine($"   Reason: {reason}");
                    }
                }
                else
                {
                    results.AppendLine($"❓ {key} not found (Recommended: {recommendedValue})");
                    results.AppendLine($"   Reason: {reason}");
                }
            }
        }

        /// <summary>
        /// Checks CrashLogger settings against recommended values
        /// </summary>
        private void CheckCrashLoggerSettings(Dictionary<string, string> settings, StringBuilder results)
        {
            var recommended = new Dictionary<string, (string Value, string Reason)>
            {
                {"Settings.EnableCrashLogger", ("true", "Required for crash logging")},
                {"Settings.IncludePluginsList", ("true", "Helps identify problematic plugins in crash logs")},
                {"Settings.DumpModList", ("true", "Helps identify problematic mods in crash logs")}
            };

            foreach (var (key, (recommendedValue, reason)) in recommended)
            {
                if (settings.TryGetValue(key, out var actualValue))
                {
                    if (string.Equals(actualValue.ToLower(), recommendedValue.ToLower()))
                    {
                        results.AppendLine($"✔️ {key} = {actualValue} (Correct)");
                    }
                    else
                    {
                        results.AppendLine($"⚠️ {key} = {actualValue} (Recommended: {recommendedValue})");
                        results.AppendLine($"   Reason: {reason}");
                    }
                }
                else
                {
                    results.AppendLine($"❓ {key} not found (Recommended: {recommendedValue})");
                    results.AppendLine($"   Reason: {reason}");
                }
            }
        }

        /// <summary>
        /// Checks EngineFixes settings against recommended values
        /// </summary>
        private void CheckEngineFixesSettings(Dictionary<string, string> settings, StringBuilder results)
        {
            var recommended = new Dictionary<string, (string Value, string Reason)>
            {
                {"Patches.EnableMemoryManager", ("true", "Improves game stability")},
                {"Patches.SaveGameMaxSize", ("true", "Prevents save corruption")},
                {"Fixes.MemoryAccessErrors", ("true", "Prevents common crashes")},
                {"Fixes.RegularQuickSaves", ("true", "Prevents save corruption")}
            };

            foreach (var (key, (recommendedValue, reason)) in recommended)
            {
                if (settings.TryGetValue(key, out var actualValue))
                {
                    if (string.Equals(actualValue.ToLower(), recommendedValue.ToLower()))
                    {
                        results.AppendLine($"✔️ {key} = {actualValue} (Correct)");
                    }
                    else
                    {
                        results.AppendLine($"⚠️ {key} = {actualValue} (Recommended: {recommendedValue})");
                        results.AppendLine($"   Reason: {reason}");
                    }
                }
                else
                {
                    results.AppendLine($"❓ {key} not found (Recommended: {recommendedValue})");
                    results.AppendLine($"   Reason: {reason}");
                }
            }
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
                return null; // CS8603
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

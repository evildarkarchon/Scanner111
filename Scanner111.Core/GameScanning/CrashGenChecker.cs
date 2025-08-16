using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Tomlyn;
using Tomlyn.Model;

namespace Scanner111.Core.GameScanning
{
    /// <summary>
    /// Checks and validates settings for Crash Generator (Buffout4) configuration.
    /// Supports both regular Buffout 4 and Buffout 4 NG (VR version).
    /// </summary>
    public class CrashGenChecker : ICrashGenChecker
    {
        private readonly IApplicationSettingsService _settingsService;
        private readonly IYamlSettingsProvider _yamlProvider;
        private readonly ILogger<CrashGenChecker> _logger;
        private readonly List<string> _messageList = new();
        private string? _pluginsPath;
        private string _crashGenName = "Buffout4";
        private string? _configFile;
        private HashSet<string> _installedPlugins = new();

        public CrashGenChecker(
            IApplicationSettingsService settingsService,
            IYamlSettingsProvider yamlProvider,
            ILogger<CrashGenChecker> logger)
        {
            _settingsService = settingsService;
            _yamlProvider = yamlProvider;
            _logger = logger;
        }

        public async Task<string> CheckAsync()
        {
            await Task.Run(() =>
            {
                InitializeSettings();
                
                // Check for obsolete plugins first
                CheckObsoletePlugins();
                
                if (string.IsNullOrEmpty(_configFile) || !File.Exists(_configFile))
                {
                    _messageList.AddRange(new[]
                    {
                        $"# [!] NOTICE : Unable to find the {_crashGenName} config file, settings check will be skipped. #\n",
                        $"  To ensure this check doesn't get skipped, {_crashGenName} has to be installed manually.\n",
                        "  [ If you are using Mod Organizer 2, you need to run Scanner111 through a shortcut in MO2. ]\n-----\n"
                    });
                    return;
                }

                _logger.LogInformation($"Checking {_crashGenName} settings in {_configFile}");
                ProcessSettings();
            });
            
            return string.Join("", _messageList);
        }

        public bool HasPlugin(List<string> pluginNames)
        {
            return pluginNames.Any(plugin => _installedPlugins.Contains(plugin.ToLowerInvariant()));
        }

        private void InitializeSettings()
        {
            // Get plugins path from settings
            var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
            _pluginsPath = settings.PluginsFolder;
            
            // Get crash generator name from YAML
            // For now, use default Buffout4 name
            // TODO: Load from YAML configuration when structure is defined
            _crashGenName = "Buffout4";
            
            // Detect installed plugins first (needed for obsolete check)
            DetectInstalledPlugins();
            
            // Find config file
            _configFile = FindConfigFile();
        }

        private void CheckObsoletePlugins()
        {
            // Check for obsolete X-Cell version
            if (_installedPlugins.Contains("x-cell-fo4.dll"))
            {
                _messageList.AddRange(new[]
                {
                    "# ⚠️ WARNING : OBSOLETE X-CELL VERSION DETECTED! #\n",
                    "  You are using x-cell-fo4.dll which is an obsolete version of the X-Cell plugin.\n",
                    "  FIX: Please update to the latest version:\n",
                    "    • For regular Fallout 4: Use x-cell-og.dll\n",
                    "    • For Next-Gen update: Use x-cell-ng2.dll\n",
                    "  Download the latest version from the X-Cell mod page.\n-----\n"
                });
            }
            
            // Check for other known obsolete plugins
            var obsoletePlugins = new Dictionary<string, string>
            {
                { "achievementsmodsenablerloader.dll", "Achievements Mod Enabler (use achievements.dll instead)" },
                { "f4se_loader_bridge.dll", "F4SE Loader Bridge (integrated into modern F4SE)" }
            };
            
            foreach (var plugin in obsoletePlugins)
            {
                if (_installedPlugins.Contains(plugin.Key))
                {
                    _messageList.AddRange(new[]
                    {
                        $"# ⚠️ WARNING : OBSOLETE PLUGIN DETECTED: {plugin.Value} #\n",
                        $"  File: {plugin.Key}\n",
                        "  This plugin is obsolete and may cause compatibility issues.\n",
                        "  FIX: Remove or update to the latest version.\n-----\n"
                    });
                }
            }
        }

        private string? FindConfigFile()
        {
            if (string.IsNullOrEmpty(_pluginsPath) || !Directory.Exists(_pluginsPath))
            {
                return null;
            }

            // Both Buffout 4 and Buffout 4 NG use the same config file location
            var crashGenToml = Path.Combine(_pluginsPath, "Buffout4", "config.toml");
            
            // For older installations or alternative locations
            var crashGenTomlAlt = Path.Combine(_pluginsPath, "Buffout4.toml");

            // Check if main config file exists
            if (File.Exists(crashGenToml))
            {
                return crashGenToml;
            }
            
            // Check alternative location
            if (File.Exists(crashGenTomlAlt))
            {
                _messageList.AddRange(new[]
                {
                    $"# ⚠️ NOTICE : {_crashGenName} config found in non-standard location #\n",
                    $"  Config file found at: {crashGenTomlAlt}\n",
                    $"  Consider moving it to: {crashGenToml}\n-----\n"
                });
                return crashGenTomlAlt;
            }

            // No config file found
            _messageList.AddRange(new[]
            {
                $"# ❌ CAUTION : {_crashGenName.ToUpper()} TOML SETTINGS FILE NOT FOUND! #\n",
                $"  Expected location: {crashGenToml}\n",
                $"  Please verify your {_crashGenName} installation.\n-----\n"
            });
            
            return null;
        }

        private void DetectInstalledPlugins()
        {
            if (string.IsNullOrEmpty(_pluginsPath) || !Directory.Exists(_pluginsPath))
            {
                return;
            }

            try
            {
                var files = Directory.GetFiles(_pluginsPath, "*.dll", SearchOption.TopDirectoryOnly);
                _installedPlugins = files.Select(f => Path.GetFileName(f).ToLowerInvariant()).ToHashSet();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accessing plugins directory");
            }
        }

        private void ProcessSettings()
        {
            if (string.IsNullOrEmpty(_configFile) || !File.Exists(_configFile))
            {
                return;
            }

            try
            {
                var tomlContent = File.ReadAllText(_configFile);
                var tomlTable = Toml.ToModel(tomlContent);
                
                var settings = GetSettingsToCheck();
                var hasBakaScrapHeap = _installedPlugins.Contains("bakascrapheap.dll");
                var hasChanges = false;

                foreach (var setting in settings)
                {
                    var currentValue = GetTomlValue(tomlTable, setting.Section, setting.Key);
                    
                    // Special case for BakaScrapHeap with MemoryManager
                    if (setting.SpecialCase == "bakascrapheap" && hasBakaScrapHeap && currentValue != null)
                    {
                        _messageList.AddRange(new[]
                        {
                            $"# ❌ CAUTION : The Baka ScrapHeap Mod is installed, but is redundant with {_crashGenName} #\n",
                            $" FIX: Uninstall the Baka ScrapHeap Mod, this prevents conflicts with {_crashGenName}.\n-----\n"
                        });
                        continue;
                    }

                    // Check if condition is met and setting needs changing
                    if (setting.Condition && !ValuesEqual(currentValue, setting.DesiredValue))
                    {
                        _messageList.AddRange(new[]
                        {
                            $"# ❌ CAUTION : {setting.Description}, but {setting.Name} parameter is set to {currentValue} #\n",
                            $"    Auto Scanner will change this parameter to {setting.DesiredValue} {setting.Reason}.\n-----\n"
                        });
                        
                        // Apply the change
                        SetTomlValue(tomlTable, setting.Section, setting.Key, setting.DesiredValue);
                        hasChanges = true;
                        _logger.LogInformation($"Changed {setting.Name} from {currentValue} to {setting.DesiredValue}");
                    }
                    else if (setting.Condition)
                    {
                        _messageList.Add($"✔️ {setting.Name} parameter is correctly configured in your {_crashGenName} settings!\n-----\n");
                    }
                }

                // Save changes if any were made
                if (hasChanges)
                {
                    var updatedToml = Toml.FromModel(tomlTable);
                    File.WriteAllText(_configFile, updatedToml);
                    _logger.LogInformation($"Saved updated configuration to {_configFile}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing {_crashGenName} settings");
                _messageList.Add($"# ❌ ERROR : Failed to process {_crashGenName} settings: {ex.Message} #\n-----\n");
            }
        }

        private List<ConfigSetting> GetSettingsToCheck()
        {
            var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
            if (settings.GameType != GameType.Fallout4 && settings.GameType != GameType.Fallout4VR)
            {
                return new List<ConfigSetting>();
            }

            // Check for X-Cell - now excluding obsolete version since we warn about it separately
            var hasXCell = HasPlugin(new List<string> { "x-cell-og.dll", "x-cell-ng2.dll" });
            var hasObsoleteXCell = _installedPlugins.Contains("x-cell-fo4.dll");
            var hasAnyXCell = hasXCell || hasObsoleteXCell;
            
            var hasAchievements = HasPlugin(new List<string> { "achievements.dll", "achievementsmodsenablerloader.dll" });
            var hasLooksMenu = _installedPlugins.Any(file => file.Contains("f4ee"));
            var isVR = settings.GameType == GameType.Fallout4VR;

            return new List<ConfigSetting>
            {
                // Patches section settings
                new ConfigSetting
                {
                    Section = "Patches",
                    Key = "Achievements",
                    Name = "Achievements",
                    Condition = hasAchievements,
                    DesiredValue = false,
                    Description = "The Achievements Mod and/or Unlimited Survival Mode is installed",
                    Reason = $"to prevent conflicts with {_crashGenName}"
                },
                new ConfigSetting
                {
                    Section = "Patches",
                    Key = "MemoryManager",
                    Name = "Memory Manager",
                    Condition = hasAnyXCell,
                    DesiredValue = false,
                    Description = "The X-Cell Mod is installed",
                    Reason = "to prevent conflicts with X-Cell",
                    SpecialCase = "bakascrapheap"
                },
                new ConfigSetting
                {
                    Section = "Patches",
                    Key = "HavokMemorySystem",
                    Name = "Havok Memory System",
                    Condition = hasAnyXCell,
                    DesiredValue = false,
                    Description = "The X-Cell Mod is installed",
                    Reason = "to prevent conflicts with X-Cell"
                },
                new ConfigSetting
                {
                    Section = "Patches",
                    Key = "BSTextureStreamerLocalHeap",
                    Name = "BS Texture Streamer Local Heap",
                    Condition = hasAnyXCell,
                    DesiredValue = false,
                    Description = "The X-Cell Mod is installed",
                    Reason = "to prevent conflicts with X-Cell"
                },
                new ConfigSetting
                {
                    Section = "Patches",
                    Key = "ScaleformAllocator",
                    Name = "Scaleform Allocator",
                    Condition = hasAnyXCell,
                    DesiredValue = false,
                    Description = "The X-Cell Mod is installed",
                    Reason = "to prevent conflicts with X-Cell"
                },
                new ConfigSetting
                {
                    Section = "Patches",
                    Key = "SmallBlockAllocator",
                    Name = "Small Block Allocator",
                    Condition = hasAnyXCell,
                    DesiredValue = false,
                    Description = "The X-Cell Mod is installed",
                    Reason = "to prevent conflicts with X-Cell"
                },
                new ConfigSetting
                {
                    Section = "Patches",
                    Key = "ArchiveLimit",
                    Name = "Archive Limit",
                    Condition = !isVR, // Archive Limit should be disabled for non-VR
                    DesiredValue = false,
                    Description = "Archive Limit is enabled",
                    Reason = "to prevent crashes"
                },
                new ConfigSetting
                {
                    Section = "Patches",
                    Key = "MaxStdIO",
                    Name = "MaxStdIO",
                    Condition = false, // This would need proper checking logic
                    DesiredValue = 2048,
                    Description = "MaxStdIO is set to a low value",
                    Reason = "to improve performance"
                },
                // Compatibility section settings
                new ConfigSetting
                {
                    Section = "Compatibility",
                    Key = "F4EE",
                    Name = "F4EE (Looks Menu)",
                    Condition = hasLooksMenu,
                    DesiredValue = true,
                    Description = "Looks Menu is installed, but F4EE parameter is set to FALSE",
                    Reason = "to prevent bugs and crashes from Looks Menu"
                }
            };
        }

        private object? GetTomlValue(TomlTable table, string section, string key)
        {
            if (table.TryGetValue(section, out var sectionObj) && sectionObj is TomlTable sectionTable)
            {
                if (sectionTable.TryGetValue(key, out var value))
                {
                    return value;
                }
            }
            return null;
        }

        private void SetTomlValue(TomlTable table, string section, string key, object value)
        {
            if (!table.TryGetValue(section, out var sectionObj) || !(sectionObj is TomlTable sectionTable))
            {
                sectionTable = new TomlTable();
                table[section] = sectionTable;
            }

            sectionTable[key] = value;
        }

        private bool ValuesEqual(object? current, object desired)
        {
            if (current == null)
                return false;
                
            return current.ToString() == desired.ToString();
        }

        private class ConfigSetting
        {
            public string Section { get; set; } = string.Empty;
            public string Key { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public bool Condition { get; set; }
            public object DesiredValue { get; set; } = new();
            public string Description { get; set; } = string.Empty;
            public string Reason { get; set; } = string.Empty;
            public string? SpecialCase { get; set; }
        }
    }
}
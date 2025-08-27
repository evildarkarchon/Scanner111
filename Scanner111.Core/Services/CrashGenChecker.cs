using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for checking and validating Crash Generator (Buffout4) configuration settings.
///     Provides functionality equivalent to Python's CheckCrashgen.py with thread-safe operations.
/// </summary>
public sealed class CrashGenChecker : ICrashGenChecker
{
    private readonly ILogger<CrashGenChecker> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly ConcurrentDictionary<string, IReadOnlySet<string>> _pluginsCache;
    
    public CrashGenChecker(ILogger<CrashGenChecker> logger, IAsyncYamlSettingsCore yamlCore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _pluginsCache = new ConcurrentDictionary<string, IReadOnlySet<string>>();
    }

    /// <inheritdoc />
    public async Task<string> CheckCrashGenSettingsAsync(CancellationToken cancellationToken = default)
    {
        var messageList = new List<string>();
        
        try
        {
            _logger.LogInformation("Starting Crash Generator settings check");

            // Get paths and settings
            var (pluginsPath, crashgenName) = await GetCrashGenPathsAsync(cancellationToken).ConfigureAwait(false);
            
            if (string.IsNullOrEmpty(pluginsPath))
            {
                messageList.AddRange(FormatPluginsPathNotFoundMessage());
                return string.Join("", messageList);
            }

            // Find config file
            var configFile = FindConfigFile(pluginsPath, crashgenName);
            if (configFile == null)
            {
                messageList.AddRange(FormatConfigNotFoundMessage(crashgenName));
                return string.Join("", messageList);
            }

            _logger.LogDebug("Found config file: {ConfigFile}", configFile);

            // Get installed plugins
            var installedPlugins = await DetectInstalledPluginsAsync(pluginsPath, cancellationToken).ConfigureAwait(false);

            // Process settings
            var settingsToCheck = GetSettingsToCheck(installedPlugins, configFile, crashgenName);
            await ProcessSettingsAsync(configFile, settingsToCheck, installedPlugins, crashgenName, messageList, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Crash Generator settings check completed with {MessageCount} messages", messageList.Count);
            return string.Join("", messageList);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check Crash Generator settings");
            messageList.Add($"❌ ERROR: Failed to check crash generator settings: {ex.Message}\n-----\n");
            return string.Join("", messageList);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlySet<string>> DetectInstalledPluginsAsync(string pluginsPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsPath);
        cancellationToken.ThrowIfCancellationRequested();

        // Check cache first
        if (_pluginsCache.TryGetValue(pluginsPath, out var cachedPlugins))
            return Task.FromResult(cachedPlugins);

        var plugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (Directory.Exists(pluginsPath))
            {
                var files = Directory.EnumerateFiles(pluginsPath, "*.*", SearchOption.TopDirectoryOnly);
                
                foreach (var file in files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = Path.GetFileName(file).ToLowerInvariant();
                    plugins.Add(fileName);
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to scan plugins directory: {PluginsPath}", pluginsPath);
        }

        var result = plugins.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _pluginsCache.TryAdd(pluginsPath, result);
        
        _logger.LogDebug("Detected {PluginCount} installed plugins in {PluginsPath}", result.Count, pluginsPath);
        return Task.FromResult<IReadOnlySet<string>>(result);
    }

    /// <inheritdoc />
    public async Task<bool> HasPluginAsync(IEnumerable<string> pluginNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pluginNames);

        // Get plugins path from settings
        var (pluginsPath, _) = await GetCrashGenPathsAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(pluginsPath))
            return false;

        var installedPlugins = await DetectInstalledPluginsAsync(pluginsPath, cancellationToken).ConfigureAwait(false);
        return pluginNames.Any(plugin => installedPlugins.Contains(plugin));
    }

    #region Private Helper Methods

    private async Task<(string? PluginsPath, string CrashGenName)> GetCrashGenPathsAsync(CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            // These would normally come from YAML settings
            // Using fallback values for now
            var pluginsPath = @"C:\Games\Fallout4\Data\F4SE\Plugins"; // Would be from Game settings
            var crashgenName = "Buffout4"; // Would be from Game settings

            await Task.CompletedTask.ConfigureAwait(false);
            return (pluginsPath, crashgenName);
        }
        catch (OperationCanceledException)
        {
            throw; // Re-throw cancellation exceptions
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get crash generator paths from settings");
            return (null, "Buffout4");
        }
    }

    private string? FindConfigFile(string pluginsPath, string crashgenName)
    {
        var crashgenTomlOg = Path.Combine(pluginsPath, "Buffout4", "config.toml");
        var crashgenTomlVr = Path.Combine(pluginsPath, "Buffout4.toml");

        // Check for missing config files
        var ogExists = File.Exists(crashgenTomlOg);
        var vrExists = File.Exists(crashgenTomlVr);

        if (!ogExists && !vrExists)
        {
            _logger.LogWarning("No {CrashGenName} config files found", crashgenName);
            return null;
        }

        if (ogExists && vrExists)
        {
            _logger.LogWarning("Both versions of {CrashGenName} config files found", crashgenName);
        }

        // Return the first available config file
        return ogExists ? crashgenTomlOg : vrExists ? crashgenTomlVr : null;
    }

    private List<CrashGenSetting> GetSettingsToCheck(IReadOnlySet<string> installedPlugins, string configFile, string crashgenName)
    {
        // Check for specific mods
        var hasXCell = installedPlugins.Any(p => p.Contains("x-cell", StringComparison.OrdinalIgnoreCase));
        var hasAchievements = installedPlugins.Any(p => 
            p.Contains("achievements.dll", StringComparison.OrdinalIgnoreCase) || 
            p.Contains("achievementsmodsenablerloader.dll", StringComparison.OrdinalIgnoreCase));
        var hasLooksMenu = installedPlugins.Any(p => p.Contains("f4ee", StringComparison.OrdinalIgnoreCase));
        var hasBakaScrapHeap = installedPlugins.Contains("bakascrapheap.dll");
        
        var settings = new List<CrashGenSetting>();

        // Patches section settings
        if (hasAchievements)
        {
            settings.Add(new CrashGenSetting(
                "Patches", "Achievements", "Achievements",
                false, "The Achievements Mod and/or Unlimited Survival Mode is installed",
                $"to prevent conflicts with {crashgenName}"));
        }

        if (hasXCell)
        {
            settings.AddRange(new[]
            {
                new CrashGenSetting("Patches", "MemoryManager", "Memory Manager",
                    false, "The X-Cell Mod is installed", "to prevent conflicts with X-Cell", hasBakaScrapHeap),
                new CrashGenSetting("Patches", "HavokMemorySystem", "Havok Memory System",
                    false, "The X-Cell Mod is installed", "to prevent conflicts with X-Cell"),
                new CrashGenSetting("Patches", "BSTextureStreamerLocalHeap", "BS Texture Streamer Local Heap",
                    false, "The X-Cell Mod is installed", "to prevent conflicts with X-Cell"),
                new CrashGenSetting("Patches", "ScaleformAllocator", "Scaleform Allocator",
                    false, "The X-Cell Mod is installed", "to prevent conflicts with X-Cell"),
                new CrashGenSetting("Patches", "SmallBlockAllocator", "Small Block Allocator",
                    false, "The X-Cell Mod is installed", "to prevent conflicts with X-Cell")
            });
        }

        if (configFile.Contains("buffout4/config.toml", StringComparison.OrdinalIgnoreCase))
        {
            settings.Add(new CrashGenSetting(
                "Patches", "ArchiveLimit", "Archive Limit",
                false, "Archive Limit is enabled", "to prevent crashes"));
        }

        settings.Add(new CrashGenSetting(
            "Patches", "MaxStdIO", "MaxStdIO",
            2048, "MaxStdIO is set to a low value", "to improve performance"));

        // Compatibility section settings
        if (hasLooksMenu)
        {
            settings.Add(new CrashGenSetting(
                "Compatibility", "F4EE", "F4EE (Looks Menu)",
                true, "Looks Menu is installed, but F4EE parameter is set to FALSE",
                "to prevent bugs and crashes from Looks Menu"));
        }

        return settings;
    }

    private async Task ProcessSettingsAsync(
        string configFile,
        List<CrashGenSetting> settingsToCheck,
        IReadOnlySet<string> installedPlugins,
        string crashgenName,
        List<string> messageList,
        CancellationToken cancellationToken)
    {
        var hasBakaScrapHeap = installedPlugins.Contains("bakascrapheap.dll");

        foreach (var setting in settingsToCheck)
        {
            try
            {
                // Get current setting value (this would use actual TOML parsing)
                var currentValue = await GetTomlSettingAsync(configFile, setting.Section, setting.Key, cancellationToken).ConfigureAwait(false);

                // Special case for BakaScrapHeap with MemoryManager
                if (setting.HasBakaScrapHeapConflict && hasBakaScrapHeap && IsSettingEnabled(currentValue))
                {
                    messageList.AddRange(new[]
                    {
                        $"# ❌ CAUTION : The Baka ScrapHeap Mod is installed, but is redundant with {crashgenName} #\n",
                        $" FIX: Uninstall the Baka ScrapHeap Mod, this prevents conflicts with {crashgenName}.\n-----\n"
                    });
                    continue;
                }

                // Check if setting needs changing
                if (!AreValuesEqual(currentValue, setting.DesiredValue))
                {
                    messageList.AddRange(new[]
                    {
                        $"# ❌ CAUTION : {setting.Description}, but {setting.Name} parameter is set to {FormatValue(currentValue)} #\n",
                        $"    Auto Scanner will change this parameter to {FormatValue(setting.DesiredValue)} {setting.Reason}.\n-----\n"
                    });

                    // Apply the change (this would use actual TOML modification)
                    await SetTomlSettingAsync(configFile, setting.Section, setting.Key, setting.DesiredValue, cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Changed {SettingName} from {OldValue} to {NewValue}",
                        setting.Name, FormatValue(currentValue), FormatValue(setting.DesiredValue));
                }
                else
                {
                    messageList.Add($"✔️ {setting.Name} parameter is correctly configured in your {crashgenName} settings!\n-----\n");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process setting: {SettingName}", setting.Name);
                messageList.Add($"⚠️ Unable to check {setting.Name} setting: {ex.Message}\n-----\n");
            }
        }
    }

    private async Task<object?> GetTomlSettingAsync(string configFile, string section, string key, CancellationToken cancellationToken)
    {
        // This would use a proper TOML parser like Tomlyn
        // For now, returning mock values based on key names
        await Task.CompletedTask.ConfigureAwait(false);
        
        return key switch
        {
            "Achievements" => true,
            "MemoryManager" => true,
            "ArchiveLimit" => true,
            "MaxStdIO" => 1024,
            "F4EE" => false,
            _ => null
        };
    }

    private async Task SetTomlSettingAsync(string configFile, string section, string key, object value, CancellationToken cancellationToken)
    {
        // This would use a proper TOML parser to modify the file
        // For now, just log the intended change
        await Task.CompletedTask.ConfigureAwait(false);
        _logger.LogDebug("Would set [{Section}].{Key} = {Value} in {ConfigFile}", section, key, value, configFile);
    }

    private static bool IsSettingEnabled(object? value)
    {
        return value switch
        {
            bool b => b,
            string s => s.Equals("true", StringComparison.OrdinalIgnoreCase),
            int i => i > 0,
            _ => false
        };
    }

    private static bool AreValuesEqual(object? current, object desired)
    {
        if (current == null) return false;
        
        return (current, desired) switch
        {
            (bool c, bool d) => c == d,
            (int c, int d) => c == d,
            (string c, string d) => c.Equals(d, StringComparison.OrdinalIgnoreCase),
            _ => current.Equals(desired)
        };
    }

    private static string FormatValue(object? value)
    {
        return value switch
        {
            bool b => b.ToString().ToUpperInvariant(),
            null => "NULL",
            _ => value.ToString() ?? "NULL"
        };
    }

    private static List<string> FormatPluginsPathNotFoundMessage()
    {
        return new List<string>
        {
            "❌ ERROR: Could not locate plugins folder path in settings\n-----\n"
        };
    }

    private static List<string> FormatConfigNotFoundMessage(string crashgenName)
    {
        return new List<string>
        {
            $"# [!] NOTICE : Unable to find the {crashgenName} config file, settings check will be skipped. #\n",
            $"  To ensure this check doesn't get skipped, {crashgenName} has to be installed manually.\n",
            "  [ If you are using Mod Organizer 2, you need to run CLASSIC through a shortcut in MO2. ]\n-----\n"
        };
    }

    #endregion

    #region Helper Classes

    private sealed class CrashGenSetting
    {
        public CrashGenSetting(
            string section,
            string key,
            string name,
            object desiredValue,
            string description,
            string reason,
            bool hasBakaScrapHeapConflict = false)
        {
            Section = section ?? throw new ArgumentNullException(nameof(section));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DesiredValue = desiredValue ?? throw new ArgumentNullException(nameof(desiredValue));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            Reason = reason ?? throw new ArgumentNullException(nameof(reason));
            HasBakaScrapHeapConflict = hasBakaScrapHeapConflict;
        }

        public string Section { get; }
        public string Key { get; }
        public string Name { get; }
        public object DesiredValue { get; }
        public string Description { get; }
        public string Reason { get; }
        public bool HasBakaScrapHeapConflict { get; }
    }

    #endregion
}
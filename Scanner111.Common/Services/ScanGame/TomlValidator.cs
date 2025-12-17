using System.Text;
using Scanner111.Common.Models.ScanGame;
using Tomlyn;
using Tomlyn.Model;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Validates TOML configuration files for crash generator mods like Buffout4.
/// </summary>
/// <remarks>
/// <para>
/// This service scans and validates crash generator TOML configuration files,
/// detecting configuration issues based on installed plugins. It operates in
/// read-only mode and never modifies configuration files.
/// </para>
/// <para>
/// The validator checks for common issues including:
/// <list type="bullet">
/// <item>Missing or duplicate TOML configuration files</item>
/// <item>Conflicts with X-Cell, Achievements, and Looks Menu plugins</item>
/// <item>Redundant mod installations (e.g., BakaScrapHeap with Buffout4)</item>
/// <item>Incorrect settings for the detected plugin configuration</item>
/// </list>
/// </para>
/// </remarks>
public sealed class TomlValidator : ITomlValidator
{
    /// <inheritdoc/>
    public async Task<TomlScanResult> ValidateAsync(
        string pluginsPath,
        string crashGenName,
        string gameName,
        CancellationToken cancellationToken = default)
    {
        return await ValidateWithProgressAsync(
            pluginsPath,
            crashGenName,
            gameName,
            progress: null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<TomlScanResult> ValidateWithProgressAsync(
        string pluginsPath,
        string crashGenName,
        string gameName,
        IProgress<TomlValidationProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        var messages = new List<string>();
        var issues = new List<ConfigIssue>();
        var installedPlugins = new List<string>();

        // Step 1: Detect installed plugins
        progress?.Report(new TomlValidationProgress("Detecting installed plugins", 0, 0));

        if (Directory.Exists(pluginsPath))
        {
            installedPlugins = await Task.Run(() =>
            {
                return Directory.GetFiles(pluginsPath)
                    .Select(f => Path.GetFileName(f).ToLowerInvariant())
                    .ToList();
            }, cancellationToken).ConfigureAwait(false);
        }

        // Step 2: Find configuration file
        progress?.Report(new TomlValidationProgress("Locating configuration file", 0, 0));

        var (configFile, hasDuplicates, configMessages) = FindConfigFile(pluginsPath, crashGenName);
        messages.AddRange(configMessages);

        if (configFile is null)
        {
            messages.Add($"# [!] NOTICE : Unable to find the {crashGenName} config file, settings check will be skipped. #\n");
            messages.Add($"  To ensure this check doesn't get skipped, {crashGenName} has to be installed manually.\n");
            messages.Add("  [ If you are using Mod Organizer 2, you need to run CLASSIC through a shortcut in MO2. ]\n-----\n");

            return new TomlScanResult
            {
                CrashGenName = crashGenName,
                ConfigFileFound = false,
                HasDuplicateConfigs = hasDuplicates,
                ConfigIssues = issues,
                InstalledPlugins = installedPlugins,
                FormattedReport = string.Join(string.Empty, messages)
            };
        }

        // Step 3: Load and parse TOML file
        progress?.Report(new TomlValidationProgress("Parsing TOML configuration", 0, 0));

        TomlTable? tomlData;
        try
        {
            var content = await File.ReadAllTextAsync(configFile, cancellationToken).ConfigureAwait(false);
            tomlData = Toml.ToModel(content);
        }
        catch (Exception ex)
        {
            messages.Add($"# ❌ ERROR: Failed to parse {crashGenName} TOML file: {ex.Message} #\n-----\n");
            return new TomlScanResult
            {
                CrashGenName = crashGenName,
                ConfigFilePath = configFile,
                ConfigFileFound = true,
                HasDuplicateConfigs = hasDuplicates,
                ConfigIssues = issues,
                InstalledPlugins = installedPlugins,
                FormattedReport = string.Join(string.Empty, messages)
            };
        }

        // Step 4: Only check settings for Fallout 4
        if (!string.Equals(gameName, "Fallout4", StringComparison.OrdinalIgnoreCase))
        {
            return new TomlScanResult
            {
                CrashGenName = crashGenName,
                ConfigFilePath = configFile,
                ConfigFileFound = true,
                HasDuplicateConfigs = hasDuplicates,
                ConfigIssues = issues,
                InstalledPlugins = installedPlugins,
                FormattedReport = string.Join(string.Empty, messages)
            };
        }

        // Step 5: Get settings to check based on installed plugins
        var settingsToCheck = GetSettingsToCheck(
            installedPlugins,
            configFile,
            crashGenName);

        // Step 6: Detect configuration issues
        var settingsChecked = 0;
        var hasBakaScrapHeap = installedPlugins.Contains("bakascrapheap.dll");

        foreach (var setting in settingsToCheck)
        {
            cancellationToken.ThrowIfCancellationRequested();
            settingsChecked++;

            progress?.Report(new TomlValidationProgress(
                $"Checking {setting.DisplayName}",
                settingsChecked,
                issues.Count));

            var currentValue = GetTomlValue(tomlData, setting.Section, setting.Key);

            // Special case for BakaScrapHeap with MemoryManager
            if (setting.SpecialCase == "bakascrapheap" && hasBakaScrapHeap && currentValue is not null)
            {
                var issue = new ConfigIssue(
                    FilePath: configFile,
                    FileName: Path.GetFileName(configFile),
                    Section: setting.Section,
                    Setting: setting.Key,
                    CurrentValue: currentValue?.ToString() ?? "null",
                    RecommendedValue: setting.DesiredValue.ToString() ?? "false",
                    Description: $"The Baka ScrapHeap Mod is installed, but is redundant with {crashGenName}. " +
                                $"Uninstall the Baka ScrapHeap Mod to prevent conflicts with {crashGenName}.",
                    Severity: ConfigIssueSeverity.Error);
                issues.Add(issue);
                messages.Add($"# ❌ CAUTION : The Baka ScrapHeap Mod is installed, but is redundant with {crashGenName} #\n");
                messages.Add($" FIX: Uninstall the Baka ScrapHeap Mod, this prevents conflicts with {crashGenName}.\n-----\n");
                continue;
            }

            // Check if condition is met and setting needs attention
            if (!setting.ShouldCheck)
            {
                continue;
            }

            // Only check settings that exist in the TOML file
            // Missing settings are not flagged as issues
            if (currentValue is null)
            {
                continue;
            }

            if (!ValuesEqual(currentValue, setting.DesiredValue))
            {
                var issue = new ConfigIssue(
                    FilePath: configFile,
                    FileName: Path.GetFileName(configFile),
                    Section: setting.Section,
                    Setting: setting.Key,
                    CurrentValue: currentValue.ToString() ?? "",
                    RecommendedValue: setting.DesiredValue.ToString() ?? "",
                    Description: $"{setting.Description}. {setting.Reason}",
                    Severity: ConfigIssueSeverity.Warning);
                issues.Add(issue);
                messages.Add($"**❌ CAUTION : {setting.Description}, but {setting.DisplayName} parameter is set to {currentValue}**\n");
                messages.Add($" FIX: Open {crashGenName}'s TOML file and change {setting.DisplayName} to {setting.DesiredValue} {setting.Reason}.\n-----\n");
            }
            else
            {
                messages.Add($"✔️ {setting.DisplayName} parameter is correctly configured in your {crashGenName} settings!\n-----\n");
            }
        }

        progress?.Report(new TomlValidationProgress(
            "Validation complete",
            settingsChecked,
            issues.Count));

        return new TomlScanResult
        {
            CrashGenName = crashGenName,
            ConfigFilePath = configFile,
            ConfigFileFound = true,
            HasDuplicateConfigs = hasDuplicates,
            ConfigIssues = issues,
            InstalledPlugins = installedPlugins,
            FormattedReport = string.Join(string.Empty, messages)
        };
    }

    /// <inheritdoc/>
    public async Task<T?> GetValueAsync<T>(
        string filePath,
        string section,
        string key,
        CancellationToken cancellationToken = default) where T : struct
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var tomlData = Toml.ToModel(content);
            var value = GetTomlValue(tomlData, section, key);

            if (value is null)
            {
                return null;
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string?> GetStringValueAsync(
        string filePath,
        string section,
        string key,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var tomlData = Toml.ToModel(content);
            var value = GetTomlValue(tomlData, section, key);
            return value?.ToString();
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsValidTomlFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            _ = Toml.ToModel(content);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Finds the TOML configuration file for the crash generator.
    /// </summary>
    private static (string? ConfigFile, bool HasDuplicates, List<string> Messages) FindConfigFile(
        string pluginsPath,
        string crashGenName)
    {
        var messages = new List<string>();

        var crashgenTomlOg = Path.Combine(pluginsPath, "Buffout4", "config.toml");
        var crashgenTomlVr = Path.Combine(pluginsPath, "Buffout4.toml");

        var ogExists = File.Exists(crashgenTomlOg);
        var vrExists = File.Exists(crashgenTomlVr);

        // Check for missing config files
        if (!ogExists && !vrExists)
        {
            messages.Add($"# ❌ CAUTION : {crashGenName.ToUpperInvariant()} TOML SETTINGS FILE NOT FOUND! #\n");
            messages.Add($"Please recheck your {crashGenName} installation and delete any obsolete files.\n-----\n");
            return (null, false, messages);
        }

        // Check for duplicate config files
        var hasDuplicates = ogExists && vrExists;
        if (hasDuplicates)
        {
            messages.Add($"# ❌ CAUTION : BOTH VERSIONS OF {crashGenName.ToUpperInvariant()} TOML SETTINGS FILES WERE FOUND! #\n");
            messages.Add($"When editing {crashGenName} toml settings, make sure you are editing the correct file.\n");
            messages.Add($"Please recheck your {crashGenName} installation and delete any obsolete files.\n-----\n");
        }

        // Return the appropriate config file (prefer OG over VR)
        var configFile = ogExists ? crashgenTomlOg : crashgenTomlVr;
        return (configFile, hasDuplicates, messages);
    }

    /// <summary>
    /// Gets a value from the parsed TOML data.
    /// </summary>
    private static object? GetTomlValue(TomlTable tomlData, string section, string key)
    {
        if (!tomlData.TryGetValue(section, out var sectionObj))
        {
            return null;
        }

        if (sectionObj is not TomlTable sectionTable)
        {
            return null;
        }

        return sectionTable.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>
    /// Compares two values for equality, handling type conversions.
    /// </summary>
    private static bool ValuesEqual(object? current, object desired)
    {
        if (current is null)
        {
            return false;
        }

        // Handle boolean comparisons
        if (desired is bool desiredBool)
        {
            if (current is bool currentBool)
            {
                return currentBool == desiredBool;
            }
            return false;
        }

        // Handle integer comparisons
        if (desired is int desiredInt)
        {
            if (current is long currentLong)
            {
                return currentLong == desiredInt;
            }
            if (current is int currentInt)
            {
                return currentInt == desiredInt;
            }
            return false;
        }

        // Handle string comparisons
        return string.Equals(current.ToString(), desired.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the list of settings to check based on installed plugins.
    /// </summary>
    private static List<TomlSettingToCheck> GetSettingsToCheck(
        List<string> installedPlugins,
        string configFile,
        string crashGenName)
    {
        var hasXcell = installedPlugins.Any(p =>
            p is "x-cell-fo4.dll" or "x-cell-og.dll" or "x-cell-ng2.dll");
        var hasAchievements = installedPlugins.Any(p =>
            p is "achievements.dll" or "achievementsmodsenablerloader.dll");
        var hasLooksMenu = installedPlugins.Any(p => p.Contains("f4ee"));
        var isOgConfig = configFile.Contains("buffout4", StringComparison.OrdinalIgnoreCase) &&
                         configFile.Contains("config.toml", StringComparison.OrdinalIgnoreCase);

        return
        [
            // Patches section settings
            new TomlSettingToCheck(
                Section: "Patches",
                Key: "Achievements",
                DisplayName: "Achievements",
                ShouldCheck: hasAchievements,
                DesiredValue: false,
                Description: "The Achievements Mod and/or Unlimited Survival Mode is installed",
                Reason: $"to prevent conflicts with {crashGenName}",
                SpecialCase: null),

            new TomlSettingToCheck(
                Section: "Patches",
                Key: "MemoryManager",
                DisplayName: "Memory Manager",
                ShouldCheck: hasXcell,
                DesiredValue: false,
                Description: "The X-Cell Mod is installed",
                Reason: "to prevent conflicts with X-Cell",
                SpecialCase: "bakascrapheap"),

            new TomlSettingToCheck(
                Section: "Patches",
                Key: "HavokMemorySystem",
                DisplayName: "Havok Memory System",
                ShouldCheck: hasXcell,
                DesiredValue: false,
                Description: "The X-Cell Mod is installed",
                Reason: "to prevent conflicts with X-Cell",
                SpecialCase: null),

            new TomlSettingToCheck(
                Section: "Patches",
                Key: "BSTextureStreamerLocalHeap",
                DisplayName: "BS Texture Streamer Local Heap",
                ShouldCheck: hasXcell,
                DesiredValue: false,
                Description: "The X-Cell Mod is installed",
                Reason: "to prevent conflicts with X-Cell",
                SpecialCase: null),

            new TomlSettingToCheck(
                Section: "Patches",
                Key: "ScaleformAllocator",
                DisplayName: "Scaleform Allocator",
                ShouldCheck: hasXcell,
                DesiredValue: false,
                Description: "The X-Cell Mod is installed",
                Reason: "to prevent conflicts with X-Cell",
                SpecialCase: null),

            new TomlSettingToCheck(
                Section: "Patches",
                Key: "SmallBlockAllocator",
                DisplayName: "Small Block Allocator",
                ShouldCheck: hasXcell,
                DesiredValue: false,
                Description: "The X-Cell Mod is installed",
                Reason: "to prevent conflicts with X-Cell",
                SpecialCase: null),

            new TomlSettingToCheck(
                Section: "Patches",
                Key: "ArchiveLimit",
                DisplayName: "Archive Limit",
                ShouldCheck: isOgConfig,
                DesiredValue: false,
                Description: "Archive Limit is enabled",
                Reason: "to prevent crashes",
                SpecialCase: null),

            new TomlSettingToCheck(
                Section: "Patches",
                Key: "MaxStdIO",
                DisplayName: "MaxStdIO",
                ShouldCheck: false, // Always check but don't flag as issue
                DesiredValue: 2048,
                Description: "MaxStdIO is set to a low value",
                Reason: "to improve performance",
                SpecialCase: null),

            // Compatibility section settings
            new TomlSettingToCheck(
                Section: "Compatibility",
                Key: "F4EE",
                DisplayName: "F4EE (Looks Menu)",
                ShouldCheck: hasLooksMenu,
                DesiredValue: true,
                Description: "Looks Menu is installed, but F4EE parameter is set to FALSE",
                Reason: "to prevent bugs and crashes from Looks Menu",
                SpecialCase: null)
        ];
    }

    /// <summary>
    /// Internal record for settings to check with condition.
    /// </summary>
    private sealed record TomlSettingToCheck(
        string Section,
        string Key,
        string DisplayName,
        bool ShouldCheck,
        object DesiredValue,
        string Description,
        string Reason,
        string? SpecialCase);
}

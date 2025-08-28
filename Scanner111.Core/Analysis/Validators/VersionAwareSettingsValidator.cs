using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using System.Text;
using System.Text.RegularExpressions;

namespace Scanner111.Core.Analysis.Validators;

/// <summary>
///     Validates settings based on version compatibility.
///     Handles version-specific settings, deprecations, and upgrade recommendations.
///     Thread-safe for concurrent validation operations.
/// </summary>
public sealed class VersionAwareSettingsValidator
{
    private readonly ILogger<VersionAwareSettingsValidator> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    
    // Version thresholds for various settings
    private static readonly Version ArchiveLimitDeprecatedVersion = new(1, 29, 0);
    private static readonly Version MemoryManagerImprovedVersion = new(1, 28, 0);
    private static readonly Version F4EEAddedVersion = new(1, 20, 0);
    private static readonly Version NGVersionStart = new(1, 30, 0);
    
    /// <summary>
    ///     Settings that were deprecated in specific versions.
    /// </summary>
    private static readonly Dictionary<string, VersionChange> DeprecatedSettings = new()
    {
        ["ArchiveLimit"] = new VersionChange
        {
            ChangeVersion = ArchiveLimitDeprecatedVersion,
            ChangeType = VersionChangeType.Deprecated,
            Message = "ArchiveLimit is deprecated in version 1.29.0+",
            Recommendation = "Remove ArchiveLimit from your configuration"
        },
        
        ["InputSwitch"] = new VersionChange
        {
            ChangeVersion = new Version(1, 25, 0),
            ChangeType = VersionChangeType.Deprecated,
            Message = "InputSwitch is no longer needed in modern versions",
            Recommendation = "Remove InputSwitch from your configuration"
        }
    };
    
    /// <summary>
    ///     Settings that were added in specific versions.
    /// </summary>
    private static readonly Dictionary<string, VersionChange> AddedSettings = new()
    {
        ["F4EE"] = new VersionChange
        {
            ChangeVersion = F4EEAddedVersion,
            ChangeType = VersionChangeType.Added,
            Message = "F4EE compatibility was added in version 1.20.0",
            Recommendation = "Update Buffout4 to use F4EE compatibility"
        },
        
        ["MemoryManagerDebug"] = new VersionChange
        {
            ChangeVersion = new Version(1, 26, 0),
            ChangeType = VersionChangeType.Added,
            Message = "MemoryManagerDebug was added in version 1.26.0",
            Recommendation = "Update Buffout4 to use debug features"
        }
    };
    
    /// <summary>
    ///     Settings with different behavior in different versions.
    /// </summary>
    private static readonly Dictionary<string, List<VersionBehavior>> VersionBehaviors = new()
    {
        ["MemoryManager"] = new List<VersionBehavior>
        {
            new()
            {
                MinVersion = null,
                MaxVersion = new Version(1, 27, 0),
                Behavior = "Basic memory management",
                RecommendedValue = true
            },
            new()
            {
                MinVersion = MemoryManagerImprovedVersion,
                MaxVersion = null,
                Behavior = "Improved memory management with better allocation strategies",
                RecommendedValue = true
            }
        },
        
        ["MaxStdIO"] = new List<VersionBehavior>
        {
            new()
            {
                MinVersion = null,
                MaxVersion = new Version(1, 24, 0),
                Behavior = "Limited to 2048",
                RecommendedValue = 2048
            },
            new()
            {
                MinVersion = new Version(1, 24, 0),
                MaxVersion = null,
                Behavior = "Supports higher values",
                RecommendedValue = -1 // Use default
            }
        }
    };

    public VersionAwareSettingsValidator(
        ILogger<VersionAwareSettingsValidator> logger,
        IAsyncYamlSettingsCore yamlCore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
    }

    /// <summary>
    ///     Validates settings based on version compatibility.
    /// </summary>
    /// <param name="settings">Crash generator settings with version information</param>
    /// <param name="gameVersion">Optional game version for additional checks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation report fragment</returns>
    public async Task<ReportFragment> ValidateVersionCompatibilityAsync(
        CrashGenSettings settings,
        string? gameVersion = null,
        CancellationToken cancellationToken = default)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var validationResults = new List<ValidationResult>();
        
        // Parse version information
        var buffoutVersion = ParseVersion(settings.Version);
        var isNGVersion = buffoutVersion != null && buffoutVersion >= NGVersionStart;
        
        _logger.LogDebug("Validating settings for {CrashGen} version {Version}",
            settings.CrashGenName, buffoutVersion?.ToString() ?? "Unknown");
        
        // Check deprecated settings
        validationResults.AddRange(CheckDeprecatedSettings(settings, buffoutVersion));
        
        // Check for settings not available in this version
        validationResults.AddRange(CheckUnavailableSettings(settings, buffoutVersion));
        
        // Check version-specific behaviors
        validationResults.AddRange(CheckVersionBehaviors(settings, buffoutVersion));
        
        // Check for upgrade recommendations
        validationResults.AddRange(SuggestUpgrades(settings, buffoutVersion));
        
        // Check game version compatibility if provided
        if (!string.IsNullOrEmpty(gameVersion))
        {
            validationResults.AddRange(await CheckGameVersionCompatibilityAsync(
                settings, gameVersion, isNGVersion, cancellationToken).ConfigureAwait(false));
        }
        
        // Check NG-specific settings
        if (isNGVersion)
        {
            validationResults.AddRange(CheckNGSpecificSettings(settings));
        }
        
        return BuildVersionReport(validationResults, settings.CrashGenName, buffoutVersion);
    }

    /// <summary>
    ///     Checks for deprecated settings being used.
    /// </summary>
    private List<ValidationResult> CheckDeprecatedSettings(
        CrashGenSettings settings,
        Version? currentVersion)
    {
        var results = new List<ValidationResult>();
        
        if (currentVersion == null)
            return results;
        
        foreach (var kvp in DeprecatedSettings)
        {
            var settingName = kvp.Key;
            var change = kvp.Value;
            
            // Check if this setting is deprecated for current version
            if (currentVersion >= change.ChangeVersion &&
                settings.RawSettings.ContainsKey(settingName))
            {
                results.Add(new ValidationResult
                {
                    Setting = settingName,
                    Level = ValidationLevel.Warning,
                    Message = change.Message,
                    Recommendation = change.Recommendation
                });
                
                _logger.LogWarning("Deprecated setting found: {Setting}", settingName);
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Checks for settings not available in the current version.
    /// </summary>
    private List<ValidationResult> CheckUnavailableSettings(
        CrashGenSettings settings,
        Version? currentVersion)
    {
        var results = new List<ValidationResult>();
        
        if (currentVersion == null)
            return results;
        
        foreach (var kvp in AddedSettings)
        {
            var settingName = kvp.Key;
            var change = kvp.Value;
            
            // Check if user is trying to use a setting not available in their version
            if (currentVersion < change.ChangeVersion &&
                settings.RawSettings.ContainsKey(settingName))
            {
                results.Add(new ValidationResult
                {
                    Setting = settingName,
                    Level = ValidationLevel.Error,
                    Message = $"{settingName} is not available in version {currentVersion}",
                    Recommendation = change.Recommendation
                });
                
                _logger.LogError("Setting not available in version: {Setting}", settingName);
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Checks version-specific behavior changes.
    /// </summary>
    private List<ValidationResult> CheckVersionBehaviors(
        CrashGenSettings settings,
        Version? currentVersion)
    {
        var results = new List<ValidationResult>();
        
        if (currentVersion == null)
            return results;
        
        foreach (var kvp in VersionBehaviors)
        {
            var settingName = kvp.Key;
            var behaviors = kvp.Value;
            
            if (!settings.RawSettings.TryGetValue(settingName, out var currentValue))
                continue;
            
            // Find the behavior for the current version
            var applicableBehavior = behaviors.FirstOrDefault(b =>
                (b.MinVersion == null || currentVersion >= b.MinVersion) &&
                (b.MaxVersion == null || currentVersion <= b.MaxVersion));
            
            if (applicableBehavior != null)
            {
                // Check if the value matches the recommendation
                if (!ValuesMatch(currentValue, applicableBehavior.RecommendedValue))
                {
                    results.Add(new ValidationResult
                    {
                        Setting = settingName,
                        Level = ValidationLevel.Info,
                        Message = $"Version {currentVersion}: {applicableBehavior.Behavior}",
                        Recommendation = $"Consider setting {settingName} = {applicableBehavior.RecommendedValue}"
                    });
                }
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Suggests upgrades based on version.
    /// </summary>
    private List<ValidationResult> SuggestUpgrades(
        CrashGenSettings settings,
        Version? currentVersion)
    {
        var results = new List<ValidationResult>();
        
        if (currentVersion == null)
        {
            results.Add(new ValidationResult
            {
                Setting = "Version",
                Level = ValidationLevel.Warning,
                Message = "Unable to determine Buffout4 version",
                Recommendation = "Ensure you're using the latest version from Nexus"
            });
            return results;
        }
        
        // Check if using an old version
        if (currentVersion < MemoryManagerImprovedVersion)
        {
            results.Add(new ValidationResult
            {
                Setting = "Version",
                Level = ValidationLevel.Warning,
                Message = $"Using outdated Buffout4 version {currentVersion}",
                Recommendation = "Update to version 1.28.0 or later for improved memory management"
            });
        }
        
        // Suggest NG version for Next-Gen update users
        if (currentVersion < NGVersionStart)
        {
            results.Add(new ValidationResult
            {
                Setting = "Version",
                Level = ValidationLevel.Info,
                Message = "Consider upgrading to Buffout4 NG for Next-Gen update support",
                Recommendation = "Visit https://www.nexusmods.com/fallout4/mods/64880 for Buffout4 NG"
            });
        }
        
        return results;
    }

    /// <summary>
    ///     Checks compatibility with game version.
    /// </summary>
    private async Task<List<ValidationResult>> CheckGameVersionCompatibilityAsync(
        CrashGenSettings settings,
        string gameVersion,
        bool isNGVersion,
        CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();
        
        // Load game version compatibility data
        var gameCompatibility = await _yamlCore.GetSettingAsync<Dictionary<string, object>>(
            YamlStore.Game, "GameVersionCompatibility", null, cancellationToken)
            .ConfigureAwait(false);
        
        // Check if using Next-Gen game with old Buffout
        if (IsNextGenGameVersion(gameVersion) && !isNGVersion)
        {
            results.Add(new ValidationResult
            {
                Setting = "Version",
                Level = ValidationLevel.Error,
                Message = "Using old Buffout4 with Next-Gen game update",
                Recommendation = "You must use Buffout4 NG with the Next-Gen update"
            });
        }
        
        // Check if using NG Buffout with old game
        if (!IsNextGenGameVersion(gameVersion) && isNGVersion)
        {
            results.Add(new ValidationResult
            {
                Setting = "Version",
                Level = ValidationLevel.Warning,
                Message = "Using Buffout4 NG with pre-Next-Gen game",
                Recommendation = "Use the original Buffout4 for better compatibility"
            });
        }
        
        return results;
    }

    /// <summary>
    ///     Checks NG-specific settings.
    /// </summary>
    private List<ValidationResult> CheckNGSpecificSettings(CrashGenSettings settings)
    {
        var results = new List<ValidationResult>();
        
        // NG version has different memory management
        if (settings.RawSettings.GetValueOrDefault("MemoryManager") is true)
        {
            results.Add(new ValidationResult
            {
                Setting = "MemoryManager",
                Level = ValidationLevel.Info,
                Message = "Buffout4 NG has enhanced memory management",
                Recommendation = "NG version includes additional memory optimizations"
            });
        }
        
        // Check for settings that behave differently in NG
        if (settings.RawSettings.ContainsKey("BSTextureStreamerLocalHeap"))
        {
            results.Add(new ValidationResult
            {
                Setting = "BSTextureStreamerLocalHeap",
                Level = ValidationLevel.Info,
                Message = "Texture streaming behaves differently in NG version",
                Recommendation = "Test texture streaming settings with your setup"
            });
        }
        
        return results;
    }

    /// <summary>
    ///     Builds the version validation report.
    /// </summary>
    private ReportFragment BuildVersionReport(
        List<ValidationResult> results,
        string crashGenName,
        Version? version)
    {
        if (results.Count == 0)
        {
            return ReportFragment.CreateInfo(
                "Version Compatibility",
                $"{crashGenName} version {version?.ToString() ?? "Unknown"} - All version checks passed!",
                120);
        }
        
        var builder = new StringBuilder();
        builder.AppendLine($"**Version Compatibility Report**");
        builder.AppendLine($"Current Version: {version?.ToString() ?? "Unknown"}");
        builder.AppendLine();
        
        var errors = results.Where(r => r.Level == ValidationLevel.Error).ToList();
        var warnings = results.Where(r => r.Level == ValidationLevel.Warning).ToList();
        var info = results.Where(r => r.Level == ValidationLevel.Info).ToList();
        
        if (errors.Count > 0)
        {
            builder.AppendLine("❌ **Version Errors:**");
            foreach (var error in errors)
            {
                builder.AppendLine($"  - **{error.Setting}**: {error.Message}");
                builder.AppendLine($"    → {error.Recommendation}");
            }
            builder.AppendLine();
        }
        
        if (warnings.Count > 0)
        {
            builder.AppendLine("⚠️ **Version Warnings:**");
            foreach (var warning in warnings)
            {
                builder.AppendLine($"  - **{warning.Setting}**: {warning.Message}");
                builder.AppendLine($"    → {warning.Recommendation}");
            }
            builder.AppendLine();
        }
        
        if (info.Count > 0)
        {
            builder.AppendLine("ℹ️ **Version Information:**");
            foreach (var item in info)
            {
                builder.AppendLine($"  - {item.Setting}: {item.Message}");
                if (!string.IsNullOrEmpty(item.Recommendation))
                    builder.AppendLine($"    → {item.Recommendation}");
            }
        }
        
        var fragmentType = errors.Count > 0 ? FragmentType.Error :
                          warnings.Count > 0 ? FragmentType.Warning :
                          FragmentType.Info;
        
        return ReportFragmentBuilder
            .Create()
            .WithType(fragmentType)
            .WithTitle("Version Compatibility")
            .Append(builder.ToString())
            .WithOrder(50)
            .Build();
    }

    private static Version? ParseVersion(Version? version)
    {
        return version; // Already a Version object
    }

    private static bool IsNextGenGameVersion(string gameVersion)
    {
        // Next-Gen update versions
        return gameVersion.Contains("1.10.980", StringComparison.OrdinalIgnoreCase) ||
               gameVersion.Contains("1.10.984", StringComparison.OrdinalIgnoreCase) ||
               gameVersion.Contains("next", StringComparison.OrdinalIgnoreCase) ||
               gameVersion.Contains("ng", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValuesMatch(object current, object recommended)
    {
        return (current, recommended) switch
        {
            (bool c, bool r) => c == r,
            (int c, int r) => c == r,
            (string c, string r) => string.Equals(c, r, StringComparison.OrdinalIgnoreCase),
            (string c, bool r) => bool.TryParse(c, out var parsed) && parsed == r,
            (string c, int r) => int.TryParse(c, out var parsed) && parsed == r,
            _ => current?.Equals(recommended) ?? recommended == null
        };
    }
}

/// <summary>
///     Represents a version-related change for a setting.
/// </summary>
public sealed class VersionChange
{
    public required Version ChangeVersion { get; init; }
    public required VersionChangeType ChangeType { get; init; }
    public required string Message { get; init; }
    public required string Recommendation { get; init; }
}

/// <summary>
///     Type of version change.
/// </summary>
public enum VersionChangeType
{
    Added,
    Deprecated,
    Modified,
    Removed
}

/// <summary>
///     Describes how a setting behaves in different versions.
/// </summary>
public sealed class VersionBehavior
{
    public Version? MinVersion { get; init; }
    public Version? MaxVersion { get; init; }
    public required string Behavior { get; init; }
    public required object RecommendedValue { get; init; }
}
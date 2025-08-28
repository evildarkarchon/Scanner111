using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using System.Text;

namespace Scanner111.Core.Analysis.Validators;

/// <summary>
///     Validates settings compatibility with installed mods.
///     Provides mod-specific recommendations and conflict detection.
///     Thread-safe for concurrent validation operations.
/// </summary>
public sealed class ModSettingsCompatibilityValidator
{
    private readonly ILogger<ModSettingsCompatibilityValidator> _logger;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    
    /// <summary>
    ///     Mod compatibility rules loaded from configuration.
    /// </summary>
    private readonly List<ModCompatibilityRule> _compatibilityRules = new();
    
    /// <summary>
    ///     Known mod conflicts that require specific settings.
    /// </summary>
    private static readonly Dictionary<string, ModSettingRequirement> KnownModRequirements = new()
    {
        ["BakaScrapHeap"] = new ModSettingRequirement
        {
            ModName = "Baka ScrapHeap",
            RequiredSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = false,
                ["SmallBlockAllocator"] = false
            },
            ConflictMessage = "Baka ScrapHeap provides its own memory management",
            Severity = ValidationLevel.Error
        },
        
        ["LooksMenu"] = new ModSettingRequirement
        {
            ModName = "Looks Menu",
            RequiredSettings = new Dictionary<string, object>
            {
                ["F4EE"] = true
            },
            ConflictMessage = "Looks Menu requires F4EE compatibility",
            Severity = ValidationLevel.Error
        },
        
        ["HighFPSPhysicsFix"] = new ModSettingRequirement
        {
            ModName = "High FPS Physics Fix",
            RequiredSettings = new Dictionary<string, object>
            {
                ["ActorIsHostileToActor"] = false
            },
            ConflictMessage = "High FPS Physics Fix handles this fix internally",
            Severity = ValidationLevel.Warning
        },
        
        ["WorkshopFramework"] = new ModSettingRequirement
        {
            ModName = "Workshop Framework",
            RequiredSettings = new Dictionary<string, object>
            {
                ["WorkshopMenu"] = true,
                ["PackageAllocateLocation"] = true
            },
            ConflictMessage = "Workshop Framework requires these fixes for stability",
            Severity = ValidationLevel.Warning
        },
        
        ["PRP"] = new ModSettingRequirement
        {
            ModName = "Previs Repair Pack (PRP)",
            RequiredSettings = new Dictionary<string, object>
            {
                ["BSPreCulledObjects"] = true
            },
            ConflictMessage = "PRP requires BSPreCulledObjects for proper functionality",
            Severity = ValidationLevel.Info
        },
        
        ["BuffOutNG"] = new ModSettingRequirement
        {
            ModName = "Buffout NG",
            RequiredSettings = new Dictionary<string, object>
            {
                ["MemoryManager"] = false
            },
            ConflictMessage = "Buffout NG has its own memory management implementation",
            Severity = ValidationLevel.Error
        }
    };
    
    /// <summary>
    ///     Mod combinations that require special settings.
    /// </summary>
    private static readonly List<ModCombinationRule> ModCombinationRules = new()
    {
        new ModCombinationRule
        {
            Mods = new[] { "LooksMenu", "AAF" },
            RequiredSettings = new Dictionary<string, object>
            {
                ["F4EE"] = true,
                ["ActorIsHostileToActor"] = true
            },
            Message = "LooksMenu + AAF combination requires specific settings",
            Severity = ValidationLevel.Warning
        },
        
        new ModCombinationRule
        {
            Mods = new[] { "WorkshopFramework", "SimSettlements2" },
            RequiredSettings = new Dictionary<string, object>
            {
                ["WorkshopMenu"] = true,
                ["PackageAllocateLocation"] = true,
                ["MemoryManager"] = true
            },
            Message = "Heavy workshop mods require optimized memory settings",
            Severity = ValidationLevel.Warning
        }
    };

    public ModSettingsCompatibilityValidator(
        ILogger<ModSettingsCompatibilityValidator> logger,
        IAsyncYamlSettingsCore yamlCore)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
    }

    /// <summary>
    ///     Validates settings compatibility with installed mods.
    /// </summary>
    /// <param name="settings">Crash generator settings</param>
    /// <param name="modSettings">Detected mod settings</param>
    /// <param name="loadOrder">Optional plugin load order for advanced checks</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation report fragment</returns>
    public async Task<ReportFragment> ValidateModCompatibilityAsync(
        CrashGenSettings settings,
        ModDetectionSettings modSettings,
        List<string>? loadOrder = null,
        CancellationToken cancellationToken = default)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));
        if (modSettings == null)
            throw new ArgumentNullException(nameof(modSettings));

        // Load custom compatibility rules from YAML
        await LoadCustomCompatibilityRulesAsync(cancellationToken).ConfigureAwait(false);
        
        var validationResults = new List<ValidationResult>();
        
        // Check each installed mod against requirements
        validationResults.AddRange(CheckModRequirements(settings, modSettings));
        
        // Check mod combinations
        validationResults.AddRange(CheckModCombinations(settings, modSettings));
        
        // Check load order impact if available
        if (loadOrder != null && loadOrder.Count > 0)
        {
            validationResults.AddRange(CheckLoadOrderImpact(settings, modSettings, loadOrder));
        }
        
        // Check for mod-specific optimizations
        validationResults.AddRange(SuggestModSpecificOptimizations(settings, modSettings));
        
        // Build and return the report
        return BuildCompatibilityReport(validationResults, settings.CrashGenName);
    }

    /// <summary>
    ///     Loads custom compatibility rules from YAML configuration.
    /// </summary>
    private async Task LoadCustomCompatibilityRulesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var customRules = await _yamlCore.GetSettingAsync<List<Dictionary<string, object>>>(
                YamlStore.Game, "ModCompatibilityRules", null, cancellationToken)
                .ConfigureAwait(false);
            
            if (customRules != null)
            {
                foreach (var rule in customRules)
                {
                    if (rule.TryGetValue("ModName", out var modName) &&
                        rule.TryGetValue("Settings", out var settings))
                    {
                        _compatibilityRules.Add(new ModCompatibilityRule
                        {
                            ModName = modName?.ToString() ?? string.Empty,
                            RequiredSettings = settings as Dictionary<string, object> ?? new()
                        });
                    }
                }
                
                _logger.LogDebug("Loaded {Count} custom compatibility rules", _compatibilityRules.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load custom compatibility rules");
        }
    }

    /// <summary>
    ///     Checks mod requirements against current settings.
    /// </summary>
    private List<ValidationResult> CheckModRequirements(
        CrashGenSettings settings,
        ModDetectionSettings modSettings)
    {
        var results = new List<ValidationResult>();
        
        foreach (var kvp in KnownModRequirements)
        {
            var modKey = kvp.Key;
            var requirement = kvp.Value;
            
            // Check if mod is installed
            if (!IsModInstalled(modKey, modSettings))
                continue;
            
            _logger.LogDebug("Checking requirements for mod: {Mod}", requirement.ModName);
            
            // Check each required setting
            foreach (var settingReq in requirement.RequiredSettings)
            {
                if (!settings.RawSettings.TryGetValue(settingReq.Key, out var currentValue))
                {
                    results.Add(new ValidationResult
                    {
                        Setting = settingReq.Key,
                        Level = requirement.Severity,
                        Message = $"{requirement.ModName} requires '{settingReq.Key}' to be configured",
                        Recommendation = $"Add {settingReq.Key} = {settingReq.Value} to your {settings.CrashGenName} TOML"
                    });
                    continue;
                }
                
                // Check if value matches requirement
                if (!ValuesMatch(currentValue, settingReq.Value))
                {
                    results.Add(new ValidationResult
                    {
                        Setting = settingReq.Key,
                        Level = requirement.Severity,
                        Message = $"{requirement.ModName}: {requirement.ConflictMessage}",
                        Recommendation = $"Set {settingReq.Key} = {settingReq.Value} for {requirement.ModName} compatibility"
                    });
                }
            }
        }
        
        // Check custom rules
        foreach (var rule in _compatibilityRules)
        {
            if (!IsModInstalled(rule.ModName, modSettings))
                continue;
                
            foreach (var settingReq in rule.RequiredSettings)
            {
                if (!settings.RawSettings.TryGetValue(settingReq.Key, out var currentValue) ||
                    !ValuesMatch(currentValue, settingReq.Value))
                {
                    results.Add(new ValidationResult
                    {
                        Setting = settingReq.Key,
                        Level = ValidationLevel.Warning,
                        Message = $"{rule.ModName} may require different settings",
                        Recommendation = $"Consider setting {settingReq.Key} = {settingReq.Value}"
                    });
                }
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Checks for mod combination conflicts.
    /// </summary>
    private List<ValidationResult> CheckModCombinations(
        CrashGenSettings settings,
        ModDetectionSettings modSettings)
    {
        var results = new List<ValidationResult>();
        
        foreach (var rule in ModCombinationRules)
        {
            // Check if all mods in the combination are installed
            var allModsInstalled = rule.Mods.All(mod => IsModInstalled(mod, modSettings));
            if (!allModsInstalled)
                continue;
            
            _logger.LogDebug("Checking mod combination: {Mods}", string.Join(" + ", rule.Mods));
            
            // Check required settings for this combination
            foreach (var settingReq in rule.RequiredSettings)
            {
                if (!settings.RawSettings.TryGetValue(settingReq.Key, out var currentValue) ||
                    !ValuesMatch(currentValue, settingReq.Value))
                {
                    results.Add(new ValidationResult
                    {
                        Setting = settingReq.Key,
                        Level = rule.Severity,
                        Message = rule.Message,
                        Recommendation = $"Set {settingReq.Key} = {settingReq.Value} for this mod combination"
                    });
                }
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Checks load order for potential conflicts.
    /// </summary>
    private List<ValidationResult> CheckLoadOrderImpact(
        CrashGenSettings settings,
        ModDetectionSettings modSettings,
        List<string> loadOrder)
    {
        var results = new List<ValidationResult>();
        
        // Check for mods that should load in specific order
        var workshopMods = loadOrder.Where(p => 
            p.Contains("Workshop", StringComparison.OrdinalIgnoreCase)).ToList();
        
        if (workshopMods.Count > 1)
        {
            // Multiple workshop mods detected
            if (settings.RawSettings.GetValueOrDefault("MemoryManager") is not true)
            {
                results.Add(new ValidationResult
                {
                    Setting = "MemoryManager",
                    Level = ValidationLevel.Warning,
                    Message = "Multiple workshop mods detected in load order",
                    Recommendation = "Enable MemoryManager for better stability with multiple workshop mods"
                });
            }
        }
        
        // Check for heavy texture mods
        var textureMods = loadOrder.Where(p => 
            p.Contains("Texture", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("HD", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("4K", StringComparison.OrdinalIgnoreCase)).ToList();
        
        if (textureMods.Count > 3)
        {
            if (settings.RawSettings.GetValueOrDefault("BSTextureStreamerLocalHeap") is not false)
            {
                results.Add(new ValidationResult
                {
                    Setting = "BSTextureStreamerLocalHeap",
                    Level = ValidationLevel.Info,
                    Message = $"Multiple texture mods detected ({textureMods.Count})",
                    Recommendation = "Ensure BSTextureStreamerLocalHeap is FALSE with heavy texture mods"
                });
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Suggests mod-specific optimizations.
    /// </summary>
    private List<ValidationResult> SuggestModSpecificOptimizations(
        CrashGenSettings settings,
        ModDetectionSettings modSettings)
    {
        var results = new List<ValidationResult>();
        
        // Check for script-heavy mods
        if (modSettings.CrashLogPlugins.Keys.Any(p => 
            p.Contains("Script", StringComparison.OrdinalIgnoreCase) ||
            p.Contains("Papyrus", StringComparison.OrdinalIgnoreCase)))
        {
            if (settings.RawSettings.GetValueOrDefault("MaxStdIO") is not int maxStdIO || maxStdIO < 2048)
            {
                results.Add(new ValidationResult
                {
                    Setting = "MaxStdIO",
                    Level = ValidationLevel.Info,
                    Message = "Script-heavy mods detected",
                    Recommendation = "Consider setting MaxStdIO to 2048 or higher for better script performance"
                });
            }
        }
        
        // Check for ENB
        if (modSettings.HasPlugin("ENB") || modSettings.XseModules.Any(m => 
            m.Contains("enb", StringComparison.OrdinalIgnoreCase)))
        {
            if (settings.RawSettings.GetValueOrDefault("ScaleformAllocator") is not true)
            {
                results.Add(new ValidationResult
                {
                    Setting = "ScaleformAllocator",
                    Level = ValidationLevel.Info,
                    Message = "ENB detected",
                    Recommendation = "Enable ScaleformAllocator for better ENB compatibility"
                });
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Builds the compatibility validation report.
    /// </summary>
    private ReportFragment BuildCompatibilityReport(
        List<ValidationResult> results,
        string crashGenName)
    {
        if (results.Count == 0)
        {
            return ReportFragment.CreateInfo(
                "Mod Compatibility Check",
                "All mod compatibility checks passed - no conflicts detected.",
                110);
        }
        
        var builder = new StringBuilder();
        builder.AppendLine("**Mod Compatibility Analysis**");
        builder.AppendLine();
        
        var groupedByLevel = results.GroupBy(r => r.Level)
            .OrderBy(g => g.Key)
            .ToList();
        
        foreach (var group in groupedByLevel)
        {
            var icon = group.Key switch
            {
                ValidationLevel.Error => "❌",
                ValidationLevel.Warning => "⚠️",
                _ => "ℹ️"
            };
            
            builder.AppendLine($"{icon} **{group.Key} Issues:**");
            foreach (var result in group)
            {
                builder.AppendLine($"  - **{result.Setting}**: {result.Message}");
                if (!string.IsNullOrEmpty(result.Recommendation))
                    builder.AppendLine($"    → {result.Recommendation}");
            }
            builder.AppendLine();
        }
        
        var fragmentType = results.Any(r => r.Level == ValidationLevel.Error) ? FragmentType.Error :
                          results.Any(r => r.Level == ValidationLevel.Warning) ? FragmentType.Warning :
                          FragmentType.Info;
        
        return ReportFragmentBuilder
            .Create()
            .WithType(fragmentType)
            .WithTitle("Mod Compatibility Check")
            .Append(builder.ToString())
            .WithOrder(45)
            .Build();
    }

    private bool IsModInstalled(string modKey, ModDetectionSettings modSettings)
    {
        // Check various mod lists
        return modSettings.HasPlugin(modKey) ||
               modSettings.HasXseModule(modKey) ||
               modSettings.CrashLogPlugins.Keys.Any(p => p.Contains(modKey, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ValuesMatch(object current, object required)
    {
        return (current, required) switch
        {
            (bool c, bool r) => c == r,
            (int c, int r) => c == r,
            (string c, string r) => string.Equals(c, r, StringComparison.OrdinalIgnoreCase),
            (string c, bool r) => bool.TryParse(c, out var parsed) && parsed == r,
            (string c, int r) => int.TryParse(c, out var parsed) && parsed == r,
            _ => current?.Equals(required) ?? required == null
        };
    }
}

/// <summary>
///     Defines mod-specific setting requirements.
/// </summary>
public sealed class ModSettingRequirement
{
    public required string ModName { get; init; }
    public required Dictionary<string, object> RequiredSettings { get; init; }
    public required string ConflictMessage { get; init; }
    public ValidationLevel Severity { get; init; } = ValidationLevel.Warning;
}

/// <summary>
///     Defines setting requirements for mod combinations.
/// </summary>
public sealed class ModCombinationRule
{
    public required string[] Mods { get; init; }
    public required Dictionary<string, object> RequiredSettings { get; init; }
    public required string Message { get; init; }
    public ValidationLevel Severity { get; init; } = ValidationLevel.Warning;
}

/// <summary>
///     Custom mod compatibility rule loaded from configuration.
/// </summary>
public sealed class ModCompatibilityRule
{
    public required string ModName { get; init; }
    public required Dictionary<string, object> RequiredSettings { get; init; }
}
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using System.Text;

namespace Scanner111.Core.Analysis.Validators;

/// <summary>
///     Comprehensive validator for Buffout4 TOML settings.
///     Handles parameter validation, dependency checks, and performance recommendations.
///     Thread-safe for concurrent validation operations.
/// </summary>
public sealed class Buffout4SettingsValidator
{
    private readonly ILogger<Buffout4SettingsValidator> _logger;
    
    // Critical settings that should always be validated
    private static readonly HashSet<string> CriticalSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "MemoryManager",
        "Achievements",
        "F4EE",
        "ArchiveLimit",
        "BSTextureStreamerLocalHeap",
        "SmallBlockAllocator",
        "ScaleformAllocator",
        "HavokMemorySystem"
    };
    
    // Settings that have performance impact
    private static readonly Dictionary<string, PerformanceImpact> PerformanceSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MemoryManagerDebug"] = new PerformanceImpact 
        { 
            Level = ImpactLevel.High, 
            Description = "Debug mode significantly impacts performance"
        },
        ["BSTextureStreamerLocalHeap"] = new PerformanceImpact 
        { 
            Level = ImpactLevel.Medium, 
            Description = "May cause texture streaming issues if misconfigured"
        },
        ["MaxStdIO"] = new PerformanceImpact 
        { 
            Level = ImpactLevel.Low, 
            Description = "Higher values may increase memory usage"
        },
        ["ActorIsHostileToActor"] = new PerformanceImpact
        {
            Level = ImpactLevel.Medium,
            Description = "Fix may impact AI calculations"
        }
    };
    
    // Settings dependencies (key requires values)
    private static readonly Dictionary<string, List<SettingDependency>> SettingDependencies = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MemoryManager"] = new List<SettingDependency>
        {
            new("SmallBlockAllocator", true, "Should be enabled with MemoryManager"),
            new("ScaleformAllocator", true, "Should be enabled with MemoryManager")
        },
        ["F4EE"] = new List<SettingDependency>
        {
            new("Compatibility", null, "F4EE requires Compatibility section")
        }
    };

    public Buffout4SettingsValidator(ILogger<Buffout4SettingsValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Performs comprehensive validation of Buffout4 settings.
    /// </summary>
    /// <param name="settings">The crash generator settings to validate</param>
    /// <param name="modSettings">Optional mod detection settings for interaction checks</param>
    /// <returns>Validation report fragment</returns>
    public ReportFragment ValidateComprehensive(
        CrashGenSettings settings,
        ModDetectionSettings? modSettings = null)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        var validationResults = new List<ValidationResult>();
        
        // Validate critical settings
        validationResults.AddRange(ValidateCriticalSettings(settings));
        
        // Check setting dependencies
        validationResults.AddRange(ValidateSettingDependencies(settings));
        
        // Analyze performance impact
        validationResults.AddRange(AnalyzePerformanceImpact(settings));
        
        // Check for incompatible combinations
        validationResults.AddRange(ValidateSettingCombinations(settings));
        
        // Validate value ranges
        validationResults.AddRange(ValidateValueRanges(settings));
        
        // Check mod-specific requirements if mod settings provided
        if (modSettings != null)
        {
            validationResults.AddRange(ValidateModSpecificSettings(settings, modSettings));
        }

        return BuildValidationReport(validationResults, settings.CrashGenName);
    }

    /// <summary>
    ///     Validates that all critical settings are present and properly configured.
    /// </summary>
    private List<ValidationResult> ValidateCriticalSettings(CrashGenSettings settings)
    {
        var results = new List<ValidationResult>();
        
        foreach (var criticalSetting in CriticalSettings)
        {
            if (!settings.RawSettings.ContainsKey(criticalSetting))
            {
                results.Add(new ValidationResult
                {
                    Setting = criticalSetting,
                    Level = ValidationLevel.Warning,
                    Message = $"Critical setting '{criticalSetting}' is missing from configuration",
                    Recommendation = $"Add {criticalSetting} to your {settings.CrashGenName} TOML file"
                });
                
                _logger.LogWarning("Critical setting missing: {Setting}", criticalSetting);
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Validates setting dependencies are met.
    /// </summary>
    private List<ValidationResult> ValidateSettingDependencies(CrashGenSettings settings)
    {
        var results = new List<ValidationResult>();
        
        foreach (var kvp in SettingDependencies)
        {
            var parentSetting = kvp.Key;
            var dependencies = kvp.Value;
            
            // Check if parent setting exists and is enabled
            if (!settings.RawSettings.TryGetValue(parentSetting, out var parentValue))
                continue;
                
            var parentEnabled = IsSettingEnabled(parentValue);
            if (!parentEnabled)
                continue;
            
            // Check each dependency
            foreach (var dependency in dependencies)
            {
                if (!settings.RawSettings.TryGetValue(dependency.SettingName, out var depValue))
                {
                    results.Add(new ValidationResult
                    {
                        Setting = parentSetting,
                        Level = ValidationLevel.Warning,
                        Message = $"{parentSetting} is enabled but dependency '{dependency.SettingName}' is missing",
                        Recommendation = dependency.Recommendation
                    });
                }
                else if (dependency.RequiredValue.HasValue)
                {
                    var depEnabled = IsSettingEnabled(depValue);
                    if (depEnabled != dependency.RequiredValue.Value)
                    {
                        results.Add(new ValidationResult
                        {
                            Setting = parentSetting,
                            Level = ValidationLevel.Warning,
                            Message = $"{parentSetting} requires {dependency.SettingName} to be {dependency.RequiredValue}",
                            Recommendation = dependency.Recommendation
                        });
                    }
                }
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Analyzes settings for performance impact.
    /// </summary>
    private List<ValidationResult> AnalyzePerformanceImpact(CrashGenSettings settings)
    {
        var results = new List<ValidationResult>();
        
        foreach (var kvp in settings.RawSettings)
        {
            if (PerformanceSettings.TryGetValue(kvp.Key, out var impact))
            {
                var isEnabled = IsSettingEnabled(kvp.Value);
                if (isEnabled && impact.Level >= ImpactLevel.Medium)
                {
                    results.Add(new ValidationResult
                    {
                        Setting = kvp.Key,
                        Level = impact.Level == ImpactLevel.High ? ValidationLevel.Warning : ValidationLevel.Info,
                        Message = $"{kvp.Key} may impact performance: {impact.Description}",
                        Recommendation = impact.Level == ImpactLevel.High 
                            ? $"Consider disabling {kvp.Key} unless needed for debugging"
                            : $"Monitor performance with {kvp.Key} enabled"
                    });
                    
                    _logger.LogDebug("Performance impact setting detected: {Setting} ({Level})", 
                        kvp.Key, impact.Level);
                }
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Validates incompatible setting combinations.
    /// </summary>
    private List<ValidationResult> ValidateSettingCombinations(CrashGenSettings settings)
    {
        var results = new List<ValidationResult>();
        
        // Check MemoryManager vs BSTextureStreamerLocalHeap
        var memManager = GetSettingBool(settings, "MemoryManager");
        var textureStreamer = GetSettingBool(settings, "BSTextureStreamerLocalHeap");
        
        if (memManager == true && textureStreamer == true)
        {
            results.Add(new ValidationResult
            {
                Setting = "MemoryManager + BSTextureStreamerLocalHeap",
                Level = ValidationLevel.Warning,
                Message = "Both MemoryManager and BSTextureStreamerLocalHeap are enabled",
                Recommendation = "BSTextureStreamerLocalHeap should typically be FALSE when MemoryManager is TRUE"
            });
        }
        
        // Check ArchiveLimit with MaxStdIO
        var archiveLimit = GetSettingBool(settings, "ArchiveLimit");
        var maxStdIO = GetSettingInt(settings, "MaxStdIO");
        
        if (archiveLimit == true && maxStdIO.HasValue && maxStdIO.Value < 2048)
        {
            results.Add(new ValidationResult
            {
                Setting = "ArchiveLimit + MaxStdIO",
                Level = ValidationLevel.Info,
                Message = "ArchiveLimit is enabled with low MaxStdIO value",
                Recommendation = "Consider increasing MaxStdIO to 2048 or higher when using ArchiveLimit"
            });
        }
        
        // Check debug settings in production
        var memDebug = GetSettingBool(settings, "MemoryManagerDebug");
        var waitDebugger = GetSettingBool(settings, "WaitForDebugger");
        
        if ((memDebug == true || waitDebugger == true) && !IsDebugEnvironment())
        {
            results.Add(new ValidationResult
            {
                Setting = "Debug Settings",
                Level = ValidationLevel.Error,
                Message = "Debug settings are enabled in production",
                Recommendation = "Disable MemoryManagerDebug and WaitForDebugger for normal gameplay"
            });
        }
        
        return results;
    }

    /// <summary>
    ///     Validates numeric setting value ranges.
    /// </summary>
    private List<ValidationResult> ValidateValueRanges(CrashGenSettings settings)
    {
        var results = new List<ValidationResult>();
        
        // Validate MaxStdIO
        var maxStdIO = GetSettingInt(settings, "MaxStdIO");
        if (maxStdIO.HasValue)
        {
            if (maxStdIO.Value > 0 && maxStdIO.Value < 1024)
            {
                results.Add(new ValidationResult
                {
                    Setting = "MaxStdIO",
                    Level = ValidationLevel.Info,
                    Message = $"MaxStdIO value {maxStdIO.Value} is quite low",
                    Recommendation = "Consider using -1 (default) or 2048+ for better compatibility"
                });
            }
            else if (maxStdIO.Value > 8192)
            {
                results.Add(new ValidationResult
                {
                    Setting = "MaxStdIO",
                    Level = ValidationLevel.Warning,
                    Message = $"MaxStdIO value {maxStdIO.Value} is very high",
                    Recommendation = "Values above 8192 may cause excessive memory usage"
                });
            }
        }
        
        return results;
    }

    /// <summary>
    ///     Validates settings based on installed mods.
    /// </summary>
    private List<ValidationResult> ValidateModSpecificSettings(
        CrashGenSettings settings,
        ModDetectionSettings modSettings)
    {
        var results = new List<ValidationResult>();
        
        // Check Baka ScrapHeap conflict
        if (modSettings.HasBakaScrapHeap && GetSettingBool(settings, "MemoryManager") == true)
        {
            results.Add(new ValidationResult
            {
                Setting = "MemoryManager",
                Level = ValidationLevel.Error,
                Message = "MemoryManager conflicts with Baka ScrapHeap",
                Recommendation = "Set MemoryManager to FALSE when using Baka ScrapHeap"
            });
        }
        
        // Check Achievements mod conflict
        if ((modSettings.HasXseModule("achievements.dll") || 
             modSettings.HasXseModule("unlimitedsurvivalmode.dll")) &&
            GetSettingBool(settings, "Achievements") == true)
        {
            results.Add(new ValidationResult
            {
                Setting = "Achievements",
                Level = ValidationLevel.Warning,
                Message = "Achievements parameter conflicts with Achievements mod",
                Recommendation = "Set Achievements to FALSE when using Achievements enabler mods"
            });
        }
        
        // Check Looks Menu (F4EE) requirement
        if (modSettings.HasXseModule("f4ee.dll") && GetSettingBool(settings, "F4EE") != true)
        {
            results.Add(new ValidationResult
            {
                Setting = "F4EE",
                Level = ValidationLevel.Error,
                Message = "Looks Menu is installed but F4EE is not enabled",
                Recommendation = "Set F4EE to TRUE under [Compatibility] section for Looks Menu"
            });
        }
        
        // Check for mods requiring specific fixes
        if (modSettings.HasPlugin("WorkshopFramework") && GetSettingBool(settings, "WorkshopMenu") != true)
        {
            results.Add(new ValidationResult
            {
                Setting = "WorkshopMenu",
                Level = ValidationLevel.Info,
                Message = "Workshop Framework detected",
                Recommendation = "Ensure WorkshopMenu is TRUE for better compatibility"
            });
        }
        
        return results;
    }

    /// <summary>
    ///     Builds a comprehensive validation report from results.
    /// </summary>
    private ReportFragment BuildValidationReport(List<ValidationResult> results, string crashGenName)
    {
        if (results.Count == 0)
        {
            return ReportFragment.CreateInfo(
                $"{crashGenName} Settings Validation",
                "All settings are properly configured - no issues detected.",
                100);
        }
        
        var builder = new StringBuilder();
        var errors = results.Where(r => r.Level == ValidationLevel.Error).ToList();
        var warnings = results.Where(r => r.Level == ValidationLevel.Warning).ToList();
        var info = results.Where(r => r.Level == ValidationLevel.Info).ToList();
        
        // Add summary
        builder.AppendLine($"**{crashGenName} Settings Validation Report**");
        builder.AppendLine();
        
        if (errors.Count > 0)
        {
            builder.AppendLine($"❌ **{errors.Count} Error(s) Found:**");
            foreach (var error in errors)
            {
                builder.AppendLine($"  - **{error.Setting}**: {error.Message}");
                builder.AppendLine($"    → {error.Recommendation}");
            }
            builder.AppendLine();
        }
        
        if (warnings.Count > 0)
        {
            builder.AppendLine($"⚠️ **{warnings.Count} Warning(s) Found:**");
            foreach (var warning in warnings)
            {
                builder.AppendLine($"  - **{warning.Setting}**: {warning.Message}");
                builder.AppendLine($"    → {warning.Recommendation}");
            }
            builder.AppendLine();
        }
        
        if (info.Count > 0)
        {
            builder.AppendLine($"ℹ️ **{info.Count} Suggestion(s):**");
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
            .WithTitle($"{crashGenName} Settings Validation")
            .Append(builder.ToString())
            .WithOrder(40) // High priority in report
            .Build();
    }

    private static bool IsSettingEnabled(object? value)
    {
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            int i => i != 0,
            _ => false
        };
    }

    private static bool? GetSettingBool(CrashGenSettings settings, string key)
    {
        if (!settings.RawSettings.TryGetValue(key, out var value))
            return null;
            
        return value switch
        {
            bool b => b,
            string s when bool.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static int? GetSettingInt(CrashGenSettings settings, string key)
    {
        if (!settings.RawSettings.TryGetValue(key, out var value))
            return null;
            
        return value switch
        {
            int i => i,
            string s when int.TryParse(s, out var parsed) => parsed,
            _ => null
        };
    }

    private static bool IsDebugEnvironment()
    {
        // Check if running in debug mode or with debugger attached
        #if DEBUG
            return true;
        #else
            return System.Diagnostics.Debugger.IsAttached;
        #endif
    }
}

/// <summary>
///     Represents a validation result for a setting.
/// </summary>
public sealed class ValidationResult
{
    public required string Setting { get; init; }
    public required ValidationLevel Level { get; init; }
    public required string Message { get; init; }
    public string? Recommendation { get; init; }
}

/// <summary>
///     Validation severity levels.
/// </summary>
public enum ValidationLevel
{
    Info,
    Warning,
    Error
}

/// <summary>
///     Describes performance impact of a setting.
/// </summary>
public sealed class PerformanceImpact
{
    public required ImpactLevel Level { get; init; }
    public required string Description { get; init; }
}

/// <summary>
///     Performance impact severity.
/// </summary>
public enum ImpactLevel
{
    Low,
    Medium,
    High
}

/// <summary>
///     Represents a setting dependency.
/// </summary>
public sealed class SettingDependency
{
    public string SettingName { get; }
    public bool? RequiredValue { get; }
    public string Recommendation { get; }
    
    public SettingDependency(string settingName, bool? requiredValue, string recommendation)
    {
        SettingName = settingName;
        RequiredValue = requiredValue;
        Recommendation = recommendation;
    }
}
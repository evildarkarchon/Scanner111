using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.Analysis.Validators;

/// <summary>
/// Validates memory management settings for conflicts and optimization opportunities.
/// Thread-safe for concurrent validation operations.
/// </summary>
public sealed class MemoryManagementValidator
{
    private readonly ILogger<MemoryManagementValidator> _logger;
    private const string Separator = "\n\n-----\n";
    private const string SuccessPrefix = "✔️ ";
    private const string WarningPrefix = "# ❌ CAUTION : ";
    private const string FixPrefix = " FIX: ";
    
    public MemoryManagementValidator(ILogger<MemoryManagementValidator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Validates memory management settings and returns a report fragment.
    /// </summary>
    public ReportFragment Validate(
        CrashGenSettings crashGenSettings,
        ModDetectionSettings modSettings)
    {
        ArgumentNullException.ThrowIfNull(crashGenSettings);
        ArgumentNullException.ThrowIfNull(modSettings);
        
        var lines = new List<string>();
        var crashGenName = crashGenSettings.CrashGenName;
        var memManagerEnabled = crashGenSettings.MemoryManager ?? false;
        
        _logger.LogDebug(
            "Validating memory settings: MemManager={MemMgr}, XCell={XCell}, BakaScrapHeap={Baka}",
            memManagerEnabled,
            modSettings.HasXCell,
            modSettings.HasBakaScrapHeap);
        
        // Check for old X-Cell version
        if (modSettings.HasOldXCell)
        {
            AddWarning(lines,
                "You have an old version of X-Cell installed, please update it to the latest version.",
                "Download the latest version from here: https://www.nexusmods.com/fallout4/mods/84214?tab=files");
            _logger.LogWarning("Outdated X-Cell version detected");
        }
        
        // Validate main memory manager configuration
        ValidateMainMemoryManager(
            lines,
            crashGenName,
            memManagerEnabled,
            modSettings.HasXCell,
            modSettings.HasBakaScrapHeap);
        
        // Check additional memory settings for X-Cell compatibility
        if (modSettings.HasXCell)
        {
            ValidateXCellCompatibility(lines, crashGenSettings);
        }
        
        return CreateFragment(lines);
    }
    
    private void ValidateMainMemoryManager(
        List<string> lines,
        string crashGenName,
        bool memManagerEnabled,
        bool hasXCell,
        bool hasBakaScrapHeap)
    {
        if (memManagerEnabled)
        {
            if (hasXCell)
            {
                AddWarning(lines,
                    "X-Cell is installed, but MemoryManager parameter is set to TRUE",
                    $"Open {crashGenName}'s TOML file and change MemoryManager to FALSE, this prevents conflicts with X-Cell.");
                _logger.LogWarning("Memory Manager enabled with X-Cell installed");
            }
            else if (hasBakaScrapHeap)
            {
                AddWarning(lines,
                    $"The Baka ScrapHeap Mod is installed, but is redundant with {crashGenName}",
                    $"Uninstall the Baka ScrapHeap Mod, this prevents conflicts with {crashGenName}.");
                _logger.LogWarning("Baka ScrapHeap installed with Memory Manager enabled");
            }
            else
            {
                AddSuccess(lines, $"Memory Manager parameter is correctly configured in your {crashGenName} settings!");
                _logger.LogInformation("Memory Manager configuration is correct");
            }
        }
        else if (hasXCell)
        {
            if (hasBakaScrapHeap)
            {
                AddWarning(lines,
                    "The Baka ScrapHeap Mod is installed, but is redundant with X-Cell",
                    "Uninstall the Baka ScrapHeap Mod, this prevents conflicts with X-Cell.");
                _logger.LogWarning("Baka ScrapHeap installed with X-Cell");
            }
            else
            {
                AddSuccess(lines, 
                    $"Memory Manager parameter is correctly configured for use with X-Cell in your {crashGenName} settings!");
                _logger.LogInformation("Memory Manager correctly disabled for X-Cell");
            }
        }
        else if (hasBakaScrapHeap)
        {
            AddWarning(lines,
                $"The Baka ScrapHeap Mod is installed, but is redundant with {crashGenName}",
                $"Uninstall the Baka ScrapHeap Mod and open {crashGenName}'s TOML file and change MemoryManager to TRUE, this improves performance.");
            _logger.LogWarning("Baka ScrapHeap installed without Memory Manager");
        }
    }
    
    private void ValidateXCellCompatibility(
        List<string> lines,
        CrashGenSettings crashGenSettings)
    {
        var memorySettings = new Dictionary<string, string>
        {
            ["HavokMemorySystem"] = "Havok Memory System",
            ["BSTextureStreamerLocalHeap"] = "BSTextureStreamerLocalHeap",
            ["ScaleformAllocator"] = "Scaleform Allocator",
            ["SmallBlockAllocator"] = "Small Block Allocator"
        };
        
        foreach (var (settingKey, displayName) in memorySettings)
        {
            var isEnabled = settingKey switch
            {
                "HavokMemorySystem" => crashGenSettings.HavokMemorySystem ?? false,
                "BSTextureStreamerLocalHeap" => crashGenSettings.BSTextureStreamerLocalHeap ?? false,
                "ScaleformAllocator" => crashGenSettings.ScaleformAllocator ?? false,
                "SmallBlockAllocator" => crashGenSettings.SmallBlockAllocator ?? false,
                _ => false
            };
            
            if (isEnabled)
            {
                AddWarning(lines,
                    $"X-Cell is installed, but {settingKey} parameter is set to TRUE",
                    $"Open {crashGenSettings.CrashGenName}'s TOML file and change {settingKey} to FALSE, this prevents conflicts with X-Cell.");
                _logger.LogWarning("X-Cell conflict: {Setting} is enabled", settingKey);
            }
            else
            {
                AddSuccess(lines,
                    $"{displayName} parameter is correctly configured for use with X-Cell in your {crashGenSettings.CrashGenName} settings!");
                _logger.LogDebug("X-Cell compatible: {Setting} is disabled", settingKey);
            }
        }
    }
    
    private void AddSuccess(List<string> lines, string message)
    {
        lines.Add($"{SuccessPrefix}{message}{Separator}");
    }
    
    private void AddWarning(List<string> lines, string warning, string fix)
    {
        lines.Add($"{WarningPrefix}{warning} # \n");
        lines.Add($"{FixPrefix}{fix}{Separator}");
    }
    
    private static ReportFragment CreateFragment(List<string> lines)
    {
        if (lines.Count == 0)
        {
            return ReportFragment.CreateInfo(
                "Memory Management Settings",
                "No memory management issues detected.",
                order: 200);
        }
        
        var content = new StringBuilder();
        foreach (var line in lines)
        {
            content.Append(line);
        }
        
        // Determine if there are warnings
        var hasWarnings = lines.Exists(l => l.Contains(WarningPrefix));
        
        return hasWarnings
            ? ReportFragment.CreateWarning(
                "Memory Management Settings",
                content.ToString(),
                order: 50)
            : ReportFragment.CreateSection(
                "Memory Management Settings",
                content.ToString(),
                order: 200);
    }
}
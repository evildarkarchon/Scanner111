using System;
using System.Collections.Generic;

namespace Scanner111.Core.Models;

/// <summary>
/// Represents crash generator (Buffout/CrashGen) configuration settings.
/// Immutable for thread-safety during concurrent analyzer execution.
/// </summary>
public sealed record CrashGenSettings
{
    /// <summary>
    /// Gets the name of the crash generator (e.g., "Buffout", "Crash Logger").
    /// </summary>
    public string CrashGenName { get; init; } = "Buffout";
    
    /// <summary>
    /// Gets the version of the crash generator.
    /// </summary>
    public Version? Version { get; init; }
    
    /// <summary>
    /// Gets whether achievements are enabled.
    /// </summary>
    public bool? Achievements { get; init; }
    
    /// <summary>
    /// Gets whether the memory manager is enabled.
    /// </summary>
    public bool? MemoryManager { get; init; }
    
    /// <summary>
    /// Gets whether the Havok memory system is enabled.
    /// </summary>
    public bool? HavokMemorySystem { get; init; }
    
    /// <summary>
    /// Gets whether the BSTextureStreamerLocalHeap is enabled.
    /// </summary>
    public bool? BSTextureStreamerLocalHeap { get; init; }
    
    /// <summary>
    /// Gets whether the Scaleform allocator is enabled.
    /// </summary>
    public bool? ScaleformAllocator { get; init; }
    
    /// <summary>
    /// Gets whether the small block allocator is enabled.
    /// </summary>
    public bool? SmallBlockAllocator { get; init; }
    
    /// <summary>
    /// Gets whether the archive limit is enabled.
    /// </summary>
    public bool? ArchiveLimit { get; init; }
    
    /// <summary>
    /// Gets whether F4EE (Looks Menu) compatibility is enabled.
    /// </summary>
    public bool? F4EE { get; init; }
    
    /// <summary>
    /// Gets all raw settings from the configuration file.
    /// </summary>
    public IReadOnlyDictionary<string, object> RawSettings { get; init; } = 
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Gets settings that should be ignored during validation.
    /// </summary>
    public IReadOnlySet<string> IgnoredSettings { get; init; } = 
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Creates CrashGenSettings from a dictionary of raw settings.
    /// </summary>
    public static CrashGenSettings FromDictionary(
        IDictionary<string, object> settings,
        string crashGenName,
        Version? version = null,
        IEnumerable<string>? ignoredSettings = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        
        return new CrashGenSettings
        {
            CrashGenName = crashGenName ?? "Buffout",
            Version = version,
            Achievements = GetBoolSetting(settings, "Achievements"),
            MemoryManager = GetBoolSetting(settings, "MemoryManager"),
            HavokMemorySystem = GetBoolSetting(settings, "HavokMemorySystem"),
            BSTextureStreamerLocalHeap = GetBoolSetting(settings, "BSTextureStreamerLocalHeap"),
            ScaleformAllocator = GetBoolSetting(settings, "ScaleformAllocator"),
            SmallBlockAllocator = GetBoolSetting(settings, "SmallBlockAllocator"),
            ArchiveLimit = GetBoolSetting(settings, "ArchiveLimit"),
            F4EE = GetBoolSetting(settings, "F4EE"),
            RawSettings = new Dictionary<string, object>(settings, StringComparer.OrdinalIgnoreCase),
            IgnoredSettings = ignoredSettings != null 
                ? new HashSet<string>(ignoredSettings, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
    }
    
    /// <summary>
    /// Safely extracts a boolean setting from the dictionary.
    /// </summary>
    private static bool? GetBoolSetting(IDictionary<string, object> settings, string key)
    {
        if (settings.TryGetValue(key, out var value))
        {
            return value switch
            {
                bool b => b,
                string s when bool.TryParse(s, out var parsed) => parsed,
                int i => i != 0,
                _ => null
            };
        }
        
        return null;
    }
}
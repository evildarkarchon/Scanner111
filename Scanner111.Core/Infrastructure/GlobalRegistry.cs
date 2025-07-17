using System.Collections.Concurrent;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Thread-safe global registry for storing application-wide values
/// </summary>
public static class GlobalRegistry
{
    private static readonly ConcurrentDictionary<string, object> _registry = new();
    
    /// <summary>
    /// Set a value in the registry
    /// </summary>
    /// <typeparam name="T">Type of value</typeparam>
    /// <param name="key">Registry key</param>
    /// <param name="value">Value to store</param>
    public static void Set<T>(string key, T value) where T : notnull
    {
        _registry[key] = value;
    }
    
    /// <summary>
    /// Get a value from the registry
    /// </summary>
    /// <typeparam name="T">Type of value</typeparam>
    /// <param name="key">Registry key</param>
    /// <returns>Value if found, null otherwise</returns>
    public static T? Get<T>(string key) where T : class
    {
        return _registry.TryGetValue(key, out var value) ? value as T : null;
    }
    
    /// <summary>
    /// Get a value type from the registry
    /// </summary>
    /// <typeparam name="T">Type of value</typeparam>
    /// <param name="key">Registry key</param>
    /// <param name="defaultValue">Default value if not found</param>
    /// <returns>Value if found, default otherwise</returns>
    public static T GetValueType<T>(string key, T defaultValue = default) where T : struct
    {
        if (_registry.TryGetValue(key, out var value) && value is T typedValue)
            return typedValue;
        return defaultValue;
    }
    
    /// <summary>
    /// Check if a key exists in the registry
    /// </summary>
    /// <param name="key">Registry key</param>
    /// <returns>True if key exists</returns>
    public static bool Contains(string key)
    {
        return _registry.ContainsKey(key);
    }
    
    /// <summary>
    /// Remove a key from the registry
    /// </summary>
    /// <param name="key">Registry key</param>
    /// <returns>True if key was removed</returns>
    public static bool Remove(string key)
    {
        return _registry.TryRemove(key, out _);
    }
    
    /// <summary>
    /// Clear all values from the registry
    /// </summary>
    public static void Clear()
    {
        _registry.Clear();
    }
    
    // Convenience properties for common values
    /// <summary>
    /// Current game being analyzed
    /// </summary>
    public static string Game
    {
        get => Get<string>("Game") ?? "Fallout4";
        set => Set("Game", value);
    }
    
    /// <summary>
    /// VR version of the game
    /// </summary>
    public static string GameVR
    {
        get => Get<string>("GameVR") ?? "";
        set => Set("GameVR", value);
    }
    
    /// <summary>
    /// Local application directory
    /// </summary>
    public static string LocalDir
    {
        get => Get<string>("LocalDir") ?? AppDomain.CurrentDomain.BaseDirectory;
        set => Set("LocalDir", value);
    }
    
    /// <summary>
    /// Configuration object
    /// </summary>
    public static Models.ClassicScanLogsInfo? Config
    {
        get => Get<Models.ClassicScanLogsInfo>("Config");
        set { if (value != null) Set("Config", value); }
    }
}
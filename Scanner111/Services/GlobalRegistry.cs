using System;
using System.Collections.Generic;

namespace Scanner111.Services;

/// <summary>
/// A global registry for managing application-wide resources and services.
/// This is a legacy system that is being phased out in favor of proper dependency injection.
/// </summary>
/// <remarks>
/// DEPRECATED: This class is maintained only for backward compatibility.
/// New code should use dependency injection instead of the GlobalRegistry.
/// </remarks>
[Obsolete("GlobalRegistry is deprecated. Use dependency injection instead.")]
public static class GlobalRegistry
{
    private static readonly Dictionary<string, object> _registry = new();
    
    /// <summary>
    /// Known registry keys.
    /// </summary>
    public static class Keys
    {
        /// <summary>
        /// Key for the YAML settings cache service.
        /// Use IYamlSettingsCache from DI instead.
        /// </summary>
        public const string YamlCache = "YamlCache";
        
        /// <summary>
        /// Key for the current game.
        /// Use IGameContextService from DI instead.
        /// </summary>
        public const string CurrentGame = "CurrentGame";
    }
    
    /// <summary>
    /// Registers an object in the global registry.
    /// </summary>
    /// <param name="key">The key to register the object under.</param>
    /// <param name="value">The object to register.</param>
    public static void Register(string key, object value)
    {
        _registry[key] = value;
    }
    
    /// <summary>
    /// Gets an object from the global registry.
    /// </summary>
    /// <typeparam name="T">The type to cast the object to.</typeparam>
    /// <param name="key">The key of the object to get.</param>
    /// <returns>The object, or default(T) if not found.</returns>
    public static T? Get<T>(string key) where T : class
    {
        if (_registry.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return null;
    }
    
    /// <summary>
    /// Gets the current game name.
    /// </summary>
    /// <returns>The current game name, or "Default" if not set.</returns>
    /// <remarks>
    /// DEPRECATED: Use IGameContextService.GetCurrentGame() instead.
    /// </remarks>
    [Obsolete("Use IGameContextService.GetCurrentGame() instead.")]
    public static string GetGame()
    {
        return Get<string>(Keys.CurrentGame) ?? "Default";
    }
}
namespace Scanner111.Core.Configuration;

/// <summary>
/// Configuration options for the YAML settings system.
/// </summary>
public class YamlSettingsOptions
{
    /// <summary>
    /// Gets or sets the cache time-to-live for dynamic YAML files.
    /// Default is 5 seconds.
    /// </summary>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromSeconds(YamlConstants.DefaultCacheTtlSeconds);
    
    /// <summary>
    /// Gets or sets the list of YAML stores that are considered static (won't change during execution).
    /// These stores will be cached permanently and cannot be modified.
    /// </summary>
    public HashSet<YamlStore> StaticStores { get; set; } = new(YamlConstants.StaticYamlStores);
    
    /// <summary>
    /// Gets or sets the default game name for path resolution.
    /// </summary>
    public string DefaultGame { get; set; } = YamlConstants.DefaultGame;
    
    /// <summary>
    /// Gets or sets whether to validate settings file structure on load.
    /// </summary>
    public bool ValidateSettingsStructure { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to automatically regenerate corrupted settings files.
    /// </summary>
    public bool AutoRegenerateCorruptedSettings { get; set; } = true;
    
    /// <summary>
    /// Gets or sets the maximum number of file-specific locks to maintain in memory.
    /// Older locks are disposed when this limit is exceeded.
    /// </summary>
    public int MaxFileLocks { get; set; } = 100;
    
    /// <summary>
    /// Gets or sets whether to enable performance metrics collection.
    /// </summary>
    public bool EnableMetrics { get; set; } = true;
}
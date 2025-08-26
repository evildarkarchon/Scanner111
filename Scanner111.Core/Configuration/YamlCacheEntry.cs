namespace Scanner111.Core.Configuration;

/// <summary>
/// Represents a cached YAML file entry with metadata.
/// </summary>
/// <param name="Data">The parsed YAML data as a dictionary.</param>
/// <param name="LastModified">The last modification time of the file.</param>
/// <param name="LastChecked">The last time the file was checked for modifications.</param>
/// <param name="FilePath">The original file path for this cache entry.</param>
public record YamlCacheEntry(
    Dictionary<string, object?> Data,
    DateTime LastModified,
    DateTime LastChecked,
    string FilePath)
{
    /// <summary>
    /// Determines if this cache entry needs to be refreshed based on TTL.
    /// </summary>
    /// <param name="ttl">The time-to-live for cache validity.</param>
    /// <returns>True if the cache entry should be refreshed, false otherwise.</returns>
    public bool NeedsRefresh(TimeSpan ttl)
    {
        return DateTime.UtcNow - LastChecked > ttl;
    }
    
    /// <summary>
    /// Creates a new cache entry with updated check time.
    /// </summary>
    /// <param name="newLastModified">Optional new last modified time.</param>
    /// <returns>A new cache entry with updated metadata.</returns>
    public YamlCacheEntry WithUpdatedCheckTime(DateTime? newLastModified = null)
    {
        return this with 
        { 
            LastChecked = DateTime.UtcNow,
            LastModified = newLastModified ?? LastModified
        };
    }
}
using System;

namespace Scanner111.Core.Data;

/// <summary>
/// Represents a cached FormID database entry with metadata.
/// </summary>
public sealed record FormIdCacheEntry
{
    /// <summary>
    /// Gets the FormID (without prefix).
    /// </summary>
    public required string FormId { get; init; }
    
    /// <summary>
    /// Gets the plugin name.
    /// </summary>
    public required string Plugin { get; init; }
    
    /// <summary>
    /// Gets the entry description/value from the database.
    /// </summary>
    public string? Value { get; init; }
    
    /// <summary>
    /// Gets when this entry was cached.
    /// </summary>
    public DateTime CachedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Gets the database source this entry came from.
    /// </summary>
    public string? DatabaseSource { get; init; }
    
    /// <summary>
    /// Creates a cache key from FormID and plugin.
    /// </summary>
    public static string CreateKey(string formId, string plugin) 
        => $"{formId}:{plugin}".ToUpperInvariant();
    
    /// <summary>
    /// Gets the cache key for this entry.
    /// </summary>
    public string CacheKey => CreateKey(FormId, Plugin);
}
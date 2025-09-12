using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Extensions;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Orchestration;

namespace Scanner111.CLI.Services;

/// <summary>
/// Interface for incremental analysis capabilities.
/// </summary>
public interface IIncrementalAnalyzer
{
    /// <summary>
    /// Performs incremental analysis on changed log data.
    /// </summary>
    /// <param name="logFile">The log file being analyzed.</param>
    /// <param name="newLogLines">New log lines to analyze.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Analysis results including cached and new results.</returns>
    Task<IEnumerable<AnalysisResult>> AnalyzeIncrementalAsync(
        string logFile, 
        string[] newLogLines, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clears the analysis cache for a specific file or all files.
    /// </summary>
    /// <param name="logFile">Optional log file to clear cache for. If null, clears all cache.</param>
    Task ClearCacheAsync(string? logFile = null);
    
    /// <summary>
    /// Gets cache statistics.
    /// </summary>
    /// <returns>Cache statistics.</returns>
    AnalysisCacheStats GetCacheStats();
}

/// <summary>
/// Cache statistics for analysis operations.
/// </summary>
public class AnalysisCacheStats
{
    /// <summary>
    /// Gets or sets the number of cached file states.
    /// </summary>
    public int CachedFiles { get; set; }
    
    /// <summary>
    /// Gets or sets the total cache size in bytes.
    /// </summary>
    public long CacheSizeBytes { get; set; }
    
    /// <summary>
    /// Gets or sets the cache hit count.
    /// </summary>
    public int CacheHits { get; set; }
    
    /// <summary>
    /// Gets or sets the cache miss count.
    /// </summary>
    public int CacheMisses { get; set; }
    
    /// <summary>
    /// Gets or sets the last cache cleanup time.
    /// </summary>
    public DateTime? LastCleanup { get; set; }
}

/// <summary>
/// Represents the state of a file for incremental analysis.
/// </summary>
internal class FileAnalysisState
{
    public string FilePath { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public long FileSize { get; set; }
    public List<AnalysisResult> CachedResults { get; set; } = new();
    public DateTime LastAnalyzed { get; set; }
    public Dictionary<string, object> AnalyzerStates { get; set; } = new();
}

/// <summary>
/// Service for performing incremental analysis with caching and optimization.
/// </summary>
public class IncrementalAnalyzer : IIncrementalAnalyzer, IDisposable
{
    private readonly IAnalyzerOrchestrator _orchestrator;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly ILogger<IncrementalAnalyzer> _logger;
    private readonly ConcurrentDictionary<string, FileAnalysisState> _fileStates = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly string _cacheDirectory;
    private readonly Timer _cleanupTimer;
    private readonly JsonSerializerOptions _jsonOptions;
    
    private int _cacheHits = 0;
    private int _cacheMisses = 0;
    private bool _disposed = false;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementalAnalyzer"/> class.
    /// </summary>
    /// <param name="orchestrator">The analysis orchestrator.</param>
    /// <param name="yamlCore">The YAML settings core.</param>
    /// <param name="logger">The logger instance.</param>
    public IncrementalAnalyzer(
        IAnalyzerOrchestrator orchestrator,
        IAsyncYamlSettingsCore yamlCore,
        ILogger<IncrementalAnalyzer> logger)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        _cacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scanner111",
            "AnalysisCache");
        
        Directory.CreateDirectory(_cacheDirectory);
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        // Setup cleanup timer (every hour)
        _cleanupTimer = new Timer(CleanupCallback, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        
        // Load existing cache on startup
        _ = Task.Run(LoadCacheAsync);
    }
    
    /// <inheritdoc />
    public async Task<IEnumerable<AnalysisResult>> AnalyzeIncrementalAsync(
        string logFile, 
        string[] newLogLines, 
        CancellationToken cancellationToken = default)
    {
        if (_disposed) throw new ObjectDisposedException(nameof(IncrementalAnalyzer));
        
        try
        {
            var normalizedPath = Path.GetFullPath(logFile);
            var fileInfo = new FileInfo(normalizedPath);
            
            if (!fileInfo.Exists)
            {
                _logger.LogWarning("Log file not found: {LogFile}", logFile);
                return Enumerable.Empty<AnalysisResult>();
            }
            
            await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var currentState = await GetCurrentFileStateAsync(normalizedPath, newLogLines, cancellationToken).ConfigureAwait(false);
                var cachedState = _fileStates.GetValueOrDefault(normalizedPath);
                
                // Check if we can use cached results
                if (cachedState != null && CanUseCachedResults(currentState, cachedState))
                {
                    _logger.LogDebug("Using cached analysis results for {LogFile}", logFile);
                    Interlocked.Increment(ref _cacheHits);
                    return cachedState.CachedResults;
                }
                
                Interlocked.Increment(ref _cacheMisses);
                
                // Determine which analyzers need to run
                var analyzersToRun = await DetermineAnalyzersToRunAsync(currentState, cachedState, cancellationToken).ConfigureAwait(false);
                
                // Perform incremental analysis
                var newResults = await PerformIncrementalAnalysisAsync(
                    normalizedPath, 
                    newLogLines, 
                    analyzersToRun, 
                    cachedState,
                    cancellationToken).ConfigureAwait(false);
                
                // Update cache
                currentState.CachedResults = newResults.ToList();
                currentState.LastAnalyzed = DateTime.UtcNow;
                _fileStates[normalizedPath] = currentState;
                
                // Persist cache asynchronously
                _ = Task.Run(() => PersistCacheAsync(normalizedPath, currentState, CancellationToken.None));
                
                _logger.LogInformation("Completed incremental analysis for {LogFile}. Results: {Count}, Cache: {CacheHits}H/{CacheMisses}M", 
                    logFile, newResults.Count(), _cacheHits, _cacheMisses);
                
                return newResults;
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during incremental analysis of {LogFile}", logFile);
            
            // Fallback to full analysis
            try
            {
                _logger.LogInformation("Falling back to full analysis for {LogFile}", logFile);
                var request = new AnalysisRequest { InputPath = logFile };
                var orchestrationResult = await _orchestrator.RunAnalysisAsync(request, cancellationToken).ConfigureAwait(false);
                return orchestrationResult.Results;
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback analysis also failed for {LogFile}", logFile);
                throw;
            }
        }
    }
    
    /// <inheritdoc />
    public async Task ClearCacheAsync(string? logFile = null)
    {
        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (string.IsNullOrEmpty(logFile))
            {
                _fileStates.Clear();
                if (Directory.Exists(_cacheDirectory))
                {
                    Directory.Delete(_cacheDirectory, true);
                    Directory.CreateDirectory(_cacheDirectory);
                }
                _logger.LogInformation("Cleared all analysis cache");
            }
            else
            {
                var normalizedPath = Path.GetFullPath(logFile);
                _fileStates.TryRemove(normalizedPath, out _);
                
                var cacheFile = GetCacheFilePath(normalizedPath);
                if (File.Exists(cacheFile))
                {
                    File.Delete(cacheFile);
                }
                
                _logger.LogInformation("Cleared analysis cache for {LogFile}", logFile);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }
    
    /// <inheritdoc />
    public AnalysisCacheStats GetCacheStats()
    {
        var cacheSize = 0L;
        if (Directory.Exists(_cacheDirectory))
        {
            try
            {
                cacheSize = Directory.GetFiles(_cacheDirectory, "*.cache")
                    .Sum(file => new FileInfo(file).Length);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to calculate cache size");
            }
        }
        
        return new AnalysisCacheStats
        {
            CachedFiles = _fileStates.Count,
            CacheSizeBytes = cacheSize,
            CacheHits = _cacheHits,
            CacheMisses = _cacheMisses,
            LastCleanup = GetLastCleanupTime()
        };
    }
    
    private async Task<FileAnalysisState> GetCurrentFileStateAsync(
        string filePath, 
        string[] newLogLines, 
        CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        var contentHash = await CalculateContentHashAsync(newLogLines, cancellationToken).ConfigureAwait(false);
        
        return new FileAnalysisState
        {
            FilePath = filePath,
            ContentHash = contentHash,
            LastModified = fileInfo.LastWriteTimeUtc,
            FileSize = fileInfo.Length
        };
    }
    
    private static bool CanUseCachedResults(FileAnalysisState current, FileAnalysisState cached)
    {
        // Use cached results if content hash matches and file hasn't been modified
        return current.ContentHash == cached.ContentHash &&
               current.LastModified <= cached.LastModified &&
               cached.CachedResults.Any() &&
               DateTime.UtcNow - cached.LastAnalyzed < TimeSpan.FromHours(24); // Cache expires after 24 hours
    }
    
    private async Task<string[]> DetermineAnalyzersToRunAsync(
        FileAnalysisState current, 
        FileAnalysisState? cached,
        CancellationToken cancellationToken)
    {
        // For now, we'll run all analyzers if there are significant changes
        // In a more sophisticated implementation, we could track which analyzers
        // are affected by specific types of changes
        
        await Task.CompletedTask;
        
        if (cached == null)
        {
            // No cache, run all analyzers
            return new[] { "all" };
        }
        
        // If content changed significantly, run all analyzers
        if (current.ContentHash != cached.ContentHash)
        {
            return new[] { "all" };
        }
        
        // If only minor changes, might run subset of analyzers
        // This is a simplification - real implementation would be more sophisticated
        return new[] { "incremental" };
    }
    
    private async Task<IEnumerable<AnalysisResult>> PerformIncrementalAnalysisAsync(
        string filePath,
        string[] newLogLines,
        string[] analyzersToRun,
        FileAnalysisState? cachedState,
        CancellationToken cancellationToken)
    {
        var context = new AnalysisContext(filePath, _yamlCore);
        
        // Add incremental data to context if needed
        if (newLogLines.Any())
        {
            context.SetSharedData("incremental_lines", newLogLines);
            context.SetSharedData("is_incremental", true);
        }
        
        // Add cached state to context for analyzers that can use it
        if (cachedState != null)
        {
            context.SetSharedData("cached_results", cachedState.CachedResults);
            context.SetSharedData("cached_analyzer_states", cachedState.AnalyzerStates);
        }
        
        // Perform analysis
        var request = new AnalysisRequest { InputPath = filePath };
        var orchestrationResult = await _orchestrator.RunAnalysisAsync(request, cancellationToken).ConfigureAwait(false);
        var results = orchestrationResult.Results;
        
        // Merge with cached results if performing incremental analysis
        if (analyzersToRun.Contains("incremental") && cachedState?.CachedResults != null)
        {
            results = MergeWithCachedResults(results, cachedState.CachedResults).ToList();
        }
        
        return results;
    }
    
    private IEnumerable<AnalysisResult> MergeWithCachedResults(
        IEnumerable<AnalysisResult> newResults, 
        List<AnalysisResult> cachedResults)
    {
        var merged = new List<AnalysisResult>(cachedResults);
        
        // Add new results, avoiding duplicates based on title and analyzer
        var existingKeys = new HashSet<string>(
            cachedResults.Select(r => $"{r.AnalyzerName}:{r.GetTitle()}"));
        
        foreach (var result in newResults)
        {
            var key = $"{result.AnalyzerName}:{result.GetTitle()}";
            if (!existingKeys.Contains(key))
            {
                merged.Add(result);
                existingKeys.Add(key);
            }
        }
        
        return merged;
    }
    
    private async Task<string> CalculateContentHashAsync(string[] lines, CancellationToken cancellationToken)
    {
        using var sha256 = SHA256.Create();
        var content = string.Join("\n", lines);
        var contentBytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = await Task.Run(() => sha256.ComputeHash(contentBytes), cancellationToken).ConfigureAwait(false);
        return Convert.ToBase64String(hashBytes);
    }
    
    private async Task LoadCacheAsync()
    {
        try
        {
            if (!Directory.Exists(_cacheDirectory)) return;
            
            var cacheFiles = Directory.GetFiles(_cacheDirectory, "*.cache");
            var loadTasks = cacheFiles.Select(LoadCacheFileAsync);
            
            await Task.WhenAll(loadTasks).ConfigureAwait(false);
            
            _logger.LogDebug("Loaded {Count} cached file states", _fileStates.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load analysis cache");
        }
    }
    
    private async Task LoadCacheFileAsync(string cacheFilePath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(cacheFilePath).ConfigureAwait(false);
            var state = JsonSerializer.Deserialize<FileAnalysisState>(json, _jsonOptions);
            
            if (state != null && !string.IsNullOrEmpty(state.FilePath))
            {
                _fileStates[state.FilePath] = state;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cache file {CacheFile}", cacheFilePath);
            
            // Delete corrupted cache file
            try
            {
                File.Delete(cacheFilePath);
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }
    
    private async Task PersistCacheAsync(string filePath, FileAnalysisState state, CancellationToken cancellationToken)
    {
        try
        {
            var cacheFilePath = GetCacheFilePath(filePath);
            var json = JsonSerializer.Serialize(state, _jsonOptions);
            
            await File.WriteAllTextAsync(cacheFilePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist cache for {FilePath}", filePath);
        }
    }
    
    private string GetCacheFilePath(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(filePath)))
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
        
        return Path.Combine(_cacheDirectory, $"{fileName}_{hash[..16]}.cache");
    }
    
    private DateTime? GetLastCleanupTime()
    {
        try
        {
            var cleanupMarker = Path.Combine(_cacheDirectory, ".cleanup");
            if (File.Exists(cleanupMarker))
            {
                return File.GetLastWriteTimeUtc(cleanupMarker);
            }
        }
        catch
        {
            // Ignore errors
        }
        
        return null;
    }
    
    private async void CleanupCallback(object? state)
    {
        try
        {
            await CleanupExpiredCacheAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cache cleanup");
        }
    }
    
    private async Task CleanupExpiredCacheAsync()
    {
        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var expiredCutoff = DateTime.UtcNow - TimeSpan.FromDays(7); // Remove cache older than 7 days
            var filesToRemove = new List<string>();
            
            foreach (var kvp in _fileStates.ToList())
            {
                if (kvp.Value.LastAnalyzed < expiredCutoff || !File.Exists(kvp.Key))
                {
                    filesToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var filePath in filesToRemove)
            {
                _fileStates.TryRemove(filePath, out _);
                
                var cacheFile = GetCacheFilePath(filePath);
                if (File.Exists(cacheFile))
                {
                    File.Delete(cacheFile);
                }
            }
            
            // Update cleanup marker
            var cleanupMarker = Path.Combine(_cacheDirectory, ".cleanup");
            await File.WriteAllTextAsync(cleanupMarker, DateTime.UtcNow.ToString("O")).ConfigureAwait(false);
            
            if (filesToRemove.Any())
            {
                _logger.LogInformation("Cleaned up {Count} expired cache entries", filesToRemove.Count);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _cleanupTimer?.Dispose();
            _cacheLock?.Dispose();
            _disposed = true;
        }
    }
}
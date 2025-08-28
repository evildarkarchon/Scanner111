using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;

namespace Scanner111.Test.Infrastructure.TestData;

/// <summary>
///     Provides access to embedded crash log resources for self-contained testing.
///     Reduces dependency on external sample_logs directory.
/// </summary>
public class EmbeddedResourceProvider
{
    private static readonly Assembly TestAssembly = typeof(EmbeddedResourceProvider).Assembly;
    private readonly ILogger<EmbeddedResourceProvider> _logger;
    private readonly Dictionary<string, string> _resourceCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    public EmbeddedResourceProvider(ILogger<EmbeddedResourceProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Gets an embedded crash log resource by name.
    /// </summary>
    /// <param name="resourceName">Name of the embedded resource (e.g., "crash-2023-09-15-01-54-49.log")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The content of the embedded resource</returns>
    public async Task<string> GetEmbeddedLogAsync(string resourceName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(resourceName))
            throw new ArgumentException("Resource name cannot be null or empty", nameof(resourceName));

        await _cacheLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Check cache first
            if (_resourceCache.TryGetValue(resourceName, out var cachedContent))
            {
                _logger.LogDebug("Returning cached embedded resource: {ResourceName}", resourceName);
                return cachedContent;
            }

            // Load from embedded resources
            var fullResourceName = $"Scanner111.Test.Resources.EmbeddedLogs.{resourceName}";
            using var stream = TestAssembly.GetManifestResourceStream(fullResourceName);
            
            if (stream == null)
            {
                var availableResources = GetAvailableEmbeddedLogs();
                throw new InvalidOperationException(
                    $"Embedded resource '{resourceName}' not found. Available resources: {string.Join(", ", availableResources)}");
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var content = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            
            // Cache the content
            _resourceCache[resourceName] = content;
            _logger.LogInformation("Loaded embedded resource: {ResourceName} ({Length} bytes)", 
                resourceName, content.Length);
            
            return content;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    /// <summary>
    ///     Gets all available embedded log resource names.
    /// </summary>
    public IEnumerable<string> GetAvailableEmbeddedLogs()
    {
        var prefix = "Scanner111.Test.Resources.EmbeddedLogs.";
        return TestAssembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(prefix))
            .Select(name => name.Substring(prefix.Length))
            .OrderBy(name => name);
    }

    /// <summary>
    ///     Writes an embedded resource to a temporary file for testing.
    /// </summary>
    public async Task<string> WriteToTempFileAsync(string resourceName, string tempDirectory, 
        CancellationToken cancellationToken = default)
    {
        var content = await GetEmbeddedLogAsync(resourceName, cancellationToken).ConfigureAwait(false);
        
        var tempPath = Path.Combine(tempDirectory, resourceName);
        var directory = Path.GetDirectoryName(tempPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Wrote embedded resource to temp file: {TempPath}", tempPath);
        
        return tempPath;
    }

    /// <summary>
    ///     Gets embedded expected output content for validation.
    /// </summary>
    public async Task<string?> GetEmbeddedExpectedOutputAsync(string crashLogName, 
        CancellationToken cancellationToken = default)
    {
        var baseName = Path.GetFileNameWithoutExtension(crashLogName);
        var outputResourceName = $"{baseName}-AUTOSCAN.md";
        
        try
        {
            return await GetEmbeddedLogAsync(outputResourceName, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            _logger.LogDebug("No embedded expected output found for: {CrashLogName}", crashLogName);
            return null;
        }
    }

    /// <summary>
    ///     Preloads all embedded resources into cache for faster access.
    /// </summary>
    public async Task PreloadAllResourcesAsync(CancellationToken cancellationToken = default)
    {
        var resources = GetAvailableEmbeddedLogs().ToList();
        _logger.LogInformation("Preloading {Count} embedded resources", resources.Count);
        
        foreach (var resource in resources)
        {
            await GetEmbeddedLogAsync(resource, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Clears the resource cache to free memory.
    /// </summary>
    public async Task ClearCacheAsync()
    {
        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var count = _resourceCache.Count;
            _resourceCache.Clear();
            _logger.LogDebug("Cleared {Count} cached embedded resources", count);
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}

/// <summary>
///     Provides critical crash log samples as embedded resources.
///     These represent diverse crash scenarios for comprehensive testing.
/// </summary>
public static class CriticalSampleLogs
{
    // Core set of representative crash logs
    public const string EarlySample = "crash-2022-06-05-12-52-17.log";
    public const string WithPluginIssues = "crash-2023-09-15-01-54-49.log";
    public const string WithMemoryIssues = "crash-2023-11-08-05-46-35.log";
    public const string RecentSample = "crash-2024-08-25-11-05-43.log";
    
    // Special cases
    public const string StackOverflow = "crash-2022-06-09-07-25-03.log";
    public const string AccessViolation = "crash-2023-10-14-05-54-22.log";
    public const string FCXMode = "crash-2023-10-25-09-49-04.log";
    public const string NoBuffout = "crash-2023-12-01-08-33-44.log";
    
    // Edge cases
    public const string LargeLogFile = "crash-2022-06-12-07-11-38.log";
    public const string MinimalLog = "crash-2022-06-15-10-02-51.log";

    /// <summary>
    ///     Gets all critical sample log names for embedding.
    /// </summary>
    public static IEnumerable<string> GetAllCriticalSamples()
    {
        yield return EarlySample;
        yield return WithPluginIssues;
        yield return WithMemoryIssues;
        yield return RecentSample;
        yield return StackOverflow;
        yield return AccessViolation;
        yield return FCXMode;
        yield return NoBuffout;
        yield return LargeLogFile;
        yield return MinimalLog;
    }
}
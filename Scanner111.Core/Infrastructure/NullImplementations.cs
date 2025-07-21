using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Null cache manager that doesn't cache anything
/// </summary>
public class NullCacheManager : ICacheManager
{
    public T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null)
    {
        return factory();
    }

    public void CacheAnalysisResult(string filePath, string analyzerName, AnalysisResult result)
    {
        // No-op
    }

    public AnalysisResult? GetCachedAnalysisResult(string filePath, string analyzerName)
    {
        return null; // Never returns cached results
    }

    public bool IsFileCacheValid(string filePath)
    {
        return false; // Always invalid
    }

    public void ClearCache()
    {
        // No-op
    }

    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalHits = 0,
            TotalMisses = 0,
            HitRate = 0,
            CachedFiles = 0,
            MemoryUsage = 0
        };
    }
}

/// <summary>
///     Error policy that never retries
/// </summary>
public class NoRetryErrorPolicy : IErrorHandlingPolicy
{
    public ErrorHandlingResult HandleError(Exception exception, string context, int attemptNumber)
    {
        return exception switch
        {
            OperationCanceledException => new ErrorHandlingResult
            {
                Action = ErrorAction.Fail,
                Message = "Operation was cancelled",
                LogLevel = LogLevel.Information
            },

            _ => new ErrorHandlingResult
            {
                Action = ErrorAction.Skip,
                Message = $"Error in {context}: {exception.Message}",
                LogLevel = LogLevel.Error
            }
        };
    }

    public bool ShouldRetry(Exception exception, int attemptNumber)
    {
        return false; // Never retry
    }
}

/// <summary>
///     Null progress implementation for generic types
/// </summary>
public class NullProgress<T> : IProgress<T>
{
    public void Report(T value)
    {
        // No-op
    }
}
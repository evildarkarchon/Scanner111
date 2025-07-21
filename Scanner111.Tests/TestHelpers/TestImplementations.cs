using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.TestHelpers;

public class TestYamlSettingsProvider : IYamlSettingsProvider
{
    public T? GetSetting<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        // Return test values for specific settings
        if (keyPath == "catch_log_records" && typeof(T) == typeof(List<string>))
        {
            return (T)(object)new List<string>();
        }
        if (keyPath == "Crashlog_Records_Exclude" && typeof(T) == typeof(List<string>))
        {
            return (T)(object)new List<string> { "excluded_record" };
        }
        if (keyPath == "catch_log_settings" && typeof(T) == typeof(List<string>))
        {
            return (T)(object)new List<string> { "test_setting", "another_setting" };
        }
        if (keyPath == "Crashlog_Settings_Exclude" && typeof(T) == typeof(List<string>))
        {
            return (T)(object)new List<string> { "excluded_setting" };
        }
        if (keyPath == "Crashlog_Plugins_Exclude" && typeof(T) == typeof(List<string>))
        {
            return (T)(object)new List<string> { "ignored.esp" };
        }
        if (keyPath == "suspects_error_list" && typeof(T) == typeof(Dictionary<string, string>))
        {
            return (T)(object)new Dictionary<string, string>
            {
                {"HIGH | Access Violation", "access violation"},
                {"MEDIUM | Null Pointer", "null pointer"},
                {"LOW | Memory Error", "memory error"}
            };
        }
        if (keyPath == "suspects_stack_list" && typeof(T) == typeof(Dictionary<string, List<string>>))
        {
            return (T)(object)new Dictionary<string, List<string>>
            {
                {"HIGH | Stack Overflow", new List<string> {"stack overflow", "ME-REQ|overflow"}},
                {"MEDIUM | Invalid Handle", new List<string> {"invalid handle", "2|bad handle"}},
                {"LOW | Debug Assert", new List<string> {"debug assert", "NOT|release mode"}}
            };
        }
        if (keyPath == "CLASSIC_Settings.Show FormID Values" && typeof(T) == typeof(bool))
        {
            return (T)(object)true;
        }
        if (keyPath == "Game_Info.CRASHGEN_LogName" && typeof(T) == typeof(string))
        {
            return (T)(object)"Buffout 4";
        }
        return defaultValue;
    }

    public void SetSetting<T>(string yamlFile, string keyPath, T value)
    {
        // Test implementation - do nothing
    }

    public T? LoadYaml<T>(string yamlFile) where T : class
    {
        if (yamlFile == "CLASSIC Fallout4" && typeof(T) == typeof(Dictionary<string, object>))
        {
            var yamlData = new Dictionary<string, object>
            {
                ["Crashlog_Error_Check"] = new Dictionary<object, object>
                {
                    {"5 | Access Violation", "access violation"},
                    {"4 | Null Pointer", "null pointer"},
                    {"3 | Memory Error", "memory error"},
                    {"6 | Stack Overflow Crash", "EXCEPTION_STACK_OVERFLOW"}
                },
                ["Crashlog_Stack_Check"] = new Dictionary<object, object>
                {
                    {"5 | Stack Overflow", new List<object> {"stack overflow", "ME-REQ|overflow"}},
                    {"4 | Invalid Handle", new List<object> {"invalid handle", "2|bad handle"}},
                    {"3 | Debug Assert", new List<object> {"debug assert", "NOT|release mode"}}
                }
            };
            return (T)(object)yamlData;
        }
        return null;
    }

    public void ClearCache()
    {
        // Test implementation - do nothing
    }
}

public class TestFormIdDatabaseService : IFormIdDatabaseService
{
    public bool DatabaseExists => true;

    public string? GetEntry(string formId, string plugin)
    {
        // Return a test entry for known FormIDs
        if (formId == "0001A332")
        {
            return "TestLocation (CELL)";
        }
        return null;
    }
}

public class TestLogger<T> : ILogger<T>
{
    public IDisposable BeginScope<TState>(TState state) => null!;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        // Test implementation - do nothing
    }
}

public class TestMessageHandler : IMessageHandler
{
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowWarning(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowError(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowDebug(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All) { }
    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info, MessageTarget target = MessageTarget.All) { }
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems) { return new Progress<ProgressInfo>(); }
    public IProgressContext CreateProgressContext(string title, int totalItems) { return new TestProgressContext(); }
}

public class TestCacheManager : ICacheManager
{
    private readonly Dictionary<string, AnalysisResult> _cache = new();
    
    public AnalysisResult? GetCachedAnalysisResult(string filePath, string analyzerName)
    {
        var key = $"{filePath}:{analyzerName}";
        return _cache.TryGetValue(key, out var result) ? result : null;
    }
    
    public void CacheAnalysisResult(string filePath, string analyzerName, AnalysisResult result)
    {
        var key = $"{filePath}:{analyzerName}";
        _cache[key] = result;
    }
    
    public bool IsFileCacheValid(string filePath)
    {
        return true; // For testing, always consider cache valid
    }
    
    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalHits = _cache.Count / 2,
            TotalMisses = _cache.Count / 2,
            HitRate = 0.5, // 50% hit rate for testing
            CachedFiles = _cache.Count
        };
    }
    
    public void ClearCache()
    {
        _cache.Clear();
    }
    
    public T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null)
    {
        // For testing, just call the factory
        return factory();
    }
}

public class TestErrorHandlingPolicy : IErrorHandlingPolicy
{
    public bool ShouldRetry(Exception exception, int attemptCount)
    {
        return attemptCount < 3; // Retry up to 3 times for testing
    }
    
    public TimeSpan GetRetryDelay(int attemptCount)
    {
        return TimeSpan.FromMilliseconds(10); // Short delay for testing
    }
    
    public bool ShouldContinueOnError(Exception exception)
    {
        return true; // Continue on errors for testing
    }
    
    public ErrorHandlingResult HandleError(Exception exception, string context, int attemptCount)
    {
        return new ErrorHandlingResult
        {
            Action = ErrorAction.Continue,
            Message = "Test error handling"
        };
    }
}

public class TestProgressContext : IProgressContext
{
    public void Dispose() { }
    public void Report(ProgressInfo value) { }
    public void Update(int current, string message) { }
    public void Complete() { }
}
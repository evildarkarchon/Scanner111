using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.TestHelpers;

/// <summary>
/// A test implementation of the IYamlSettingsProvider interface for use in testing scenarios.
/// Provides methods for accessing and manipulating YAML-based settings within tests.
/// </summary>
public class TestYamlSettingsProvider : IYamlSettingsProvider
{
    /// Retrieves a specific setting from a YAML file based on the provided key path.
    /// <typeparam name="T">The type of the setting value to be returned.</typeparam>
    /// <param name="yamlFile">The path or name of the YAML file containing the settings.</param>
    /// <param name="keyPath">The key path within the YAML file to locate the setting.</param>
    /// <param name="defaultValue">An optional default value to return if the key does not exist or cannot be fetched.</param>
    /// <returns>The value of the setting of the specified type if found; otherwise, returns the default value.</returns>
    public T? GetSetting<T>(string yamlFile, string keyPath, T? defaultValue = default)
    {
        // Return test values for specific settings
        if (keyPath == "catch_log_records" && typeof(T) == typeof(List<string>)) return (T)(object)new List<string>();
        if (keyPath == "Crashlog_Records_Exclude" && typeof(T) == typeof(List<string>))
            return (T)(object)new List<string> { "excluded_record" };
        if (keyPath == "catch_log_settings" && typeof(T) == typeof(List<string>))
            return (T)(object)new List<string> { "test_setting", "another_setting" };
        if (keyPath == "Crashlog_Settings_Exclude" && typeof(T) == typeof(List<string>))
            return (T)(object)new List<string> { "excluded_setting" };
        if (keyPath == "Crashlog_Plugins_Exclude" && typeof(T) == typeof(List<string>))
            return (T)(object)new List<string> { "ignored.esp" };
        if (keyPath == "suspects_error_list" && typeof(T) == typeof(Dictionary<string, string>))
            return (T)(object)new Dictionary<string, string>
            {
                { "HIGH | Access Violation", "access violation" },
                { "MEDIUM | Null Pointer", "null pointer" },
                { "LOW | Memory Error", "memory error" }
            };
        if (keyPath == "suspects_stack_list" && typeof(T) == typeof(Dictionary<string, List<string>>))
            return (T)(object)new Dictionary<string, List<string>>
            {
                { "HIGH | Stack Overflow", new List<string> { "stack overflow", "ME-REQ|overflow" } },
                { "MEDIUM | Invalid Handle", new List<string> { "invalid handle", "2|bad handle" } },
                { "LOW | Debug Assert", new List<string> { "debug assert", "NOT|release mode" } }
            };
        if (keyPath == "CLASSIC_Settings.Show FormID Values" && typeof(T) == typeof(bool)) return (T)(object)true;
        if (keyPath == "Game_Info.CRASHGEN_LogName" && typeof(T) == typeof(string)) return (T)(object)"Buffout 4";
        return defaultValue;
    }

    /// Updates or sets a specific setting in a YAML file at the specified key path.
    /// <typeparam name="T">The type of the setting value to be updated or set.</typeparam>
    /// <param name="yamlFile">The path or name of the YAML file where the setting is stored.</param>
    /// <param name="keyPath">The key path within the YAML file where the setting is to be updated or added.</param>
    /// <param name="value">The value to be set for the specified key path in the YAML file.</param>
    public void SetSetting<T>(string yamlFile, string keyPath, T value)
    {
        // Test implementation - do nothing
    }

    /// Loads and deserializes data from a YAML file into a specified object type.
    /// <typeparam name="T">The type of the object to deserialize the YAML content into. Must be a reference class.</typeparam>
    /// <param name="yamlFile">The path or name of the YAML file to load and parse.</param>
    /// <returns>An object of type <typeparamref name="T"/> populated with data from the YAML file if successful; otherwise, returns null.</returns>
    public T? LoadYaml<T>(string yamlFile) where T : class
    {
        if (yamlFile != "CLASSIC Fallout4" || typeof(T) != typeof(Dictionary<string, object>)) return null;
        var yamlData = new Dictionary<string, object>
        {
            ["Crashlog_Error_Check"] = new Dictionary<object, object>
            {
                { "5 | Access Violation", "access violation" },
                { "4 | Null Pointer", "null pointer" },
                { "3 | Memory Error", "memory error" },
                { "6 | Stack Overflow Crash", "EXCEPTION_STACK_OVERFLOW" }
            },
            ["Crashlog_Stack_Check"] = new Dictionary<object, object>
            {
                { "5 | Stack Overflow", new List<object> { "stack overflow", "ME-REQ|overflow" } },
                { "4 | Invalid Handle", new List<object> { "invalid handle", "2|bad handle" } },
                { "3 | Debug Assert", new List<object> { "debug assert", "NOT|release mode" } }
            }
        };
        return (T)(object)yamlData;

    }

    /// Clears any cached settings or data within the provider implementation.
    /// This operation is useful to ensure the state of the provider is reset
    /// or to reload fresh settings when necessary.
    public void ClearCache()
    {
        // Test implementation - do nothing
    }
}

/// <summary>
/// A test implementation of the IFormIdDatabaseService interface for use in testing scenarios.
/// Provides methods for querying database entries and determining the existence of the database.
/// </summary>
public class TestFormIdDatabaseService : IFormIdDatabaseService
{
    public bool DatabaseExists => true;

    /// Retrieves a database entry based on the specified Form ID and plugin name.
    /// <param name="formId">The unique identifier of the form whose database entry is to be retrieved.</param>
    /// <param name="plugin">The name of the plugin associated with the specified Form ID.</param>
    /// <returns>The database entry as a string if the record is found; otherwise, null.</returns>
    public string? GetEntry(string formId, string plugin)
    {
        // Return a test entry for known FormIDs
        return formId == "0001A332" ? "TestLocation (CELL)" : null;
    }
}

/// <summary>
/// A test implementation of the ILogger interface for use in testing scenarios with generic type support.
/// Provides methods to log messages with specified log levels, event IDs, and formatting,
/// while allowing for testing and validation of logging behavior within test cases.
/// </summary>
/// <typeparam name="T">The type associated with the logger instance, used for categorization of log messages.</typeparam>
public class TestLogger<T> : ILogger<T>
{
    /// Creates a new logging scope with the specified state.
    /// This method is typically used to group log messages together within a logical operation or context.
    /// <typeparam name="TState">The type of the state object being used for the scope.</typeparam>
    /// <param name="state">The state object that defines the scope. This is used as part of log message formatting.</param>
    /// <returns>An IDisposable instance representing the created logging scope. The scope is disposed to end the logical operation.</returns>
    public IDisposable BeginScope<TState>(TState state) where TState : notnull
    {
        return null!;
    }

    /// Determines whether the logger instance is enabled for the specified log level.
    /// Used to check if log messages for a particular log level should be processed or ignored.
    /// <param name="logLevel">The log level to check.</param>
    /// <returns>true if the logger is enabled for the specified log level; otherwise, false.</returns>
    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    /// Logs a message with the specified log level, event ID, state, and exception.
    /// The formatter function is used to convert the state and exception into a log message string.
    /// This method is implemented for testing purposes and does not perform actual logging.
    /// <typeparam name="TState">The type of the state object to log.</typeparam>
    /// <param name="logLevel">The level of the log message (e.g., Information, Warning, Error).</param>
    /// <param name="eventId">A structure that identifies the event being logged.</param>
    /// <param name="state">The state object that includes information to log.</param>
    /// <param name="exception">An optional exception to log. Can be null if no exception is associated with the log entry.</param>
    /// <param name="formatter">A function that formats the state and exception into a string message to be logged.</param>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        // Test implementation - do nothing
    }
}

/// <summary>
/// A test implementation of the IMessageHandler interface, designed for use in testing scenarios.
/// Provides no-op methods for displaying messages of various types and simulating progress operations.
/// </summary>
public class TestMessageHandler : IMessageHandler
{
    /// Displays an informational message targeted at a specified audience or all targets by default.
    /// <param name="message">The informational message to be displayed or logged.</param>
    /// <param name="target">Specifies the target audience or medium for the message. Defaults to <see cref="MessageTarget.All"/>.</param>
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
    }

    /// Displays a warning message to the specified target(s).
    /// <param name="message">The warning message to display.</param>
    /// <param name="target">The target audience for the message. Defaults to all targets.</param>
    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
    }

    /// Displays an error message to the specified target destination(s).
    /// <param name="message">The error message to display.</param>
    /// <param name="target">Specifies where the message should be displayed. Defaults to <c>MessageTarget.All</c>.</param>
    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
    }

    /// Displays a success message to the specified target(s).
    /// <param name="message">The success message to be displayed.</param>
    /// <param name="target">The target destination(s) for the message. The default is <see cref="MessageTarget.All"/>.</param>
    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
    }

    /// Displays a debug message to the specified target(s).
    /// <param name="message">The debug message to display.</param>
    /// <param name="target">Specifies the target(s) to which the debug message should be sent. Defaults to all targets.</param>
    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
    }

    /// Displays a critical message to the specified target or targets.
    /// <param name="message">The critical message to display.</param>
    /// <param name="target">
    /// The target audience for the message. Defaults to `MessageTarget.All`,
    /// which sends the message to all available outputs.
    /// </param>
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
    }

    /// Displays a message with optional details, type, and target indication.
    /// <param name="message">The main message to be displayed.</param>
    /// <param name="details">Additional details or context for the message. This parameter is optional.</param>
    /// <param name="messageType">The type of the message, indicating its importance or nature. Defaults to <c>MessageType.Info</c>.</param>
    /// <param name="target">Specifies the target audience or system for the message. Defaults to <c>MessageTarget.All</c>.</param>
    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
    }

    /// Reports progress during an operation using the specified title and total item count.
    /// <param name="title">The title or description of the progress operation.</param>
    /// <param name="totalItems">The total number of items that the operation will process.</param>
    /// <returns>An instance of <see cref="IProgress{ProgressInfo}"/> to report progress updates.</returns>
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        return new Progress<ProgressInfo>();
    }

    /// Creates a progress context for tracking and reporting progress of a task.
    /// <param name="title">The title of the progress context, typically describing the task being tracked.</param>
    /// <param name="totalItems">The total number of items or units of work for the task.</param>
    /// <returns>An instance of <see cref="IProgressContext"/> for tracking progress.</returns>
    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new TestProgressContext();
    }
}

/// <summary>
/// A test implementation of the ICacheManager interface for use in testing scenarios.
/// Provides methods for simulating caching behavior for analysis results and other test-related operations.
/// </summary>
public class TestCacheManager : ICacheManager
{
    private readonly Dictionary<string, AnalysisResult> _cache = new();

    /// Retrieves a cached analysis result based on the specified file path and analyzer name.
    /// <param name="filePath">The path of the file for which the analysis result was cached.</param>
    /// <param name="analyzerName">The name of the analyzer that generated the cached analysis result.</param>
    /// <returns>The cached analysis result if found; otherwise, null.</returns>
    public AnalysisResult? GetCachedAnalysisResult(string filePath, string analyzerName)
    {
        var key = $"{filePath}:{analyzerName}";
        return _cache.TryGetValue(key, out var result) ? result : null;
    }

    /// Caches the analysis result for a specific file and analyzer combination.
    /// <param name="filePath">The file path that was analyzed.</param>
    /// <param name="analyzerName">The name of the analyzer used for the analysis.</param>
    /// <param name="result">The analysis result to be cached.</param>
    public void CacheAnalysisResult(string filePath, string analyzerName, AnalysisResult result)
    {
        var key = $"{filePath}:{analyzerName}";
        _cache[key] = result;
    }

    /// Determines whether the file cache is valid for the specified file path.
    /// <param name="filePath">The path of the file to check cache validity for.</param>
    /// <returns>True if the file cache is valid; otherwise, false.</returns>
    public bool IsFileCacheValid(string filePath)
    {
        return true; // For testing, always consider cache valid
    }

    /// Retrieves the current statistics of the cache, including hit rate, total cached files, and hit/miss counts.
    /// <returns>A CacheStatistics object containing details about the cache's performance and state.</returns>
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

    /// Clears all entries within the cache.
    /// Removes all cached data, ensuring that the cache is empty and ready to be repopulated.
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// Retrieves or sets a YAML setting value based on the provided key path, using a factory function for initialization if the value does not exist.
    /// <typeparam name="T">The type of the setting value to be returned or stored.</typeparam>
    /// <param name="yamlFile">The path or name of the YAML file containing the settings.</param>
    /// <param name="keyPath">The key path within the YAML file to locate the setting.</param>
    /// <param name="factory">A function that generates the value in case it is not already available.</param>
    /// <param name="expiry">An optional expiration duration for the cached value. If null, no expiration is applied.</param>
    /// <returns>The value of the setting of the specified type if found or generated; otherwise, returns null if the factory does not produce a value.</returns>
    public T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null)
    {
        // For testing, just call the factory
        return factory();
    }
}

/// <summary>
/// A test implementation of the IErrorHandlingPolicy interface for use in testing scenarios.
/// Provides methods for simulating retry logic, error handling, and continuation behavior in tests.
/// </summary>
public class TestErrorHandlingPolicy : IErrorHandlingPolicy
{
    /// Determines whether a retry should be attempted based on the provided exception and the current attempt count.
    /// <param name="exception">The exception that occurred, which may provide contextual information about the error.</param>
    /// <param name="attemptCount">The current number of attempts made so far.</param>
    /// <returns>True if a retry should be attempted; otherwise, false.</returns>
    public bool ShouldRetry(Exception exception, int attemptCount)
    {
        return attemptCount < 3; // Retry up to 3 times for testing
    }

    /// Handles an error based on the provided exception, context, and attempt count, returning an object
    /// that specifies the action to take and additional details regarding the error handling result.
    /// <param name="exception">The exception that occurred and needs to be handled.</param>
    /// <param name="context">The context or operation during which the exception occurred.</param>
    /// <param name="attemptCount">The number of attempts made prior to this error handling invocation.</param>
    /// <returns>An instance of <see cref="ErrorHandlingResult"/> containing the action to take, an optional message, and other error handling details.</returns>
    public ErrorHandlingResult HandleError(Exception exception, string context, int attemptCount)
    {
        return new ErrorHandlingResult
        {
            Action = ErrorAction.Continue,
            Message = "Test error handling"
        };
    }

    /// Calculates the delay duration before the next retry attempt based on the current attempt count.
    /// <param name="attemptCount">The number of retry attempts made so far.</param>
    /// <returns>A TimeSpan representing the delay duration before the next retry attempt.</returns>
    public TimeSpan GetRetryDelay(int attemptCount)
    {
        return TimeSpan.FromMilliseconds(10); // Short delay for testing
    }

    /// Determines whether the operation should continue execution after an error has occurred.
    /// <param name="exception">The exception that occurred, providing details about the error encountered.</param>
    /// <returns>True if the operation should continue despite the error; otherwise, false.</returns>
    public bool ShouldContinueOnError(Exception exception)
    {
        return true; // Continue on errors for testing
    }
}

public class TestProgressContext : IProgressContext
{
    public void Dispose()
    {
    }

    public void Report(ProgressInfo value)
    {
    }

    public void Update(int current, string message)
    {
    }

    public void Complete()
    {
    }
}
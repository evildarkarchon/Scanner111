using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.FCX;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;
using Scanner111.Core.Pipeline;
using GameVersionInfo = Scanner111.Core.Models.Yaml.GameVersionInfo;

namespace Scanner111.Tests.TestHelpers;

/// <summary>
///     A test implementation of the IYamlSettingsProvider interface for use in testing scenarios.
///     Provides methods for accessing and manipulating YAML-based settings within tests.
/// </summary>
public class TestYamlSettingsProvider : IYamlSettingsProvider
{
    /// Loads and deserializes data from a YAML file into a specified object type.
    /// <typeparam name="T">The type of the object to deserialize the YAML content into. Must be a reference class.</typeparam>
    /// <param name="yamlFile">The path or name of the YAML file to load and parse.</param>
    /// <returns>
    ///     An object of type <typeparamref name="T" /> populated with data from the YAML file if successful; otherwise,
    ///     returns null.
    /// </returns>
    public T? LoadYaml<T>(string yamlFile) where T : class
    {
        return yamlFile switch
        {
            "CLASSIC Main" when typeof(T) == typeof(ClassicMainYaml) => CreateTestMainYaml() as T,
            "CLASSIC Fallout4" when typeof(T) == typeof(ClassicFallout4YamlV2) => CreateTestFallout4YamlV2() as T,
            "CLASSIC Fallout4" when typeof(T) == typeof(Dictionary<string, object>) =>
                CreateTestFallout4Dictionary() as T,
            _ => null
        };
    }

    public Task<T?> LoadYamlAsync<T>(string yamlFile) where T : class
    {
        return Task.FromResult(LoadYaml<T>(yamlFile));
    }

    /// Clears any cached settings or data within the provider implementation.
    /// This operation is useful to ensure the state of the provider is reset
    /// or to reload fresh settings when necessary.
    public void ClearCache()
    {
        // Test implementation - do nothing
    }

    private ClassicMainYaml CreateTestMainYaml()
    {
        return new ClassicMainYaml
        {
            ClassicInfo = new ClassicInfo
            {
                Version = "CLASSIC v7.35.0",
                VersionDate = "25.06.11",
                IsPrerelease = true,
                DefaultSettings = "Test settings",
                DefaultLocalYaml = "Test local yaml",
                DefaultIgnorefile = "Test ignore file"
            },
            CatchLogRecords = new List<string> { ".bgsm", ".dds", ".dll+" },
            ExcludeLogRecords = new List<string> { "(Main*)", "(size_t)" },
            CatchLogErrors = new List<string> { "critical", "error", "failed" },
            ExcludeLogErrors = new List<string> { "failed to get next record", "failed to open pdb" },
            ExcludeLogFiles = new List<string> { "cbpfo4", "crash-", "CreationKit" }
        };
    }

    private ClassicFallout4YamlV2 CreateTestFallout4YamlV2()
    {
        return new ClassicFallout4YamlV2
        {
            GameInfo = new GameInfoV2
            {
                MainRootName = "Fallout 4",
                MainDocsName = "Fallout4",
                MainSteamId = 377160,
                CrashgenLogName = "Buffout 4",
                CrashgenIgnore = new List<string> { "F4EE", "WaitForDebugger", "Achievements" },
                Versions = new Dictionary<string, GameVersionInfo>
                {
                    ["pre_ng"] = new()
                    {
                        Name = "Pre-Next Gen",
                        BuffoutLatest = "v1.28.0",
                        GameVersion = "1.10.163.0"
                    },
                    ["next_gen"] = new()
                    {
                        Name = "Next Gen",
                        BuffoutLatest = "v1.28.0",
                        GameVersion = "1.10.984.0"
                    }
                }
            },
            CrashlogRecordsExclude = new List<string> { "\"\"", "...", "FE:" },
            CrashlogPluginsExclude = new List<string> { "Buffout4.dll", "Fallout4.esm", "DLCCoast.esm", "ignored.esp" },
            CrashlogErrorCheck = new Dictionary<string, string>
            {
                { "5 | Access Violation", "access violation" },
                { "4 | Null Pointer", "null pointer" },
                { "3 | Memory Error", "memory error" },
                { "6 | Stack Overflow Crash", "EXCEPTION_STACK_OVERFLOW" }
            },
            CrashlogStackCheck = new Dictionary<string, List<string>>
            {
                { "5 | Stack Overflow", new List<string> { "stack overflow", "ME-REQ|overflow" } },
                { "4 | Invalid Handle", new List<string> { "invalid handle", "2|bad handle" } },
                { "3 | Debug Assert", new List<string> { "debug assert", "NOT|release mode" } }
            }
        };
    }

    private Dictionary<string, object> CreateTestFallout4Dictionary()
    {
        return new Dictionary<string, object>
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
    }
}

/// <summary>
///     A test implementation of the IFormIdDatabaseService interface for use in testing scenarios.
///     Provides methods for querying database entries and determining the existence of the database.
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
///     A test implementation of the ILogger interface for use in testing scenarios with generic type support.
///     Provides methods to log messages with specified log levels, event IDs, and formatting,
///     while allowing for testing and validation of logging behavior within test cases.
/// </summary>
/// <typeparam name="T">The type associated with the logger instance, used for categorization of log messages.</typeparam>
public class TestLogger<T> : ILogger<T>
{
    /// Creates a new logging scope with the specified state.
    /// This method is typically used to group log messages together within a logical operation or context.
    /// <typeparam name="TState">The type of the state object being used for the scope.</typeparam>
    /// <param name="state">The state object that defines the scope. This is used as part of log message formatting.</param>
    /// <returns>
    ///     An IDisposable instance representing the created logging scope. The scope is disposed to end the logical
    ///     operation.
    /// </returns>
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
///     A test implementation of the IMessageHandler interface, designed for use in testing scenarios.
///     Provides no-op methods for displaying messages of various types and simulating progress operations.
/// </summary>
public class TestMessageHandler : IMessageHandler
{
    /// Displays an informational message targeted at a specified audience or all targets by default.
    /// <param name="message">The informational message to be displayed or logged.</param>
    /// <param name="target">
    ///     Specifies the target audience or medium for the message. Defaults to
    ///     <see cref="MessageTarget.All" />.
    /// </param>
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
    /// <param name="target">The target destination(s) for the message. The default is <see cref="MessageTarget.All" />.</param>
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
    ///     The target audience for the message. Defaults to `MessageTarget.All`,
    ///     which sends the message to all available outputs.
    /// </param>
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
    }

    /// Displays a message with optional details, type, and target indication.
    /// <param name="message">The main message to be displayed.</param>
    /// <param name="details">Additional details or context for the message. This parameter is optional.</param>
    /// <param name="messageType">
    ///     The type of the message, indicating its importance or nature. Defaults to
    ///     <c>MessageType.Info</c>.
    /// </param>
    /// <param name="target">Specifies the target audience or system for the message. Defaults to <c>MessageTarget.All</c>.</param>
    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
    }

    /// Reports progress during an operation using the specified title and total item count.
    /// <param name="title">The title or description of the progress operation.</param>
    /// <param name="totalItems">The total number of items that the operation will process.</param>
    /// <returns>An instance of <see cref="IProgress{ProgressInfo}" /> to report progress updates.</returns>
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        return new Progress<ProgressInfo>();
    }

    /// Creates a progress context for tracking and reporting progress of a task.
    /// <param name="title">The title of the progress context, typically describing the task being tracked.</param>
    /// <param name="totalItems">The total number of items or units of work for the task.</param>
    /// <returns>An instance of <see cref="IProgressContext" /> for tracking progress.</returns>
    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new TestProgressContext();
    }
}

/// <summary>
///     A test implementation of the ICacheManager interface for use in testing scenarios.
///     Provides methods for simulating caching behavior for analysis results and other test-related operations.
/// </summary>
public class TestCacheManager : ICacheManager
{
    private readonly ConcurrentDictionary<string, AnalysisResult> _cache = new();
    private readonly ConcurrentDictionary<string, object> _yamlCache = new();

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
        _yamlCache.Clear();
    }

    /// Retrieves or sets a YAML setting value based on the provided key path, using a factory function for initialization if the value does not exist.
    /// <typeparam name="T">The type of the setting value to be returned or stored.</typeparam>
    /// <param name="yamlFile">The path or name of the YAML file containing the settings.</param>
    /// <param name="keyPath">The key path within the YAML file to locate the setting.</param>
    /// <param name="factory">A function that generates the value in case it is not already available.</param>
    /// <param name="expiry">An optional expiration duration for the cached value. If null, no expiration is applied.</param>
    /// <returns>
    ///     The value of the setting of the specified type if found or generated; otherwise, returns null if the factory
    ///     does not produce a value.
    /// </returns>
    public T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null)
    {
        var cacheKey = $"{yamlFile}:{keyPath}";

        if (_yamlCache.TryGetValue(cacheKey, out var cached)) return (T?)cached;

        var result = factory();
        if (result != null) _yamlCache[cacheKey] = result;

        return result;
    }

    public async Task<T?> GetOrSetYamlSettingAsync<T>(string yamlFile, string keyPath, Func<Task<T?>> factory,
        TimeSpan? expiry = null)
    {
        var cacheKey = $"{yamlFile}:{keyPath}";

        if (_yamlCache.TryGetValue(cacheKey, out var cached)) return (T?)cached;

        var result = await factory().ConfigureAwait(false);
        if (result != null) _yamlCache[cacheKey] = result;

        return result;
    }
}

/// <summary>
///     A test implementation of the IErrorHandlingPolicy interface for use in testing scenarios.
///     Provides methods for simulating retry logic, error handling, and continuation behavior in tests.
/// </summary>
public class TestErrorHandlingPolicy : IErrorHandlingPolicy
{
    /// Handles an error based on the provided exception, context, and attempt count, returning an object
    /// that specifies the action to take and additional details regarding the error handling result.
    /// <param name="exception">The exception that occurred and needs to be handled.</param>
    /// <param name="context">The context or operation during which the exception occurred.</param>
    /// <param name="attemptCount">The number of attempts made prior to this error handling invocation.</param>
    /// <returns>
    ///     An instance of <see cref="ErrorHandlingResult" /> containing the action to take, an optional message, and
    ///     other error handling details.
    /// </returns>
    public ErrorHandlingResult HandleError(Exception exception, string context, int attemptCount)
    {
        return new ErrorHandlingResult
        {
            Action = ErrorAction.Continue,
            Message = "Test error handling"
        };
    }

    /// Determines whether a retry should be attempted based on the provided exception and the current attempt count.
    /// <param name="exception">The exception that occurred, which may provide contextual information about the error.</param>
    /// <param name="attemptCount">The current number of attempts made so far.</param>
    /// <returns>True if a retry should be attempted; otherwise, false.</returns>
    public bool ShouldRetry(Exception exception, int attemptCount)
    {
        return attemptCount < 3; // Retry up to 3 times for testing
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
    public TestProgressContext(string description = "", int total = 100)
    {
        Description = description;
        Total = total;
    }

    public string Description { get; }
    public int Total { get; }
    public bool IsCompleted { get; private set; }
    public int CurrentValue { get; private set; }
    public string LastMessage { get; private set; } = string.Empty;

    public void Update(int current, string message)
    {
        CurrentValue = current;
        LastMessage = message;
    }

    public void Complete()
    {
        IsCompleted = true;
    }

    public void Report(ProgressInfo value)
    {
        CurrentValue = value.Current;
        LastMessage = value.Message;
    }

    public void Dispose()
    {
    }
}

/// <summary>
///     A test implementation of the IApplicationSettingsService interface for use in testing scenarios.
///     Provides methods for simulating application settings behavior in tests.
/// </summary>
public class TestApplicationSettingsService : IApplicationSettingsService
{
    public ApplicationSettings Settings { get; } = new()
    {
        ShowFormIdValues = true,
        FcxMode = false,
        SimplifyLogs = false,
        MoveUnsolvedLogs = true,
        VrMode = false
    };

    public Task<ApplicationSettings> LoadSettingsAsync()
    {
        return Task.FromResult(Settings);
    }

    public Task SaveSettingsAsync(ApplicationSettings settings)
    {
        // Update all properties from the provided settings
        Settings.FcxMode = settings.FcxMode;
        Settings.ShowFormIdValues = settings.ShowFormIdValues;
        Settings.SimplifyLogs = settings.SimplifyLogs;
        Settings.MoveUnsolvedLogs = settings.MoveUnsolvedLogs;
        Settings.VrMode = settings.VrMode;
        Settings.DefaultGamePath = settings.DefaultGamePath;
        Settings.ModsFolder = settings.ModsFolder;
        Settings.DefaultLogPath = settings.DefaultLogPath;
        Settings.GamePath = settings.GamePath;
        Settings.DefaultScanDirectory = settings.DefaultScanDirectory;
        Settings.CrashLogsDirectory = settings.CrashLogsDirectory;
        Settings.BackupDirectory = settings.BackupDirectory;
        Settings.IniFolder = settings.IniFolder;
        return Task.CompletedTask;
    }

    public Task SaveSettingAsync(string key, object value)
    {
        return Task.CompletedTask;
    }

    public ApplicationSettings GetDefaultSettings()
    {
        return new ApplicationSettings();
    }
}

/// <summary>
///     Test implementation of IScanPipeline for testing purposes
/// </summary>
public class TestScanPipeline : IScanPipeline
{
    private readonly List<ScanResult> _results = new();
    private readonly Dictionary<string, ScanResult> _resultsByPath = new();

    public List<string> ProcessedPaths { get; } = new();
    public bool IsDisposed { get; private set; }

    public virtual Task<ScanResult> ProcessSingleAsync(string logPath, CancellationToken cancellationToken = default)
    {
        ProcessedPaths.Add(logPath);

        // First check if we have a result specifically for this path
        if (_resultsByPath.TryGetValue(logPath, out var specificResult)) return Task.FromResult(specificResult);

        // If no specific result, return the first available result (for single result scenarios)
        // but update its LogPath to match the requested path
        var result = _results.FirstOrDefault();
        if (result != null)
            // Clone the result with the correct path
            result = new ScanResult
            {
                LogPath = logPath,
                AnalysisResults = result.AnalysisResults,
                CrashLog = result.CrashLog,
                ErrorMessages = result.ErrorMessages,
                ProcessingTime = result.ProcessingTime,
                Report = result.Report,
                Statistics = result.Statistics,
                Status = result.Status,
                WasCopiedFromXse = result.WasCopiedFromXse
            };
        else
            // Default empty result
            result = new ScanResult
            {
                LogPath = logPath,
                AnalysisResults = new List<AnalysisResult>()
            };

        return Task.FromResult(result);
    }

    public async IAsyncEnumerable<ScanResult> ProcessBatchAsync(
        IEnumerable<string> logPaths,
        Core.Pipeline.ScanOptions? options = null,
        IProgress<BatchProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var path in logPaths)
        {
            ProcessedPaths.Add(path);
            cancellationToken.ThrowIfCancellationRequested();

            var result = _results.FirstOrDefault() ?? new ScanResult
            {
                LogPath = path,
                AnalysisResults = new List<AnalysisResult>()
            };

            yield return result;
            await Task.Yield();
        }
    }

    public ValueTask DisposeAsync()
    {
        IsDisposed = true;
        return ValueTask.CompletedTask;
    }

    public void SetResult(ScanResult result)
    {
        _results.Clear();
        _resultsByPath.Clear();
        _results.Add(result);
        if (!string.IsNullOrEmpty(result.LogPath)) _resultsByPath[result.LogPath] = result;
    }

    public void SetBatchResults(IEnumerable<ScanResult> results)
    {
        _results.Clear();
        _resultsByPath.Clear();
        _results.AddRange(results);
        foreach (var result in _results)
            if (!string.IsNullOrEmpty(result.LogPath))
                _resultsByPath[result.LogPath] = result;
    }

    public void Dispose()
    {
        IsDisposed = true;
    }
}

/// <summary>
///     Test implementation of IModScanner for unit testing FCX analyzers
/// </summary>
public class TestModScanner : IModScanner
{
    private readonly ModScanResult _result;

    public TestModScanner()
    {
        _result = new ModScanResult();
    }

    public TestModScanner(ModScanResult result)
    {
        _result = result;
    }

    public Task<ModScanResult> ScanUnpackedModsAsync(string modPath, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(_result);
    }

    public Task<ModScanResult> ScanArchivedModsAsync(string modPath, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        return Task.FromResult(_result);
    }

    public Task<ModScanResult> ScanAllModsAsync(string modPath, IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        _result.TotalFilesScanned = 100;
        _result.TotalArchivesScanned = 10;
        return Task.FromResult(_result);
    }

    public void AddIssue(ModIssue issue)
    {
        _result.Issues.Add(issue);
    }
}

/// <summary>
///     Test implementation of IHashValidationService for unit testing
/// </summary>
public class TestHashValidationService : IHashValidationService
{
    private readonly Dictionary<string, string> _fileHashes = new();
    private readonly Dictionary<string, HashValidation> _validationResults = new();

    public Task<string> CalculateFileHashAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_fileHashes.TryGetValue(filePath, out var hash) ? hash : "default_hash");
    }

    public Task<HashValidation> ValidateFileAsync(string filePath, string expectedHash,
        CancellationToken cancellationToken = default)
    {
        if (_validationResults.TryGetValue(filePath, out var result)) return Task.FromResult(result);

        var actualHash = _fileHashes.TryGetValue(filePath, out var hash) ? hash : "different_hash";
        return Task.FromResult(new HashValidation
        {
            FilePath = filePath,
            ExpectedHash = expectedHash,
            ActualHash = actualHash,
            HashType = "SHA256"
        });
    }

    public Task<Dictionary<string, HashValidation>> ValidateBatchAsync(Dictionary<string, string> fileHashMap,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HashValidation>();
        foreach (var kvp in fileHashMap)
            results[kvp.Key] = ValidateFileAsync(kvp.Key, kvp.Value, cancellationToken).Result;
        return Task.FromResult(results);
    }

    public Task<string> CalculateFileHashWithProgressAsync(string filePath, IProgress<long>? progress,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(100);
        return CalculateFileHashAsync(filePath, cancellationToken);
    }

    public void SetFileHash(string filePath, string hash)
    {
        _fileHashes[filePath] = hash;
    }

    public void SetValidationResult(string filePath, HashValidation result)
    {
        _validationResults[filePath] = result;
    }
}
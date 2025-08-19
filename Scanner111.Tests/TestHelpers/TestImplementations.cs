using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Scanner111.Core.Abstractions;
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
    public ApplicationSettings Settings { get; set; } = new()
    {
        ShowFormIdValues = true,
        FcxMode = false,
        SimplifyLogs = false,
        MoveUnsolvedLogs = true,
        VrMode = false,
        DefaultOutputFormat = "text",
        AutoLoadF4SeLogs = true,
        MaxLogMessages = 100,
        WindowWidth = 1200,
        WindowHeight = 800
    };

    public Task<ApplicationSettings> LoadSettingsAsync()
    {
        return Task.FromResult(Settings);
    }

    public Task SaveSettingsAsync(ApplicationSettings settings)
    {
        // Deep copy all properties
        Settings = new ApplicationSettings
        {
            FcxMode = settings.FcxMode,
            ShowFormIdValues = settings.ShowFormIdValues,
            SimplifyLogs = settings.SimplifyLogs,
            MoveUnsolvedLogs = settings.MoveUnsolvedLogs,
            VrMode = settings.VrMode,
            DefaultGamePath = settings.DefaultGamePath,
            ModsFolder = settings.ModsFolder,
            DefaultLogPath = settings.DefaultLogPath,
            GamePath = settings.GamePath,
            DefaultScanDirectory = settings.DefaultScanDirectory,
            CrashLogsDirectory = settings.CrashLogsDirectory,
            BackupDirectory = settings.BackupDirectory,
            IniFolder = settings.IniFolder,
            AutoLoadF4SeLogs = settings.AutoLoadF4SeLogs,
            MaxLogMessages = settings.MaxLogMessages,
            EnableProgressNotifications = settings.EnableProgressNotifications,
            RememberWindowSize = settings.RememberWindowSize,
            WindowWidth = settings.WindowWidth,
            WindowHeight = settings.WindowHeight,
            EnableDebugLogging = settings.EnableDebugLogging,
            MaxRecentItems = settings.MaxRecentItems,
            AutoSaveResults = settings.AutoSaveResults,
            DefaultOutputFormat = settings.DefaultOutputFormat,
            GameType = settings.GameType,
            SkipXseCopy = settings.SkipXseCopy,
            DisableColors = settings.DisableColors,
            DisableProgress = settings.DisableProgress,
            VerboseLogging = settings.VerboseLogging,
            EnableUpdateCheck = settings.EnableUpdateCheck,
            UpdateSource = settings.UpdateSource,
            AudioNotifications = settings.AudioNotifications,
            EnableAudioNotifications = settings.EnableAudioNotifications,
            AudioVolume = settings.AudioVolume,
            CustomNotificationSounds = settings.CustomNotificationSounds,
            EnablePapyrusMonitoring = settings.EnablePapyrusMonitoring,
            PapyrusLogPath = settings.PapyrusLogPath,
            PapyrusMonitorInterval = settings.PapyrusMonitorInterval,
            PapyrusErrorThreshold = settings.PapyrusErrorThreshold,
            PapyrusWarningThreshold = settings.PapyrusWarningThreshold,
            PapyrusAutoExport = settings.PapyrusAutoExport,
            PapyrusExportPath = settings.PapyrusExportPath,
            PapyrusHistoryLimit = settings.PapyrusHistoryLimit,
            AutoDetectModManagers = settings.AutoDetectModManagers,
            DefaultModManager = settings.DefaultModManager,
            MO2InstallPath = settings.MO2InstallPath,
            MO2DefaultProfile = settings.MO2DefaultProfile,
            VortexDataPath = settings.VortexDataPath,
            ModManagerSettings = settings.ModManagerSettings,
            EnableUnicodeDisplay = settings.EnableUnicodeDisplay,
            RecentLogFiles = settings.RecentLogFiles ?? new List<string>(),
            RecentGamePaths = settings.RecentGamePaths ?? new List<string>(),
            RecentScanDirectories = settings.RecentScanDirectories ?? new List<string>(),
            LastUsedAnalyzers = settings.LastUsedAnalyzers ?? new List<string>(),
            MaxConcurrentScans = settings.MaxConcurrentScans,
            CacheEnabled = settings.CacheEnabled,
            PluginsFolder = settings.PluginsFolder,
            GameExecutablePath = settings.GameExecutablePath
        };
        return Task.CompletedTask;
    }

    public Task SaveSettingAsync(string key, object value)
    {
        var property = typeof(ApplicationSettings).GetProperty(key);
        if (property != null && property.CanWrite)
        {
            property.SetValue(Settings, value);
        }
        return Task.CompletedTask;
    }

    public ApplicationSettings GetDefaultSettings()
    {
        return new ApplicationSettings
        {
            DefaultLogPath = string.Empty,
            AutoLoadF4SeLogs = true,
            MaxLogMessages = 100,
            EnableProgressNotifications = true,
            RememberWindowSize = true,
            WindowWidth = 1200,
            WindowHeight = 800,
            EnableDebugLogging = false,
            MaxRecentItems = 10,
            AutoSaveResults = true,
            DefaultOutputFormat = "detailed",
            ShowFormIdValues = false,
            FcxMode = false,
            SimplifyLogs = false,
            MoveUnsolvedLogs = false,
            VrMode = false,
            SkipXseCopy = false
        };
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
        ScanOptions? options = null,
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

    public async Task<Dictionary<string, HashValidation>> ValidateBatchAsync(Dictionary<string, string> fileHashMap,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, HashValidation>();
        foreach (var kvp in fileHashMap)
            results[kvp.Key] = await ValidateFileAsync(kvp.Key, kvp.Value, cancellationToken);
        return results;
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

/// <summary>
///     Test implementation of IFileSystem for unit testing
/// </summary>
public class TestFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new();
    private readonly HashSet<string> _directories = new();
    private readonly Dictionary<string, DateTime> _lastWriteTimes = new();
    private readonly Dictionary<string, long> _fileSizes = new();

    public TestFileSystem()
    {
        // Add some default directories
        _directories.Add(@"C:\");
        _directories.Add(@"C:\Windows");
        _directories.Add(@"C:\Program Files");
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(NormalizePath(path));
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(NormalizePath(path));
    }

    public string[] GetFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var normalizedPath = NormalizePath(path);
        var files = _files.Keys
            .Where(f => f.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
            .Where(f => MatchesPattern(Path.GetFileName(f), searchPattern));

        if (searchOption == SearchOption.TopDirectoryOnly)
        {
            files = files.Where(f => Path.GetDirectoryName(f) == normalizedPath);
        }

        return files.ToArray();
    }

    public string[] GetDirectories(string path)
    {
        var normalizedPath = NormalizePath(path);
        return _directories
            .Where(d => d != normalizedPath && d.StartsWith(normalizedPath, StringComparison.OrdinalIgnoreCase))
            .Where(d => d.Count(c => c == '\\') == normalizedPath.Count(c => c == '\\') + 1)
            .ToArray();
    }

    public Stream OpenRead(string path)
    {
        if (!_files.TryGetValue(NormalizePath(path), out var content))
            throw new FileNotFoundException($"File not found: {path}");
        
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    public Stream OpenWrite(string path)
    {
        return new TestMemoryStream(this, NormalizePath(path));
    }

    public void CreateDirectory(string path)
    {
        _directories.Add(NormalizePath(path));
    }

    public void DeleteFile(string path)
    {
        var normalized = NormalizePath(path);
        _files.Remove(normalized);
        _lastWriteTimes.Remove(normalized);
        _fileSizes.Remove(normalized);
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        var normalized = NormalizePath(path);
        _directories.Remove(normalized);
        
        if (recursive)
        {
            var toRemove = _directories.Where(d => d.StartsWith(normalized + "\\")).ToList();
            foreach (var dir in toRemove)
                _directories.Remove(dir);
        }
    }

    public void CopyFile(string source, string destination, bool overwrite = false)
    {
        var normalizedSource = NormalizePath(source);
        var normalizedDest = NormalizePath(destination);
        
        if (!_files.ContainsKey(normalizedSource))
            throw new FileNotFoundException($"Source file not found: {source}");
            
        if (_files.ContainsKey(normalizedDest) && !overwrite)
            throw new IOException($"Destination file already exists: {destination}");
            
        _files[normalizedDest] = _files[normalizedSource];
        if (_lastWriteTimes.ContainsKey(normalizedSource))
            _lastWriteTimes[normalizedDest] = DateTime.Now;
        if (_fileSizes.ContainsKey(normalizedSource))
            _fileSizes[normalizedDest] = _fileSizes[normalizedSource];
    }

    public void MoveFile(string source, string destination)
    {
        CopyFile(source, destination, true);
        DeleteFile(source);
    }

    public DateTime GetLastWriteTime(string path)
    {
        var normalized = NormalizePath(path);
        return _lastWriteTimes.TryGetValue(normalized, out var time) ? time : DateTime.Now;
    }

    public long GetFileSize(string path)
    {
        var normalized = NormalizePath(path);
        if (_fileSizes.TryGetValue(normalized, out var size))
            return size;
        if (_files.TryGetValue(normalized, out var content))
            return content.Length;
        return 0;
    }

    public string ReadAllText(string path)
    {
        if (!_files.TryGetValue(NormalizePath(path), out var content))
            throw new FileNotFoundException($"File not found: {path}");
        return content;
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(ReadAllText(path));
    }

    public void WriteAllText(string path, string content)
    {
        var normalized = NormalizePath(path);
        _files[normalized] = content;
        _lastWriteTimes[normalized] = DateTime.Now;
        _fileSizes[normalized] = content?.Length ?? 0;
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        WriteAllText(path, content);
        return Task.CompletedTask;
    }

    // Helper methods for test setup
    public void AddFile(string path, string content = "")
    {
        var normalized = NormalizePath(path);
        _files[normalized] = content;
        _lastWriteTimes[normalized] = DateTime.Now;
        _fileSizes[normalized] = content.Length;
    }

    public void AddDirectory(string path)
    {
        _directories.Add(NormalizePath(path));
    }

    public void SetLastWriteTime(string path, DateTime time)
    {
        _lastWriteTimes[NormalizePath(path)] = time;
    }

    public void SetFileSize(string path, long size)
    {
        _fileSizes[NormalizePath(path)] = size;
    }

    private string NormalizePath(string path)
    {
        return path?.Replace('/', '\\').TrimEnd('\\') ?? string.Empty;
    }

    private bool MatchesPattern(string fileName, string pattern)
    {
        if (pattern == "*" || pattern == "*.*")
            return true;
            
        if (pattern.StartsWith("*."))
            return fileName.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
            
        return fileName.Equals(pattern, StringComparison.OrdinalIgnoreCase);
    }

    private class TestMemoryStream : MemoryStream
    {
        private readonly TestFileSystem _fileSystem;
        private readonly string _path;

        public TestMemoryStream(TestFileSystem fileSystem, string path)
        {
            _fileSystem = fileSystem;
            _path = path;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var content = Encoding.UTF8.GetString(ToArray());
                _fileSystem._files[_path] = content;
                _fileSystem._lastWriteTimes[_path] = DateTime.Now;
                _fileSystem._fileSizes[_path] = content.Length;
            }
            base.Dispose(disposing);
        }
    }
}

/// <summary>
///     Test implementation of IPathService for unit testing
/// </summary>
public class TestPathService : IPathService
{
    public string Combine(params string[] paths)
    {
        return Path.Combine(paths);
    }

    public string? GetDirectoryName(string path)
    {
        return Path.GetDirectoryName(path);
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path);
    }

    public string GetFileNameWithoutExtension(string path)
    {
        return Path.GetFileNameWithoutExtension(path);
    }

    public string GetExtension(string path)
    {
        return Path.GetExtension(path);
    }

    public string GetFullPath(string path)
    {
        // For testing, just return the path as-is
        return path;
    }

    public string NormalizePath(string path)
    {
        return path?.Replace('/', '\\').TrimEnd('\\') ?? string.Empty;
    }

    public bool IsPathRooted(string path)
    {
        return Path.IsPathRooted(path);
    }

    public char DirectorySeparatorChar => '\\';
}

/// <summary>
///     Test implementation of IEnvironmentPathProvider for unit testing
/// </summary>
public class TestEnvironmentPathProvider : IEnvironmentPathProvider
{
    private readonly Dictionary<string, string> _environmentVariables = new();
    private readonly Dictionary<Environment.SpecialFolder, string> _specialFolders = new();

    public TestEnvironmentPathProvider()
    {
        // Set up default paths
        _specialFolders[Environment.SpecialFolder.MyDocuments] = @"C:\Users\TestUser\Documents";
        _specialFolders[Environment.SpecialFolder.ApplicationData] = @"C:\Users\TestUser\AppData\Roaming";
        _specialFolders[Environment.SpecialFolder.LocalApplicationData] = @"C:\Users\TestUser\AppData\Local";
        _specialFolders[Environment.SpecialFolder.ProgramFiles] = @"C:\Program Files";
        _specialFolders[Environment.SpecialFolder.ProgramFilesX86] = @"C:\Program Files (x86)";
        
        _environmentVariables["USERPROFILE"] = @"C:\Users\TestUser";
        _environmentVariables["APPDATA"] = @"C:\Users\TestUser\AppData\Roaming";
        _environmentVariables["LOCALAPPDATA"] = @"C:\Users\TestUser\AppData\Local";
    }

    public string GetFolderPath(Environment.SpecialFolder folder)
    {
        return _specialFolders.TryGetValue(folder, out var path) ? path : string.Empty;
    }

    public string? GetEnvironmentVariable(string variable)
    {
        return _environmentVariables.TryGetValue(variable, out var value) ? value : null;
    }

    public string ExpandEnvironmentVariables(string name)
    {
        var result = name;
        foreach (var kvp in _environmentVariables)
        {
            result = result.Replace($"%{kvp.Key}%", kvp.Value);
        }
        return result;
    }

    public string CurrentDirectory => @"C:\Users\TestUser\Projects";
    public string TempPath => @"C:\Users\TestUser\AppData\Local\Temp";
    public string UserName => "TestUser";
    public string MachineName => "TESTMACHINE";

    // Helper methods for test setup
    public void SetSpecialFolder(Environment.SpecialFolder folder, string path)
    {
        _specialFolders[folder] = path;
    }

    public void SetEnvironmentVariable(string variable, string value)
    {
        _environmentVariables[variable] = value;
    }
}

/// <summary>
///     Test implementation of IFileVersionInfoProvider for unit testing
/// </summary>
public class TestFileVersionInfoProvider : IFileVersionInfoProvider
{
    private readonly Dictionary<string, (string FileVersion, string ProductVersion)> _versionInfo = new();

    public FileVersionInfo GetVersionInfo(string fileName)
    {
        // Since we can't create FileVersionInfo directly, we'll throw an exception
        // Tests should use mocking frameworks for this interface when FileVersionInfo is needed
        throw new NotImplementedException("Use a mocking framework to mock IFileVersionInfoProvider when FileVersionInfo is needed");
    }

    public bool TryGetVersionInfo(string fileName, out FileVersionInfo? versionInfo)
    {
        // For test purposes, we always return false
        // Tests should use mocking frameworks when FileVersionInfo is needed
        versionInfo = null;
        return false;
    }

    // Helper methods for simple version string testing
    public void SetVersionInfo(string fileName, string fileVersion, string productVersion)
    {
        _versionInfo[fileName] = (fileVersion, productVersion);
    }

    public string? GetFileVersion(string fileName)
    {
        return _versionInfo.TryGetValue(fileName, out var info) ? info.FileVersion : null;
    }

    public string? GetProductVersion(string fileName)
    {
        return _versionInfo.TryGetValue(fileName, out var info) ? info.ProductVersion : null;
    }
}

/// <summary>
///     Test implementation of IZipService for unit testing
/// </summary>
public class TestZipService : IZipService
{
    private readonly Dictionary<string, List<ZipEntry>> _zipFiles = new();
    private readonly IFileSystem _fileSystem;
    private readonly IPathService _pathService;

    public class ZipEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    public TestZipService(IFileSystem fileSystem, IPathService pathService)
    {
        _fileSystem = fileSystem;
        _pathService = pathService;
    }

    public Task<bool> CreateZipAsync(string zipPath, Dictionary<string, string> files, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var entries = new List<ZipEntry>();

        foreach (var file in files)
        {
            if (_fileSystem.FileExists(file.Key))
            {
                var content = _fileSystem.ReadAllText(file.Key);
                    
                entries.Add(new ZipEntry 
                { 
                    Name = file.Value,
                    Content = content,
                    Size = content.Length
                });
            }
        }

        if (entries.Count > 0)
        {
            _zipFiles[zipPath] = entries;
            
            // Also create a placeholder file in the file system
            // This ensures BackupService can see the zip file exists
            if (_fileSystem is TestFileSystem testFs)
            {
                testFs.AddFile(zipPath, $"ZIP archive with {entries.Count} entries");
            }
            else if (_fileSystem != null)
            {
                // For any IFileSystem, ensure the zip file exists
                var dir = _pathService.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(dir))
                    _fileSystem.CreateDirectory(dir);
                // Note: Real IFileSystem would need actual file creation
            }
            
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task<bool> AddFileToZipAsync(string zipPath, string sourceFile, string entryName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!_fileSystem.FileExists(sourceFile))
            return Task.FromResult(false);

        if (!_zipFiles.ContainsKey(zipPath))
            _zipFiles[zipPath] = new List<ZipEntry>();

        var content = _fileSystem.ReadAllText(sourceFile);
            
        _zipFiles[zipPath].Add(new ZipEntry 
        { 
            Name = entryName,
            Content = content,
            Size = content.Length
        });

        return Task.FromResult(true);
    }

    public Task<IEnumerable<string>> ExtractZipAsync(string zipPath, string targetDirectory, 
        IEnumerable<string>? filesToExtract = null, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var extractedFiles = new List<string>();

        if (!_zipFiles.TryGetValue(zipPath, out var entries))
            return Task.FromResult<IEnumerable<string>>(extractedFiles);

        _fileSystem.CreateDirectory(targetDirectory);

        foreach (var entry in entries)
        {
            if (filesToExtract != null && !filesToExtract.Contains(entry.Name))
                continue;

            var targetPath = _pathService.Combine(targetDirectory, entry.Name.Replace('/', '\\'));
            
            // Ensure target directory exists
            var targetDir = _pathService.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
                _fileSystem.CreateDirectory(targetDir);

            // "Extract" the file
            if (_fileSystem is TestFileSystem testFs)
            {
                testFs.AddFile(targetPath, entry.Content);
            }

            extractedFiles.Add(targetPath);
        }

        return Task.FromResult<IEnumerable<string>>(extractedFiles);
    }

    public Task<bool> ExtractFileFromZipAsync(string zipPath, string entryName, string targetPath, 
        bool overwrite = true, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        if (!_zipFiles.TryGetValue(zipPath, out var entries))
            return Task.FromResult(false);

        var entry = entries.FirstOrDefault(e => e.Name.Replace('/', '\\') == entryName.Replace('/', '\\'));
        if (entry == null)
            return Task.FromResult(false);

        // Ensure target directory exists
        var targetDir = _pathService.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir))
            _fileSystem.CreateDirectory(targetDir);

        // "Extract" the file
        if (_fileSystem is TestFileSystem testFs)
        {
            testFs.AddFile(targetPath, entry.Content);
        }

        return Task.FromResult(true);
    }

    public Task<IEnumerable<string>> ListZipEntriesAsync(string zipPath)
    {
        if (!_zipFiles.TryGetValue(zipPath, out var entries))
            return Task.FromResult<IEnumerable<string>>(new List<string>());

        return Task.FromResult<IEnumerable<string>>(entries.Select(e => e.Name));
    }

    public Task<int> GetZipEntryCountAsync(string zipPath)
    {
        if (!_zipFiles.TryGetValue(zipPath, out var entries))
            return Task.FromResult(0);

        return Task.FromResult(entries.Count);
    }

    public Task<bool> IsValidZipAsync(string zipPath)
    {
        return Task.FromResult(_zipFiles.ContainsKey(zipPath));
    }

    // Helper method for tests to verify zip contents
    public List<ZipEntry>? GetZipContents(string zipPath)
    {
        return _zipFiles.TryGetValue(zipPath, out var entries) ? entries : null;
    }

    // Helper method for tests to add mock zip
    public void AddMockZip(string zipPath, List<ZipEntry> entries)
    {
        _zipFiles[zipPath] = entries;
        
        if (_fileSystem is TestFileSystem testFs)
        {
            testFs.AddFile(zipPath, $"ZIP archive with {entries.Count} entries");
        }
    }
}

/// <summary>
/// Test implementation of IFileWatcher for unit testing
/// </summary>
public class TestFileWatcher : IFileWatcher
{
    private bool _disposed;
    private Timer? _simulationTimer;
    
    public string Path { get; set; }
    public string Filter { get; set; }
    public NotifyFilters NotifyFilter { get; set; }
    public bool EnableRaisingEvents { get; set; }
    
    public event FileSystemEventHandler? Changed;
    public event ErrorEventHandler? Error;
    
    public TestFileWatcher(string path, string filter)
    {
        Path = path;
        Filter = filter;
        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
        EnableRaisingEvents = false;
    }
    
    /// <summary>
    /// Simulates a file change event for testing
    /// </summary>
    public void SimulateFileChange(string fullPath)
    {
        if (EnableRaisingEvents)
        {
            var args = new FileSystemEventArgs(WatcherChangeTypes.Changed, System.IO.Path.GetDirectoryName(fullPath)!, System.IO.Path.GetFileName(fullPath));
            Changed?.Invoke(this, args);
        }
    }
    
    /// <summary>
    /// Simulates an error event for testing
    /// </summary>
    public void SimulateError(Exception exception)
    {
        if (EnableRaisingEvents)
        {
            var args = new ErrorEventArgs(exception);
            Error?.Invoke(this, args);
        }
    }
    
    /// <summary>
    /// Starts automatic file change simulation for testing
    /// </summary>
    public void StartSimulation(int intervalMs = 1000)
    {
        _simulationTimer = new Timer(_ =>
        {
            if (EnableRaisingEvents)
            {
                SimulateFileChange(System.IO.Path.Combine(Path, Filter));
            }
        }, null, intervalMs, intervalMs);
    }
    
    /// <summary>
    /// Stops automatic file change simulation
    /// </summary>
    public void StopSimulation()
    {
        _simulationTimer?.Dispose();
        _simulationTimer = null;
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        
        StopSimulation();
        _disposed = true;
    }
}

/// <summary>
/// Test implementation of IFileWatcherFactory for unit testing
/// </summary>
public class TestFileWatcherFactory : IFileWatcherFactory
{
    private readonly List<TestFileWatcher> _createdWatchers = new();
    
    public IFileWatcher CreateWatcher(string path, string filter)
    {
        var watcher = new TestFileWatcher(path, filter);
        _createdWatchers.Add(watcher);
        return watcher;
    }
    
    /// <summary>
    /// Gets all watchers created by this factory (for testing)
    /// </summary>
    public IReadOnlyList<TestFileWatcher> CreatedWatchers => _createdWatchers;
    
    /// <summary>
    /// Simulates a change in all active watchers
    /// </summary>
    public void SimulateChangeInAllWatchers()
    {
        foreach (var watcher in _createdWatchers.Where(w => w.EnableRaisingEvents))
        {
            watcher.SimulateFileChange(System.IO.Path.Combine(watcher.Path, watcher.Filter));
        }
    }
}

/// <summary>
///     Test implementation of IGamePathDetection for unit testing
/// </summary>
public class TestSettingsHelper : ISettingsHelper
{
    private readonly IFileSystem _fileSystem;
    private readonly IPathService _pathService;
    private readonly IEnvironmentPathProvider _environmentProvider;
    private readonly Dictionary<string, object> _settings = new();

    public TestSettingsHelper()
    {
        _fileSystem = new TestFileSystem();
        _pathService = new TestPathService();
        _environmentProvider = new TestEnvironmentPathProvider();
    }

    public TestSettingsHelper(IFileSystem fileSystem, IPathService pathService, IEnvironmentPathProvider environmentProvider)
    {
        _fileSystem = fileSystem;
        _pathService = pathService;
        _environmentProvider = environmentProvider;
    }

    public string GetSettingsDirectory()
    {
        return _pathService.Combine(
            _environmentProvider.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Scanner111");
    }

    public void EnsureSettingsDirectoryExists()
    {
        var directory = GetSettingsDirectory();
        _fileSystem.CreateDirectory(directory);
    }

    public async Task<T> LoadSettingsAsync<T>(string filePath, Func<T> defaultFactory) where T : class
    {
        try
        {
            if (!_fileSystem.FileExists(filePath))
            {
                var defaultSettings = defaultFactory();
                await SaveSettingsAsync(filePath, defaultSettings).ConfigureAwait(false);
                return defaultSettings;
            }

            var json = await _fileSystem.ReadAllTextAsync(filePath).ConfigureAwait(false);
            var settings = JsonSerializer.Deserialize<T>(json, SettingsHelper.JsonOptions);
            return settings ?? defaultFactory();
        }
        catch
        {
            return defaultFactory();
        }
    }

    public async Task SaveSettingsAsync<T>(string filePath, T settings) where T : class
    {
        var directory = _pathService.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) 
            _fileSystem.CreateDirectory(directory);

        var json = JsonSerializer.Serialize(settings, SettingsHelper.JsonOptions);
        await _fileSystem.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
    }

    public object ConvertValue(object value, Type targetType)
    {
        if (value == null) return null!;
        if (value.GetType() == targetType) return value;
        
        try
        {
            return Convert.ChangeType(value, targetType);
        }
        catch
        {
            return value;
        }
    }

    public string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var words = input.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Join("", words.Select(w =>
            char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
    }
}

public class TestGamePathDetection : IGamePathDetection
{
    private readonly Dictionary<string, string> _gamePaths = new();
    private readonly Dictionary<string, bool> _validPaths = new();
    private string? _xseLogPath;

    public TestGamePathDetection()
    {
        // Set default test paths
        _gamePaths["Fallout4"] = @"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4";
        _gamePaths["Skyrim"] = @"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition";
        _validPaths[@"C:\Program Files (x86)\Steam\steamapps\common\Fallout 4"] = true;
        _validPaths[@"C:\Program Files (x86)\Steam\steamapps\common\Skyrim Special Edition"] = true;
    }

    public string TryDetectGamePath()
    {
        // Return the first available game path (default behavior)
        return _gamePaths.Values.FirstOrDefault() ?? string.Empty;
    }

    public string TryDetectGamePath(string gameType)
    {
        if (string.IsNullOrEmpty(gameType))
            return _gamePaths.Values.FirstOrDefault() ?? string.Empty;
        
        return _gamePaths.TryGetValue(gameType, out var path) ? path : string.Empty;
    }

    public bool ValidateGamePath(string path)
    {
        return _validPaths.TryGetValue(path, out var valid) && valid;
    }

    public string GetGameDocumentsPath(string gameType)
    {
        if (gameType.Contains("Fallout"))
            return @"C:\Users\TestUser\Documents\My Games\Fallout4";
        if (gameType.Contains("Skyrim"))
            return @"C:\Users\TestUser\Documents\My Games\Skyrim Special Edition";
        return string.Empty;
    }

    public GameConfiguration? DetectGameConfiguration(string gameType = "Fallout4")
    {
        var gamePath = TryDetectGamePath(gameType);
        if (string.IsNullOrEmpty(gamePath) || !ValidateGamePath(gamePath))
            return null;

        return new GameConfiguration
        {
            RootPath = gamePath,
            GameName = gameType.Contains("Fallout") ? "Fallout 4" : "Skyrim Special Edition",
            ExecutablePath = Path.Combine(gamePath, gameType.Contains("Fallout") ? "Fallout4.exe" : "SkyrimSE.exe"),
            DocumentsPath = GetGameDocumentsPath(gameType)
            // IsValid and DataPath are computed properties, not settable
        };
    }

    public string TryGetGamePathFromXseLog()
    {
        return _xseLogPath ?? string.Empty;
    }

    public string TryGetGamePathFromRegistry()
    {
        // Return first available game path as if from registry
        return _gamePaths.Values.FirstOrDefault() ?? string.Empty;
    }

    // Helper methods for test setup
    public void SetGamePath(string gameType, string path)
    {
        _gamePaths[gameType] = path;
        _validPaths[path] = true;
    }

    public void SetValidPath(string path, bool isValid)
    {
        _validPaths[path] = isValid;
    }

    public void SetXseLogPath(string path)
    {
        _xseLogPath = path;
    }
}

/// <summary>
///     Test implementation of ICrashLogParser for unit testing
/// </summary>
public class TestCrashLogParser : ICrashLogParser
{
    private readonly Dictionary<string, CrashLog> _crashLogs = new();
    private readonly HashSet<string> _invalidPaths = new();
    private CrashLog? _defaultCrashLog;
    private bool _returnNullForUnknownFiles;

    public Task<CrashLog?> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // Properly handle cancellation
        cancellationToken.ThrowIfCancellationRequested();
        
        // Check if this path was marked as invalid
        if (_invalidPaths.Contains(filePath))
            return Task.FromResult<CrashLog?>(null);
            
        // Check for specific test patterns that should return null
        if (filePath == "nonexistent.log" || filePath.StartsWith("/nonexistent"))
            return Task.FromResult<CrashLog?>(null);
            
        if (_crashLogs.TryGetValue(filePath, out var crashLog))
            return Task.FromResult<CrashLog?>(crashLog);
        
        // If configured to return null for unknown files
        if (_returnNullForUnknownFiles)
            return Task.FromResult<CrashLog?>(null);
        
        // Otherwise return a valid crash log for testing
        var result = _defaultCrashLog ?? new CrashLog
        {
            FilePath = filePath,
            GameType = "Fallout4",
            GameVersion = "1.10.163",
            Plugins = new Dictionary<string, string>
            {
                ["00:000"] = "Fallout4.esm"
            },
            XseModules = new HashSet<string>(),
            OriginalLines = new List<string>()
        };
        
        // Create a new instance with the correct FilePath
        return Task.FromResult<CrashLog?>(new CrashLog
        {
            FilePath = filePath,
            GameType = result.GameType,
            GameVersion = result.GameVersion,
            Plugins = result.Plugins ?? new Dictionary<string, string>(),
            XseModules = result.XseModules ?? new HashSet<string>(),
            OriginalLines = result.OriginalLines ?? new List<string>(),
            MainError = result.MainError,
            CallStack = result.CallStack,
            CrashgenSettings = result.CrashgenSettings,
            CrashGenVersion = result.CrashGenVersion,
            CrashTime = result.CrashTime,
            IsIncomplete = result.IsIncomplete,
            GamePath = result.GamePath
        });
    }

    public Task<CrashLog?> ParseFromContentAsync(string content, string filePath = "", CancellationToken cancellationToken = default)
    {
        // For testing, return a new crash log with the specified file path
        // We can't modify _defaultCrashLog.FilePath since it's init-only
        if (_defaultCrashLog != null)
        {
            // Create a new CrashLog with the same data but different FilePath
            return Task.FromResult<CrashLog?>(new CrashLog
            {
                FilePath = filePath,
                GameType = _defaultCrashLog.GameType,
                OriginalLines = _defaultCrashLog.OriginalLines,
                MainError = _defaultCrashLog.MainError,
                CallStack = _defaultCrashLog.CallStack,
                Plugins = _defaultCrashLog.Plugins,
                XseModules = _defaultCrashLog.XseModules,
                CrashgenSettings = _defaultCrashLog.CrashgenSettings,
                CrashGenVersion = _defaultCrashLog.CrashGenVersion,
                CrashTime = _defaultCrashLog.CrashTime,
                GameVersion = _defaultCrashLog.GameVersion,
                IsIncomplete = _defaultCrashLog.IsIncomplete,
                GamePath = _defaultCrashLog.GamePath
            });
        }

        return Task.FromResult<CrashLog?>(new CrashLog
        {
            FilePath = filePath,
            GameType = "Fallout4",
            Plugins = new Dictionary<string, string>(),
            XseModules = new HashSet<string>()
        });
    }

    // Helper methods for test setup
    public void SetCrashLog(string filePath, CrashLog crashLog)
    {
        _crashLogs[filePath] = crashLog;
    }

    public void SetDefaultCrashLog(CrashLog crashLog)
    {
        _defaultCrashLog = crashLog;
    }
    
    public void SetInvalidPath(string filePath)
    {
        _invalidPaths.Add(filePath);
    }
    
    public void SetReturnNullForUnknownFiles(bool value)
    {
        _returnNullForUnknownFiles = value;
    }
}
using Scanner111.Core.Analyzers;
using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Scanner111.GUI.Models;
using Scanner111.GUI.Services;
using Scanner111.GUI.ViewModels;

namespace Scanner111.Tests.GUI.TestHelpers;

public class MockSettingsService : ISettingsService
{
    private ApplicationSettings _applicationSettings;
    private UserSettings _userSettings;

    public MockSettingsService()
    {
        _applicationSettings = new ApplicationSettings
        {
            DefaultLogPath = @"C:\Test\default.log",
            DefaultGamePath = @"C:\Games\Fallout4",
            DefaultScanDirectory = @"C:\Test\Scans",
            AutoLoadF4SeLogs = true,
            MaxLogMessages = 100,
            EnableProgressNotifications = true,
            RememberWindowSize = true,
            WindowWidth = 1200,
            WindowHeight = 800,
            EnableDebugLogging = false,
            MaxRecentItems = 10,
            AutoSaveResults = true,
            DefaultOutputFormat = "text",
            CrashLogsDirectory = @"C:\Test\CrashLogs",
            SkipXseCopy = false,
            EnableUpdateCheck = false,
            UpdateSource = "Both"
        };

        _userSettings = CreateUserSettingsFromApplicationSettings(_applicationSettings);
    }

    public bool SaveCalled { get; private set; }
    public bool LoadCalled { get; private set; }
    public Exception? LoadException { get; set; }
    public Exception? SaveException { get; set; }

    public Task<ApplicationSettings> LoadSettingsAsync()
    {
        LoadCalled = true;
        if (LoadException != null)
            throw LoadException;
        return Task.FromResult(_applicationSettings);
    }

    public Task SaveSettingsAsync(ApplicationSettings settings)
    {
        SaveCalled = true;
        if (SaveException != null)
            throw SaveException;
        _applicationSettings = settings;
        return Task.CompletedTask;
    }

    public ApplicationSettings GetDefaultSettings()
    {
        return new ApplicationSettings
        {
            DefaultLogPath = "",
            DefaultGamePath = "",
            DefaultScanDirectory = "",
            AutoLoadF4SeLogs = true,
            MaxLogMessages = 100,
            EnableProgressNotifications = true,
            RememberWindowSize = true,
            WindowWidth = 1200,
            WindowHeight = 800,
            EnableDebugLogging = false,
            MaxRecentItems = 10,
            AutoSaveResults = false,
            DefaultOutputFormat = "text",
            CrashLogsDirectory = "",
            SkipXseCopy = false,
            EnableUpdateCheck = false,
            UpdateSource = "Both"
        };
    }

    public Task<UserSettings> LoadUserSettingsAsync()
    {
        LoadCalled = true;
        if (LoadException != null)
            throw LoadException;
        return Task.FromResult(_userSettings);
    }

    public Task SaveUserSettingsAsync(UserSettings settings)
    {
        SaveCalled = true;
        if (SaveException != null)
            throw SaveException;
        _userSettings = settings;
        _applicationSettings = CreateApplicationSettingsFromUserSettings(settings);
        return Task.CompletedTask;
    }

    public void SetApplicationSettings(ApplicationSettings settings)
    {
        _applicationSettings = settings;
        _userSettings = CreateUserSettingsFromApplicationSettings(settings);
    }

    public void SetUserSettings(UserSettings settings)
    {
        _userSettings = settings;
        _applicationSettings = CreateApplicationSettingsFromUserSettings(settings);
    }

    private UserSettings CreateUserSettingsFromApplicationSettings(ApplicationSettings appSettings)
    {
        var userSettings = new UserSettings
        {
            DefaultLogPath = appSettings.DefaultLogPath,
            DefaultGamePath = appSettings.DefaultGamePath,
            DefaultScanDirectory = appSettings.DefaultScanDirectory,
            AutoLoadF4SeLogs = appSettings.AutoLoadF4SeLogs,
            MaxLogMessages = appSettings.MaxLogMessages,
            EnableProgressNotifications = appSettings.EnableProgressNotifications,
            RememberWindowSize = appSettings.RememberWindowSize,
            WindowWidth = appSettings.WindowWidth,
            WindowHeight = appSettings.WindowHeight,
            EnableDebugLogging = appSettings.EnableDebugLogging,
            MaxRecentItems = appSettings.MaxRecentItems,
            AutoSaveResults = appSettings.AutoSaveResults,
            DefaultOutputFormat = appSettings.DefaultOutputFormat,
            CrashLogsDirectory = appSettings.CrashLogsDirectory,
            SkipXseCopy = appSettings.SkipXseCopy,
            EnableUpdateCheck = appSettings.EnableUpdateCheck,
            UpdateSource = appSettings.UpdateSource,
            FcxMode = false,
            MoveUnsolvedLogs = false
        };
        return userSettings;
    }

    private ApplicationSettings CreateApplicationSettingsFromUserSettings(UserSettings userSettings)
    {
        return new ApplicationSettings
        {
            DefaultLogPath = userSettings.DefaultLogPath,
            DefaultGamePath = userSettings.DefaultGamePath,
            DefaultScanDirectory = userSettings.DefaultScanDirectory,
            AutoLoadF4SeLogs = userSettings.AutoLoadF4SeLogs,
            MaxLogMessages = userSettings.MaxLogMessages,
            EnableProgressNotifications = userSettings.EnableProgressNotifications,
            RememberWindowSize = userSettings.RememberWindowSize,
            WindowWidth = userSettings.WindowWidth,
            WindowHeight = userSettings.WindowHeight,
            EnableDebugLogging = userSettings.EnableDebugLogging,
            MaxRecentItems = userSettings.MaxRecentItems,
            AutoSaveResults = userSettings.AutoSaveResults,
            DefaultOutputFormat = userSettings.DefaultOutputFormat,
            CrashLogsDirectory = userSettings.CrashLogsDirectory,
            SkipXseCopy = userSettings.SkipXseCopy,
            EnableUpdateCheck = userSettings.EnableUpdateCheck,
            UpdateSource = userSettings.UpdateSource
        };
    }
}

public class MockGuiMessageHandlerService : GuiMessageHandlerService
{
    public List<string> Messages { get; } = new();
    public List<double> ProgressValues { get; } = new();
    public List<string> ProgressTexts { get; } = new();
    public MainWindowViewModel? TestViewModel { get; private set; }

    public new void SetViewModel(MainWindowViewModel viewModel)
    {
        TestViewModel = viewModel;
        base.SetViewModel(viewModel);
    }

    public void ShowMessage(string message, MessageType type = MessageType.Info)
    {
        Messages.Add($"[{type}] {message}");
        base.ShowMessage(message, null, type);
    }

    public void UpdateProgress(double value, string text)
    {
        ProgressValues.Add(value);
        ProgressTexts.Add(text);
        if (TestViewModel != null)
        {
            TestViewModel.ProgressValue = value;
            TestViewModel.ProgressText = text;
        }
    }
}

public class MockUpdateService : IUpdateService
{
    public bool IsLatestVersionCalled { get; private set; }
    public bool GetUpdateInfoCalled { get; private set; }
    public bool? IsLatestVersionResult { get; set; } = true;
    public UpdateCheckResult? UpdateCheckResult { get; set; }
    public Exception? CheckException { get; set; }

    public Task<bool> IsLatestVersionAsync(bool quiet = true, CancellationToken cancellationToken = default)
    {
        IsLatestVersionCalled = true;
        if (CheckException != null)
            throw CheckException;
        return Task.FromResult(IsLatestVersionResult ?? true);
    }

    public Task<UpdateCheckResult> GetUpdateInfoAsync(CancellationToken cancellationToken = default)
    {
        GetUpdateInfoCalled = true;
        if (CheckException != null)
            throw CheckException;
        return Task.FromResult(UpdateCheckResult ?? new UpdateCheckResult
        {
            CurrentVersion = new Version("1.0.0"),
            LatestGitHubVersion = new Version("1.0.0"),
            LatestNexusVersion = new Version("1.0.0"),
            IsUpdateAvailable = false,
            CheckSuccessful = true
        });
    }

    public void SetUpdateAvailable(string version, string notes = "New version available")
    {
        IsLatestVersionResult = false;
        UpdateCheckResult = new UpdateCheckResult
        {
            CurrentVersion = new Version("1.0.0"),
            LatestGitHubVersion = new Version(version),
            LatestNexusVersion = new Version(version),
            IsUpdateAvailable = true,
            CheckSuccessful = true,
            UpdateSource = "Both"
        };
    }
}

public class MockCacheManager : ICacheManager
{
    private readonly Dictionary<string, object> _cache = new();
    private readonly Dictionary<string, DateTime> _fileModificationTimes = new();
    public bool ClearCacheCalled { get; private set; }

    public T? GetOrSetYamlSetting<T>(string yamlFile, string keyPath, Func<T?> factory, TimeSpan? expiry = null)
    {
        var cacheKey = $"yaml:{yamlFile}:{keyPath}";
        if (_cache.TryGetValue(cacheKey, out var cached) && cached is T typedValue) return typedValue;

        var value = factory();
        if (value != null) _cache[cacheKey] = value;
        return value;
    }

    public async Task<T?> GetOrSetYamlSettingAsync<T>(string yamlFile, string keyPath, Func<Task<T?>> factory,
        TimeSpan? expiry = null)
    {
        var cacheKey = $"yaml:{yamlFile}:{keyPath}";
        if (_cache.TryGetValue(cacheKey, out var cached) && cached is T typedValue) return typedValue;

        var value = await factory().ConfigureAwait(false);
        if (value != null) _cache[cacheKey] = value;
        return value;
    }

    public void CacheAnalysisResult(string filePath, string analyzerName, AnalysisResult result)
    {
        var cacheKey = $"analysis:{filePath}:{analyzerName}";
        _cache[cacheKey] = result;
        _fileModificationTimes[filePath] = DateTime.UtcNow;
    }

    public AnalysisResult? GetCachedAnalysisResult(string filePath, string analyzerName)
    {
        var cacheKey = $"analysis:{filePath}:{analyzerName}";
        return _cache.TryGetValue(cacheKey, out var value) ? value as AnalysisResult : null;
    }

    public bool IsFileCacheValid(string filePath)
    {
        return _fileModificationTimes.ContainsKey(filePath);
    }

    public void ClearCache()
    {
        ClearCacheCalled = true;
        _cache.Clear();
        _fileModificationTimes.Clear();
    }

    public CacheStatistics GetStatistics()
    {
        return new CacheStatistics
        {
            TotalHits = 0,
            TotalMisses = 0,
            HitRate = 0.0,
            CachedFiles = _fileModificationTimes.Count,
            MemoryUsage = 0
        };
    }

    public void Reset()
    {
        _cache.Clear();
        _fileModificationTimes.Clear();
        ClearCacheCalled = false;
    }
}

public class MockUnsolvedLogsMover : IUnsolvedLogsMover
{
    public List<string> MovedLogs { get; } = new();
    public bool ShouldFail { get; set; }
    public string? FailureMessage { get; set; }

    public Task<bool> MoveUnsolvedLogAsync(string crashLogPath, ApplicationSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        if (ShouldFail)
        {
            if (!string.IsNullOrEmpty(FailureMessage))
                throw new InvalidOperationException(FailureMessage);
            return Task.FromResult(false);
        }

        MovedLogs.Add(crashLogPath);
        return Task.FromResult(true);
    }

    public void Reset()
    {
        MovedLogs.Clear();
        ShouldFail = false;
        FailureMessage = null;
    }
}

public class MockGameScannerService : IGameScannerService
{
    public GameScanResult? NextScanResult { get; set; }
    public string NextCrashGenResult { get; set; } = "CrashGen: OK";
    public string NextXseResult { get; set; } = "XSE Plugins: Valid";
    public string NextModIniResult { get; set; } = "Mod INIs: No issues";
    public string NextWryeBashResult { get; set; } = "Wrye Bash: Configured";
    public Exception? SimulateException { get; set; }
    public int ScanGameCallCount { get; private set; }
    public int CheckCrashGenCallCount { get; private set; }
    public int ValidateXsePluginsCallCount { get; private set; }
    public int ScanModInisCallCount { get; private set; }
    public int CheckWryeBashCallCount { get; private set; }
    public List<CancellationToken> ReceivedCancellationTokens { get; } = new();

    public Task<GameScanResult> ScanGameAsync(CancellationToken cancellationToken = default)
    {
        ScanGameCallCount++;
        ReceivedCancellationTokens.Add(cancellationToken);

        if (SimulateException != null)
            throw SimulateException;

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        return Task.FromResult(NextScanResult ?? new GameScanResult
        {
            CrashGenResults = NextCrashGenResult,
            XsePluginResults = NextXseResult,
            ModIniResults = NextModIniResult,
            WryeBashResults = NextWryeBashResult,
            HasIssues = false,
            CriticalIssues = new List<string>(),
            Warnings = new List<string>()
        });
    }

    public Task<string> CheckCrashGenAsync(CancellationToken cancellationToken = default)
    {
        CheckCrashGenCallCount++;
        ReceivedCancellationTokens.Add(cancellationToken);

        if (SimulateException != null)
            throw SimulateException;

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        return Task.FromResult(NextCrashGenResult);
    }

    public Task<string> ValidateXsePluginsAsync(CancellationToken cancellationToken = default)
    {
        ValidateXsePluginsCallCount++;
        ReceivedCancellationTokens.Add(cancellationToken);

        if (SimulateException != null)
            throw SimulateException;

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        return Task.FromResult(NextXseResult);
    }

    public Task<string> ScanModInisAsync(CancellationToken cancellationToken = default)
    {
        ScanModInisCallCount++;
        ReceivedCancellationTokens.Add(cancellationToken);

        if (SimulateException != null)
            throw SimulateException;

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        return Task.FromResult(NextModIniResult);
    }

    public Task<string> CheckWryeBashAsync(CancellationToken cancellationToken = default)
    {
        CheckWryeBashCallCount++;
        ReceivedCancellationTokens.Add(cancellationToken);

        if (SimulateException != null)
            throw SimulateException;

        if (cancellationToken.IsCancellationRequested)
            throw new OperationCanceledException();

        return Task.FromResult(NextWryeBashResult);
    }

    public void Reset()
    {
        NextScanResult = null;
        NextCrashGenResult = "CrashGen: OK";
        NextXseResult = "XSE Plugins: Valid";
        NextModIniResult = "Mod INIs: No issues";
        NextWryeBashResult = "Wrye Bash: Configured";
        SimulateException = null;
        ScanGameCallCount = 0;
        CheckCrashGenCallCount = 0;
        ValidateXsePluginsCallCount = 0;
        ScanModInisCallCount = 0;
        CheckWryeBashCallCount = 0;
        ReceivedCancellationTokens.Clear();
    }
}

public class MockMessageHandler : IMessageHandler
{
    public List<string> InfoMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();
    public List<string> SuccessMessages { get; } = new();
    public List<string> DebugMessages { get; } = new();
    public List<string> CriticalMessages { get; } = new();
    public List<(double value, string text)> ProgressUpdates { get; } = new();
    public List<string> ProgressTitles { get; } = new();

    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        InfoMessages.Add(message);
    }

    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        WarningMessages.Add(message);
    }

    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        ErrorMessages.Add(message);
    }

    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        SuccessMessages.Add(message);
    }

    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        DebugMessages.Add(message);
    }

    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        CriticalMessages.Add(message);
    }

    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        switch (messageType)
        {
            case MessageType.Info:
                ShowInfo(message, target);
                break;
            case MessageType.Warning:
                ShowWarning(message, target);
                break;
            case MessageType.Error:
                ShowError(message, target);
                break;
            case MessageType.Success:
                ShowSuccess(message, target);
                break;
            case MessageType.Debug:
                ShowDebug(message, target);
                break;
            case MessageType.Critical:
                ShowCritical(message, target);
                break;
        }
    }

    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        ProgressTitles.Add(title);
        return new MockProgress(this, totalItems);
    }

    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        ProgressTitles.Add(title);
        return new MockProgressContext(this, totalItems);
    }

    public void UpdateProgress(double value, string text)
    {
        ProgressUpdates.Add((value, text));
    }

    public void Reset()
    {
        InfoMessages.Clear();
        WarningMessages.Clear();
        ErrorMessages.Clear();
        SuccessMessages.Clear();
        DebugMessages.Clear();
        CriticalMessages.Clear();
        ProgressUpdates.Clear();
        ProgressTitles.Clear();
    }

    private class MockProgress : IProgress<ProgressInfo>
    {
        private readonly MockMessageHandler _handler;
        private readonly int _totalItems;

        public MockProgress(MockMessageHandler handler, int totalItems)
        {
            _handler = handler;
            _totalItems = totalItems;
        }

        public void Report(ProgressInfo value)
        {
            _handler.UpdateProgress(value.Percentage, value.Message);
        }
    }

    private class MockProgressContext : IProgressContext
    {
        private readonly MockMessageHandler _handler;
        private readonly int _totalItems;
        private bool _disposed;

        public MockProgressContext(MockMessageHandler handler, int totalItems)
        {
            _handler = handler;
            _totalItems = totalItems;
        }

        public void Report(ProgressInfo value)
        {
            _handler.UpdateProgress(value.Percentage, value.Message);
        }

        public void Update(int current, string message)
        {
            var percentage = _totalItems > 0 ? current * 100.0 / _totalItems : 0;
            _handler.UpdateProgress(percentage, message);
        }

        public void Complete()
        {
            _handler.UpdateProgress(100, "Complete");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Complete();
                _disposed = true;
            }
        }
    }
}
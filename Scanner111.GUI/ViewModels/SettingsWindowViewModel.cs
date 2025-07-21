using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Scanner111.GUI.Models;
using Scanner111.GUI.Services;

namespace Scanner111.GUI.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private bool _autoLoadF4SeLogs = true;
    private bool _autoSaveResults = true;
    private string _defaultGamePath = "";
    private string _defaultLogPath = "";
    private string _defaultOutputFormat = "detailed";
    private string _defaultScanDirectory = "";
    private bool _enableDebugLogging;
    private bool _enableProgressNotifications = true;
    private int _maxLogMessages = 100;
    private int _maxRecentItems = 10;
    private UserSettings _originalSettings;
    private bool _rememberWindowSize = true;
    private double _windowHeight = 800;
    private double _windowWidth = 1200;

    public SettingsWindowViewModel() : this(new SettingsService())
    {
    }

    public SettingsWindowViewModel(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _originalSettings = new UserSettings();

        BrowseLogPathCommand = ReactiveCommand.CreateFromTask(BrowseLogPath);
        BrowseGamePathCommand = ReactiveCommand.CreateFromTask(BrowseGamePath);
        BrowseScanDirectoryCommand = ReactiveCommand.CreateFromTask(BrowseScanDirectory);
        ClearRecentFilesCommand = ReactiveCommand.Create(ClearRecentFiles);
        ResetToDefaultsCommand = ReactiveCommand.Create(ResetToDefaults);
        SaveCommand = ReactiveCommand.CreateFromTask(SaveSettings);
        CancelCommand = ReactiveCommand.Create(Cancel);

        // Load settings on initialization
        _ = LoadSettingsAsync();
    }

    public string DefaultLogPath
    {
        get => _defaultLogPath;
        set => this.RaiseAndSetIfChanged(ref _defaultLogPath, value);
    }

    public string DefaultGamePath
    {
        get => _defaultGamePath;
        set => this.RaiseAndSetIfChanged(ref _defaultGamePath, value);
    }

    public string DefaultScanDirectory
    {
        get => _defaultScanDirectory;
        set => this.RaiseAndSetIfChanged(ref _defaultScanDirectory, value);
    }

    public bool AutoLoadF4SeLogs
    {
        get => _autoLoadF4SeLogs;
        set => this.RaiseAndSetIfChanged(ref _autoLoadF4SeLogs, value);
    }

    public int MaxLogMessages
    {
        get => _maxLogMessages;
        set => this.RaiseAndSetIfChanged(ref _maxLogMessages, value);
    }

    public bool EnableProgressNotifications
    {
        get => _enableProgressNotifications;
        set => this.RaiseAndSetIfChanged(ref _enableProgressNotifications, value);
    }

    public bool RememberWindowSize
    {
        get => _rememberWindowSize;
        set => this.RaiseAndSetIfChanged(ref _rememberWindowSize, value);
    }

    public double WindowWidth
    {
        get => _windowWidth;
        set => this.RaiseAndSetIfChanged(ref _windowWidth, value);
    }

    public double WindowHeight
    {
        get => _windowHeight;
        set => this.RaiseAndSetIfChanged(ref _windowHeight, value);
    }

    public bool EnableDebugLogging
    {
        get => _enableDebugLogging;
        set => this.RaiseAndSetIfChanged(ref _enableDebugLogging, value);
    }

    public int MaxRecentItems
    {
        get => _maxRecentItems;
        set => this.RaiseAndSetIfChanged(ref _maxRecentItems, value);
    }

    public bool AutoSaveResults
    {
        get => _autoSaveResults;
        set => this.RaiseAndSetIfChanged(ref _autoSaveResults, value);
    }

    public string DefaultOutputFormat
    {
        get => _defaultOutputFormat;
        set => this.RaiseAndSetIfChanged(ref _defaultOutputFormat, value);
    }

    public ObservableCollection<string> RecentLogFiles { get; } = new();
    public ObservableCollection<string> RecentGamePaths { get; } = new();
    public ObservableCollection<string> RecentScanDirectories { get; } = new();

    public bool HasRecentLogFiles => RecentLogFiles.Count > 0;
    public bool HasRecentGamePaths => RecentGamePaths.Count > 0;
    public bool HasRecentScanDirectories => RecentScanDirectories.Count > 0;

    public ReactiveCommand<Unit, Unit> BrowseLogPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseGamePathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseScanDirectoryCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearRecentFilesCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public Func<string, string, Task<string>>? ShowFilePickerAsync { get; set; }
    public Func<string, Task<string>>? ShowFolderPickerAsync { get; set; }
    public Action? CloseWindow { get; set; }

    private UserSettings CreateDeepCopy(UserSettings source)
    {
        var copy = new UserSettings
        {
            DefaultLogPath = source.DefaultLogPath,
            DefaultGamePath = source.DefaultGamePath,
            DefaultScanDirectory = source.DefaultScanDirectory,
            AutoLoadF4SeLogs = source.AutoLoadF4SeLogs,
            MaxLogMessages = source.MaxLogMessages,
            EnableProgressNotifications = source.EnableProgressNotifications,
            RememberWindowSize = source.RememberWindowSize,
            WindowWidth = source.WindowWidth,
            WindowHeight = source.WindowHeight,
            EnableDebugLogging = source.EnableDebugLogging,
            MaxRecentItems = source.MaxRecentItems,
            AutoSaveResults = source.AutoSaveResults,
            DefaultOutputFormat = source.DefaultOutputFormat,
            CrashLogsDirectory = source.CrashLogsDirectory,
            SkipXseCopy = source.SkipXseCopy
        };

        // Deep copy the lists
        foreach (var file in source.RecentLogFiles)
            copy.RecentLogFiles.Add(file);

        foreach (var path in source.RecentGamePaths)
            copy.RecentGamePaths.Add(path);

        foreach (var dir in source.RecentScanDirectories)
            copy.RecentScanDirectories.Add(dir);

        foreach (var analyzer in source.LastUsedAnalyzers)
            copy.LastUsedAnalyzers.Add(analyzer);

        return copy;
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadUserSettingsAsync();
            _originalSettings = CreateDeepCopy(settings);
            LoadFromSettings(settings);
        }
        catch (Exception)
        {
            var defaultSettings = _settingsService.GetDefaultSettings();
            var userDefaults = new UserSettings
            {
                DefaultLogPath = defaultSettings.DefaultLogPath,
                DefaultGamePath = defaultSettings.DefaultGamePath,
                DefaultScanDirectory = defaultSettings.DefaultScanDirectory,
                AutoLoadF4SeLogs = defaultSettings.AutoLoadF4SeLogs,
                MaxLogMessages = defaultSettings.MaxLogMessages,
                EnableProgressNotifications = defaultSettings.EnableProgressNotifications,
                RememberWindowSize = defaultSettings.RememberWindowSize,
                WindowWidth = defaultSettings.WindowWidth,
                WindowHeight = defaultSettings.WindowHeight,
                EnableDebugLogging = defaultSettings.EnableDebugLogging,
                MaxRecentItems = defaultSettings.MaxRecentItems,
                AutoSaveResults = defaultSettings.AutoSaveResults,
                DefaultOutputFormat = defaultSettings.DefaultOutputFormat,
                CrashLogsDirectory = defaultSettings.CrashLogsDirectory,
                SkipXseCopy = defaultSettings.SkipXseCopy
            };
            _originalSettings = CreateDeepCopy(userDefaults);
            ResetToDefaults();
        }
    }

    private void LoadFromSettings(UserSettings settings)
    {
        DefaultLogPath = settings.DefaultLogPath;
        DefaultGamePath = settings.DefaultGamePath;
        DefaultScanDirectory = settings.DefaultScanDirectory;
        AutoLoadF4SeLogs = settings.AutoLoadF4SeLogs;
        MaxLogMessages = settings.MaxLogMessages;
        EnableProgressNotifications = settings.EnableProgressNotifications;
        RememberWindowSize = settings.RememberWindowSize;
        WindowWidth = settings.WindowWidth;
        WindowHeight = settings.WindowHeight;
        EnableDebugLogging = settings.EnableDebugLogging;
        MaxRecentItems = settings.MaxRecentItems;
        AutoSaveResults = settings.AutoSaveResults;
        DefaultOutputFormat = settings.DefaultOutputFormat;

        RecentLogFiles.Clear();
        foreach (var file in settings.RecentLogFiles)
            RecentLogFiles.Add(file);

        RecentGamePaths.Clear();
        foreach (var path in settings.RecentGamePaths)
            RecentGamePaths.Add(path);

        RecentScanDirectories.Clear();
        foreach (var dir in settings.RecentScanDirectories)
            RecentScanDirectories.Add(dir);

        this.RaisePropertyChanged(nameof(HasRecentLogFiles));
        this.RaisePropertyChanged(nameof(HasRecentGamePaths));
        this.RaisePropertyChanged(nameof(HasRecentScanDirectories));
    }

    private UserSettings CreateSettingsFromViewModel()
    {
        var settings = new UserSettings
        {
            DefaultLogPath = DefaultLogPath,
            DefaultGamePath = DefaultGamePath,
            DefaultScanDirectory = DefaultScanDirectory,
            AutoLoadF4SeLogs = AutoLoadF4SeLogs,
            MaxLogMessages = MaxLogMessages,
            EnableProgressNotifications = EnableProgressNotifications,
            RememberWindowSize = RememberWindowSize,
            WindowWidth = WindowWidth,
            WindowHeight = WindowHeight,
            EnableDebugLogging = EnableDebugLogging,
            MaxRecentItems = MaxRecentItems,
            AutoSaveResults = AutoSaveResults,
            DefaultOutputFormat = DefaultOutputFormat
        };

        foreach (var file in RecentLogFiles)
            settings.RecentLogFiles.Add(file);

        foreach (var path in RecentGamePaths)
            settings.RecentGamePaths.Add(path);

        foreach (var dir in RecentScanDirectories)
            settings.RecentScanDirectories.Add(dir);

        return settings;
    }

    private async Task BrowseLogPath()
    {
        if (ShowFilePickerAsync != null)
        {
            var result = await ShowFilePickerAsync("Select Default Log File", "*.log");
            if (!string.IsNullOrEmpty(result)) DefaultLogPath = result;
        }
    }

    private async Task BrowseGamePath()
    {
        if (ShowFolderPickerAsync != null)
        {
            var result = await ShowFolderPickerAsync("Select Game Installation Directory");
            if (!string.IsNullOrEmpty(result)) DefaultGamePath = result;
        }
    }

    private async Task BrowseScanDirectory()
    {
        if (ShowFolderPickerAsync != null)
        {
            var result = await ShowFolderPickerAsync("Select Default Scan Directory");
            if (!string.IsNullOrEmpty(result)) DefaultScanDirectory = result;
        }
    }

    private void ClearRecentFiles()
    {
        RecentLogFiles.Clear();
        RecentGamePaths.Clear();
        RecentScanDirectories.Clear();

        this.RaisePropertyChanged(nameof(HasRecentLogFiles));
        this.RaisePropertyChanged(nameof(HasRecentGamePaths));
        this.RaisePropertyChanged(nameof(HasRecentScanDirectories));
    }

    private void ResetToDefaults()
    {
        var appSettings = _settingsService.GetDefaultSettings();
        var defaultSettings = new UserSettings
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
            SkipXseCopy = appSettings.SkipXseCopy
        };
        LoadFromSettings(defaultSettings);
    }

    private async Task SaveSettings()
    {
        try
        {
            var settings = CreateSettingsFromViewModel();
            await _settingsService.SaveUserSettingsAsync(settings);
            CloseWindow?.Invoke();
        }
        catch (Exception)
        {
            // Settings save failed - ignore silently for GUI
        }
    }

    private void Cancel()
    {
        // Restore original settings
        LoadFromSettings(_originalSettings);
        CloseWindow?.Invoke();
    }
}
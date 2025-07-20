using ReactiveUI;
using Scanner111.GUI.Models;
using Scanner111.GUI.Services;
using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.GUI.ViewModels;

public class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private UserSettings _originalSettings;
    private string _defaultLogPath = "";
    private string _defaultGamePath = "";
    private string _defaultScanDirectory = "";
    private bool _autoLoadF4SELogs = true;
    private int _maxLogMessages = 100;
    private bool _enableProgressNotifications = true;
    private bool _rememberWindowSize = true;
    private double _windowWidth = 1200;
    private double _windowHeight = 800;
    private bool _enableDebugLogging = false;
    private int _maxRecentItems = 10;
    private bool _autoSaveResults = true;
    private string _defaultOutputFormat = "detailed";

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

    public bool AutoLoadF4SELogs
    {
        get => _autoLoadF4SELogs;
        set => this.RaiseAndSetIfChanged(ref _autoLoadF4SELogs, value);
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

    private async Task LoadSettingsAsync()
    {
        try
        {
            var settings = await _settingsService.LoadUserSettingsAsync();
            _originalSettings = settings;
            LoadFromSettings(settings);
        }
        catch (Exception ex)
        {
            ResetToDefaults();
        }
    }

    private void LoadFromSettings(UserSettings settings)
    {
        DefaultLogPath = settings.DefaultLogPath;
        DefaultGamePath = settings.DefaultGamePath;
        DefaultScanDirectory = settings.DefaultScanDirectory;
        AutoLoadF4SELogs = settings.AutoLoadF4SELogs;
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
            AutoLoadF4SELogs = AutoLoadF4SELogs,
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
            if (!string.IsNullOrEmpty(result))
            {
                DefaultLogPath = result;
            }
        }
    }

    private async Task BrowseGamePath()
    {
        if (ShowFolderPickerAsync != null)
        {
            var result = await ShowFolderPickerAsync("Select Game Installation Directory");
            if (!string.IsNullOrEmpty(result))
            {
                DefaultGamePath = result;
            }
        }
    }

    private async Task BrowseScanDirectory()
    {
        if (ShowFolderPickerAsync != null)
        {
            var result = await ShowFolderPickerAsync("Select Default Scan Directory");
            if (!string.IsNullOrEmpty(result))
            {
                DefaultScanDirectory = result;
            }
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
            AutoLoadF4SELogs = appSettings.AutoLoadF4SELogs,
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
            SkipXSECopy = appSettings.SkipXSECopy
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
        catch (Exception ex)
        {
            // Settings save failed - ignore silently for GUI
        }
    }

    private void Cancel()
    {
        CloseWindow?.Invoke();
    }
}
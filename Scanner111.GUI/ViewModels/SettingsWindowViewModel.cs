using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using Scanner111.GUI.Models;
using Scanner111.GUI.Services;

namespace Scanner111.GUI.ViewModels;

/// Represents the ViewModel for the settings window in the application.
/// Responsible for handling user-configurable application settings and maintains
/// the state for the UI. Provides commands for user interactions such as browsing
/// directories, clearing recent files, resetting to default values, and saving/canceling changes.
public class SettingsWindowViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private bool _autoLoadF4SeLogs = true;
    private bool _autoSaveResults = true;
    private string _defaultGamePath = "";
    private string _defaultLogPath = "";
    private string _defaultOutputFormat = "text"; // Hardcoded to text - JSON/XML formats not yet implemented
    private string _defaultScanDirectory = "";
    private bool _enableDebugLogging;
    private bool _enableProgressNotifications = true;
    private int _maxLogMessages = 100;
    private int _maxRecentItems = 10;
    private UserSettings _originalSettings;
    private bool _rememberWindowSize = true;
    private double _windowHeight = 800;
    private double _windowWidth = 1200;
    private bool _enableUpdateCheck = true;
    private string _updateSource = "Both";

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
    /// <summary>
    /// Gets or sets the default log file path used by the application.
    /// This property is typically bound to the UI for user configuration of the default crash log file.
    /// It allows users to specify a file path to load or save log data as needed during runtime.
    /// Changes to this property raise notifications to update bindings or trigger additional logic.
    /// </summary>
    public string DefaultLogPath
    {
        get => _defaultLogPath;
        set => this.RaiseAndSetIfChanged(ref _defaultLogPath, value);
    }
    /// <summary>
    /// Gets or sets the default game installation path used by the application.
    /// This property is typically used to configure and store the location of the game's installation directory.
    /// It allows users to specify a directory path that the application can utilize for game-related operations.
    /// Changes to this property raise notifications to update bindings or trigger related logic as needed.
    /// </summary>
    public string DefaultGamePath
    {
        get => _defaultGamePath;
        set => this.RaiseAndSetIfChanged(ref _defaultGamePath, value);
    }
    /// <summary>
    /// Gets or sets the default directory path used to scan for files or logs within the application.
    /// This property is utilized for configuring the location to be scanned during runtime.
    /// Typically bound to the user interface, it allows users to specify or update the scan directory through the settings page.
    /// Changes to this property raise notifications to update relevant bindings or trigger associated logic.
    /// </summary>
    public string DefaultScanDirectory
    {
        get => _defaultScanDirectory;
        set => this.RaiseAndSetIfChanged(ref _defaultScanDirectory, value);
    }
    /// <summary>
    /// Gets or sets a value indicating whether F4SE crash logs should be automatically loaded by the application.
    /// This property is typically used to control the automated behavior of loading logs upon application startup
    /// or when specific events occur, improving user convenience.
    /// Changes to this property trigger notifications to update bindings, ensuring the UI reflects the current state.
    /// </summary>
    public bool AutoLoadF4SeLogs
    {
        get => _autoLoadF4SeLogs;
        set => this.RaiseAndSetIfChanged(ref _autoLoadF4SeLogs, value);
    }
    /// <summary>
    /// Gets or sets the maximum number of log messages that can be retained in the application's log history.
    /// This property is configurable by the user to control how many log entries are kept when logging activities.
    /// It is typically bound to UI elements for user input to dynamically adjust the log limit.
    /// </summary>
    public int MaxLogMessages
    {
        get => _maxLogMessages;
        set => this.RaiseAndSetIfChanged(ref _maxLogMessages, value);
    }
    /// <summary>
    /// Gets or sets a value indicating whether progress notifications are enabled in the application.
    /// This property is typically used to control the display or management of notifications
    /// related to the progress of ongoing operations. It supports binding to the user interface,
    /// allowing users to toggle this functionality as needed. Changes to this property raise notifications
    /// to update bindings or trigger additional logic.
    /// </summary>
    public bool EnableProgressNotifications
    {
        get => _enableProgressNotifications;
        set => this.RaiseAndSetIfChanged(ref _enableProgressNotifications, value);
    }
    /// <summary>
    /// Gets or sets a value indicating whether the application should remember
    /// the window's size and position across sessions.
    /// When enabled, this property ensures that the window is restored to its
    /// last known dimensions and location on subsequent launches.
    /// Changes to this property may trigger updates to UI bindings or save
    /// configuration settings for future use.
    /// </summary>
    public bool RememberWindowSize
    {
        get => _rememberWindowSize;
        set => this.RaiseAndSetIfChanged(ref _rememberWindowSize, value);
    }
    /// <summary>
    /// Gets or sets the width of the application window.
    /// This property represents the current width of the UI window and is usually bound to ensure
    /// persistent user interface settings, such as remembering the window size between application sessions.
    /// Changes to this property trigger notifications to update UI bindings or respond to layout adjustments.
    ///</summary>
    public double WindowWidth
    {
        get => _windowWidth;
        set => this.RaiseAndSetIfChanged(ref _windowWidth, value);
    }
    /// <summary>
    /// Gets or sets the height of the application window.
    /// This property determines the vertical size of the window and can be bound to the UI for user customization.
    /// Changes to this property trigger notifications to update bindings or apply window size adjustments at runtime.
    /// Default value is set to 800, but it is adjustable within specified constraints, such as during user configuration.
    /// </summary>
    public double WindowHeight
    {
        get => _windowHeight;
        set => this.RaiseAndSetIfChanged(ref _windowHeight, value);
    }
    /// <summary>
    /// Gets or sets a value indicating whether debug logging is enabled in the application.
    /// This property allows users to toggle detailed logging, primarily used for diagnostic purposes.
    /// Changes to this property may trigger UI updates or logging behavior modifications in the system.
    /// </summary>
    public bool EnableDebugLogging
    {
        get => _enableDebugLogging;
        set => this.RaiseAndSetIfChanged(ref _enableDebugLogging, value);
    }
    /// <summary>
    /// Gets or sets the maximum number of recent items to maintain in the application.
    /// This property determines the limit for how many recent items or paths are tracked
    /// and displayed in the user interface, such as in dropdowns or menus.
    /// Used for customizing the application behavior related to recent file or directory tracking.\
    /// </summary>
    public int MaxRecentItems
    {
        get => _maxRecentItems;
        set => this.RaiseAndSetIfChanged(ref _maxRecentItems, value);
    }
    /// <summary>
    /// Gets or sets a value indicating whether scan results should be automatically saved.
    /// When enabled, the application saves results without requiring explicit user action, streamlining the workflow.
    /// Typically bound to the user interface, this setting controls the behavior of automated result storage during operations.
    /// </summary>
    public bool AutoSaveResults
    {
        get => _autoSaveResults;
        set => this.RaiseAndSetIfChanged(ref _autoSaveResults, value);
    }
    /// <summary>
    /// Gets or sets the default format used for output in the application.
    /// This property is typically used to configure how output data is represented
    /// and could be set through the user interface or programmatically.
    /// Updates to this property may trigger property change notifications for data bindings.
    /// </summary>
    public string DefaultOutputFormat
    {
        get => _defaultOutputFormat;
        set => this.RaiseAndSetIfChanged(ref _defaultOutputFormat, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether automatic update checking is enabled.
    /// When enabled, the application will check for updates on startup.
    /// </summary>
    public bool EnableUpdateCheck
    {
        get => _enableUpdateCheck;
        set => this.RaiseAndSetIfChanged(ref _enableUpdateCheck, value);
    }

    /// <summary>
    /// Gets or sets the update source for checking new versions.
    /// Valid values are "Both", "GitHub", or "Nexus".
    /// </summary>
    public string UpdateSource
    {
        get => _updateSource;
        set => this.RaiseAndSetIfChanged(ref _updateSource, value);
    }

    public ObservableCollection<string> RecentLogFiles { get; } = [];
    public ObservableCollection<string> RecentGamePaths { get; } = [];
    public ObservableCollection<string> RecentScanDirectories { get; } = [];

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
    ///<summary>
    /// Creates a deep copy of the provided UserSettings instance.
    /// This method performs a deep copy of the UserSettings object,
    /// including copying all primitive properties and duplicating the contents
    /// of any collections to ensure no shared references between the original
    /// and copied objects.
    /// </summary>
    /// <param name="source">
    /// The UserSettings instance to copy. This value should not be null.
    /// </param>
    /// <returns>
    /// A new instance of UserSettings that is a deep copy of the source.
    /// </returns>
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
            SkipXseCopy = source.SkipXseCopy,
            EnableUpdateCheck = source.EnableUpdateCheck,
            UpdateSource = source.UpdateSource
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

    /// <summary>
    /// Asynchronously loads user settings from the settings service
    /// and applies the retrieved values to the current instance.
    /// If loading the settings fails, default settings are applied
    /// and the view model is reset accordingly.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of loading
    /// and applying settings.
    /// </returns>
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
                SkipXseCopy = defaultSettings.SkipXseCopy,
                EnableUpdateCheck = defaultSettings.EnableUpdateCheck,
                UpdateSource = defaultSettings.UpdateSource
            };
            _originalSettings = CreateDeepCopy(userDefaults);
            ResetToDefaults();
        }
    }

    /// <summary>
    /// Loads user settings into the current view model by populating its properties
    /// with values from the provided <see cref="UserSettings"/> instance.
    /// Additionally, notifies changes to relevant properties and updates collections.
    /// </summary>
    /// <param name="settings">
    /// The instance of <see cref="UserSettings"/> containing the values to apply.
    /// This parameter must not be null.
    /// </param>
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
        EnableUpdateCheck = settings.EnableUpdateCheck;
        UpdateSource = settings.UpdateSource;

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

    /// <summary>
    /// Creates a UserSettings instance using the current state of the SettingsWindowViewModel.
    /// This method extracts relevant properties and collections from the ViewModel
    /// and maps them to a new UserSettings instance.
    /// </summary>
    /// <returns>
    /// A UserSettings instance populated with the values and collections
    /// from the current SettingsWindowViewModel.
    /// </returns>
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
            DefaultOutputFormat = DefaultOutputFormat,
            EnableUpdateCheck = EnableUpdateCheck,
            UpdateSource = UpdateSource
        };

        foreach (var file in RecentLogFiles)
            settings.RecentLogFiles.Add(file);

        foreach (var path in RecentGamePaths)
            settings.RecentGamePaths.Add(path);

        foreach (var dir in RecentScanDirectories)
            settings.RecentScanDirectories.Add(dir);

        return settings;
    }

    /// <summary>
    /// Opens a file picker to allow the user to select a default log file.
    /// This method sets the DefaultLogPath property to the selected file path
    /// if a file is picked successfully and the result is not null or empty.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of opening a file picker
    /// and updating the default log path.
    /// </returns>
    private async Task BrowseLogPath()
    {
        if (ShowFilePickerAsync != null)
        {
            var result = await ShowFilePickerAsync("Select Default Log File", "*.log");
            if (!string.IsNullOrEmpty(result)) DefaultLogPath = result;
        }
    }

    /// <summary>
    /// Opens a folder picker dialog to allow the user to select the game installation directory.
    /// Updates the DefaultGamePath with the selected path if a valid path is chosen.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of displaying the folder picker.
    /// The task result has no return value.
    /// </returns>
    private async Task BrowseGamePath()
    {
        if (ShowFolderPickerAsync != null)
        {
            var result = await ShowFolderPickerAsync("Select Game Installation Directory");
            if (!string.IsNullOrEmpty(result)) DefaultGamePath = result;
        }
    }

    /// <summary>
    /// Opens a folder picker dialog to allow the user to select a default scan directory.
    /// If the user selects a directory, the selected path is assigned to the DefaultScanDirectory property.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of browsing and potentially updating the DefaultScanDirectory property.
    /// </returns>
    private async Task BrowseScanDirectory()
    {
        if (ShowFolderPickerAsync != null)
        {
            var result = await ShowFolderPickerAsync("Select Default Scan Directory");
            if (!string.IsNullOrEmpty(result)) DefaultScanDirectory = result;
        }
    }

    /// <summary>
    /// Clears all recent file paths stored in the ViewModel, including recent log files,
    /// recent game paths, and recent scan directories. After clearing these collections,
    /// it updates the associated properties that indicate the presence of recent items.
    /// </summary>
    private void ClearRecentFiles()
    {
        RecentLogFiles.Clear();
        RecentGamePaths.Clear();
        RecentScanDirectories.Clear();

        this.RaisePropertyChanged(nameof(HasRecentLogFiles));
        this.RaisePropertyChanged(nameof(HasRecentGamePaths));
        this.RaisePropertyChanged(nameof(HasRecentScanDirectories));
    }

    /// <summary>
    /// Resets the user's settings to their default values.
    /// This method retrieves the application's default settings
    /// using the settings service and applies them to the current
    /// settings, ensuring the user interface reflects these defaults.
    /// </summary>
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
            SkipXseCopy = appSettings.SkipXseCopy,
            EnableUpdateCheck = appSettings.EnableUpdateCheck,
            UpdateSource = appSettings.UpdateSource
        };
        LoadFromSettings(defaultSettings);
    }

    /// <summary>
    /// Saves the current user settings asynchronously.
    /// This method gathers the settings from the view model,
    /// persists them using the configured settings service,
    /// and then triggers the window close operation.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous save operation.
    /// </returns>
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

    /// <summary>
    /// Cancels any changes made to the user settings and reverts them to their original state.
    /// This method reloads the original settings that were saved at the start of the session
    /// and invokes the action to close the settings window if it has been defined.
    /// </summary>
    private void Cancel()
    {
        // Restore original settings
        LoadFromSettings(_originalSettings);
        CloseWindow?.Invoke();
    }
}
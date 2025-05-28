using System;
using System.Collections.Generic;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;
using Scanner111.Services;
using Scanner111.Services.Configuration;
using System.IO;
using Microsoft.Extensions.Logging;

namespace Scanner111.ViewModels.Tabs;

public class SettingsTabViewModel : ViewModelBase
{
    private readonly IEnhancedDialogService _dialogService;
    private readonly IConfigurationService _config;
    private readonly GameConfigurationHelper _gameConfig;
    private readonly ILogger<SettingsTabViewModel> _logger;

    private string _iniPath = "";
    private string _modsPath = "";
    private string _customScanPath = "";
    private bool _fcxMode = true;
    private bool _simplifyLogs = false;
    private bool _updateCheck = true;
    private bool _vrMode = false;
    private bool _showFormIdValues = false;
    private bool _moveUnsolvedLogs = true;
    private bool _audioNotifications = true;
    private string _updateSource = "Both";

    /// <summary>
    /// Provides the ViewModel logic for the Settings tab, enabling configuration of application settings
    /// such as file paths, preferences, and operational modes. This class manages user interaction
    /// through reactive commands for tasks like browsing directories, saving configurations, and resetting values.
    /// It utilizes relevant services for dialog interactions, configuration management, and logging,
    /// ensuring proper functionality and persistence of settings.
    /// </summary>
    public SettingsTabViewModel(
        IEnhancedDialogService dialogService,
        IConfigurationService config,
        GameConfigurationHelper gameConfig,
        ILogger<SettingsTabViewModel> logger)
    {
        _dialogService = dialogService;
        _config = config;
        _gameConfig = gameConfig;
        _logger = logger;

        // Initialize commands
        BrowseIniPathCommand = ReactiveCommand.CreateFromTask(BrowseIniPathAsync);
        BrowseModsPathCommand = ReactiveCommand.CreateFromTask(BrowseModsPathAsync);
        BrowseCustomScanPathCommand = ReactiveCommand.CreateFromTask(BrowseCustomScanPathAsync);
        SaveSettingsCommand = ReactiveCommand.CreateFromTask(SaveSettingsAsync);
        ResetSettingsCommand = ReactiveCommand.CreateFromTask(ResetSettingsAsync);

        // Load settings from configuration service
        LoadSettings();
    }

    // Folder Paths
    public string IniPath
    {
        get => _iniPath;
        set => this.RaiseAndSetIfChanged(ref _iniPath, value);
    }

    public string ModsPath
    {
        get => _modsPath;
        set => this.RaiseAndSetIfChanged(ref _modsPath, value);
    }

    public string CustomScanPath
    {
        get => _customScanPath;
        set => this.RaiseAndSetIfChanged(ref _customScanPath, value);
    }

    // Boolean Settings
    public bool FcxMode
    {
        get => _fcxMode;
        set => this.RaiseAndSetIfChanged(ref _fcxMode, value);
    }

    public bool SimplifyLogs
    {
        get => _simplifyLogs;
        set => this.RaiseAndSetIfChanged(ref _simplifyLogs, value);
    }

    public bool UpdateCheck
    {
        get => _updateCheck;
        set => this.RaiseAndSetIfChanged(ref _updateCheck, value);
    }

    public bool VrMode
    {
        get => _vrMode;
        set => this.RaiseAndSetIfChanged(ref _vrMode, value);
    }

    public bool ShowFormIdValues
    {
        get => _showFormIdValues;
        set => this.RaiseAndSetIfChanged(ref _showFormIdValues, value);
    }

    public bool MoveUnsolvedLogs
    {
        get => _moveUnsolvedLogs;
        set => this.RaiseAndSetIfChanged(ref _moveUnsolvedLogs, value);
    }

    public bool AudioNotifications
    {
        get => _audioNotifications;
        set => this.RaiseAndSetIfChanged(ref _audioNotifications, value);
    }

    // Update Source Options
    public string UpdateSource
    {
        get => _updateSource;
        set => this.RaiseAndSetIfChanged(ref _updateSource, value);
    }

    public List<string> UpdateSourceOptions { get; } = new() { "Nexus", "GitHub", "Both" };

    // Commands
    public ReactiveCommand<Unit, Unit> BrowseIniPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseModsPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseCustomScanPathCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveSettingsCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetSettingsCommand { get; }

    // Command implementations
    ///<summary>
    /// Asynchronously handles browsing for the INI file directory by presenting
    /// a folder selection dialog to the user. If a valid selection is made, the
    /// selected path is assigned to the IniPath property and logged. In case of
    /// an error during the process, it logs the error and displays an error message
    /// dialog to the user.
    /// </summary>
    /// <returns>
    /// A Task representing the asynchronous operation.
    /// </returns>
    private async Task BrowseIniPathAsync()
    {
        try
        {
            var selectedPath = await _dialogService.ShowFolderPickerAsync(
                "Select Game INI Files Directory",
                string.IsNullOrEmpty(IniPath) ? GetDefaultIniPath() : IniPath);

            if (!string.IsNullOrEmpty(selectedPath))
            {
                IniPath = selectedPath;
                _logger.LogDebug("INI path updated to: {Path}", selectedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing for INI path");
            await _dialogService.ShowMessageBoxAsync("Error", "Failed to browse for INI directory.");
        }
    }

    ///<summary>
    /// Handles the asynchronous logic to browse for the Mods folder path, allowing the user
    /// to select a directory through a dialog interface. Updates the `ModsPath` property
    /// if a valid path is selected and logs the change for debugging purposes. Displays
    /// an error message if an exception occurs during the operation.
    /// </summary>
    /// <returns>
    /// A Task representing the asynchronous operation, which completes once the browsing
    /// process has ended and the `ModsPath` is updated or an error is handled.
    /// </returns>
    private async Task BrowseModsPathAsync()
    {
        try
        {
            var selectedPath = await _dialogService.ShowFolderPickerAsync(
                "Select Staging Mods Folder (Mod Manager)",
                string.IsNullOrEmpty(ModsPath) ? null : ModsPath);

            if (!string.IsNullOrEmpty(selectedPath))
            {
                ModsPath = selectedPath;
                _logger.LogDebug("Mods path updated to: {Path}", selectedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing for mods path");
            await _dialogService.ShowMessageBoxAsync("Error", "Failed to browse for mods directory.");
        }
    }

    ///<summary>
    /// Asynchronously allows the user to browse and select a custom scan folder path.
    /// This method utilizes a folder picker dialog to let the user choose a directory
    /// for custom crash logs and updates the corresponding property. Logging is performed
    /// to capture updates or any errors encountered during the folder browsing process.
    /// Internally, this method interacts with the enhanced dialog service to show the
    /// folder picker and handles potential exceptions that might occur during the operation.
    /// </summary>
    /// <remarks>Dependencies:<br/>
    /// - `IEnhancedDialogService`: Used to display the folder picker dialog.<br/>
    /// - `ILogger`: Used to log debug or error information during execution.
    /// </remarks>
    /// <returns>
    /// A `Task` representing the asynchronous operation.
    /// </returns>
    private async Task BrowseCustomScanPathAsync()
    {
        try
        {
            var selectedPath = await _dialogService.ShowFolderPickerAsync(
                "Select Custom Crash Logs Folder",
                string.IsNullOrEmpty(CustomScanPath) ? null : CustomScanPath);

            if (!string.IsNullOrEmpty(selectedPath))
            {
                CustomScanPath = selectedPath;
                _logger.LogDebug("Custom scan path updated to: {Path}", selectedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing for custom scan path");
            await _dialogService.ShowMessageBoxAsync("Error", "Failed to browse for custom scan directory.");
        }
    }

    ///<summary>
    /// Asynchronously saves the current application settings to the configuration service.
    /// This method persists both folder paths and various feature toggles to the application's
    /// configuration storage. It provides user feedback through the dialog service and logs the
    /// operation's outcome. In case of an error during the save process, it logs detailed information
    /// and informs the user of the failure.
    /// </summary>
    /// <return>Returns a Task representing the asynchronous save operation.</return>
    private async Task SaveSettingsAsync()
    {
        try
        {
            // Save folder paths
            await _config.SetSettingAsync("INI Folder Path", IniPath);
            await _config.SetSettingAsync("MODS Folder Path", ModsPath);
            await _config.SetSettingAsync("SCAN Custom Path", CustomScanPath);

            // Save boolean settings
            await _config.SetSettingAsync("FCX Mode", FcxMode);
            await _config.SetSettingAsync("Simplify Logs", SimplifyLogs);
            await _config.SetSettingAsync("Update Check", UpdateCheck);
            await _config.SetSettingAsync("VR Mode", VrMode);
            await _config.SetSettingAsync("Show FormID Values", ShowFormIdValues);
            await _config.SetSettingAsync("Move Unsolved Logs", MoveUnsolvedLogs);
            await _config.SetSettingAsync("Audio Notifications", AudioNotifications);
            await _config.SetSettingAsync("Update Source", UpdateSource);

            await _dialogService.ShowMessageBoxAsync("Settings Saved",
                "All settings have been saved successfully!");

            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings");
            await _dialogService.ShowMessageBoxAsync("Error",
                "Failed to save settings. Please check the application logs for details.");
        }
    }

    ///<summary>
    /// Resets the application settings to their default values asynchronously.
    /// This method displays a confirmation dialog to the user. If confirmed, it resets
    /// all configurable settings to their predefined defaults, persists these changes,
    /// and logs an informational message. If an error occurs during the process, it
    /// logs the error and shows an error message dialog.
    /// </summary>
    ///<remarks>
    /// Dependencies:<br/>
    /// - IEnhancedDialogService: Used to display confirmation and error dialogs.<br/>
    /// - ILogger: Used to log actions, such as resets and errors.<br/>
    /// - SaveSettingsAsync: Called to persist default settings.
    /// </remarks>
    /// <return> A task that represents the asynchronous operation of resetting settings.</return>
    private async Task ResetSettingsAsync()
    {
        try
        {
            var confirmed = await _dialogService.ShowConfirmationAsync("Reset Settings",
                "Are you sure you want to reset all settings to their default values?\n\n" +
                "This action cannot be undone.");

            if (confirmed)
            {
                ResetToDefaults();
                await SaveSettingsAsync(); // Save the defaults

                _logger.LogInformation("Settings reset to defaults");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting settings");
            await _dialogService.ShowMessageBoxAsync("Error", "Failed to reset settings.");
        }
    }

    ///<summary>
    /// Resets all application settings and configurations to their default values.
    /// This method is used to initialize or revert settings to a predefined state,
    /// matching the application's defaults. It modifies relevant properties such as
    /// file paths, application modes, log settings, update preferences, and UI-related
    /// options to ensure consistent default behavior.
    /// </summary>
    /// <remarks>
    /// Properties reset include:<br/>
    /// - IniPath: Resets to an empty string.<br/>
    /// - ModsPath: Resets to an empty string.<br/>
    /// - CustomScanPath: Resets to an empty string.<br/>
    /// - FcxMode: Resets to false.<br/>
    /// - SimplifyLogs: Resets to false.<br/>
    /// - UpdateCheck: Resets to true.<br/>
    /// - VrMode: Resets to false.<br/>
    /// - ShowFormIdValues: Resets to true.<br/>
    /// - MoveUnsolvedLogs: Resets to true.<br/>
    /// - AudioNotifications: Resets to true.<br/>
    /// - UpdateSource: Resets to "Both".<br/>
    /// </remarks>
    private void ResetToDefaults()
    {
        // Reset to default values matching the Python version
        IniPath = "";
        ModsPath = "";
        CustomScanPath = "";
        FcxMode = false;
        SimplifyLogs = false;
        UpdateCheck = true;
        VrMode = false;
        ShowFormIdValues = true;
        MoveUnsolvedLogs = true;
        AudioNotifications = true;
        UpdateSource = "Both";
    }

    ///<summary>
    /// Loads the application settings for the ViewModel, initializing properties
    /// with values retrieved from the configuration services and applying default
    /// values where necessary.
    /// This method retrieves user-defined paths, scanner settings, and various
    /// configuration options from the GameConfigurationHelper service. It ensures
    /// sensible defaults are applied if expected values are not present. The method
    /// also separately handles VR mode as it may impact other settings.
    /// Errors during the loading process are logged, and the settings are reset to defaults
    /// in case of a failure.
    /// </summary>
    /// <remarks>
    /// Dependencies:<br/>
    /// - GameConfigurationHelper: Used to retrieve user paths and scanner settings.<br/>
    /// - ILogger: Logs debug and error information related to the loading process.
    /// </remarks>
    private void LoadSettings()
    {
        try
        {
            // Load user paths
            var userPaths = _gameConfig.GetUserPaths();
            IniPath = userPaths.IniPath ?? "";
            ModsPath = userPaths.ModsPath ?? "";
            CustomScanPath = userPaths.CustomScanPath ?? "";

            // Load scanner settings
            var scannerSettings = _gameConfig.GetScannerSettings();
            FcxMode = scannerSettings.FcxMode;
            SimplifyLogs = scannerSettings.SimplifyLogs;
            UpdateCheck = scannerSettings.UpdateCheck;
            ShowFormIdValues = scannerSettings.ShowFormIdValues;
            MoveUnsolvedLogs = scannerSettings.MoveUnsolvedLogs;
            AudioNotifications = scannerSettings.AudioNotifications;
            UpdateSource = scannerSettings.UpdateSource;

            // Load VR mode separately as it affects other settings
            VrMode = _gameConfig.IsVrMode();

            // Try to set a reasonable default for INI path
            if (string.IsNullOrEmpty(IniPath))
            {
                var defaultIniPath = GetDefaultIniPath();
                if (Directory.Exists(defaultIniPath)) IniPath = defaultIniPath;
            }

            _logger.LogDebug("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings, using defaults");
            ResetToDefaults();
        }
    }

    ///<summary>
    /// Determines the default path to the Fallout 4 INI files, located in
    /// the "My Games" directory within the user's documents folder.
    /// The returned path is constructed based on the system environment
    /// and adheres to the standard file structure for Fallout 4 settings.
    /// </summary>
    /// <return>
    /// A string representing the default path to the Fallout 4 INI files.
    /// </return>
    private static string GetDefaultIniPath()
    {
        // Try to find the default Fallout 4 INI path
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var fallout4Path = Path.Combine(documentsPath, "My Games", "Fallout4");
        return fallout4Path;
    }

    // Setting descriptions for tooltips (matching Python descriptions)
    public string FcxModeDescription =>
        "Enable extended file integrity checks for comprehensive diagnostics of game and mod files";

    public string SimplifyLogsDescription =>
        "Remove redundant lines from crash logs (permanent changes - may hide info useful for debugger programs)";

    public string UpdateCheckDescription =>
        "Automatically check for Scanner 111 updates online through GitHub and Nexus";

    public string VrModeDescription =>
        "Prioritize settings for VR version of the game (affects file paths and configurations)";

    public string ShowFormIdValuesDescription =>
        "Look up FormID names from database (slower scans but provides more detailed information)";

    public string MoveUnsolvedLogsDescription =>
        "Move incomplete/unscannable crash logs to separate 'Unsolved Logs' folder";

    public string AudioNotificationsDescription =>
        "Play notification sounds when scanning completes or errors occur";
}
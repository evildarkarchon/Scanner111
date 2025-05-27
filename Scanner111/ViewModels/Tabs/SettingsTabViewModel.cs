using System.Collections.Generic;
using ReactiveUI;
using System.Reactive;
using System.Threading.Tasks;

namespace Scanner111.ViewModels.Tabs;

public class SettingsTabViewModel : ViewModelBase
{
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

    public SettingsTabViewModel()
    {
        // Initialize commands
        BrowseIniPathCommand = ReactiveCommand.CreateFromTask(BrowseIniPathAsync);
        BrowseModsPathCommand = ReactiveCommand.CreateFromTask(BrowseModsPathAsync);
        BrowseCustomScanPathCommand = ReactiveCommand.CreateFromTask(BrowseCustomScanPathAsync);
        SaveSettingsCommand = ReactiveCommand.Create(SaveSettings);
        ResetSettingsCommand = ReactiveCommand.Create(ResetSettings);

        // TODO: Load settings from configuration file
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
    private async Task BrowseIniPathAsync()
    {
        // TODO: Implement folder browser dialog
        await Task.Delay(100);
        // Placeholder - would use Avalonia's file dialog
        IniPath = @"C:\Users\Example\Documents\My Games\Fallout4";
    }

    private async Task BrowseModsPathAsync()
    {
        // TODO: Implement folder browser dialog
        await Task.Delay(100);
        ModsPath = @"C:\ModOrganizer2\Fallout4\mods";
    }

    private async Task BrowseCustomScanPathAsync()
    {
        // TODO: Implement folder browser dialog
        await Task.Delay(100);
        CustomScanPath = @"C:\CrashLogs";
    }

    private void SaveSettings()
    {
        // TODO: Implement settings save to configuration file
        // For now just a placeholder
    }

    private void ResetSettings()
    {
        // Reset to default values
        IniPath = "";
        ModsPath = "";
        CustomScanPath = "";
        FcxMode = true;
        SimplifyLogs = false;
        UpdateCheck = true;
        VrMode = false;
        ShowFormIdValues = false;
        MoveUnsolvedLogs = true;
        AudioNotifications = true;
        UpdateSource = "Both";
    }

    private void LoadSettings()
    {
        // TODO: Load settings from configuration file
        // For now using defaults set in field initializers
    }

    // Setting descriptions for tooltips
    public string FcxModeDescription => "Enable extended file integrity checks for comprehensive diagnostics";
    public string SimplifyLogsDescription => "Remove redundant lines from crash logs (permanent changes)";
    public string UpdateCheckDescription => "Automatically check for Scanner 111 updates";
    public string VrModeDescription => "Prioritize settings for VR version of the game";
    public string ShowFormIdValuesDescription => "Look up FormID names (slower scans but more detailed)";
    public string MoveUnsolvedLogsDescription => "Move incomplete/unscannable logs to separate folder";
    public string AudioNotificationsDescription => "Play sounds for scan completion and errors";
}
using System;
using System.Diagnostics;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Scanner111.Common.Services.Logging;
using Scanner111.Services;

namespace Scanner111.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly ILogPathProvider _logPathProvider;

    public ReactiveCommand<Unit, Unit> BrowseScanPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseModsFolderPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseIniFolderPathCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenLogsFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> CopyLogPathCommand { get; }

    [Reactive] public string ScanPath { get; set; } = string.Empty;
    [Reactive] public string ModsFolderPath { get; set; } = string.Empty;
    [Reactive] public int MaxConcurrent { get; set; } = 50;
    [Reactive] public bool FcxMode { get; set; }
    [Reactive] public bool ShowFormIdValues { get; set; }
    [Reactive] public bool CheckForUpdatesOnStartup { get; set; } = true;
    [Reactive] public bool IncludePrereleases { get; set; }
    [Reactive] public bool VrMode { get; set; }
    [Reactive] public bool SimplifyLogs { get; set; }
    [Reactive] public bool MoveUnsolvedLogs { get; set; }
    [Reactive] public string IniFolderPath { get; set; } = string.Empty;
    [Reactive] public string StatusText { get; set; } = string.Empty;
    [Reactive] public string LogFolderPath { get; private set; } = string.Empty;

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService, ILogPathProvider logPathProvider)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _logPathProvider = logPathProvider;

        // Load current settings
        ScanPath = _settingsService.ScanPath;
        ModsFolderPath = _settingsService.ModsFolderPath;
        MaxConcurrent = _settingsService.MaxConcurrent;
        FcxMode = _settingsService.FcxMode;
        ShowFormIdValues = _settingsService.ShowFormIdValues;
        CheckForUpdatesOnStartup = _settingsService.CheckForUpdatesOnStartup;
        IncludePrereleases = _settingsService.IncludePrereleases;
        VrMode = _settingsService.VrMode;
        SimplifyLogs = _settingsService.SimplifyLogs;
        MoveUnsolvedLogs = _settingsService.MoveUnsolvedLogs;
        IniFolderPath = _settingsService.IniFolderPath;
        LogFolderPath = _logPathProvider.GetLogDirectory();

        BrowseScanPathCommand = ReactiveCommand.CreateFromTask(BrowseScanPathAsync);
        BrowseModsFolderPathCommand = ReactiveCommand.CreateFromTask(BrowseModsFolderPathAsync);
        BrowseIniFolderPathCommand = ReactiveCommand.CreateFromTask(BrowseIniFolderPathAsync);
        SaveCommand = ReactiveCommand.Create(SaveSettings);
        ResetToDefaultsCommand = ReactiveCommand.Create(ResetToDefaults);
        OpenLogsFolderCommand = ReactiveCommand.Create(OpenLogsFolder);
        CopyLogPathCommand = ReactiveCommand.CreateFromTask(CopyLogPathAsync);
    }

    private async Task BrowseScanPathAsync()
    {
        var folder = await _dialogService.ShowFolderPickerAsync("Select Scan Path", ScanPath);
        if (!string.IsNullOrEmpty(folder))
        {
            ScanPath = folder;
        }
    }

    private async Task BrowseModsFolderPathAsync()
    {
        var folder = await _dialogService.ShowFolderPickerAsync("Select Mods Folder", ModsFolderPath);
        if (!string.IsNullOrEmpty(folder))
        {
            ModsFolderPath = folder;
        }
    }

    private async Task BrowseIniFolderPathAsync()
    {
        var folder = await _dialogService.ShowFolderPickerAsync("Select INI Folder", IniFolderPath);
        if (!string.IsNullOrEmpty(folder))
        {
            IniFolderPath = folder;
        }
    }

    private void SaveSettings()
    {
        _settingsService.ScanPath = ScanPath;
        _settingsService.ModsFolderPath = ModsFolderPath;
        _settingsService.MaxConcurrent = MaxConcurrent;
        _settingsService.FcxMode = FcxMode;
        _settingsService.ShowFormIdValues = ShowFormIdValues;
        _settingsService.CheckForUpdatesOnStartup = CheckForUpdatesOnStartup;
        _settingsService.IncludePrereleases = IncludePrereleases;
        _settingsService.VrMode = VrMode;
        _settingsService.SimplifyLogs = SimplifyLogs;
        _settingsService.MoveUnsolvedLogs = MoveUnsolvedLogs;
        _settingsService.IniFolderPath = IniFolderPath;
        _settingsService.Save();

        StatusText = "Settings saved!";
    }

    private void ResetToDefaults()
    {
        _settingsService.ResetToDefaults();

        // Reload values from service
        ScanPath = _settingsService.ScanPath;
        ModsFolderPath = _settingsService.ModsFolderPath;
        MaxConcurrent = _settingsService.MaxConcurrent;
        FcxMode = _settingsService.FcxMode;
        ShowFormIdValues = _settingsService.ShowFormIdValues;
        CheckForUpdatesOnStartup = _settingsService.CheckForUpdatesOnStartup;
        IncludePrereleases = _settingsService.IncludePrereleases;
        VrMode = _settingsService.VrMode;
        SimplifyLogs = _settingsService.SimplifyLogs;
        MoveUnsolvedLogs = _settingsService.MoveUnsolvedLogs;
        IniFolderPath = _settingsService.IniFolderPath;

        StatusText = "Settings reset to defaults.";
    }

    private void OpenLogsFolder()
    {
        var logDirectory = _logPathProvider.GetLogDirectory();

        // Create directory if it doesn't exist
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }

        // Open in Windows Explorer
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = logDirectory,
            UseShellExecute = true
        });

        StatusText = "Opened logs folder in Explorer.";
    }

    private async Task CopyLogPathAsync()
    {
        var logDirectory = _logPathProvider.GetLogDirectory();

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboard = desktop.MainWindow?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(logDirectory);
                StatusText = "Log folder path copied to clipboard.";
            }
        }
    }
}
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Scanner111.Services;

namespace Scanner111.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;

    public ReactiveCommand<Unit, Unit> BrowseScanPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseModsFolderPathCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }
    public ReactiveCommand<Unit, Unit> ResetToDefaultsCommand { get; }

    [Reactive] public string ScanPath { get; set; } = string.Empty;
    [Reactive] public string ModsFolderPath { get; set; } = string.Empty;
    [Reactive] public int MaxConcurrent { get; set; } = 50;
    [Reactive] public bool FcxMode { get; set; }
    [Reactive] public bool ShowFormIdValues { get; set; }
    [Reactive] public string StatusText { get; set; } = string.Empty;

    public SettingsViewModel(ISettingsService settingsService, IDialogService dialogService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;

        // Load current settings
        ScanPath = _settingsService.ScanPath;
        ModsFolderPath = _settingsService.ModsFolderPath;
        MaxConcurrent = _settingsService.MaxConcurrent;
        FcxMode = _settingsService.FcxMode;
        ShowFormIdValues = _settingsService.ShowFormIdValues;

        BrowseScanPathCommand = ReactiveCommand.CreateFromTask(BrowseScanPathAsync);
        BrowseModsFolderPathCommand = ReactiveCommand.CreateFromTask(BrowseModsFolderPathAsync);
        SaveCommand = ReactiveCommand.Create(SaveSettings);
        ResetToDefaultsCommand = ReactiveCommand.Create(ResetToDefaults);
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

    private void SaveSettings()
    {
        _settingsService.ScanPath = ScanPath;
        _settingsService.ModsFolderPath = ModsFolderPath;
        _settingsService.MaxConcurrent = MaxConcurrent;
        _settingsService.FcxMode = FcxMode;
        _settingsService.ShowFormIdValues = ShowFormIdValues;
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

        StatusText = "Settings reset to defaults.";
    }
}
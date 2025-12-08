using System.Reactive;
using ReactiveUI;
using ReactiveUI.Fody.Helpers; // Required for [Reactive] attribute
using System; // Required for Console.WriteLine

namespace Scanner111.ViewModels;

public class SettingsViewModel : ViewModelBase
{
    public ReactiveCommand<Unit, Unit> BrowseScanPathCommand { get; }
    public ReactiveCommand<Unit, Unit> BrowseModsFolderPathCommand { get; }
    public ReactiveCommand<Unit, Unit> SaveCommand { get; }

    [Reactive] public string ScanPath { get; set; } = string.Empty;
    [Reactive] public string ModsFolderPath { get; set; } = string.Empty;
    [Reactive] public int MaxConcurrent { get; set; } = 50;

    public SettingsViewModel()
    {
        BrowseScanPathCommand = ReactiveCommand.Create(BrowseScanPath);
        BrowseModsFolderPathCommand = ReactiveCommand.Create(BrowseModsFolderPath);
        SaveCommand = ReactiveCommand.Create(SaveSettings);
    }

    private void BrowseScanPath()
    {
        Console.WriteLine("Browse Scan Path clicked"); // Placeholder
    }
    
    private void BrowseModsFolderPath()
    {
        Console.WriteLine("Browse Mods Folder Path clicked"); // Placeholder
    }

    private void SaveSettings()
    {
        Console.WriteLine("Save Settings clicked"); // Placeholder
    }
}
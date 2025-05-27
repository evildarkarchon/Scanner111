using System;
using ReactiveUI;
using Scanner111.ViewModels.Tabs;
using System.Reactive;

namespace Scanner111.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private int _selectedTabIndex;
    private string _statusMessage = "Ready";
    private bool _isScanning;

    public MainWindowViewModel()
    {
        // Initialize tab ViewModels
        MainTabViewModel = new MainTabViewModel();
        SettingsTabViewModel = new SettingsTabViewModel();
        ArticlesTabViewModel = new ArticlesTabViewModel();
        BackupsTabViewModel = new BackupsTabViewModel();

        // Initialize commands
        ShowAboutCommand = ReactiveCommand.Create(ShowAbout);
        ExitCommand = ReactiveCommand.Create(Exit);

        // Set up property change subscriptions if needed
        this.WhenAnyValue(x => x.IsScanning)
            .Subscribe(isScanning => StatusMessage = isScanning ? "Scanning..." : "Ready");
    }

    // Tab ViewModels
    public MainTabViewModel MainTabViewModel { get; }
    public SettingsTabViewModel SettingsTabViewModel { get; }
    public ArticlesTabViewModel ArticlesTabViewModel { get; }
    public BackupsTabViewModel BackupsTabViewModel { get; }

    // Properties
    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => this.RaiseAndSetIfChanged(ref _selectedTabIndex, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => this.RaiseAndSetIfChanged(ref _isScanning, value);
    }

    // Commands
    public ReactiveCommand<Unit, Unit> ShowAboutCommand { get; }
    public ReactiveCommand<Unit, Unit> ExitCommand { get; }

    // Command implementations
    private void ShowAbout()
    {
        // TODO: Implement about dialog
        StatusMessage = "About dialog - TODO";
    }

    private void Exit()
    {
        // TODO: Implement proper exit logic
        Environment.Exit(0);
    }

    // Application title and version
    public string Title => "Scanner 111 - Vault-Tec Diagnostic Tool";
    public string Version => "v1.0.0"; // TODO: Get from assembly or config
}
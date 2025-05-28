using System;
using ReactiveUI;
using Scanner111.ViewModels.Tabs;
using System.Reactive;
using Scanner111.Services;
using Scanner111.Services.Configuration;
using Microsoft.Extensions.Logging;

namespace Scanner111.ViewModels;

/// <summary>
/// Serves as the view model for the main window of the Scanner 111 application.
/// </summary>
/// <remarks>
/// Provides core functionality for managing the main application window, including:
/// - Coordination of multiple tab view models such as Main, Settings, Articles, and Backups.
/// - Handling user interactions, command execution, and status updates.
/// - Integration with logging, configuration, and dialog services.
/// Encapsulates the UI logic and ensures seamless communication between the UI and backend services.
/// </remarks>
public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IConfigurationService _config;
    private int _selectedTabIndex;
    private string _statusMessage = "Ready";
    private bool _isScanning;

    /// <summary>
    /// Represents the main view model for the application's main window.
    /// </summary>
    /// <remarks>
    /// This view model serves as the central point for coordinating UI functionality in the main window.
    /// It manages application tabs, dialog services, commands, and reactive properties for UI interactions.
    /// </remarks>
    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IConfigurationService config,
        IEnhancedDialogService dialogService,
        MainTabViewModel mainTabViewModel,
        SettingsTabViewModel settingsTabViewModel,
        ArticlesTabViewModel articlesTabViewModel,
        BackupsTabViewModel backupsTabViewModel)
    {
        _logger = logger;
        _config = config;

        // Set dialog service reference
        DialogService = dialogService;

        // Initialize tab ViewModels (injected)
        MainTabViewModel = mainTabViewModel;
        SettingsTabViewModel = settingsTabViewModel;
        ArticlesTabViewModel = articlesTabViewModel;
        BackupsTabViewModel = backupsTabViewModel;

        // Initialize commands
        ShowAboutCommand = ReactiveCommand.Create(ShowAbout);
        ExitCommand = ReactiveCommand.Create(Exit);

        // Set up property change subscriptions
        this.WhenAnyValue(x => x.IsScanning)
            .Subscribe(isScanning => StatusMessage = isScanning ? "Scanning..." : "Ready");

        _logger.LogInformation("Main window view model initialized");
    }

    // Dialog service
    public IDialogService DialogService { get; }

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
    /// <summary>
    /// Displays the "About" dialog with application information.
    /// </summary>
    /// <remarks>
    /// This method provides details about the application including its name, version,
    /// the game being managed, and a brief description of its functionality.
    /// It uses the dialog service to show the information in a message box.
    /// If an error occurs while attempting to display the dialog, an error message is logged
    /// and an error dialog is displayed.
    /// </remarks>
    private async void ShowAbout()
    {
        try
        {
            var version = Version;
            var managedGame = _config.GetSetting("Managed Game", "Fallout 4");

            await DialogService.ShowMessageBoxAsync("About Scanner 111",
                $"Scanner 111 - Vault-Tec Diagnostic Tool\n\n" +
                $"Version: {version}\n" +
                $"Managed Game: {managedGame}\n\n" +
                $"A comprehensive tool for diagnosing and fixing issues with Bethesda RPGs.\n\n" +
                $"Based on the CLASSIC framework.\n" +
                $"Built with Avalonia and C#.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing about dialog");
            await DialogService.ShowMessageBoxAsync("Error", "Failed to show about information.");
        }
    }

    private void Exit()
    {
        try
        {
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during application exit");
        }
    }

    // Application title and version
    public string Title => "Scanner 111 - Vault-Tec Diagnostic Tool";
    public string Version => "v1.0.0"; // TODO: Get from assembly or config
}
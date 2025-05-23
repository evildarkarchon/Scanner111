using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using Scanner111.Services;
using Scanner111.Services.Interfaces;

namespace Scanner111.ViewModels;

/// <summary>
///     ViewModel for handling update checking functionality in the UI
/// </summary>
public class UpdateViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly ILogger _logger;
    private readonly IUpdateService _updateService;

    private bool _isCheckingForUpdates;

    public UpdateViewModel(IUpdateService updateService, IDialogService dialogService, ILogger logger)
    {
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync);
    }

    /// <summary>
    ///     Command to check for updates
    /// </summary>
    public ICommand CheckForUpdatesCommand { get; }

    /// <summary>
    ///     Indicates if an update check is in progress
    /// </summary>
    public bool IsCheckingForUpdates
    {
        get => _isCheckingForUpdates;
        private set => this.RaiseAndSetIfChanged(ref _isCheckingForUpdates, value);
    }

    /// <summary>
    ///     Asynchronously checks for updates and shows appropriate dialogs based on results
    /// </summary>
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates)
            return;

        try
        {
            IsCheckingForUpdates = true;
            _logger.Info("Checking for updates...");

            // Set quiet=true to avoid console output in GUI context
            await _updateService.IsLatestVersionAsync(true);

            // If we get here, there are no updates (exceptions are thrown if updates available)
            await _dialogService.ShowInfoDialogAsync("Update Check", "You have the latest version of CLASSIC!");
        }
        catch (UpdateCheckException ex) when (ex.Message.Contains("new version is available"))
        {
            var openBrowser = await _dialogService.ShowYesNoDialogAsync(
                "Update Available",
                "A new version of CLASSIC is available. Would you like to open the download page?");

            if (openBrowser)
            {
                // Open browser to the download page
                var psi = new ProcessStartInfo
                {
                    FileName = "https://www.nexusmods.com/fallout4/mods/56255",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
        }
        catch (UpdateCheckException ex)
        {
            _logger.Error($"Update check failed: {ex.Message}");
            await _dialogService.ShowErrorDialogAsync("Update Check Failed",
                $"Could not check for updates: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error during update check: {ex.Message}", ex);
            await _dialogService.ShowErrorDialogAsync("Update Check Error",
                "An unexpected error occurred while checking for updates.");
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    /// <summary>
    ///     Silently checks for updates during application startup
    /// </summary>
    public async Task CheckForUpdatesStartupAsync()
    {
        try
        {
            _logger.Debug("Performing startup update check...");
            await _updateService.IsLatestVersionAsync(true, false);
            // No UI feedback needed for startup check if up to date
        }
        catch (UpdateCheckException ex) when (ex.Message.Contains("new version is available"))
        {
            await _dialogService.ShowInfoDialogAsync("Update Available",
                "A new version of CLASSIC is available. Check the updates button for details.");
        }
        catch (Exception ex)
        {
            _logger.Error($"Startup update check failed: {ex.Message}");
            // Silently fail on startup, don't show error dialog
        }
    }
}
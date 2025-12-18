using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using Scanner111.Common.Services.Updates;
using Scanner111.Services;

namespace Scanner111.ViewModels;

/// <summary>
/// ViewModel for the About page showing version and update information.
/// </summary>
public class AboutViewModel : ViewModelBase
{
    private readonly IUpdateService _updateService;
    private readonly ISettingsService _settingsService;

    [Reactive] public string CurrentVersion { get; set; } = "Loading...";
    [Reactive] public string LatestVersion { get; set; } = "Unknown";
    [Reactive] public string UpdateStatus { get; set; } = "Checking for updates...";
    [Reactive] public string ReleaseNotes { get; set; } = string.Empty;
    [Reactive] public string ReleaseUrl { get; set; } = string.Empty;
    [Reactive] public bool IsUpdateAvailable { get; set; }
    [Reactive] public bool IsChecking { get; set; }
    [Reactive] public bool HasError { get; set; }
    [Reactive] public string ErrorMessage { get; set; } = string.Empty;
    [Reactive] public bool IsPrerelease { get; set; }
    [Reactive] public DateTimeOffset? PublishedAt { get; set; }

    public ReactiveCommand<Unit, Unit> CheckForUpdatesCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenReleasePageCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenIssuesCommand { get; }

    public AboutViewModel(IUpdateService updateService, ISettingsService settingsService)
    {
        _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        CurrentVersion = _updateService.GetCurrentVersion();

        var canCheck = this.WhenAnyValue(x => x.IsChecking, checking => !checking);

        CheckForUpdatesCommand = ReactiveCommand.CreateFromTask(CheckForUpdatesAsync, canCheck);
        OpenReleasePageCommand = ReactiveCommand.Create(OpenReleasePage);
        OpenGitHubCommand = ReactiveCommand.Create(() => OpenUrl("https://github.com/evildarkarchon/Scanner111"));
        OpenIssuesCommand = ReactiveCommand.Create(() => OpenUrl("https://github.com/evildarkarchon/Scanner111/issues"));

        // Auto-check on construction
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        IsChecking = true;
        HasError = false;
        ErrorMessage = string.Empty;
        UpdateStatus = "Checking for updates...";

        try
        {
            var result = await _updateService.CheckForUpdatesAsync(
                _settingsService.IncludePrereleases);

            if (result.Success)
            {
                CurrentVersion = result.CurrentVersion ?? "Unknown";

                if (result.LatestRelease is not null)
                {
                    LatestVersion = result.LatestRelease.Version;
                    ReleaseNotes = result.LatestRelease.ReleaseNotes ?? "No release notes available.";
                    ReleaseUrl = result.LatestRelease.HtmlUrl;
                    IsPrerelease = result.LatestRelease.IsPrerelease;
                    PublishedAt = result.LatestRelease.PublishedAt;
                }

                IsUpdateAvailable = result.IsUpdateAvailable;
                UpdateStatus = result.IsUpdateAvailable
                    ? $"Update available: {LatestVersion}"
                    : "You are running the latest version.";
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? "Unknown error occurred.";
                UpdateStatus = "Failed to check for updates.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            UpdateStatus = "Failed to check for updates.";
        }
        finally
        {
            IsChecking = false;
        }
    }

    private void OpenReleasePage()
    {
        if (!string.IsNullOrEmpty(ReleaseUrl))
        {
            OpenUrl(ReleaseUrl);
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Silently fail if browser can't be opened
        }
    }
}

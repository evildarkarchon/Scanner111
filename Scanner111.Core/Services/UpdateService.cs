using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
///     Service for checking application updates from GitHub and Nexus sources
/// </summary>
public class UpdateService : IUpdateService
{
    private const string GitHubOwner = "evildarkarchon";
    private const string GitHubRepo = "Scanner111";
    private const string NexusModUrl = "https://www.nexusmods.com/fallout4/mods/56255";
    private static readonly HttpClient HttpClient = new();

    private readonly ILogger<UpdateService> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly IApplicationSettingsService _settingsService;

    static UpdateService()
    {
        // GitHub API requires a User-Agent header
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "Scanner111/1.0");
        // Accept header for GitHub API
        HttpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
    }

    public UpdateService(
        ILogger<UpdateService> logger,
        IApplicationSettingsService settingsService,
        IMessageHandler messageHandler)
    {
        _logger = logger;
        _settingsService = settingsService;
        _messageHandler = messageHandler;
    }

    public async Task<bool> IsLatestVersionAsync(bool quiet = false, CancellationToken cancellationToken = default)
    {
        var result = await GetUpdateInfoAsync(cancellationToken).ConfigureAwait(false);

        if (!result.CheckSuccessful)
        {
            if (!quiet) _messageHandler.ShowError($"Update check failed: {result.ErrorMessage}");
            return false;
        }

        if (result.IsUpdateAvailable)
        {
            if (!quiet)
            {
                var message = "A new version is available!\n" +
                              $"Current Version: {result.CurrentVersion}\n";

                if (result.LatestGitHubVersion != null)
                    message += $"Latest GitHub Version: {result.LatestGitHubVersion}\n";

                if (result.LatestNexusVersion != null)
                    message += $"Latest Nexus Version: {result.LatestNexusVersion}\n";

                _messageHandler.ShowWarning(message);
            }

            return false;
        }

        if (!quiet)
        {
            var message = $"Your Scanner 111 Version: {result.CurrentVersion}\n";

            if (result.LatestGitHubVersion != null) message += $"Latest GitHub Version: {result.LatestGitHubVersion}\n";

            if (result.LatestNexusVersion != null) message += $"Latest Nexus Version: {result.LatestNexusVersion}\n";

            message += "\n✔️ You have the latest version of Scanner 111!";

            _messageHandler.ShowSuccess(message);
        }

        return true;
    }

    public async Task<UpdateCheckResult> GetUpdateInfoAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);

        if (!settings.EnableUpdateCheck)
            return new UpdateCheckResult
            {
                CheckSuccessful = false,
                ErrorMessage = "Update checking is disabled in settings"
            };

        _logger.LogDebug("Starting update check");

        var result = new UpdateCheckResult
        {
            CurrentVersion = GetCurrentVersion(),
            UpdateSource = settings.UpdateSource
        };

        var useGitHub = settings.UpdateSource is "Both" or "GitHub";
        var useNexus = settings.UpdateSource is "Both" or "Nexus";

        try
        {
            var tasks = new List<Task>();

            if (useGitHub)
                tasks.Add(Task.Run(
                    async () =>
                    {
                        result.LatestGitHubVersion =
                            await GetLatestGitHubVersionAsync(cancellationToken).ConfigureAwait(false);
                    }, cancellationToken));

            if (useNexus)
                tasks.Add(Task.Run(
                    async () =>
                    {
                        result.LatestNexusVersion =
                            await GetLatestNexusVersionAsync(cancellationToken).ConfigureAwait(false);
                    }, cancellationToken));

            await Task.WhenAll(tasks).ConfigureAwait(false);

            // Check if any sources failed based on configuration
            var gitHubFailed = useGitHub && result.LatestGitHubVersion == null;
            var nexusFailed = useNexus && result.LatestNexusVersion == null;

            if (settings.UpdateSource == "GitHub" && gitHubFailed)
            {
                result.ErrorMessage = "Unable to fetch version information from GitHub";
                return result;
            }

            if (settings.UpdateSource == "Nexus" && nexusFailed)
            {
                result.ErrorMessage = "Unable to fetch version information from Nexus";
                return result;
            }

            if (settings.UpdateSource == "Both" && gitHubFailed && nexusFailed)
            {
                result.ErrorMessage = "Unable to fetch version information from both GitHub and Nexus";
                return result;
            }

            result.CheckSuccessful = true;
            result.IsUpdateAvailable = IsUpdateAvailable(result.CurrentVersion, result.LatestGitHubVersion,
                result.LatestNexusVersion);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during update check");
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            return result;
        }
    }

    private static Version? GetCurrentVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<Version?> GetLatestGitHubVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug($"Fetching GitHub release details for {GitHubOwner}/{GitHubRepo}");

            var url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
            var response = await HttpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogInformation($"No releases found for {GitHubOwner}/{GitHubRepo}");
                return null;
            }

            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(jsonContent);
            var root = document.RootElement;

            if (root.TryGetProperty("prerelease", out var prereleaseElement) && prereleaseElement.GetBoolean())
            {
                _logger.LogWarning("Latest GitHub release is a prerelease, skipping");
                return null;
            }

            if (root.TryGetProperty("name", out var nameElement))
            {
                var releaseName = nameElement.GetString();
                var version = TryParseVersion(releaseName);
                if (version != null) _logger.LogInformation($"Found GitHub version: {version}");
                return version;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching GitHub version");
            return null;
        }
    }

    private async Task<Version?> GetLatestNexusVersionAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Fetching Nexus version");

            var response = await HttpClient.GetAsync(NexusModUrl, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning($"Failed to fetch Nexus mod page: HTTP {response.StatusCode}");
                return null;
            }

            var htmlContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // Look for version meta tag pattern: <meta property="twitter:data1" content="version">
            var versionMatch = Regex.Match(htmlContent,
                @"<meta\s+property=""twitter:data1""\s+content=""([^""]+)""",
                RegexOptions.IgnoreCase);

            if (versionMatch.Success)
            {
                var versionString = versionMatch.Groups[1].Value;
                var version = TryParseVersion(versionString);
                if (version != null) _logger.LogInformation($"Found Nexus version: {version}");
                return version;
            }

            _logger.LogDebug("Version meta tag not found on Nexus page");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Nexus version");
            return null;
        }
    }

    private static Version? TryParseVersion(string? versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        try
        {
            // Extract the last part after a space, common for "Name v1.2.3"
            var potentialVersionPart = versionString.Split(' ', StringSplitOptions.RemoveEmptyEntries).Last();

            // Remove leading 'v' if present
            if (potentialVersionPart.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                potentialVersionPart.Length > 1) potentialVersionPart = potentialVersionPart[1..];

            return Version.Parse(potentialVersionPart);
        }
        catch
        {
            // Fallback: try the original string
            try
            {
                if (versionString.StartsWith("v", StringComparison.OrdinalIgnoreCase) && versionString.Length > 1)
                    return Version.Parse(versionString[1..]);
                return Version.Parse(versionString);
            }
            catch
            {
                return null;
            }
        }
    }

    private static bool IsUpdateAvailable(Version? currentVersion, Version? gitHubVersion, Version? nexusVersion)
    {
        if (currentVersion == null)
            // If we can't determine current version but remote versions exist, assume update needed
            return gitHubVersion != null || nexusVersion != null;

        if (gitHubVersion != null && currentVersion < gitHubVersion) return true;

        if (nexusVersion != null && currentVersion < nexusVersion) return true;

        return false;
    }
}
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Scanner111.Common.Models.Update;

namespace Scanner111.Common.Services.Updates;

/// <summary>
/// Service for checking GitHub releases for application updates.
/// </summary>
public sealed partial class UpdateService : IUpdateService
{
    private const string Owner = "evildarkarchon";
    private const string Repo = "Scanner111";
    private const string LatestReleaseUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    private const string AllReleasesUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases";

    private readonly ILogger<UpdateService> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Regex to extract version from tag or name (handles "v1.2.3", "Name v1.2.3", "1.2.3").
    /// </summary>
    [GeneratedRegex(@"v?(\d+\.\d+\.\d+(?:\.\d+)?(?:-[\w.]+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="httpClient">The HTTP client for making requests.</param>
    public UpdateService(ILogger<UpdateService> logger, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(httpClient);

        _logger = logger;
        _httpClient = httpClient;
    }

    /// <inheritdoc/>
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersion();

        try
        {
            ReleaseInfo? latestRelease;

            if (includePrerelease)
            {
                latestRelease = await GetLatestPrereleaseAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                latestRelease = await GetLatestStableReleaseAsync(cancellationToken).ConfigureAwait(false);
            }

            if (latestRelease is null)
            {
                return UpdateCheckResult.CreateFailure(
                    "Could not retrieve release information from GitHub.",
                    currentVersion);
            }

            var isUpdateAvailable = CompareVersions(currentVersion, latestRelease.Version);

            return isUpdateAvailable
                ? UpdateCheckResult.CreateUpdateAvailable(currentVersion, latestRelease)
                : UpdateCheckResult.CreateUpToDate(currentVersion, latestRelease);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while checking for updates");
            return UpdateCheckResult.CreateFailure($"Network error: {ex.Message}", currentVersion);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning("Update check was cancelled");
            return UpdateCheckResult.CreateFailure("Update check was cancelled.", currentVersion);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Update check timed out");
            return UpdateCheckResult.CreateFailure("Request timed out.", currentVersion);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing GitHub API response");
            return UpdateCheckResult.CreateFailure($"Error parsing response: {ex.Message}", currentVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during update check");
            return UpdateCheckResult.CreateFailure($"Unexpected error: {ex.Message}", currentVersion);
        }
    }

    /// <inheritdoc/>
    public string GetCurrentVersion()
    {
        var assembly = typeof(UpdateService).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString(3) ?? "0.0.0";
    }

    private async Task<ReleaseInfo?> GetLatestStableReleaseAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(LatestReleaseUrl, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogInformation("No releases found for {Owner}/{Repo}", Owner, Repo);
            return null;
        }

        response.EnsureSuccessStatusCode();

        var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (release is null || release.Prerelease)
        {
            return null;
        }

        return MapToReleaseInfo(release);
    }

    private async Task<ReleaseInfo?> GetLatestPrereleaseAsync(CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(AllReleasesUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var releases = await response.Content.ReadFromJsonAsync<List<GitHubRelease>>(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (releases is null || releases.Count == 0)
        {
            return null;
        }

        // When includePrerelease is true, we want the newest release (first in list, which is newest from API)
        // This could be a prerelease or stable - whichever is most recent
        var targetRelease = releases.FirstOrDefault();

        return targetRelease is not null ? MapToReleaseInfo(targetRelease) : null;
    }

    private ReleaseInfo MapToReleaseInfo(GitHubRelease release)
    {
        var version = ParseVersion(release.TagName) ?? ParseVersion(release.Name) ?? "Unknown";

        return new ReleaseInfo
        {
            Version = version,
            TagName = release.TagName ?? string.Empty,
            Name = release.Name ?? string.Empty,
            ReleaseNotes = release.Body,
            HtmlUrl = release.HtmlUrl ?? $"https://github.com/{Owner}/{Repo}/releases",
            PublishedAt = release.PublishedAt,
            IsPrerelease = release.Prerelease
        };
    }

    private static string? ParseVersion(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var match = VersionRegex().Match(input);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool CompareVersions(string currentVersion, string latestVersion)
    {
        if (!Version.TryParse(NormalizeVersion(currentVersion), out var current) ||
            !Version.TryParse(NormalizeVersion(latestVersion), out var latest))
        {
            // Fall back to string comparison if parsing fails
            return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }

        return latest > current;
    }

    private static string NormalizeVersion(string version)
    {
        // Remove prerelease suffix for comparison (e.g., "1.2.3-beta" -> "1.2.3")
        var dashIndex = version.IndexOf('-');
        return dashIndex > 0 ? version[..dashIndex] : version;
    }

    /// <summary>
    /// GitHub API release model for JSON deserialization.
    /// </summary>
    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset PublishedAt { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }
}

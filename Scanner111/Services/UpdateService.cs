using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;
using Scanner111.Models;
using Scanner111.Services.Interfaces;

namespace Scanner111.Services;

/// <summary>
///     Service responsible for checking if the current version of CLASSIC is the latest available.
///     Compares local version against GitHub and/or Nexus sources based on configuration.
/// </summary>
public partial class UpdateService(
    ILogger logger,
    IYamlSettingsService settingsService,
    IGameContextService gameContextService,
    HttpClient? httpClient = null)
    : IUpdateService
{
    // Constants
    private const string RepoOwner = "evildarkarchon";
    private const string RepoName = "CLASSIC-Fallout4";
    private const string NexusModUrl = "https://www.nexusmods.com/fallout4/mods/56255";
    private const string VersionPropertyName = "twitter:label1";
    private const string VersionPropertyValue = "Version";
    private const string VersionDataProperty = "twitter:data1";

    // Null version representation similar to Python's NULL_VERSION
    private static readonly SemanticVersion? NullVersion = new(0, 0, 0);
    private static readonly string[] SourceArray = ["Both", "GitHub", "Nexus"];

    private readonly IGameContextService _gameContextService =
        gameContextService ?? throw new ArgumentNullException(nameof(gameContextService));

    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();
    private readonly ILogger _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    private readonly IYamlSettingsService _settingsService =
        settingsService ?? throw new ArgumentNullException(nameof(settingsService));

    /// <inheritdoc />
    public async Task<bool> IsLatestVersionAsync(bool quiet = false, bool guiRequest = true)
    {
        _logger.Debug("- - - INITIATED UPDATE CHECK");

        // Check if update check is enabled in settings
        if (!guiRequest && !_settingsService.GetBoolSetting("Update Check"))
        {
            if (quiet) return false; // False because it's not the "latest" if checks are off (unless for GUI)
            Console.WriteLine("\n❌ NOTICE: UPDATE CHECK IS DISABLED IN CLASSIC Settings.yaml \n");
            Console.WriteLine("\n===============================================================================");
            return false; // False because it's not the "latest" if checks are off (unless for GUI)
        }

        // Get update source from settings
        var updateSource = _settingsService.GetStringSetting("Update Source") ?? "Both";
        if (!SourceArray.Contains(updateSource))
        {
            if (quiet) return false; // Invalid source, cannot determine if latest
            Console.WriteLine("\n❌ NOTICE: INVALID VALUE FOR UPDATE SOURCE IN CLASSIC Settings.yaml \n");
            Console.WriteLine("\n===============================================================================");
            return false; // Invalid source, cannot determine if latest
        }

        // Parse local version
        var classicLocalStr = _settingsService.GetStringSetting("CLASSIC_Info.version", "Main");
        SemanticVersion? versionLocal = null;

        if (!string.IsNullOrEmpty(classicLocalStr))
        {
            var parts = classicLocalStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var parsedLocalVersionStr = parts[^1]; // Last element, equivalent to parts[-1] in Python
                if (parsedLocalVersionStr.StartsWith("v")) parsedLocalVersionStr = parsedLocalVersionStr[1..];

                SemanticVersion.TryParse(parsedLocalVersionStr, out versionLocal);
            }
        }

        if (!quiet)
        {
            Console.WriteLine("❓ (Needs internet connection) CHECKING FOR NEW CLASSIC VERSIONS...");
            Console.WriteLine("   (You can disable this check in the EXE or CLASSIC Settings.yaml) \n");
        }

        var useGithub = updateSource is "Both" or "GitHub";
        var useNexus = updateSource is "Both" or "Nexus" &&
                       !_settingsService.GetBoolSetting("CLASSIC_Info.is_prerelease", "Main");

        SemanticVersion? versionGithubToCompare = null;
        SemanticVersion? versionNexusToCompare = null;

        try
        {
            if (useGithub)
            {
                _logger.Debug($"Fetching GitHub release details for {RepoOwner}/{RepoName}");
                var githubDetails = await GetLatestAndTopReleaseDetailsAsync(RepoOwner, RepoName);

                {
                    var candidateStableVersions = new List<SemanticVersion?>();

                    if (githubDetails.LatestEndpointRelease?.Version != null &&
                        !githubDetails.LatestEndpointRelease.IsPreRelease)
                        candidateStableVersions.Add(githubDetails.LatestEndpointRelease.Version);

                    if (githubDetails.TopOfListRelease?.Version != null &&
                        !githubDetails.TopOfListRelease.IsPreRelease)
                        candidateStableVersions.Add(githubDetails.TopOfListRelease.Version);

                    if (candidateStableVersions.Count > 0)
                    {
                        versionGithubToCompare = candidateStableVersions.Max();
                        _logger.Info($"Determined latest stable GitHub version: {versionGithubToCompare}");
                    }
                }
            }

            if (useNexus)
            {
                _logger.Debug("Fetching Nexus version");
                versionNexusToCompare = await GetNexusVersionAsync();

                _logger.Info($"Determined Nexus version: {versionNexusToCompare}");
            }

            var nexusSourceFailed = useNexus && versionNexusToCompare == null;
            var githubSourceFailed = useGithub && versionGithubToCompare == null;

            // Check if sources failed and raise appropriate exception if needed
            CheckSourceFailuresAndRaise(useGithub, useNexus, githubSourceFailed, nexusSourceFailed);
        }
        catch (HttpRequestException ex)
        {
            _logger.Error($"Update check failed during version fetching: {ex.Message}");

            if (!quiet)
            {
                Console.WriteLine(ex.Message);
                var unableMsg = _settingsService.GetStringSetting(
                    $"CLASSIC_Interface.update_unable_{_gameContextService.GetCurrentGame()}", "Main");
                Console.WriteLine(unableMsg);
            }

            if (guiRequest) throw new UpdateCheckException(ex.Message, ex);

            return false;
        }
        catch (UpdateCheckException ex)
        {
            _logger.Error($"Update check failed: {ex.Message}");

            if (!quiet)
            {
                Console.WriteLine(ex.Message);
                var unableMsg = _settingsService.GetStringSetting(
                    $"CLASSIC_Interface.update_unable_{_gameContextService.GetCurrentGame()}", "Main");
                Console.WriteLine(unableMsg);
            }

            if (guiRequest) throw;

            return false;
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error during update check: {ex.Message}", ex);

            if (guiRequest) throw new UpdateCheckException($"An unexpected error occurred: {ex.Message}", ex);

            return false;
        }

        var isOutdated = false;

        if (versionLocal == null)
        {
            _logger.Warning("Local version is unknown. Assuming update is needed or there's an issue.");

            // For safety, if local version is unknown, and remote versions exist, consider it outdated
            if (versionGithubToCompare != null || versionNexusToCompare != null) isOutdated = true;
        }
        else
        {
            if (versionGithubToCompare != null && versionLocal < versionGithubToCompare)
            {
                _logger.Info(
                    $"Local version {versionLocal} is older than GitHub version {versionGithubToCompare}.");
                isOutdated = true;
            }

            if (!isOutdated && versionNexusToCompare != null && versionLocal < versionNexusToCompare)
            {
                _logger.Info($"Local version {versionLocal} is older than Nexus version {versionNexusToCompare}.");
                isOutdated = true;
            }
        }

        if (isOutdated)
        {
            if (!quiet)
            {
                var warningMsg =
                    _settingsService.GetStringSetting(
                        $"CLASSIC_Interface.update_warning_{_gameContextService.GetCurrentGame()}", "Main") ??
                    "A new version of CLASSIC is available!";
                Console.WriteLine(warningMsg);
            }

            if (guiRequest)
                // GUI catches this to indicate an update is available
                throw new UpdateCheckException("A new version is available.");

            return false; // Outdated
        }

        // If not outdated
        if (quiet) return true;
        Console.Write($"Your CLASSIC Version: {versionLocal?.ToString() ?? "Unknown"}");

        if (useGithub)
            Console.Write(versionGithubToCompare != null
                ? $"\nLatest GitHub Version: {versionGithubToCompare}"
                : "\nLatest GitHub Version: Not found/checked");

        if (useNexus)
            Console.Write(versionNexusToCompare != null
                ? $"\nLatest Nexus Version: {versionNexusToCompare}"
                : "\nLatest Nexus Version: Not found/checked");

        Console.WriteLine("\n\n✔️ You have the latest version of CLASSIC!\n");

        return true;
    }

    #region Helper Methods

    /// <summary>
    ///     Attempts to parse a version string into a SemanticVersion object
    /// </summary>
    private SemanticVersion? TryParseVersion(string? versionStr)
    {
        if (string.IsNullOrEmpty(versionStr)) return null;

        // Extract the last part after a space, common for "Name v1.2.3"
        var potentialVersionPart = versionStr.Split(' ').Last();

        // Try with the potential version part first
        if (potentialVersionPart.StartsWith("v") && potentialVersionPart.Length > 1)
            potentialVersionPart = potentialVersionPart.Substring(1);

        if (SemanticVersion.TryParse(potentialVersionPart, out var version)) return version;

        // If that fails and we split the string, try with the original string
        if (versionStr != potentialVersionPart)
        {
            var originalVersion = versionStr;
            if (originalVersion.StartsWith("v") && originalVersion.Length > 1)
                originalVersion = originalVersion.Substring(1);

            if (SemanticVersion.TryParse(originalVersion, out version)) return version;
        }

        _logger.Debug($"Could not parse version from GitHub release name: {versionStr}");
        return null;
    }

    /// <summary>
    ///     Fetches the latest stable release version from a GitHub repository
    /// </summary>
    private async Task<SemanticVersion?> GetGithubLatestStableVersionFromEndpointAsync(string owner, string repo)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        try
        {
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.Info($"No '/releases/latest' found for {owner}/{repo} (status 404).");
                return null;
            }

            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            var releaseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            if (releaseData.TryGetProperty("prerelease", out var prereleaseElement) &&
                prereleaseElement.ValueKind == JsonValueKind.True)
            {
                _logger.Warning($"{url} returned a prerelease. Expected a stable release.");
                return null;
            }

            if (releaseData.TryGetProperty("name", out var nameElement) &&
                nameElement.ValueKind == JsonValueKind.String)
            {
                var releaseName = nameElement.GetString();
                return TryParseVersion(releaseName);
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error($"Error fetching latest stable release from {url}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///     Fetches the latest prerelease version from a GitHub repository's releases list
    /// </summary>
    private async Task<SemanticVersion?> GetGithubLatestPrereleaseVersionFromListAsync(string owner, string repo)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases";

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var responseJson = await response.Content.ReadAsStringAsync();
            var releasesData = JsonSerializer.Deserialize<JsonElement[]>(responseJson);

            if (releasesData == null)
            {
                _logger.Warning($"Expected a list of releases from {url}, got null");
                return null;
            }

            foreach (var releaseData in releasesData) // Iterates from newest to oldest
            {
                if (!releaseData.TryGetProperty("prerelease", out var prereleaseElement) ||
                    prereleaseElement.ValueKind != JsonValueKind.True) continue;
                if (!releaseData.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String) continue;
                var prereleaseName = nameElement.GetString();
                var parsedVersion = TryParseVersion(prereleaseName);
                return parsedVersion;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.Error($"Error fetching releases list from {url}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///     Fetches details of the latest and top releases from a GitHub repository
    /// </summary>
    private async Task<ReleaseDetailsInfo?> GetLatestAndTopReleaseDetailsAsync(string owner, string repo)
    {
        var latestUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var allReleasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";

        var results = new ReleaseDetailsInfo
        {
            LatestEndpointRelease = null,
            TopOfListRelease = null,
            AreSameReleaseById = false
        };

        try
        {
            // 1. Get release from /releases/latest
            var latestResponse = await _httpClient.GetAsync(latestUrl);

            if (latestResponse.StatusCode != HttpStatusCode.NotFound)
            {
                latestResponse.EnsureSuccessStatusCode();
                var latestJson = await latestResponse.Content.ReadAsStringAsync();
                var latestData = JsonSerializer.Deserialize<JsonElement>(latestJson);

                results.LatestEndpointRelease = new ReleaseInfo
                {
                    Id = latestData.GetProperty("id").ToString(),
                    TagName = latestData.GetProperty("tag_name").GetString() ?? string.Empty,
                    Name = latestData.GetProperty("name").GetString() ?? string.Empty,
                    Version = TryParseVersion(latestData.GetProperty("name").GetString()),
                    IsPreRelease = latestData.GetProperty("prerelease").GetBoolean(),
                    PublishedAt = latestData.TryGetProperty("published_at", out var publishedAt)
                        ? DateTimeOffset.Parse(publishedAt.GetString() ?? string.Empty)
                        : null
                };
            }
            else
            {
                _logger.Info($"No '/releases/latest' found for {owner}/{repo} (status 404).");
            }

            // 2. Get all releases and take the top one
            var allResponse = await _httpClient.GetAsync(allReleasesUrl);
            allResponse.EnsureSuccessStatusCode();
            var allJson = await allResponse.Content.ReadAsStringAsync();
            var allData = JsonSerializer.Deserialize<JsonElement[]>(allJson);

            if (allData == null || allData.Length == 0)
            {
                _logger.Warning($"No releases found or unexpected format from {allReleasesUrl}");
            }
            else
            {
                var topReleaseData = allData[0];
                results.TopOfListRelease = new ReleaseInfo
                {
                    Id = topReleaseData.GetProperty("id").ToString(),
                    TagName = topReleaseData.GetProperty("tag_name").GetString() ?? string.Empty,
                    Name = topReleaseData.GetProperty("name").GetString() ?? string.Empty,
                    Version = TryParseVersion(topReleaseData.GetProperty("name").GetString()),
                    IsPreRelease = topReleaseData.GetProperty("prerelease").GetBoolean(),
                    PublishedAt = topReleaseData.TryGetProperty("published_at", out var publishedAt)
                        ? DateTimeOffset.Parse(publishedAt.GetString() ?? string.Empty)
                        : null
                };
            }

            if (results is { LatestEndpointRelease: not null, TopOfListRelease: not null })
                results.AreSameReleaseById = results.LatestEndpointRelease.Id == results.TopOfListRelease.Id;

            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger.Error($"GitHub API ClientError for {owner}/{repo}: {ex.Message}");
            return results.LatestEndpointRelease != null || results.TopOfListRelease != null ? results : null;
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error fetching release details for {owner}/{repo}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Fetches the NexusMods version information for the CLASSIC Fallout 4 mod
    /// </summary>
    private async Task<SemanticVersion?> GetNexusVersionAsync()
    {
        try
        {
            // Set a timeout to prevent hanging on large responses
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            var response = await _httpClient.GetAsync(NexusModUrl, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Warning($"Failed to fetch Nexus mod page: HTTP {response.StatusCode}");
                return null;
            }

            // Check content type to ensure we're parsing HTML
            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType != "text/html" && !contentType?.StartsWith("text/html") == true)
            {
                _logger.Warning($"Unexpected content type from Nexus mod page: {contentType}");
                return null;
            }

            // Set a reasonable size limit for the HTML content
            const int maxHtmlLength = 10 * 1024 * 1024; // 10 MB
            var contentLength = response.Content.Headers.ContentLength ?? 0;
            if (contentLength > maxHtmlLength)
            {
                _logger.Warning($"Nexus mod page content too large: {contentLength} bytes (max: {maxHtmlLength})");
                return null;
            }

            var htmlContent = await response.Content.ReadAsStringAsync(cts.Token);

            // Additional validation of HTML content
            if (string.IsNullOrEmpty(htmlContent) || htmlContent.Length > maxHtmlLength)
            {
                _logger.Warning("Invalid HTML content received from Nexus mod page (empty or too large)");
                return null;
            }

            var htmlDoc = new HtmlDocument
            {
                // Configure HtmlAgilityPack's options for safety
                OptionCheckSyntax = true,
                OptionFixNestedTags = true
            };

            // Use try-catch specifically for the HTML parsing step
            try
            {
                htmlDoc.LoadHtml(htmlContent);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error parsing HTML from Nexus mod page: {ex.Message}");
                return null;
            }

            // Find the meta tag that indicates version label using a safe XPath query
            var versionLabelNode = htmlDoc.DocumentNode.SelectSingleNode(
                $"//meta[@property='{VersionPropertyName}' and @content='{VersionPropertyValue}']");

            if (versionLabelNode == null)
            {
                _logger.Debug("Version label meta tag not found");
                return null;
            }

            // Look for the next meta tag with version data
            var versionDataNode =
                htmlDoc.DocumentNode.SelectSingleNode($"//meta[@property='{VersionDataProperty}']");

            if (versionDataNode == null)
            {
                _logger.Debug("Version data meta tag not found");
                return null;
            }

            // Use SafeGetAttribute to handle potential edge cases in attribute values
            var versionStr = SafeGetAttribute(versionDataNode, "content");

            if (string.IsNullOrEmpty(versionStr))
            {
                _logger.Debug("Version string from meta tag is empty or null.");
                return NullVersion;
            }

            // Validate and sanitize the version string before parsing
            versionStr = SanitizeVersionString(versionStr);

            var parsedVersion = TryParseVersion(versionStr);

            _logger.Debug(parsedVersion != null
                ? $"Successfully parsed Nexus version: {parsedVersion}"
                : $"Failed to parse version string: '{versionStr}'");

            return parsedVersion;
        }
        catch (TaskCanceledException)
        {
            _logger.Error("Timeout while fetching Nexus version");
        }
        catch (HttpRequestException ex)
        {
            _logger.Error($"Network error while fetching Nexus version: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error parsing Nexus version: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///     Safely retrieves an attribute from an HTML node, with error handling
    /// </summary>
    private string? SafeGetAttribute(HtmlNode? node, string attributeName)
    {
        if (node == null || string.IsNullOrEmpty(attributeName)) return null;

        try
        {
            return node.GetAttributeValue(attributeName, null!);
        }
        catch (Exception ex)
        {
            _logger.Error($"Error retrieving '{attributeName}' attribute: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Sanitizes a version string to remove potentially harmful or unexpected characters
    /// </summary>
    private string? SanitizeVersionString(string? versionStr)
    {
        if (string.IsNullOrEmpty(versionStr)) return string.Empty;

        // Trim whitespace
        versionStr = versionStr.Trim();

        // Limit length
        if (versionStr.Length > 100) versionStr = versionStr[..100];

        // Limit to version-relevant characters (alphanumeric, dots, dashes, plus)
        // This regex matches valid version string characters and replaces everything else
        return SanitizeVersionRegex().Replace(versionStr, "");
    }

    /// <summary>
    ///     Helper to check source failures and raise UpdateCheckException if appropriate
    /// </summary>
    private void CheckSourceFailuresAndRaise(bool useGithub, bool useNexus, bool githubFailed, bool nexusFailed)
    {
        if (useGithub && !useNexus && githubFailed)
            throw new UpdateCheckException(
                "Unable to fetch version information from GitHub (selected as only source).");

        if (useNexus && !useGithub && nexusFailed)
            throw new UpdateCheckException(
                "Unable to fetch version information from Nexus (selected as only source).");

        if (useGithub && useNexus && githubFailed && nexusFailed)
            throw new UpdateCheckException("Unable to fetch version information from both GitHub and Nexus.");
    }

    [GeneratedRegex(@"[^a-zA-Z0-9.\-+]")]
    private static partial Regex SanitizeVersionRegex();

    #endregion
}
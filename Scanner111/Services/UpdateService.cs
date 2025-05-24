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

        // Validate settings and early returns
        if (!ValidateUpdateSettings(quiet, guiRequest))
            return false;

        // Parse local version
        var versionLocal = ParseLocalVersion();

        if (!quiet) DisplayUpdateCheckStartMessage();

        // Determine which update sources to use
        var updateSource = _settingsService.GetStringSetting("Update Source") ?? "Both";
        var useGithub = updateSource is "Both" or "GitHub";
        var useNexus = updateSource is "Both" or "Nexus" &&
                       !_settingsService.GetBoolSetting("CLASSIC_Info.is_prerelease", "Main");

        // Fetch remote versions
        var (githubVersion, nexusVersion) =
            await FetchRemoteVersionsAsync(useGithub, useNexus, quiet, guiRequest);

        // Check if local version is outdated
        var isOutdated = IsVersionOutdated(versionLocal, githubVersion, nexusVersion);

        if (isOutdated) return HandleOutdatedVersion(quiet, guiRequest);

        // Display up-to-date message if not in quiet mode
        if (!quiet) DisplayUpToDateMessage(versionLocal, useGithub, githubVersion, useNexus, nexusVersion);

        return true;
    }

    private bool ValidateUpdateSettings(bool quiet, bool guiRequest)
    {
        // Check if update check is enabled in settings
        if (!guiRequest && !_settingsService.GetBoolSetting("Update Check"))
        {
            if (quiet) return false;

            Console.WriteLine("\n❌ NOTICE: UPDATE CHECK IS DISABLED IN CLASSIC Settings.yaml \n");
            Console.WriteLine("\n===============================================================================");
            return false;
        }

        // Get update source from settings
        var updateSource = _settingsService.GetStringSetting("Update Source") ?? "Both";
        if (!SourceArray.Contains(updateSource))
        {
            if (quiet) return false;

            Console.WriteLine("\n❌ NOTICE: INVALID VALUE FOR UPDATE SOURCE IN CLASSIC Settings.yaml \n");
            Console.WriteLine("\n===============================================================================");
            return false;
        }

        return true;
    }

    private SemanticVersion? ParseLocalVersion()
    {
        var classicLocalStr = _settingsService.GetStringSetting("CLASSIC_Info.version", "Main");
        if (string.IsNullOrEmpty(classicLocalStr))
            return null;

        var parts = classicLocalStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        var parsedLocalVersionStr = parts[^1]; // Last element
        if (parsedLocalVersionStr.StartsWith("v"))
            parsedLocalVersionStr = parsedLocalVersionStr[1..];

        SemanticVersion.TryParse(parsedLocalVersionStr, out var versionLocal);
        return versionLocal;
    }

    private void DisplayUpdateCheckStartMessage()
    {
        Console.WriteLine("❓ (Needs internet connection) CHECKING FOR NEW CLASSIC VERSIONS...");
        Console.WriteLine("   (You can disable this check in the EXE or CLASSIC Settings.yaml) \n");
    }

    private async Task<(SemanticVersion? GithubVersion, SemanticVersion? NexusVersion)> FetchRemoteVersionsAsync(
        bool useGithub, bool useNexus, bool quiet, bool guiRequest)
    {
        SemanticVersion? versionGithubToCompare = null;
        SemanticVersion? versionNexusToCompare = null;

        try
        {
            if (useGithub) versionGithubToCompare = await FetchGithubVersionAsync();

            if (useNexus) versionNexusToCompare = await FetchNexusVersionAsync();

            var nexusSourceFailed = useNexus && versionNexusToCompare == null;
            var githubSourceFailed = useGithub && versionGithubToCompare == null;

            // Check if sources failed and raise appropriate exception if needed
            CheckSourceFailuresAndRaise(useGithub, useNexus, githubSourceFailed, nexusSourceFailed);
        }
        catch (Exception ex)
        {
            HandleUpdateCheckException(ex, quiet, guiRequest);
        }

        return (versionGithubToCompare, versionNexusToCompare);
    }

    private async Task<SemanticVersion?> FetchNexusVersionAsync()
    {
        _logger.Debug("Fetching Nexus version");
        var nexusVersion = await GetNexusVersionAsync();

        if (nexusVersion != null) _logger.Info($"Determined Nexus version: {nexusVersion}");

        return nexusVersion;
    }

    private async Task<SemanticVersion?> FetchGithubVersionAsync()
    {
        _logger.Debug($"Fetching GitHub release details for {RepoOwner}/{RepoName}");
        var githubDetails = await GetLatestAndTopReleaseDetailsAsync(RepoOwner, RepoName);

        var candidateStableVersions = new List<SemanticVersion?>();

        if (githubDetails?.LatestEndpointRelease?.Version != null &&
            !githubDetails.LatestEndpointRelease.IsPreRelease)
            candidateStableVersions.Add(githubDetails.LatestEndpointRelease.Version);

        if (githubDetails?.TopOfListRelease?.Version != null &&
            !githubDetails.TopOfListRelease.IsPreRelease)
            candidateStableVersions.Add(githubDetails.TopOfListRelease.Version);

        if (candidateStableVersions.Count <= 0) return null;
        var version = candidateStableVersions.Max();
        _logger.Info($"Determined latest stable GitHub version: {version}");
        return version;
    }

    private void HandleUpdateCheckException(Exception ex, bool quiet, bool guiRequest)
    {
        string errorMessage;
        Exception exceptionToThrow;

        switch (ex)
        {
            case HttpRequestException httpEx:
                errorMessage = $"Update check failed during version fetching: {httpEx.Message}";
                exceptionToThrow = new UpdateCheckException(httpEx.Message, httpEx);
                _logger.Error(errorMessage);
                break;
            case UpdateCheckException updateEx:
                errorMessage = $"Update check failed: {updateEx.Message}";
                exceptionToThrow = updateEx;
                _logger.Error(errorMessage);
                break;
            default:
                errorMessage = $"Unexpected error during update check: {ex.Message}";
                exceptionToThrow = new UpdateCheckException($"An unexpected error occurred: {ex.Message}", ex);
                _logger.Error(errorMessage, ex);
                break;
        }

        if (!quiet)
        {
            Console.WriteLine(ex.Message);
            var unableMsg = _settingsService.GetStringSetting(
                $"CLASSIC_Interface.update_unable_{_gameContextService.GetCurrentGame()}", "Main");
            Console.WriteLine(unableMsg);
        }

        if (guiRequest) throw exceptionToThrow;
    }

    private bool IsVersionOutdated(
        SemanticVersion? versionLocal,
        SemanticVersion? githubVersion,
        SemanticVersion? nexusVersion)
    {
        if (versionLocal == null)
        {
            _logger.Warning("Local version is unknown. Assuming update is needed if remote versions exist.");
            return githubVersion != null || nexusVersion != null;
        }

        if (githubVersion != null && versionLocal < githubVersion)
        {
            _logger.Info($"Local version {versionLocal} is older than GitHub version {githubVersion}.");
            return true;
        }

        if (nexusVersion == null || versionLocal >= nexusVersion) return false;
        _logger.Info($"Local version {versionLocal} is older than Nexus version {nexusVersion}.");
        return true;
    }

    private bool HandleOutdatedVersion(bool quiet, bool guiRequest)
    {
        if (!quiet)
        {
            var warningMsg = _settingsService.GetStringSetting(
                                 $"CLASSIC_Interface.update_warning_{_gameContextService.GetCurrentGame()}", "Main")
                             ?? "A new version of CLASSIC is available!";
            Console.WriteLine(warningMsg);
        }

        if (guiRequest)
            // GUI catches this to indicate an update is available
            throw new UpdateCheckException("A new version is available.");

        return false; // Outdated
    }

    private void DisplayUpToDateMessage(
        SemanticVersion? versionLocal,
        bool useGithub,
        SemanticVersion? githubVersion,
        bool useNexus,
        SemanticVersion? nexusVersion)
    {
        Console.Write($"Your CLASSIC Version: {versionLocal?.ToString() ?? "Unknown"}");

        if (useGithub)
            Console.Write(githubVersion != null
                ? $"\nLatest GitHub Version: {githubVersion}"
                : "\nLatest GitHub Version: Not found/checked");

        if (useNexus)
            Console.Write(nexusVersion != null
                ? $"\nLatest Nexus Version: {nexusVersion}"
                : "\nLatest Nexus Version: Not found/checked");

        Console.WriteLine("\n\n✔️ You have the latest version of CLASSIC!\n");
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

    #region Helper Methods

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
    private static string SanitizeVersionString(string? versionStr)
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

    [GeneratedRegex(@"[^a-zA-Z0-9.\-+]")]
    private static partial Regex SanitizeVersionRegex();

    #endregion
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service for checking if Scanner111 is up to date by comparing
///     local version with remote versions from GitHub and/or Nexus.
/// </summary>
public class UpdateCheckService : IUpdateCheckService
{
    // Constants for GitHub and Nexus
    private const string NullVersion = "0.0.0";
    private readonly AppSettings _appSettings;
    private readonly ILogger<UpdateCheckService>? _logger;
    private readonly IYamlSettingsCacheService _yamlSettingsCache;

    /// <summary>
    ///     Initializes a new instance of the UpdateCheckService class.
    /// </summary>
    /// <param name="yamlSettingsCache">Settings cache service.</param>
    /// <param name="appSettings">Application settings.</param>
    /// <param name="logger">Optional logger.</param>
    public UpdateCheckService(
        IYamlSettingsCacheService yamlSettingsCache,
        AppSettings appSettings,
        ILogger<UpdateCheckService>? logger = null)
    {
        _yamlSettingsCache = yamlSettingsCache ?? throw new ArgumentNullException(nameof(yamlSettingsCache));
        _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
        _logger = logger;
    }

    /// <summary>
    ///     Attempts to parse a version string into a Version object.
    /// </summary>
    /// <param name="versionStr">The version string to parse.</param>
    /// <returns>A Version object or null if parsing fails.</returns>
    public Version? TryParseVersion(string? versionStr)
    {
        if (string.IsNullOrEmpty(versionStr))
            return null;

        // Extract the last part after a space, common for "Name v1.2.3"
        var potentialVersionPart = versionStr.Split(' ').LastOrDefault() ?? versionStr;

        try
        {
            // Remove a leading 'v' if present
            if (potentialVersionPart.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                potentialVersionPart.Length > 1)
                return new Version(potentialVersionPart.Substring(1));

            return new Version(potentialVersionPart);
        }
        catch (ArgumentException)
        {
            // Fallback: if the above fails, try the original string if it was simple
            if (versionStr == potentialVersionPart)
                return null;

            try
            {
                if (versionStr.StartsWith("v", StringComparison.OrdinalIgnoreCase) && versionStr.Length > 1)
                    return new Version(versionStr.Substring(1));

                return new Version(versionStr);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is FormatException || ex is OverflowException)
            {
                _logger?.LogDebug($"Could not parse version from release name: {versionStr}");
                return null;
            }
        }
    }

    /// <summary>
    ///     Checks if the current version of Scanner111 is the latest available version.
    /// </summary>
    /// <param name="quiet">If true, suppresses detailed output to logs.</param>
    /// <param name="guiRequest">Indicates if the request originates from the GUI.</param>
    /// <returns>True if the installed version is the latest; False otherwise.</returns>
    public async Task<bool> IsLatestVersionAsync(bool quiet = false, bool guiRequest = true)
    {
        void CheckSourceFailuresAndRaise(bool useGithub, bool useNexus, bool githubFetchFailed,
            bool nexusFetchFailed)
        {
            if (useGithub && !useNexus && githubFetchFailed)
                throw new UpdateCheckException(
                    "Unable to fetch version information from GitHub (selected as only source).");

            if (useNexus && !useGithub && nexusFetchFailed)
                throw new UpdateCheckException(
                    "Unable to fetch version information from Nexus (selected as only source).");

            if (useGithub && useNexus && githubFetchFailed && nexusFetchFailed)
                throw new UpdateCheckException("Unable to fetch version information from both GitHub and Nexus.");
        }

        // Repository info for Scanner111
        const string repoOwner = "evildarkarchon"; // Update with your actual repo owner
        const string repoName = "Scanner111"; // Update with your actual repo name

        _logger?.LogDebug("- - - INITIATED UPDATE CHECK");

        // Check if update check is disabled
        if (!(guiRequest || GetSetting<bool?>(Yaml.Settings, "Update Check") == true))
        {
            if (quiet) return false; // False because it's not the "latest" if checks are off
            Console.WriteLine("\n❌ NOTICE: UPDATE CHECK IS DISABLED IN SETTINGS \n");
            Console.WriteLine(
                "\n===============================================================================");

            return false; // False because it's not the "latest" if checks are off
        }

        // Determine which source(s) to check
        var updateSource = GetSetting<string>(Yaml.Settings, "Update Source") ?? "Both";
        if (updateSource != "Both" && updateSource != "GitHub" && updateSource != "Nexus")
        {
            if (quiet) return false; // Invalid source, cannot determine if latest
            Console.WriteLine("\n❌ NOTICE: INVALID VALUE FOR UPDATE SOURCE IN SETTINGS \n");
            Console.WriteLine(
                "\n===============================================================================");

            return false; // Invalid source, cannot determine if latest
        } // Get local version

        var localVersionStr = GetSetting<string>(Yaml.Main, "Scanner111_Info.version");

        // Parse local version string (e.g., "Scanner111 v1.0.0" -> "1.0.0")
        string? parsedLocalVersionStr = null;
        if (!string.IsNullOrEmpty(localVersionStr))
        {
            var parts = localVersionStr.Split(' ');
            if (parts.Length > 0) parsedLocalVersionStr = parts.Last();
        }

        var localVersion = parsedLocalVersionStr != null ? TryParseVersion(parsedLocalVersionStr) : null;

        if (!quiet)
        {
            Console.WriteLine("❓ (Needs internet connection) CHECKING FOR NEW SCANNER111 VERSIONS...");
            Console.WriteLine("   (You can disable this check in the Settings) \n");
        }

        var useGithub = updateSource == "Both" || updateSource == "GitHub";
        var useNexus = (updateSource == "Both" || updateSource == "Nexus") &&
                       GetSetting<bool?>(Yaml.Main, "Scanner111_Info.is_prerelease") != true;

        Version? githubVersionToCompare = null;
        Version? nexusVersionToCompare = null;

        try
        {
            using var client = new HttpClient();
            if (useGithub)
            {
                _logger?.LogDebug($"Fetching GitHub release details for {repoOwner}/{repoName}");
                var githubDetails = await GetLatestAndTopReleaseDetailsAsync(client, repoOwner, repoName);

                if (githubDetails != null)
                {
                    var latestEpInfo = githubDetails["latest_endpoint_release"] as Dictionary<string, object?>;
                    var topListInfo = githubDetails["top_of_list_release"] as Dictionary<string, object?>;

                    var candidateStableVersions = new List<Version>();

                    if (latestEpInfo != null &&
                        latestEpInfo.TryGetValue("version", out var latestVersionObj) &&
                        latestVersionObj is Version latestVersion &&
                        latestEpInfo.TryGetValue("prerelease", out var latestIsPrereleaseObj) &&
                        latestIsPrereleaseObj is bool latestIsPrerelease && !latestIsPrerelease)
                        candidateStableVersions.Add(latestVersion);

                    if (topListInfo != null &&
                        topListInfo.TryGetValue("version", out var topVersionObj) &&
                        topVersionObj is Version topVersion &&
                        topListInfo.TryGetValue("prerelease", out var topIsPrereleaseObj) &&
                        topIsPrereleaseObj is bool topIsPrerelease && !topIsPrerelease)
                        candidateStableVersions.Add(topVersion);

                    if (candidateStableVersions.Count > 0)
                    {
                        githubVersionToCompare = candidateStableVersions.Max();
                        _logger?.LogInformation(
                            "Determined latest stable GitHub version: {GithubVersionToCompare}",
                            githubVersionToCompare);
                    }
                }
            }

            if (useNexus)
            {
                _logger?.LogDebug("Fetching Nexus version");
                nexusVersionToCompare = await GetNexusVersionAsync(client);
                if (nexusVersionToCompare != null)
                    _logger?.LogInformation("Determined Nexus version: {NexusVersionToCompare}", nexusVersionToCompare);
            }

            var nexusSourceFailed = useNexus && nexusVersionToCompare == null;
            var githubSourceFailed = useGithub && githubVersionToCompare == null;

            CheckSourceFailuresAndRaise(useGithub, useNexus, githubSourceFailed, nexusSourceFailed);
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError("Update check failed during version fetching: {ExMessage}", ex.Message);
            if (!quiet)
            {
                Console.WriteLine(ex.Message);

                var unableMsg = GetSetting<string>(Yaml.Main, "Scanner111_Interface.update_unable");
                if (!string.IsNullOrEmpty(unableMsg)) Console.WriteLine(unableMsg);
            }

            if (guiRequest) throw new UpdateCheckException(ex.Message, ex);

            return false;
        }
        catch (UpdateCheckException) // Keep ex if needed for logging or re-throwing with more context
        {
            // Just rethrow the custom exception if it's already that type
            if (guiRequest) throw;

            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Unexpected error during update check: {ExMessage}", ex.Message);
            if (guiRequest) throw new UpdateCheckException($"An unexpected error occurred: {ex.Message}", ex);

            return false;
        }

        var isOutdated = false;
        if (localVersion == null)
        {
            _logger?.LogWarning("Local version is unknown. Assuming update is needed or there's an issue.");

            // If local version is unknown, and remote versions exist, consider it outdated.
            if (githubVersionToCompare != null || nexusVersionToCompare != null) isOutdated = true;
        }
        else
        {
            if (githubVersionToCompare != null && localVersion < githubVersionToCompare)
            {
                _logger?.LogInformation(
                    "Local version {LocalVersion} is older than GitHub version {GithubVersionToCompare}.", localVersion,
                    githubVersionToCompare);
                isOutdated = true;
            }

            if (!isOutdated && nexusVersionToCompare != null && localVersion < nexusVersionToCompare)
            {
                _logger?.LogInformation(
                    "Local version {LocalVersion} is older than Nexus version {NexusVersionToCompare}.", localVersion,
                    nexusVersionToCompare);
                isOutdated = true;
            }
        }

        if (isOutdated)
        {
            if (!quiet)
            {
                var warningMsg = GetSetting<string>(Yaml.Main, "Scanner111_Interface.update_warning") ??
                                 "A new version of Scanner111 is available. Please update to the latest version.";
                Console.WriteLine(warningMsg);
            }

            if (guiRequest)
                // GUI catches this to indicate an update is available.
                throw new UpdateCheckException("A new version is available.");

            return false; // Outdated
        }

        // If not outdated
        if (quiet) return true;
        var sb = new StringBuilder();
        sb.Append($"Your Scanner111 Version: {localVersion ?? new Version(0, 0, 0)}");

        switch (useGithub)
        {
            case true when githubVersionToCompare != null:
                sb.Append($"\nLatest GitHub Version: {githubVersionToCompare}");
                break;
            case true:
                sb.Append("\nLatest GitHub Version: Not found/checked");
                break;
        }

        switch (useNexus)
        {
            case true when nexusVersionToCompare != null:
                sb.Append($"\nLatest Nexus Version: {nexusVersionToCompare}");
                break;
            case true:
                sb.Append("\nLatest Nexus Version: Not found/checked");
                break;
        }

        sb.Append("\n\n✔️ You have the latest version of Scanner111!\n");
        Console.WriteLine(sb.ToString());

        return true;
    } // Helper method to get settings from the YAML cache

    /// <summary>
    ///     Gets the latest stable release version from GitHub API.
    /// </summary>
    /// <param name="client">HTTP client for making requests.</param>
    /// <param name="owner">Repository owner/organization name.</param>
    /// <param name="repo">Repository name.</param>
    /// <returns>Latest stable version or null.</returns>
    private async Task<Version?> GetGithubLatestStableVersionFromEndpointAsync(HttpClient client, string owner,
        string repo)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        try
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Scanner111"); // GitHub API requires a user agent
            var response = await client.GetAsync(url);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger?.LogInformation($"No '/releases/latest' found for {owner}/{repo} (status 404).");
                return null;
            }

            response.EnsureSuccessStatusCode();
            var jsonContent = await response.Content.ReadAsStringAsync();
            var releaseInfo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent);

            if (releaseInfo != null)
            {
                if (releaseInfo.TryGetValue("prerelease", out var prereleaseVal) &&
                    prereleaseVal.ValueKind == JsonValueKind.True)
                {
                    _logger?.LogWarning($"{url} returned a prerelease. Expected a stable release.");
                    return null;
                }

                if (releaseInfo.TryGetValue("name", out var nameVal) &&
                    nameVal.ValueKind == JsonValueKind.String)
                    return TryParseVersion(nameVal.GetString());
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError($"Error fetching latest stable release from {url}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    ///     Gets the latest prerelease version from GitHub API.
    /// </summary>
    /// <param name="client">HTTP client for making requests.</param>
    /// <param name="owner">Repository owner/organization name.</param>
    /// <param name="repo">Repository name.</param>
    /// <returns>Latest prerelease version or null.</returns>
    private async Task<Version?> GetGithubLatestPrereleaseVersionFromListAsync(HttpClient client, string owner,
        string repo)
    {
        var url = $"https://api.github.com/repos/{owner}/{repo}/releases";
        try
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Scanner111"); // GitHub API requires a user agent
            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonContent = await response.Content.ReadAsStringAsync();
            var releasesArray = JsonSerializer.Deserialize<JsonElement[]>(jsonContent);

            if (releasesArray == null)
            {
                _logger?.LogWarning($"Expected a list of releases from {url}, got null");
                return null;
            }

            // Iterate through releases (newest first) to find prereleases
            foreach (var releaseData in releasesArray)
            {
                if (!releaseData.TryGetProperty("prerelease", out var prereleaseVal) ||
                    prereleaseVal.ValueKind != JsonValueKind.True) continue;
                if (!releaseData.TryGetProperty("name", out var nameVal) ||
                    nameVal.ValueKind != JsonValueKind.String) continue;
                var releaseName = nameVal.GetString();
                if (string.IsNullOrEmpty(releaseName)) continue;
                var parsedVersion = TryParseVersion(releaseName);
                if (parsedVersion != null) return parsedVersion;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError("Error fetching releases list from {Url}: {ExMessage}", url, ex.Message);
        }

        return null;
    }

    /// <summary>
    ///     Gets detailed information about releases from GitHub.
    /// </summary>
    /// <param name="client">HTTP client for making requests.</param>
    /// <param name="owner">Repository owner/organization name.</param>
    /// <param name="repo">Repository name.</param>
    /// <returns>Dictionary with release details or null.</returns>
    private async Task<Dictionary<string, object?>?> GetLatestAndTopReleaseDetailsAsync(HttpClient client,
        string owner,
        string repo)
    {
        var latestUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
        var allReleasesUrl = $"https://api.github.com/repos/{owner}/{repo}/releases";

        var results = new Dictionary<string, object?>
        {
            ["latest_endpoint_release"] = null,
            ["top_of_list_release"] = null,
            ["are_same_release_by_id"] = false
        };

        try
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Scanner111"); // GitHub API requires a user agent

            // 1. Get release from /releases/latest
            var latestResponse = await client.GetAsync(latestUrl);
            if (latestResponse.StatusCode != HttpStatusCode.NotFound)
            {
                latestResponse.EnsureSuccessStatusCode();
                var latestJson = await latestResponse.Content.ReadAsStringAsync();
                var latestRelease = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(latestJson);

                if (latestRelease != null)
                {
                    var latestInfo = new Dictionary<string, object?>();

                    // Extract relevant fields
                    if (latestRelease.TryGetValue("id", out var idVal))
                        latestInfo["id"] = idVal.ToString();

                    if (latestRelease.TryGetValue("tag_name", out var tagVal))
                        latestInfo["tag_name"] = tagVal.GetString();

                    if (latestRelease.TryGetValue("name", out var nameVal))
                    {
                        var name = nameVal.GetString();
                        latestInfo["name"] = name;
                        if (name != null)
                            latestInfo["version"] = TryParseVersion(name);
                        else
                            latestInfo["version"] = null;
                    }

                    if (latestRelease.TryGetValue("prerelease", out var prereleaseVal))
                        latestInfo["prerelease"] = prereleaseVal.ValueKind == JsonValueKind.True;

                    if (latestRelease.TryGetValue("published_at", out var pubDateVal))
                        latestInfo["published_at"] = pubDateVal.GetString();

                    results["latest_endpoint_release"] = latestInfo;
                }
            }
            else
            {
                _logger?.LogInformation($"No '/releases/latest' found for {owner}/{repo} (status 404).");
            }

            // 2. Get all releases and take the top one
            var allReleasesResponse = await client.GetAsync(allReleasesUrl);
            allReleasesResponse.EnsureSuccessStatusCode();
            var allReleasesJson = await allReleasesResponse.Content.ReadAsStringAsync();
            var allReleases = JsonSerializer.Deserialize<JsonElement[]>(allReleasesJson);

            if (allReleases is { Length: > 0 })
            {
                var topRelease = allReleases[0]; // First release in the list
                var topInfo = new Dictionary<string, object?>();

                // Extract relevant fields
                if (topRelease.TryGetProperty("id", out var idVal))
                    topInfo["id"] = idVal.ToString();

                if (topRelease.TryGetProperty("tag_name", out var tagVal))
                    topInfo["tag_name"] = tagVal.GetString();

                if (topRelease.TryGetProperty("name", out var nameVal))
                {
                    var name = nameVal.GetString();
                    topInfo["name"] = name;
                    if (name != null)
                        topInfo["version"] = TryParseVersion(name);
                    else
                        topInfo["version"] = null;
                }

                if (topRelease.TryGetProperty("prerelease", out var prereleaseVal))
                    topInfo["prerelease"] = prereleaseVal.ValueKind == JsonValueKind.True;

                if (topRelease.TryGetProperty("published_at", out var pubDateVal))
                    topInfo["published_at"] = pubDateVal.GetString();

                results["top_of_list_release"] = topInfo;
            }
            else
            {
                _logger?.LogWarning("No releases found or unexpected format from {AllReleasesUrl}", allReleasesUrl);
            }

            // Check if they're the same release

            if (results["latest_endpoint_release"] is Dictionary<string, object?> latestEndpointRelease &&
                results["top_of_list_release"] is Dictionary<string, object?> topOfListRelease &&
                latestEndpointRelease.TryGetValue("id", out var latestIdObj) && latestIdObj != null &&
                topOfListRelease.TryGetValue("id", out var topIdObj) && topIdObj != null)
                results["are_same_release_by_id"] = latestIdObj.ToString() == topIdObj.ToString();
            else
                results["are_same_release_by_id"] = false; // Or handle as appropriate if one or both are null

            return results;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError("GitHub API ClientError for {Owner}/{Repo}: {ExMessage}", owner, repo, ex.Message);
            return results["latest_endpoint_release"] != null || results["top_of_list_release"] != null
                ? results
                : null;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Unexpected error fetching release details for {Owner}/{Repo}: {ExMessage}", owner, repo,
                ex.Message);
            return null;
        }
    }

    /// <summary>
    ///     Gets the version information from NexusMods.
    /// </summary>
    /// <param name="client">HTTP client for making requests.</param>
    /// <returns>Nexus version or null.</returns>
    private async Task<Version?> GetNexusVersionAsync(HttpClient client)
    {
        // Constants
        const string nexusModUrl = "https://www.nexusmods.com/fallout4/mods/56255";
        const string versionPropertyName = "twitter:label1";
        const string versionPropertyValue = "Version";
        const string versionDataProperty = "twitter:data1"; // This line was identified with CS0219

        try
        {
            var response = await client.GetAsync(nexusModUrl);

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Failed to fetch Nexus mod page: HTTP {ResponseStatusCode}", response.StatusCode);
                return null;
            }

            var htmlContent = await response.Content.ReadAsStringAsync();
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(htmlContent);

            // Find the meta tag that indicates version label
            var versionLabelTag = htmlDoc.DocumentNode.SelectSingleNode(
                $"//meta[@property='{versionPropertyName}' and @content='{versionPropertyValue}']");

            if (versionLabelTag == null)
            {
                _logger?.LogDebug("Version label meta tag not found");
                return null;
            }

            // Look for the next meta tag with version data
            // Corrected: Use the variable versionDataProperty
            var versionDataTag =
                htmlDoc.DocumentNode.SelectSingleNode($"//meta[@property='{versionDataProperty}']");

            if (versionDataTag == null) // This was line 366 in the error report
            {
                _logger?.LogDebug("Version data meta tag not found");
                return null;
            }

            // Ensure versionDataTag is used, this was line 372 in the error report
            var versionStr = versionDataTag.GetAttributeValue("content", string.Empty);
            if (!string.IsNullOrEmpty(versionStr))
            {
                var parsedVersion = TryParseVersion(versionStr);
                if (parsedVersion != null)
                {
                    _logger?.LogDebug("Successfully parsed Nexus version: {ParsedVersion}", parsedVersion);
                    return parsedVersion;
                }

                _logger?.LogDebug("Failed to parse version string: '{VersionStr}'", versionStr);
            }
            else
            {
                _logger?.LogDebug("Version string from meta tag is null or empty.");
            }

            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError("Network error while fetching Nexus version: {ExMessage}", ex.Message);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Unexpected error parsing Nexus version: {ExMessage}", ex.Message);
        }

        return null;
    }

    private T? GetSetting<T>(Yaml yamlType, string key)
    {
        try
        {
            return _yamlSettingsCache.GetSetting<T>(yamlType, key);
        }
        catch
        {
            return default;
        }
    }
}

/// <summary>
///     Exception thrown when an update check fails or a newer version is found.
/// </summary>
public class UpdateCheckException : Exception
{
    public UpdateCheckException(string message) : base(message)
    {
    }

    public UpdateCheckException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
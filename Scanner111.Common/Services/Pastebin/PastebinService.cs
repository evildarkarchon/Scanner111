using System.Text.RegularExpressions;
using Scanner111.Common.Models.Pastebin;

namespace Scanner111.Common.Services.Pastebin;

/// <summary>
/// Service for fetching crash logs from various pastebin services.
/// </summary>
public sealed partial class PastebinService : IPastebinService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseSavePath;

    // Regex patterns for supported pastebin services
    [GeneratedRegex(@"^https?://(www\.)?pastebin\.com/(raw/)?(\w+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PastebinComRegex();

    [GeneratedRegex(@"^https?://(www\.)?paste\.ee/(p|r)/(\w+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PasteEeRegex();

    [GeneratedRegex(@"^https?://(www\.)?(hastebin\.com|haste\.zneix\.eu)/(raw/)?(\w+)(\.txt)?$", RegexOptions.IgnoreCase)]
    private static partial Regex HastebinRegex();

    // Simple alphanumeric ID pattern (for direct paste IDs)
    [GeneratedRegex(@"^[a-zA-Z0-9]+$")]
    private static partial Regex SimpleIdRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="PastebinService"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for making requests.</param>
    /// <param name="baseSavePath">Base path for saving downloaded logs. Defaults to current directory.</param>
    public PastebinService(HttpClient httpClient, string? baseSavePath = null)
    {
        _httpClient = httpClient;
        _baseSavePath = baseSavePath ?? Directory.GetCurrentDirectory();
    }

    /// <inheritdoc/>
    public async Task<PastebinFetchResult> FetchAsync(string urlOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(urlOrId))
        {
            return PastebinFetchResult.CreateFailure("URL or paste ID cannot be empty.");
        }

        urlOrId = urlOrId.Trim();

        try
        {
            var (rawUrl, pasteId, sourceUrl) = ParseAndNormalizeUrl(urlOrId);

            if (rawUrl is null || pasteId is null)
            {
                return PastebinFetchResult.CreateFailure($"Could not parse pastebin URL or ID: {urlOrId}");
            }

            using var response = await _httpClient.GetAsync(rawUrl, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // Ensure save directory exists
            var saveDir = Path.Combine(_baseSavePath, "Crash Logs", "Pastebin");
            Directory.CreateDirectory(saveDir);

            // Save to file
            var fileName = $"crash-{pasteId}.log";
            var filePath = Path.Combine(saveDir, fileName);
            await File.WriteAllTextAsync(filePath, content, cancellationToken).ConfigureAwait(false);

            return PastebinFetchResult.CreateSuccess(content, filePath, sourceUrl, pasteId);
        }
        catch (HttpRequestException ex)
        {
            return PastebinFetchResult.CreateFailure($"Network error: {ex.Message}", urlOrId);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            return PastebinFetchResult.CreateFailure("Fetch operation was cancelled.", urlOrId);
        }
        catch (TaskCanceledException)
        {
            return PastebinFetchResult.CreateFailure("Request timed out.", urlOrId);
        }
        catch (Exception ex)
        {
            return PastebinFetchResult.CreateFailure($"Unexpected error: {ex.Message}", urlOrId);
        }
    }

    /// <inheritdoc/>
    public bool IsValidInput(string urlOrId)
    {
        if (string.IsNullOrWhiteSpace(urlOrId))
        {
            return false;
        }

        urlOrId = urlOrId.Trim();

        // Check if it's a URL for any supported service
        if (PastebinComRegex().IsMatch(urlOrId) ||
            PasteEeRegex().IsMatch(urlOrId) ||
            HastebinRegex().IsMatch(urlOrId))
        {
            return true;
        }

        // Check if it's a simple paste ID (assumes pastebin.com)
        return SimpleIdRegex().IsMatch(urlOrId) && urlOrId.Length >= 4 && urlOrId.Length <= 16;
    }

    /// <summary>
    /// Parses and normalizes the URL/ID to get the raw content URL.
    /// </summary>
    /// <returns>Tuple of (raw URL, paste ID, source URL).</returns>
    private static (string? RawUrl, string? PasteId, string SourceUrl) ParseAndNormalizeUrl(string urlOrId)
    {
        // Try pastebin.com
        var match = PastebinComRegex().Match(urlOrId);
        if (match.Success)
        {
            var pasteId = match.Groups[3].Value;
            var rawUrl = $"https://pastebin.com/raw/{pasteId}";
            return (rawUrl, pasteId, urlOrId);
        }

        // Try paste.ee
        match = PasteEeRegex().Match(urlOrId);
        if (match.Success)
        {
            var pasteId = match.Groups[3].Value;
            var rawUrl = $"https://paste.ee/r/{pasteId}";
            return (rawUrl, pasteId, urlOrId);
        }

        // Try hastebin.com or haste.zneix.eu
        match = HastebinRegex().Match(urlOrId);
        if (match.Success)
        {
            var host = match.Groups[2].Value;
            var pasteId = match.Groups[4].Value;
            var rawUrl = $"https://{host}/raw/{pasteId}";
            return (rawUrl, pasteId, urlOrId);
        }

        // Assume it's a simple paste ID for pastebin.com
        if (SimpleIdRegex().IsMatch(urlOrId) && urlOrId.Length >= 4 && urlOrId.Length <= 16)
        {
            var rawUrl = $"https://pastebin.com/raw/{urlOrId}";
            var sourceUrl = $"https://pastebin.com/{urlOrId}";
            return (rawUrl, urlOrId, sourceUrl);
        }

        return (null, null, urlOrId);
    }
}

using System.Text;
using System.Text.RegularExpressions;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Services;

/// <summary>
/// Service implementation for fetching crash logs from Pastebin URLs and saving them locally.
/// </summary>
public partial class PastebinService : IPastebinService
{
    private const string PastebinDomain = "pastebin.com";
    private const string PastebinRawPath = "pastebin.com/raw";
    private const string LocalPastebinDirectory = "Crash Logs/Pastebin";
    
    private static readonly HttpClient HttpClient = new();
    private static readonly Regex PastebinUrlRegex = PastebinUrlPattern();
    private static readonly Regex PastebinIdRegex = PastebinIdPattern();
    
    private readonly ILogger<PastebinService> _logger;
    private readonly IMessageHandler _messageHandler;
    private readonly IApplicationSettingsService _settingsService;
    private readonly SemaphoreSlim _downloadSemaphore;

    static PastebinService()
    {
        HttpClient.DefaultRequestHeaders.Add("User-Agent", "Scanner111/1.0");
        HttpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public PastebinService(
        ILogger<PastebinService> logger,
        IApplicationSettingsService settingsService,
        IMessageHandler messageHandler)
    {
        _logger = logger;
        _settingsService = settingsService;
        _messageHandler = messageHandler;
        _downloadSemaphore = new SemaphoreSlim(3, 3); // Allow up to 3 concurrent downloads
    }

    /// <inheritdoc />
    public async Task<string?> FetchAndSaveAsync(string urlOrId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(urlOrId))
        {
            _logger.LogWarning("Empty Pastebin URL or ID provided");
            return null;
        }

        if (!IsValidPastebinInput(urlOrId))
        {
            _logger.LogWarning("Invalid Pastebin input: {Input}", urlOrId);
            _messageHandler.ShowError($"Invalid Pastebin URL or ID: {urlOrId}");
            return null;
        }

        var rawUrl = ConvertToRawUrl(urlOrId);
        _logger.LogInformation("Fetching from Pastebin: {Url}", rawUrl);

        await _downloadSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await HttpClient.GetAsync(rawUrl, cancellationToken).ConfigureAwait(false);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Failed to fetch from Pastebin. Status: {Status}", response.StatusCode);
                _messageHandler.ShowError($"Failed to fetch from Pastebin: HTTP {response.StatusCode}");
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Pastebin returned empty content");
                _messageHandler.ShowError("Pastebin returned empty content");
                return null;
            }

            // Extract ID from URL for filename
            var pastebinId = ExtractPastebinId(urlOrId);
            var filePath = await SaveContentAsync(content, pastebinId, cancellationToken).ConfigureAwait(false);
            
            _logger.LogInformation("Successfully saved Pastebin content to: {Path}", filePath);
            _messageHandler.ShowSuccess($"Log fetched from Pastebin and saved to: {Path.GetFileName(filePath)}");
            
            return filePath;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error while fetching from Pastebin");
            _messageHandler.ShowError($"Network error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Pastebin fetch was cancelled or timed out");
            _messageHandler.ShowError("Request timed out or was cancelled");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching from Pastebin");
            _messageHandler.ShowError($"Unexpected error: {ex.Message}");
            return null;
        }
        finally
        {
            _downloadSemaphore.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string?>> FetchMultipleAsync(
        IEnumerable<string> urlsOrIds, 
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, string?>();
        var tasks = new List<Task<(string Input, string? Path)>>();

        foreach (var urlOrId in urlsOrIds)
        {
            tasks.Add(FetchSingleWithInputAsync(urlOrId, cancellationToken));
        }

        var completedTasks = await Task.WhenAll(tasks).ConfigureAwait(false);
        
        foreach (var (input, path) in completedTasks)
        {
            results[input] = path;
        }

        return results;
    }

    /// <inheritdoc />
    public bool IsValidPastebinInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return false;

        return PastebinUrlRegex.IsMatch(input) || PastebinIdRegex.IsMatch(input);
    }

    /// <inheritdoc />
    public string ConvertToRawUrl(string urlOrId)
    {
        // If it's just an ID, convert to full URL
        if (PastebinIdRegex.IsMatch(urlOrId) && !urlOrId.Contains("://"))
        {
            return $"https://{PastebinRawPath}/{urlOrId}";
        }

        // If it's already a raw URL, return as-is
        if (urlOrId.Contains("/raw/") || urlOrId.Contains(PastebinRawPath))
        {
            return urlOrId;
        }

        // Convert regular Pastebin URL to raw URL
        if (urlOrId.Contains(PastebinDomain))
        {
            return urlOrId.Replace(PastebinDomain, PastebinRawPath);
        }

        // Fallback: assume it's an ID
        return $"https://{PastebinRawPath}/{urlOrId}";
    }

    private async Task<(string Input, string? Path)> FetchSingleWithInputAsync(
        string urlOrId, 
        CancellationToken cancellationToken)
    {
        var path = await FetchAndSaveAsync(urlOrId, cancellationToken).ConfigureAwait(false);
        return (urlOrId, path);
    }

    private async Task<string> SaveContentAsync(string content, string pastebinId, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(LocalPastebinDirectory);
        Directory.CreateDirectory(directory);

        var fileName = $"crash-{pastebinId}.log";
        var filePath = Path.Combine(directory, fileName);

        // Use async file operations
        await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        
        return filePath;
    }

    private static string ExtractPastebinId(string urlOrId)
    {
        // If it's just an ID, return it
        if (PastebinIdRegex.IsMatch(urlOrId) && !urlOrId.Contains("://"))
        {
            return urlOrId;
        }

        // Extract ID from URL
        var match = PastebinUrlRegex.Match(urlOrId);
        if (match.Success && match.Groups.Count > 1)
        {
            return match.Groups[1].Value;
        }

        // Try to extract from path
        var uri = new Uri(urlOrId, UriKind.RelativeOrAbsolute);
        if (uri.IsAbsoluteUri)
        {
            var segments = uri.Segments;
            if (segments.Length > 0)
            {
                var lastSegment = segments[^1].TrimEnd('/');
                if (!string.IsNullOrEmpty(lastSegment) && lastSegment != "raw")
                {
                    return lastSegment;
                }
            }
        }

        // Fallback to a timestamp-based ID
        return DateTime.Now.ToString("yyyyMMddHHmmss");
    }

    [GeneratedRegex(@"^https?://pastebin\.com/(raw/)?(\w+)$", RegexOptions.IgnoreCase)]
    private static partial Regex PastebinUrlPattern();

    [GeneratedRegex(@"^[a-zA-Z0-9]+$")]
    private static partial Regex PastebinIdPattern();
}
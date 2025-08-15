namespace Scanner111.Core.Services;

/// <summary>
/// Service for fetching crash logs from Pastebin URLs and saving them locally.
/// </summary>
public interface IPastebinService
{
    /// <summary>
    /// Fetches a crash log from a Pastebin URL and saves it to the local file system.
    /// </summary>
    /// <param name="urlOrId">The Pastebin URL or ID to fetch. IDs will be converted to URLs.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The path to the saved file if successful, or null if the fetch failed.</returns>
    Task<string?> FetchAndSaveAsync(string urlOrId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches multiple crash logs from Pastebin URLs.
    /// </summary>
    /// <param name="urlsOrIds">Collection of Pastebin URLs or IDs to fetch.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A dictionary mapping input URLs/IDs to their saved file paths (null if failed).</returns>
    Task<Dictionary<string, string?>> FetchMultipleAsync(IEnumerable<string> urlsOrIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a string is a valid Pastebin URL or ID.
    /// </summary>
    /// <param name="input">The input string to validate.</param>
    /// <returns>True if the input is a valid Pastebin URL or ID.</returns>
    bool IsValidPastebinInput(string input);

    /// <summary>
    /// Converts a Pastebin ID or URL to a raw content URL.
    /// </summary>
    /// <param name="urlOrId">The Pastebin URL or ID.</param>
    /// <returns>The raw content URL for fetching.</returns>
    string ConvertToRawUrl(string urlOrId);
}
namespace Scanner111.Common.Models.Pastebin;

/// <summary>
/// Result of a pastebin fetch operation.
/// </summary>
public sealed class PastebinFetchResult
{
    /// <summary>
    /// Gets a value indicating whether the fetch was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets the downloaded content, if successful.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Gets the local file path where the content was saved, if successful.
    /// </summary>
    public string? SavedFilePath { get; init; }

    /// <summary>
    /// Gets the original pastebin URL that was fetched.
    /// </summary>
    public string? SourceUrl { get; init; }

    /// <summary>
    /// Gets the paste ID extracted from the URL.
    /// </summary>
    public string? PasteId { get; init; }

    /// <summary>
    /// Gets the error message if the fetch failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static PastebinFetchResult CreateSuccess(
        string content,
        string savedFilePath,
        string sourceUrl,
        string pasteId) => new()
    {
        Success = true,
        Content = content,
        SavedFilePath = savedFilePath,
        SourceUrl = sourceUrl,
        PasteId = pasteId
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static PastebinFetchResult CreateFailure(string errorMessage, string? sourceUrl = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        SourceUrl = sourceUrl
    };
}

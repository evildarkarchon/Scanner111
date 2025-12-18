using Scanner111.Common.Models.Pastebin;

namespace Scanner111.Common.Services.Pastebin;

/// <summary>
/// Service for fetching crash logs from various pastebin services.
/// </summary>
public interface IPastebinService
{
    /// <summary>
    /// Fetches content from a pastebin URL or ID and saves it locally.
    /// </summary>
    /// <param name="urlOrId">The pastebin URL or paste ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the downloaded content and file path.</returns>
    /// <remarks>
    /// Supported pastebin services:
    /// <list type="bullet">
    /// <item>pastebin.com</item>
    /// <item>paste.ee</item>
    /// <item>hastebin.com</item>
    /// <item>haste.zneix.eu</item>
    /// </list>
    /// </remarks>
    Task<PastebinFetchResult> FetchAsync(string urlOrId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the given input is a valid pastebin URL or ID.
    /// </summary>
    /// <param name="urlOrId">The URL or ID to validate.</param>
    /// <returns>True if the input appears to be a valid pastebin reference.</returns>
    bool IsValidInput(string urlOrId);
}

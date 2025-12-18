namespace Scanner111.Common.Models.Update;

/// <summary>
/// Represents information about a GitHub release.
/// </summary>
public sealed class ReleaseInfo
{
    /// <summary>
    /// Gets the version string (e.g., "1.2.3").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Gets the release tag name (e.g., "v1.2.3").
    /// </summary>
    public required string TagName { get; init; }

    /// <summary>
    /// Gets the release name/title.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the release notes/body content in markdown format.
    /// </summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>
    /// Gets the URL to the release page on GitHub.
    /// </summary>
    public required string HtmlUrl { get; init; }

    /// <summary>
    /// Gets the publication date of the release.
    /// </summary>
    public DateTimeOffset PublishedAt { get; init; }

    /// <summary>
    /// Gets a value indicating whether this is a prerelease.
    /// </summary>
    public bool IsPrerelease { get; init; }
}

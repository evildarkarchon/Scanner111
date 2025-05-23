using System;
using NuGet.Versioning;

namespace Scanner111.Models;

/// <summary>
///     Represents version information for a release from GitHub or Nexus
/// </summary>
public class ReleaseInfo
{
    /// <summary>
    ///     The unique ID of the release (typically from GitHub)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     The tag name of the release (typically from GitHub)
    /// </summary>
    public string TagName { get; set; } = string.Empty;

    /// <summary>
    ///     The display name of the release
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     The parsed semantic version of the release
    /// </summary>
    public SemanticVersion? Version { get; set; }

    /// <summary>
    ///     Whether the release is a pre-release
    /// </summary>
    public bool IsPreRelease { get; set; }

    /// <summary>
    ///     When the release was published
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }
}

/// <summary>
///     Contains detailed information about release sources and their compatibility
/// </summary>
public class ReleaseDetailsInfo
{
    /// <summary>
    ///     Release from the GitHub "latest" endpoint
    /// </summary>
    public ReleaseInfo? LatestEndpointRelease { get; set; }

    /// <summary>
    ///     First release from the GitHub releases list
    /// </summary>
    public ReleaseInfo? TopOfListRelease { get; set; }

    /// <summary>
    ///     Whether both releases are the same (by ID)
    /// </summary>
    public bool AreSameReleaseById { get; set; }
}
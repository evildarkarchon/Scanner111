namespace Scanner111.Core.Models;

/// <summary>
///     Represents information about different Address Library versions and their requirements.
/// </summary>
public sealed class AddressLibraryInfo
{
    private AddressLibraryInfo(string version, string filename, string description, string downloadUrl)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Filename = filename ?? throw new ArgumentNullException(nameof(filename));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        DownloadUrl = downloadUrl ?? throw new ArgumentNullException(nameof(downloadUrl));
    }

    /// <summary>
    ///     Gets the version identifier (e.g., "VR", "OG", "NG").
    /// </summary>
    public string Version { get; }

    /// <summary>
    ///     Gets the expected filename for this version.
    /// </summary>
    public string Filename { get; }

    /// <summary>
    ///     Gets the human-readable description of this version.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Gets the download URL for this version.
    /// </summary>
    public string DownloadUrl { get; }

    /// <summary>
    ///     Gets the known Address Library information for all supported versions.
    /// </summary>
    public static IReadOnlyDictionary<string, AddressLibraryInfo> KnownVersions { get; } =
        new Dictionary<string, AddressLibraryInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["VR"] = new AddressLibraryInfo(
                "VR",
                "version-1-2-72-0.csv",
                "Virtual Reality (VR) version",
                "https://www.nexusmods.com/fallout4/mods/64879?tab=files"),

            ["OG"] = new AddressLibraryInfo(
                "OG",
                "version-1-10-163-0.bin",
                "Non-VR (Regular) version",
                "https://www.nexusmods.com/fallout4/mods/47327?tab=files"),

            ["NG"] = new AddressLibraryInfo(
                "NG",
                "version-1-10-984-0.bin",
                "Non-VR (New Game) version",
                "https://www.nexusmods.com/fallout4/mods/47327?tab=files")
        };

    /// <summary>
    ///     Determines the correct and incorrect Address Library versions based on VR mode.
    /// </summary>
    /// <param name="isVrMode">Whether VR mode is enabled</param>
    /// <returns>Tuple of correct versions and incorrect versions</returns>
    public static (IReadOnlyList<AddressLibraryInfo> CorrectVersions, IReadOnlyList<AddressLibraryInfo> IncorrectVersions) 
        DetermineRelevantVersions(bool isVrMode)
    {
        if (isVrMode)
        {
            var correctVersions = new[] { KnownVersions["VR"] };
            var incorrectVersions = new[] { KnownVersions["OG"], KnownVersions["NG"] };
            return (correctVersions, incorrectVersions);
        }
        else
        {
            var correctVersions = new[] { KnownVersions["OG"], KnownVersions["NG"] };
            var incorrectVersions = new[] { KnownVersions["VR"] };
            return (correctVersions, incorrectVersions);
        }
    }

    public override string ToString()
    {
        return $"{Version}: {Description} ({Filename})";
    }
}
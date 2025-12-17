namespace Scanner111.Common.Models.DocsPath;

/// <summary>
/// Contains derived document paths based on a game's documents root folder.
/// </summary>
/// <remarks>
/// <para>
/// These paths are generated from the root documents folder (e.g., Documents/My Games/Fallout4)
/// and represent common file locations used for analysis and configuration.
/// </para>
/// <para>
/// All paths are constructed using <see cref="Path.Combine(string, string)"/> for cross-platform compatibility.
/// </para>
/// </remarks>
public record GeneratedDocsPaths
{
    /// <summary>
    /// Gets the root documents folder path (e.g., Documents/My Games/Fallout4).
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// Gets the path to the XSE configuration folder (e.g., Documents/My Games/Fallout4/F4SE).
    /// </summary>
    public required string XseFolderPath { get; init; }

    /// <summary>
    /// Gets the path to the Papyrus log file (e.g., Documents/My Games/Fallout4/Logs/Script/Papyrus.0.log).
    /// </summary>
    public required string PapyrusLogPath { get; init; }

    /// <summary>
    /// Gets the path to the Wrye Bash ModChecker file (e.g., Documents/My Games/Fallout4/ModChecker.html).
    /// </summary>
    public required string WryeBashModCheckerPath { get; init; }

    /// <summary>
    /// Gets the path to the XSE log file (e.g., Documents/My Games/Fallout4/F4SE/f4se.log).
    /// </summary>
    public required string XseLogPath { get; init; }

    /// <summary>
    /// Gets the path to the main INI file (e.g., Documents/My Games/Fallout4/Fallout4.ini).
    /// </summary>
    public required string MainIniPath { get; init; }

    /// <summary>
    /// Gets the path to the custom INI file (e.g., Documents/My Games/Fallout4/Fallout4Custom.ini).
    /// </summary>
    public required string CustomIniPath { get; init; }

    /// <summary>
    /// Gets the path to the prefs INI file (e.g., Documents/My Games/Fallout4/Fallout4Prefs.ini).
    /// </summary>
    public required string PrefsIniPath { get; init; }
}

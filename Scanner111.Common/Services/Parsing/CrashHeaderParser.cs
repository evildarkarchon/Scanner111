using System.Globalization;
using System.Text.RegularExpressions;
using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Parsing;

/// <summary>
/// Parses crash log headers to extract metadata.
/// </summary>
public partial class CrashHeaderParser
{
    /// <summary>
    /// Regex to match Fallout 4 version.
    /// Example: "Fallout 4 v1.10.163.0"
    /// </summary>
    [GeneratedRegex(@"Fallout\s+4\s+v([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Fallout4VersionRegex();

    /// <summary>
    /// Regex to match Skyrim version.
    /// Example: "Skyrim SE v1.5.97.0"
    /// </summary>
    [GeneratedRegex(@"Skyrim\s+(?:SE|Special Edition)?\s*v([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SkyrimVersionRegex();

    /// <summary>
    /// Regex to match crash generator version.
    /// Example: "Buffout 4 v1.26.2" or "Crash Logger v1.0.0"
    /// </summary>
    [GeneratedRegex(@"(Buffout\s+4|Crash Logger|Trainwreck)\s+v([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CrashGeneratorRegex();

    /// <summary>
    /// Regex to match main error message.
    /// Example: Unhandled exception "EXCEPTION_ACCESS_VIOLATION"
    /// </summary>
    [GeneratedRegex(@"Unhandled exception\s+""(.+?)""", RegexOptions.IgnoreCase)]
    private static partial Regex MainErrorRegex();

    /// <summary>
    /// Regex to match crash timestamp.
    /// Example: "2023-12-07 02:24:27"
    /// </summary>
    [GeneratedRegex(@"(\d{4})-(\d{2})-(\d{2})\s+(\d{2}):(\d{2}):(\d{2})")]
    private static partial Regex TimestampRegex();

    /// <summary>
    /// Parses the crash header from log content.
    /// </summary>
    /// <param name="logContent">The full crash log content.</param>
    /// <returns>A <see cref="CrashHeader"/> if successful; otherwise, null.</returns>
    public CrashHeader? Parse(string logContent)
    {
        if (string.IsNullOrWhiteSpace(logContent))
        {
            return null;
        }

        // Only parse the first 2000 characters for performance
        // (header information is always at the beginning)
        var headerSection = logContent.Length > 2000
            ? logContent[..2000]
            : logContent;

        var gameVersion = ExtractGameVersion(headerSection);
        var crashGeneratorVersion = ExtractCrashGeneratorVersion(headerSection);
        var mainError = ExtractMainError(headerSection);
        var timestamp = ExtractTimestamp(logContent); // Use full content for timestamp

        // At minimum, we need either a game version or crash generator version
        if (string.IsNullOrEmpty(gameVersion) && string.IsNullOrEmpty(crashGeneratorVersion))
        {
            return null;
        }

        return new CrashHeader
        {
            GameVersion = gameVersion,
            CrashGeneratorVersion = crashGeneratorVersion,
            MainError = mainError,
            CrashTimestamp = timestamp
        };
    }

    private string ExtractGameVersion(string headerSection)
    {
        var fo4Match = Fallout4VersionRegex().Match(headerSection);
        if (fo4Match.Success)
        {
            return fo4Match.Groups[1].Value;
        }

        var skyrimMatch = SkyrimVersionRegex().Match(headerSection);
        if (skyrimMatch.Success)
        {
            return skyrimMatch.Groups[1].Value;
        }

        return string.Empty;
    }

    private string ExtractCrashGeneratorVersion(string headerSection)
    {
        var match = CrashGeneratorRegex().Match(headerSection);
        if (match.Success)
        {
            var generatorName = match.Groups[1].Value;
            var version = match.Groups[2].Value;
            return $"{generatorName} v{version}";
        }

        return string.Empty;
    }

    private string ExtractMainError(string headerSection)
    {
        var match = MainErrorRegex().Match(headerSection);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private DateTime? ExtractTimestamp(string logContent)
    {
        var match = TimestampRegex().Match(logContent);
        if (!match.Success)
        {
            return null;
        }

        try
        {
            var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var day = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            var hour = int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture);
            var minute = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
            var second = int.Parse(match.Groups[6].Value, CultureInfo.InvariantCulture);

            return new DateTime(year, month, day, hour, minute, second);
        }
        catch
        {
            return null;
        }
    }
}

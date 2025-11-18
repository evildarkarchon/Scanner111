using System.Text.RegularExpressions;
using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Scans crash logger settings and validates them against recommended configurations.
/// </summary>
public partial class SettingsScanner : ISettingsScanner
{
    /// <summary>
    /// Regex to match setting lines in the format: SettingName: value
    /// Example: "MemoryManager: false"
    /// </summary>
    [GeneratedRegex(@"^\s*([A-Za-z0-9_]+)\s*:\s*(.+?)\s*$", RegexOptions.Multiline)]
    private static partial Regex SettingLineRegex();

    /// <summary>
    /// Regex to match version information.
    /// Example: "Buffout 4 v1.26.2" or "Crash Logger v1.0.0"
    /// </summary>
    [GeneratedRegex(@"(Buffout\s+4|Crash Logger|Trainwreck)\s+v([\d.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex VersionRegex();

    /// <inheritdoc/>
    public async Task<SettingsScanResult> ScanAsync(
        LogSegment? compatibilitySegment,
        GameSettings expectedSettings,
        CancellationToken cancellationToken = default)
    {
        // Allow async operation to be cancelled
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (compatibilitySegment == null || compatibilitySegment.Lines.Count == 0)
        {
            return new SettingsScanResult
            {
                Warnings = new[] { "No Compatibility segment found in crash log" }
            };
        }

        var content = string.Join("\n", compatibilitySegment.Lines);

        var detectedSettings = ExtractSettings(content);
        var detectedVersion = ExtractVersion(content);
        var misconfigurations = FindMisconfigurations(detectedSettings, expectedSettings);
        var warnings = GenerateWarnings(detectedVersion, expectedSettings, misconfigurations);

        var isOutdated = IsVersionOutdated(detectedVersion, expectedSettings.LatestCrashLoggerVersion);

        return new SettingsScanResult
        {
            DetectedSettings = detectedSettings,
            Misconfigurations = misconfigurations,
            Warnings = warnings,
            DetectedVersion = detectedVersion,
            IsOutdated = isOutdated
        };
    }

    private Dictionary<string, string> ExtractSettings(string content)
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var matches = SettingLineRegex().Matches(content);

        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value;
            settings[key] = value;
        }

        return settings;
    }

    private string? ExtractVersion(string content)
    {
        var match = VersionRegex().Match(content);
        if (match.Success)
        {
            return $"{match.Groups[1].Value} v{match.Groups[2].Value}";
        }
        return null;
    }

    private List<string> FindMisconfigurations(
        IReadOnlyDictionary<string, string> detected,
        GameSettings expected)
    {
        var misconfigurations = new List<string>();

        foreach (var (key, expectedValue) in expected.RecommendedSettings)
        {
            if (detected.TryGetValue(key, out var actualValue))
            {
                if (!string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    misconfigurations.Add(
                        $"{key}: Expected '{expectedValue}', but found '{actualValue}'");
                }
            }
            else
            {
                misconfigurations.Add($"{key}: Setting not found in crash log");
            }
        }

        return misconfigurations;
    }

    private List<string> GenerateWarnings(
        string? detectedVersion,
        GameSettings expectedSettings,
        IReadOnlyList<string> misconfigurations)
    {
        var warnings = new List<string>();

        if (detectedVersion == null)
        {
            warnings.Add("Could not detect crash logger version");
        }
        else if (IsVersionOutdated(detectedVersion, expectedSettings.LatestCrashLoggerVersion))
        {
            warnings.Add($"Outdated crash logger detected: {detectedVersion}. " +
                        $"Latest version is {expectedSettings.LatestCrashLoggerVersion}");
        }

        if (misconfigurations.Count > 0)
        {
            warnings.Add($"Found {misconfigurations.Count} setting misconfiguration(s)");
        }

        return warnings;
    }

    private bool IsVersionOutdated(string? detectedVersion, string latestVersion)
    {
        if (string.IsNullOrWhiteSpace(detectedVersion) || string.IsNullOrWhiteSpace(latestVersion))
        {
            return false;
        }

        // Extract version numbers from strings like "Buffout 4 v1.26.2"
        var detectedMatch = Regex.Match(detectedVersion, @"v([\d.]+)");
        var latestMatch = Regex.Match(latestVersion, @"[\d.]+");

        if (!detectedMatch.Success || !latestMatch.Success)
        {
            return false;
        }

        var detected = Version.Parse(detectedMatch.Groups[1].Value);
        var latest = Version.Parse(latestMatch.Value);

        return detected < latest;
    }
}

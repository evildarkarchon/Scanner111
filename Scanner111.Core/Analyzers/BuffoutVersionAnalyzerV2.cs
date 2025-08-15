using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;

namespace Scanner111.Core.Analyzers;

/// <summary>
///     Analyzes Buffout 4 version and determines if an update is available
///     Uses the new YAML structure with version variants
/// </summary>
public class BuffoutVersionAnalyzerV2 : IAnalyzer
{
    private readonly IYamlSettingsProvider _yamlSettings;

    public BuffoutVersionAnalyzerV2(IYamlSettingsProvider yamlSettings)
    {
        _yamlSettings = yamlSettings;
    }

    public string Name => "Buffout Version Analyzer";
    public int Priority => 95; // Run early to show version info prominently
    public bool CanRunInParallel => true; // No dependencies on other analyzers

    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask.ConfigureAwait(false); // Keep async for consistency

        var result = new GenericAnalysisResult
        {
            AnalyzerName = Name,
            Success = true,
            HasFindings = false
        };

        // Only analyze if we have a crash generator version
        if (string.IsNullOrEmpty(crashLog.CrashGenVersion)) return result;

        // Load YAML data with new structure
        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        if (fallout4Yaml?.GameInfo?.Versions == null)
            return new GenericAnalysisResult
            {
                AnalyzerName = Name,
                Success = false,
                HasFindings = false,
                ReportLines = { "Warning: Could not load Buffout 4 version data\n" }
            };

        // Parse current version
        var currentVersion = ParseBuffoutVersion(crashLog.CrashGenVersion);

        // Check against all available versions
        var isLatest = false;
        string? recommendedVersion = null;
        Version? highestVersion = null;

        if (currentVersion != null)
            foreach (var versionEntry in fallout4Yaml.GameInfo.Versions)
            {
                var versionInfo = versionEntry.Value;
                if (!string.IsNullOrEmpty(versionInfo.BuffoutLatest))
                {
                    var latestVersion = ParseBuffoutVersion(versionInfo.BuffoutLatest);
                    if (latestVersion != null)
                    {
                        // Track the highest version available
                        if (highestVersion == null || latestVersion > highestVersion)
                        {
                            highestVersion = latestVersion;
                            recommendedVersion = versionInfo.BuffoutLatest;
                        }

                        // User has latest if they have ANY of the latest versions
                        if (currentVersion >= latestVersion) isLatest = true;
                    }
                }
            }

        // Generate report lines
        result.ReportLines.Add($"Main Error: {crashLog.MainError}\n");
        result.ReportLines.Add($"Detected Buffout 4 Version: {crashLog.CrashGenVersion} \n");

        if (isLatest || highestVersion == null)
        {
            result.ReportLines.Add("* You have the latest version of Buffout 4! *\n");
        }
        else if (!string.IsNullOrEmpty(recommendedVersion))
        {
            result.ReportLines.Add($">>> AN UPDATE IS AVAILABLE FOR Buffout 4: {recommendedVersion} <<<\n");
            result.ReportLines.Add(">>> SEE: https://www.nexusmods.com/fallout4/mods/47359 <<<\n");
        }

        result.ReportLines.Add("\n");

        return new GenericAnalysisResult
        {
            AnalyzerName = Name,
            Success = true,
            HasFindings = !isLatest && highestVersion != null,
            ReportLines = result.ReportLines
        };
    }

    /// <summary>
    ///     Parse Buffout 4 version string into a Version object
    /// </summary>
    private static Version? ParseBuffoutVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        try
        {
            // Handle formats like "Buffout 4 v1.28.6" or "Buffout 4 v1.37.0 Mar 12 2025 22:11:48"
            var parts = versionString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Find the version part (starts with 'v')
            string? versionPart = null;
            foreach (var part in parts)
                if (part.StartsWith("v", StringComparison.OrdinalIgnoreCase) && part.Length > 1)
                {
                    versionPart = part.Substring(1); // Remove the 'v'
                    break;
                }

            if (versionPart == null)
                return null;

            // Version.Parse requires at least Major.Minor
            var versionParts = versionPart.Split('.');
            if (versionParts.Length == 2)
                versionPart += ".0"; // Add revision if missing
            else if (versionParts.Length == 1) versionPart += ".0.0"; // Add minor and revision if missing

            return Version.Parse(versionPart);
        }
        catch
        {
            return null;
        }
    }
}
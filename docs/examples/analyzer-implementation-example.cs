using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Models.Yaml;
using Microsoft.Extensions.Logging;

namespace Scanner111.Core.Analyzers;

/// <summary>
/// Example implementation of RecordScanner using strongly-typed YAML models
/// </summary>
public class RecordScanner : IAnalyzer
{
    private readonly IYamlSettingsProvider _yamlSettings;
    private readonly ILogger<RecordScanner> _logger;
    
    // Cache the YAML data on initialization to avoid repeated file access
    private readonly ClassicMainYaml? _mainYaml;
    private readonly ClassicFallout4YamlV2? _fallout4Yaml;

    public RecordScanner(
        IYamlSettingsProvider yamlSettings, 
        ILogger<RecordScanner> logger,
        IApplicationSettingsService appSettings)
    {
        _yamlSettings = yamlSettings;
        _logger = logger;
        
        // Load YAML data once during initialization
        _mainYaml = _yamlSettings.LoadYaml<ClassicMainYaml>("CLASSIC Main");
        
        // Load game-specific YAML based on current game setting
        var currentGame = appSettings.GetCurrentSettings().ManagedGame;
        if (currentGame == "Fallout 4")
        {
            _fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        }
    }

    public string Name => "Record Scanner";
    public int Priority => 30;
    public bool CanRunInParallel => true;

    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask; // Make it async-ready

        var reportLines = new List<string>();
        var recordsMatches = new List<string>();

        // Use strongly-typed models instead of dictionary access
        var catchLogRecords = _mainYaml?.CatchLogRecords ?? new List<string>();
        var excludeRecords = _mainYaml?.ExcludeLogRecords ?? new List<string>();
        
        // Scan call stack for records
        foreach (var line in crashLog.CallStack)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            // Check if line contains any record patterns
            foreach (var pattern in catchLogRecords)
            {
                if (line.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    // Check if it should be excluded
                    if (!excludeRecords.Any(exclude => line.Contains(exclude, StringComparison.OrdinalIgnoreCase)))
                    {
                        recordsMatches.Add(line);
                        _logger.LogDebug("Found record match: {Pattern} in line: {Line}", pattern, line);
                    }
                }
            }
        }

        // Generate report based on findings
        if (recordsMatches.Count > 0)
        {
            reportLines.Add("## Named Records Found");
            reportLines.Add("");
            
            foreach (var match in recordsMatches.Distinct())
            {
                reportLines.Add($"- {match}");
            }
            
            // Add game-specific warnings if available
            if (_fallout4Yaml != null && crashLog.GameName == "Fallout 4")
            {
                AddGameSpecificWarnings(reportLines, recordsMatches);
            }
        }

        return new GenericAnalysisResult
        {
            AnalyzerName = Name,
            ReportLines = reportLines,
            HasFindings = recordsMatches.Count > 0,
            Data = new Dictionary<string, object>
            {
                { "RecordsMatches", recordsMatches },
                { "CatchPatterns", catchLogRecords },
                { "ExcludePatterns", excludeRecords }
            }
        };
    }

    private void AddGameSpecificWarnings(List<string> reportLines, List<string> matches)
    {
        // Example: Check for XSE-related issues
        if (matches.Any(m => m.Contains("f4se", StringComparison.OrdinalIgnoreCase)))
        {
            reportLines.Add("");
            reportLines.Add("### XSE Warning");
            
            // Use strongly-typed warning message
            var warning = _fallout4Yaml?.WarningsXse?.WarnOutdated;
            if (!string.IsNullOrEmpty(warning))
            {
                reportLines.Add(warning);
            }
        }
        
        // Check for missing Address Library
        if (matches.Any(m => m.Contains("version.bin", StringComparison.OrdinalIgnoreCase) || 
                            m.Contains("version.csv", StringComparison.OrdinalIgnoreCase)))
        {
            reportLines.Add("");
            reportLines.Add("### Address Library Warning");
            
            var warning = _fallout4Yaml?.WarningsMods?.WarnAdlibMissing;
            if (!string.IsNullOrEmpty(warning))
            {
                reportLines.Add(warning);
            }
        }
    }
}

/// <summary>
/// Example of using the YAML models in a service
/// </summary>
public class GameDetectionService
{
    private readonly IYamlSettingsProvider _yamlSettings;
    private readonly Dictionary<string, GameInfo> _gameInfoCache;

    public GameDetectionService(IYamlSettingsProvider yamlSettings)
    {
        _yamlSettings = yamlSettings;
        _gameInfoCache = new Dictionary<string, GameInfo>();
        
        // Pre-load game info for all supported games
        LoadGameInfo();
    }

    private void LoadGameInfo()
    {
        // Load Fallout 4 info
        var fallout4Yaml = _yamlSettings.LoadYaml<ClassicFallout4YamlV2>("CLASSIC Fallout4");
        if (fallout4Yaml != null)
        {
            // Note: V2 doesn't have separate VR info - it's included in Versions
            _gameInfoCache["Fallout 4"] = fallout4Yaml.GameInfo;
        }
        
        // Could load other games here
        // var skyrimYaml = _yamlSettings.LoadYaml<ClassicSkyrimYaml>("CLASSIC SkyrimSE");
    }

    public string? DetectGamePath(string gameName)
    {
        if (_gameInfoCache.TryGetValue(gameName, out var gameInfo))
        {
            // Check Steam installation
            var steamId = gameInfo.MainSteamId;
            if (steamId > 0)
            {
                var steamPath = GetSteamGamePath(steamId);
                if (!string.IsNullOrEmpty(steamPath))
                    return steamPath;
            }
            
            // Check configured path
            if (!string.IsNullOrEmpty(gameInfo.RootFolderGame))
            {
                if (Directory.Exists(gameInfo.RootFolderGame))
                    return gameInfo.RootFolderGame;
            }
        }
        
        return null;
    }

    public string? GetLatestXseVersion(string gameName)
    {
        if (_gameInfoCache.TryGetValue(gameName, out var gameInfo))
        {
            return gameInfo.XseVerLatest;
        }
        
        return null;
    }

    private string? GetSteamGamePath(int steamId)
    {
        // Implementation would check Steam registry/config
        // This is just a placeholder
        return null;
    }
}

/// <summary>
/// Example of backward compatibility wrapper
/// </summary>
public static class YamlSettingsCompatibility
{
    /// <summary>
    /// Helper method to migrate from old dictionary-based access
    /// </summary>
    public static List<string> GetCatchLogRecords(IYamlSettingsProvider yamlSettings)
    {
        // New way - strongly typed
        var mainYaml = yamlSettings.LoadYaml<ClassicMainYaml>("CLASSIC Main");
        if (mainYaml?.CatchLogRecords != null)
        {
            return mainYaml.CatchLogRecords;
        }
        
        // Return empty list if YAML data couldn't be loaded
        return new List<string>();
    }
}
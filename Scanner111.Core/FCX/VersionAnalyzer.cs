using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.FCX
{
    public class VersionAnalyzer : IAnalyzer
    {
        private readonly ILogger<VersionAnalyzer> _logger;
        private readonly IHashValidationService _hashService;
        private readonly IYamlSettingsProvider _yamlSettings;
        private readonly IApplicationSettingsService _appSettings;

        public VersionAnalyzer(
            ILogger<VersionAnalyzer> logger,
            IHashValidationService hashService,
            IYamlSettingsProvider yamlSettings,
            IApplicationSettingsService appSettings)
        {
            _logger = logger;
            _hashService = hashService;
            _yamlSettings = yamlSettings;
            _appSettings = appSettings;
        }

        public string Name => "Version Analyzer";
        public int Priority => 10; // Run early to provide version context
        public bool CanRunInParallel => true;

        private string GetLatestKnownVersion(GameType gameType)
        {
            // These should be updated as new versions are released
            return gameType == GameType.Fallout4 ? "1.10.163.0" : "1.6.1170.0";
        }


        private bool IsVersionDowngrade(string currentVersion, string latestVersion)
        {
            try
            {
                var current = Version.Parse(currentVersion);
                var latest = Version.Parse(latestVersion);
                return current < latest;
            }
            catch
            {
                // If we can't parse versions, assume no downgrade
                return false;
            }
        }
        
        private GameType DetectGameTypeFromString(string gameTypeString)
        {
            return gameTypeString?.ToLower() switch
            {
                "fallout4" => GameType.Fallout4,
                "fallout4vr" => GameType.Fallout4, // Treat VR as regular Fallout 4
                "skyrimse" => GameType.SkyrimSE,
                "skyrim" => GameType.Skyrim,
                _ => GameType.Fallout4 // Default to Fallout 4
            };
        }
        
        public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            var reportLines = new List<string>();
            var settings = await _appSettings.LoadSettingsAsync().ConfigureAwait(false);
            
            // Only run in FCX mode
            if (!settings.FcxMode)
            {
                return new GenericAnalysisResult
                {
                    AnalyzerName = Name,
                    Success = true,
                    HasFindings = false,
                    ReportLines = reportLines
                };
            }
            
            try
            {
                // Check if game path is valid
                if (string.IsNullOrEmpty(settings.DefaultGamePath))
                {
                    reportLines.Add("## Game Version Detected\n");
                    reportLines.Add("Game Version: Unknown\n");
                    reportLines.Add("⚠️ No game path configured\n");
                    reportLines.Add("\n");
                    
                    return new GenericAnalysisResult
                    {
                        AnalyzerName = Name,
                        Success = true,
                        HasFindings = true,
                        ReportLines = reportLines
                    };
                }
                
                // Detect game type from path
                var gameTypeString = CrashLogDirectoryManager.DetectGameType(settings.DefaultGamePath);
                var gameType = DetectGameTypeFromString(gameTypeString);
                
                var gameExePath = gameType == GameType.Fallout4 
                    ? Path.Combine(settings.DefaultGamePath, "Fallout4.exe")
                    : Path.Combine(settings.DefaultGamePath, "SkyrimSE.exe");
                    
                // Check if game executable exists
                if (!File.Exists(gameExePath))
                {
                    reportLines.Add("## Game Version Detected\n");
                    reportLines.Add("❌ ERROR: Game executable not found!\n");
                    reportLines.Add($"Expected location: {gameExePath}\n");
                    reportLines.Add("Please ensure the game is installed and the path is configured correctly.\n");
                    
                    return new GenericAnalysisResult
                    {
                        AnalyzerName = Name,
                        Success = true,
                        HasFindings = true, // Error is considered a finding
                        ReportLines = reportLines
                    };
                }
                    
                // Detect current game version
                var detectedVersion = await GameVersionDetection.DetectGameVersionAsync(gameExePath, cancellationToken).ConfigureAwait(false);
                
                if (detectedVersion == null)
                {
                    _logger.LogWarning("Could not detect game version");
                    // Still report version as unknown
                    reportLines.Add("## Game Version Detected\n");
                    reportLines.Add("Game Version: Unknown\n");
                    reportLines.Add("⚠️ Could not detect game version - executable may be modified or unrecognized\n");
                    reportLines.Add("\n");
                    
                    return new GenericAnalysisResult
                    {
                        AnalyzerName = Name,
                        Success = true,
                        HasFindings = true, // Report as finding since version is unknown
                        ReportLines = reportLines
                    };
                }
                
                // Add header that tests are looking for
                reportLines.Add("## Game Version Detected\n");
                
                // Handle unknown version case
                if (detectedVersion.Version == "Unknown")
                {
                    reportLines.Add("Game Version: Unknown\n");
                }
                else
                {
                    reportLines.Add($"Game Version: {detectedVersion.Name}\n");
                    reportLines.Add($"Version: {detectedVersion.Version}\n");
                }
                if (detectedVersion.ReleaseDate.HasValue)
                {
                    reportLines.Add($"Release Date: {detectedVersion.ReleaseDate.Value:yyyy-MM-dd}\n");
                }
                reportLines.Add("\n");
                
                // Check for version downgrade
                var latestVersion = GetLatestKnownVersion(gameType);
                if (IsVersionDowngrade(detectedVersion.Version, latestVersion))
                {
                    reportLines.Add("⚠️ VERSION DOWNGRADE DETECTED ⚠️\n");
                    reportLines.Add($"Current version ({detectedVersion.Version}) is older than the latest version ({latestVersion})\n");
                    reportLines.Add("Downgraded versions may have compatibility issues with newer mods\n");
                    reportLines.Add("\n");
                }
                
                // Check XSE version compatibility
                var xseAcronym = gameType == GameType.Fallout4 ? "F4SE" : "SKSE";
                var xsePath = gameType == GameType.Fallout4 
                    ? Path.Combine(settings.DefaultGamePath, "f4se_loader.exe")
                    : Path.Combine(settings.DefaultGamePath, "skse64_loader.exe");
                    
                if (!File.Exists(xsePath))
                {
                    reportLines.Add($"{xseAcronym} not found\n");
                    reportLines.Add($"{xseAcronym} is required for many mods but was not detected\n");
                    reportLines.Add($"Expected location: {xsePath}\n");
                    reportLines.Add($"Install {xseAcronym} from https://www.nexusmods.com/{(gameType == GameType.Fallout4 ? "fallout4/mods/42147" : "skyrimspecialedition/mods/30379")}\n");
                    reportLines.Add("\n");
                }
                else if (!string.IsNullOrEmpty(detectedVersion.RequiredF4seVersion))
                {
                    reportLines.Add($"{xseAcronym} detected\n");
                    reportLines.Add($"Game version {detectedVersion.Version} requires {xseAcronym} version {detectedVersion.RequiredF4seVersion} or higher\n");
                    reportLines.Add($"Please ensure you have the correct {xseAcronym} version installed\n");
                    reportLines.Add("\n");
                }
                
                // Add version-specific notes
                if (detectedVersion.Notes != null && detectedVersion.Notes.Length > 0)
                {
                    reportLines.Add("# VERSION-SPECIFIC NOTES #\n");
                    foreach (var note in detectedVersion.Notes)
                    {
                        reportLines.Add($"• {note}\n");
                    }
                    reportLines.Add("\n");
                }
                
                var hasFindings = reportLines.Count > 0;
                
                return new GenericAnalysisResult
                {
                    AnalyzerName = Name,
                    Success = true,
                    HasFindings = hasFindings,
                    ReportLines = reportLines,
                    Data = new Dictionary<string, object>
                    {
                        ["Version"] = detectedVersion.Version,
                        ["Name"] = detectedVersion.Name,
                        ["IsDowngrade"] = IsVersionDowngrade(detectedVersion.Version, latestVersion)
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during version analysis");
                return new GenericAnalysisResult
                {
                    AnalyzerName = Name,
                    Success = false,
                    HasFindings = false,
                    Errors = new[] { ex.Message },
                    ReportLines = reportLines
                };
            }
        }
    }
}
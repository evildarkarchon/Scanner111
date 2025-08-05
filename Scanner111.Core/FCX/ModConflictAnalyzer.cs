using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.FCX
{
    public class ModConflictAnalyzer : IAnalyzer
    {
        private readonly ILogger<ModConflictAnalyzer> _logger;
        private readonly IModScanner _modScanner;
        private readonly IApplicationSettingsService _appSettings;

        public ModConflictAnalyzer(
            ILogger<ModConflictAnalyzer> logger,
            IModScanner modScanner,
            IApplicationSettingsService appSettings)
        {
            _logger = logger;
            _modScanner = modScanner;
            _appSettings = appSettings;
        }

        public string Name => "Mod Conflict Analyzer";
        public int Priority => 50; // Run after other analyzers
        public bool CanRunInParallel => true;
        
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

        private (string summary, List<string> details) CreateResultForIssueType(ModIssueType type, List<ModIssue> issues, string xseAcronym)
        {
            
            switch (type)
            {
                case ModIssueType.TextureDimensionsInvalid:
                    var details = new List<string>
                    {
                        "▶️ Any mods that have texture files with incorrect dimensions",
                        "  are very likely to cause a *Texture (DDS) Crash*. For further details,",
                        "  read the *How To Read Crash Logs.pdf* included with Scanner 111.",
                        "",
                        "Affected files:"
                    }.Concat(issues.Select(i => $"  - {i.FilePath} ({i.AdditionalInfo})")).ToList();
                    return ("⚠️ DDS DIMENSIONS ARE NOT DIVISIBLE BY 2 ⚠️", details);
                    
                case ModIssueType.TextureFormatIncorrect:
                    details = new List<string>
                    {
                        "▶️ Any files with an incorrect file format will not work.",
                        "  Mod authors should convert these files to their proper game format.",
                        "  If possible, notify the original mod authors about these problems.",
                        "",
                        "Affected files:"
                    }.Concat(issues.Select(i => $"  - {i.FilePath}")).ToList();
                    return ("❓ TEXTURE FILES HAVE INCORRECT FORMAT, SHOULD BE DDS ❓", details);
                    
                case ModIssueType.SoundFormatIncorrect:
                    details = new List<string>
                    {
                        "▶️ Any files with an incorrect file format will not work.",
                        "  Mod authors should convert these files to their proper game format.",
                        "  If possible, notify the original mod authors about these problems.",
                        "",
                        "Affected files:"
                    }.Concat(issues.Select(i => $"  - {i.FilePath}")).ToList();
                    return ("❓ SOUND FILES HAVE INCORRECT FORMAT, SHOULD BE XWM OR WAV ❓", details);
                    
                case ModIssueType.XseScriptFile:
                    details = new List<string>
                    {
                        "▶️ Any mods with copies of original Script Extender files",
                        "  may cause script related problems or crashes.",
                        "",
                        "Affected mods:"
                    }.Concat(issues.Select(i => $"  - {i.FilePath}")).ToList();
                    return ($"⚠️ MODS CONTAIN COPIES OF *{xseAcronym}* SCRIPT FILES ⚠️", details);
                    
                case ModIssueType.PrevisFile:
                    details = new List<string>
                    {
                        "▶️ Any mods that contain custom precombine/previs files",
                        "  should load after the PRP.esp plugin from Previs Repair Pack (PRP).",
                        "  Otherwise, see if there is a PRP patch available for these mods.",
                        "",
                        "Affected mods:"
                    }.Concat(issues.Select(i => $"  - {i.FilePath}")).ToList();
                    return ("🔧 PREVIS FILES DETECTED 🔧", details);
                    
                case ModIssueType.AnimationData:
                    details = new List<string>
                    {
                        "▶️ Any mods that have their own custom Animation File Data",
                        "  may rarely cause an *Animation Corruption Crash*. For further details,",
                        "  read the *How To Read Crash Logs.pdf* included with Scanner 111.",
                        "",
                        "Affected mods:"
                    }.Concat(issues.Select(i => $"  - {i.FilePath}")).ToList();
                    return ("💀 BROKEN ANIMATION DATA FILES 💀", details);
                    
                case ModIssueType.ArchiveFormatIncorrect:
                    details = new List<string>
                    {
                        "▶️ Any files with an incorrect file format will not work.",
                        "  BA2 archives should be BTDX-GNRL or BTDX-DX10 format.",
                        "  If possible, notify the original mod authors about these problems.",
                        "",
                        "Affected archives:"
                    }.Concat(issues.Select(i => $"  - {i.FilePath} ({i.AdditionalInfo})")).ToList();
                    return ("❓ BA2 ARCHIVES HAVE INCORRECT FORMAT ❓", details);
                    
                case ModIssueType.CleanupFile:
                    details = new List<string>
                    {
                        "▶️ These files are unintended and can be safely deleted.",
                        "",
                        "Affected files:"
                    }.Concat(issues.Select(i => $"  - {i.FilePath}")).ToList();
                    return ("🗑️ DETECTED UNINTENDED FILES 🗑️", details);
                    
                default:
                    return (string.Empty, new List<string>());
            }
        }
        
        public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
        {
            var reportLines = new List<string>();
            var settings = await _appSettings.LoadSettingsAsync();
            
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
            
            // Get mod path
            var modPath = settings.ModsFolder;
            if (string.IsNullOrWhiteSpace(modPath))
            {
                _logger.LogWarning("Mods path not configured, skipping mod conflict analysis");
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
                // Scan all mods
                var scanResult = await _modScanner.ScanAllModsAsync(modPath, null, cancellationToken).ConfigureAwait(false);
                
                if (scanResult.Issues.Count == 0)
                {
                    return new GenericAnalysisResult
                    {
                        AnalyzerName = Name,
                        Success = true,
                        HasFindings = false,
                        ReportLines = reportLines
                    };
                }
                
                // Group issues by type
                var issueGroups = scanResult.Issues.GroupBy(i => i.Type).OrderBy(g => g.Key);
                
                // Detect game type from path
                var gameTypeString = CrashLogDirectoryManager.DetectGameType(settings.DefaultGamePath);
                var gameType = DetectGameTypeFromString(gameTypeString);
                var xseAcronym = gameType == GameType.Fallout4 ? "F4SE" : "SKSE";
                
                foreach (var group in issueGroups)
                {
                    var (summary, details) = CreateResultForIssueType(group.Key, group.ToList(), xseAcronym);
                    if (!string.IsNullOrEmpty(summary))
                    {
                        reportLines.Add(summary + "\n");
                        reportLines.AddRange(details.Select(d => d + "\n"));
                        reportLines.Add("\n");
                    }
                }
                
                return new GenericAnalysisResult
                {
                    AnalyzerName = Name,
                    Success = true,
                    HasFindings = true,
                    ReportLines = reportLines,
                    Data = new Dictionary<string, object>
                    {
                        ["TotalIssues"] = scanResult.Issues.Count,
                        ["FilesScanned"] = scanResult.TotalFilesScanned,
                        ["ArchivesScanned"] = scanResult.TotalArchivesScanned,
                        ["ScanDuration"] = scanResult.ScanDuration.TotalSeconds,
                        ["CleanedFiles"] = scanResult.CleanedFiles.Count
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during mod conflict analysis");
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
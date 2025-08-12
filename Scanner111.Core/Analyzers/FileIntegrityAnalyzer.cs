using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Core.Analyzers;

/// <summary>
///     Analyzes file integrity and validates game installation for FCX mode
/// </summary>
public class FileIntegrityAnalyzer : IAnalyzer
{
    private readonly IHashValidationService _hashValidationService;
    private readonly IApplicationSettingsService _settingsService;
    private readonly IYamlSettingsProvider _yamlSettings;
    private readonly IMessageHandler _messageHandler;

    public FileIntegrityAnalyzer(
        IHashValidationService hashValidationService,
        IApplicationSettingsService settingsService,
        IYamlSettingsProvider yamlSettings,
        IMessageHandler messageHandler)
    {
        _hashValidationService = hashValidationService;
        _settingsService = settingsService;
        _yamlSettings = yamlSettings;
        _messageHandler = messageHandler;
    }

    /// <summary>
    ///     Name of the analyzer
    /// </summary>
    public string Name => "FCX File Integrity";

    /// <summary>
    ///     Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 10;  // Run after basic analyzers

    /// <summary>
    ///     Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    ///     Analyze file integrity for the game installation
    /// </summary>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        // Only run if FCX mode is enabled
        var settings = await _settingsService.LoadSettingsAsync().ConfigureAwait(false);
        if (!settings.FcxMode)
        {
            return new FcxScanResult
            {
                AnalyzerName = Name,
                Success = true,
                HasFindings = false,
                ReportLines = new List<string> { "FCX mode is disabled.\n" }
            };
        }

        var result = new FcxScanResult
        {
            AnalyzerName = Name,
            Success = true
        };

        var reportLines = new List<string>();
        reportLines.Add("=== FCX FILE INTEGRITY CHECK ===\n\n");

        try
        {
            // Get game configuration
            var gameConfig = GetGameConfiguration(crashLog);
            if (gameConfig == null || !gameConfig.IsValid)
            {
                result.GameStatus = GameIntegrityStatus.Invalid;
                reportLines.Add("❌ Could not find valid game installation.\n");
                result = new FcxScanResult
                {
                    AnalyzerName = Name,
                    Success = true,
                    GameStatus = GameIntegrityStatus.Invalid,
                    ReportLines = reportLines,
                    HasFindings = true
                };
                return result;
            }

            result.GameConfig = gameConfig;

            // Check game executable
            await CheckGameExecutable(gameConfig, result, reportLines, cancellationToken).ConfigureAwait(false);

            // Check F4SE installation
            await CheckF4SE(gameConfig, result, reportLines, cancellationToken).ConfigureAwait(false);

            // Check core mod files
            await CheckCoreMods(gameConfig, result, reportLines, cancellationToken).ConfigureAwait(false);

            // Determine overall status
            DetermineOverallStatus(result);

            // Add recommendations if issues found
            if (result.GameStatus != GameIntegrityStatus.Good)
            {
                AddRecommendations(result, reportLines);
            }

            reportLines.Add("\n");

            // Create new result with all the data
            result = new FcxScanResult
            {
                AnalyzerName = Name,
                Success = true,
                GameStatus = result.GameStatus,
                GameConfig = result.GameConfig,
                FileChecks = result.FileChecks,
                HashValidations = result.HashValidations,
                VersionWarnings = result.VersionWarnings,
                RecommendedFixes = result.RecommendedFixes,
                ReportLines = reportLines,
                HasFindings = result.GameStatus != GameIntegrityStatus.Good
            };
        }
        catch (Exception ex)
        {
            reportLines.Add($"❌ FCX analysis error: {ex.Message}\n");
            result = new FcxScanResult
            {
                AnalyzerName = Name,
                Success = false,
                Errors = new[] { $"FCX analysis failed: {ex.Message}" },
                ReportLines = reportLines,
                HasFindings = true
            };
        }

        return result;
    }

    private GameConfiguration? GetGameConfiguration(CrashLog crashLog)
    {
        // Use the crash log's game paths to build configuration
        var config = new GameConfiguration
        {
            GameName = "Fallout 4",  // Currently only supporting Fallout 4
            RootPath = crashLog.GamePath ?? string.Empty
        };

        if (string.IsNullOrEmpty(config.RootPath))
        {
            // Try to detect from settings or default paths
            var detectedPath = GamePathDetection.TryDetectGamePath();
            if (!string.IsNullOrEmpty(detectedPath))
            {
                config.RootPath = detectedPath;
            }
        }

        if (!string.IsNullOrEmpty(config.RootPath) && Directory.Exists(config.RootPath))
        {
            config.ExecutablePath = Path.Combine(config.RootPath, "Fallout4.exe");
            config.XsePath = Path.Combine(config.RootPath, "f4se_loader.exe");

            // Detect platform
            if (config.RootPath.Contains("steamapps", StringComparison.OrdinalIgnoreCase))
            {
                config.Platform = "Steam";
                config.SteamAppId = "377160";
            }
            else if (config.RootPath.Contains("GOG", StringComparison.OrdinalIgnoreCase))
            {
                config.Platform = "GOG";
            }
            else
            {
                config.Platform = "Unknown";
            }
        }

        return config;
    }

    private async Task CheckGameExecutable(GameConfiguration gameConfig, FcxScanResult result,
        List<string> reportLines, CancellationToken cancellationToken)
    {
        reportLines.Add("📁 Game Executable Check:\n");

        var exeCheck = new FileIntegrityCheck
        {
            FilePath = gameConfig.ExecutablePath,
            FileType = "Executable",
            Exists = File.Exists(gameConfig.ExecutablePath)
        };

        if (!exeCheck.Exists)
        {
            exeCheck.IsValid = false;
            exeCheck.ErrorMessage = "Game executable not found";
            reportLines.Add($"   ❌ Fallout4.exe not found at: {gameConfig.ExecutablePath}\n");
        }
        else
        {
            try
            {
                var fileInfo = new FileInfo(gameConfig.ExecutablePath);
                exeCheck.FileSize = fileInfo.Length;
                exeCheck.LastModified = fileInfo.LastWriteTime;

                // Calculate hash to determine version
                var hash = await _hashValidationService.CalculateFileHashAsync(
                    gameConfig.ExecutablePath, cancellationToken).ConfigureAwait(false);

                // Check against known hashes
                var knownVersions = GetKnownGameVersions();
                var matchedVersion = knownVersions.FirstOrDefault(kv =>
                    string.Equals(kv.Value, hash, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(matchedVersion.Key))
                {
                    gameConfig.Version = matchedVersion.Key;
                    exeCheck.IsValid = true;
                    reportLines.Add($"   ✅ Fallout4.exe found - Version: {matchedVersion.Key} ({gameConfig.Platform})\n");

                    // Add version-specific warnings
                    if (matchedVersion.Key.Contains("1.10.163"))
                    {
                        result.VersionWarnings.Add("Pre-Next Gen Update version detected. Most mods compatible.");
                    }
                    else if (matchedVersion.Key.Contains("1.10.984"))
                    {
                        result.VersionWarnings.Add("Next Gen Update version detected. Some mods may need updates.");
                    }
                }
                else
                {
                    exeCheck.IsValid = true;  // File exists but version unknown
                    reportLines.Add($"   ⚠️  Fallout4.exe found but version unknown (Hash: {hash.Substring(0, 8)}...)\n");
                    result.VersionWarnings.Add("Unknown game version detected. Mod compatibility uncertain.");
                }

                result.HashValidations.Add(new HashValidation
                {
                    FilePath = gameConfig.ExecutablePath,
                    ActualHash = hash,
                    ExpectedHash = matchedVersion.Value ?? "",
                    Version = gameConfig.Version
                });
            }
            catch (Exception ex)
            {
                exeCheck.IsValid = false;
                exeCheck.ErrorMessage = $"Failed to validate: {ex.Message}";
                reportLines.Add($"   ❌ Failed to validate Fallout4.exe: {ex.Message}\n");
            }
        }

        result.FileChecks.Add(exeCheck);
    }

    private async Task CheckF4SE(GameConfiguration gameConfig, FcxScanResult result,
        List<string> reportLines, CancellationToken cancellationToken)
    {
        reportLines.Add("\n📁 F4SE (Script Extender) Check:\n");

        var f4seCheck = new FileIntegrityCheck
        {
            FilePath = gameConfig.XsePath,
            FileType = "F4SE Loader",
            Exists = File.Exists(gameConfig.XsePath)
        };

        if (!f4seCheck.Exists)
        {
            f4seCheck.IsValid = false;
            f4seCheck.ErrorMessage = "F4SE not installed";
            reportLines.Add("   ⚠️  F4SE not found - Script extender required for many mods\n");
            result.RecommendedFixes.Add("Install F4SE from https://f4se.silverlock.org/");
        }
        else
        {
            try
            {
                // Use async file info retrieval to justify async method
                var fileInfo = await Task.Run(() => new FileInfo(gameConfig.XsePath), cancellationToken).ConfigureAwait(false);
                f4seCheck.FileSize = fileInfo.Length;
                f4seCheck.LastModified = fileInfo.LastWriteTime;
                f4seCheck.IsValid = true;

                // Check F4SE DLL for version compatibility
                var f4seDllPath = Path.Combine(gameConfig.RootPath, "f4se_1_10_163.dll");
                var f4seDllPathNewGen = Path.Combine(gameConfig.RootPath, "f4se_1_10_984.dll");

                // Use async file existence checks
                var hasPreNgDll = await Task.Run(() => File.Exists(f4seDllPath), cancellationToken).ConfigureAwait(false);
                var hasNextGenDll = await Task.Run(() => File.Exists(f4seDllPathNewGen), cancellationToken).ConfigureAwait(false);

                if (hasPreNgDll)
                {
                    gameConfig.XseVersion = "0.6.23 (Pre-NG)";
                    reportLines.Add($"   ✅ F4SE found - Version: {gameConfig.XseVersion}\n");

                    if (!gameConfig.Version.Contains("1.10.163"))
                    {
                        reportLines.Add("   ⚠️  F4SE version may not match game version!\n");
                        result.VersionWarnings.Add("F4SE version mismatch detected");
                    }
                }
                else if (hasNextGenDll)
                {
                    gameConfig.XseVersion = "0.7.2+ (Next Gen)";
                    reportLines.Add($"   ✅ F4SE found - Version: {gameConfig.XseVersion}\n");

                    if (!gameConfig.Version.Contains("1.10.984"))
                    {
                        reportLines.Add("   ⚠️  F4SE version may not match game version!\n");
                        result.VersionWarnings.Add("F4SE version mismatch detected");
                    }
                }
                else
                {
                    reportLines.Add("   ⚠️  F4SE loader found but version unknown\n");
                }
            }
            catch (Exception ex)
            {
                f4seCheck.IsValid = false;
                f4seCheck.ErrorMessage = $"Failed to validate: {ex.Message}";
                reportLines.Add($"   ❌ Failed to validate F4SE: {ex.Message}\n");
            }
        }

        result.FileChecks.Add(f4seCheck);
    }

    private async Task CheckCoreMods(GameConfiguration gameConfig, FcxScanResult result,
        List<string> reportLines, CancellationToken cancellationToken)
    {
        reportLines.Add("\n📁 Core Mod Files Check:\n");

        // Define core mods to check
        var coreMods = new Dictionary<string, string>
        {
            ["Buffout4.dll"] = "Crash logger",
            ["f4se\\plugins\\Buffout4.dll"] = "Crash logger (F4SE plugin)",
            ["f4se\\plugins\\AddressLibrary.dll"] = "Address Library",
            ["f4se\\plugins\\ConsoleUtilF4.dll"] = "Console Utils"
        };

        foreach (var (relativePath, description) in coreMods)
        {
            var fullPath = Path.Combine(gameConfig.RootPath, relativePath);

            // Use async file existence check
            var exists = await Task.Run(() => File.Exists(fullPath), cancellationToken).ConfigureAwait(false);

            var modCheck = new FileIntegrityCheck
            {
                FilePath = fullPath,
                FileType = "Core Mod",
                Exists = exists
            };

            if (!modCheck.Exists)
            {
                modCheck.IsValid = false;
                modCheck.ErrorMessage = $"{description} not found";
                reportLines.Add($"   ⚠️  {description} not found: {relativePath}\n");

                if (relativePath.Contains("Buffout4"))
                {
                    result.RecommendedFixes.Add("Install Buffout 4 for better crash logging");
                }
            }
            else
            {
                try
                {
                    // Use async file info retrieval
                    var fileInfo = await Task.Run(() => new FileInfo(fullPath), cancellationToken).ConfigureAwait(false);
                    modCheck.FileSize = fileInfo.Length;
                    modCheck.LastModified = fileInfo.LastWriteTime;
                    modCheck.IsValid = true;
                    reportLines.Add($"   ✅ {description} found: {relativePath}\n");
                }
                catch (Exception ex)
                {
                    modCheck.IsValid = false;
                    modCheck.ErrorMessage = $"Failed to validate: {ex.Message}";
                    reportLines.Add($"   ❌ Failed to check {description}: {ex.Message}\n");
                }
            }

            result.FileChecks.Add(modCheck);
        }
    }

    private void DetermineOverallStatus(FcxScanResult result)
    {
        var criticalIssues = result.FileChecks.Count(fc => !fc.IsValid && fc.FileType == "Executable");
        var warnings = result.FileChecks.Count(fc => !fc.IsValid && fc.FileType != "Executable");

        if (criticalIssues > 0)
        {
            result.GameStatus = GameIntegrityStatus.Critical;
        }
        else if (warnings > 0 || result.VersionWarnings.Count > 0)
        {
            result.GameStatus = GameIntegrityStatus.Warning;
        }
        else
        {
            result.GameStatus = GameIntegrityStatus.Good;
        }
    }

    private void AddRecommendations(FcxScanResult result, List<string> reportLines)
    {
        if (result.RecommendedFixes.Count > 0)
        {
            reportLines.Add("\n💡 Recommendations:\n");
            foreach (var fix in result.RecommendedFixes)
            {
                reportLines.Add($"   • {fix}\n");
            }
        }

        if (result.VersionWarnings.Count > 0)
        {
            reportLines.Add("\n⚠️  Version Notes:\n");
            foreach (var warning in result.VersionWarnings)
            {
                reportLines.Add($"   • {warning}\n");
            }
        }
    }

    private Dictionary<string, string> GetKnownGameVersions()
    {
        // TODO: Load these from YAML configuration
        // For now, return known hashes for Fallout 4 versions
        return new Dictionary<string, string>
        {
            ["1.10.163.0"] = "7B0E5D0B7C5B4E8F9C2A3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4", // Pre-NG Update
            ["1.10.984.0"] = "8C1F2E3D4C5B6A7F8E9D0C1B2A3F4E5D6C7B8A9F0E1D2C3B4A5F6E7D8C9B0A1"  // Next Gen Update
            // Note: These are placeholder hashes - real hashes need to be collected from community
        };
    }
}
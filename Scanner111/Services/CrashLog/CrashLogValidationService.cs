using System;
using Scanner111.Models.CrashLog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Scanner111.Services.Configuration;

namespace Scanner111.Services.CrashLog;

/// <summary>
/// Service for validating crash log files and extracting header information
/// </summary>
public partial class CrashLogValidationService : ICrashLogValidationService
{
    private readonly ILogger<CrashLogValidationService> _logger;
    private readonly IConfigurationService _config;
    private readonly List<SupportedCombination> _supportedCombinations;

    // Regex patterns for parsing crash log headers
    private static readonly Regex GameLinePattern = InitializeGameLineRegex();

    private static readonly Regex CrashGenLinePattern = InitializeCrashGenLineRegex();

    public CrashLogValidationService(
        ILogger<CrashLogValidationService> logger,
        IConfigurationService config)
    {
        _logger = logger;
        _config = config;
        _supportedCombinations = LoadSupportedCombinations();
    }

    /// <inheritdoc />
    public async Task<bool> IsValidCrashLogAsync(string filePath)
    {
        var info = await GetCrashLogInfoAsync(filePath);
        return info != null;
    }

    /// <inheritdoc />
    public async Task<CrashLogInfo?> GetCrashLogInfoAsync(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            _logger.LogDebug("File does not exist: {FilePath}", filePath);
            return null;
        }

        try
        {
            // Read first few lines of the file
            var lines = await ReadFileHeaderAsync(filePath);
            if (lines.Count < 2)
            {
                _logger.LogDebug("File has insufficient header lines: {FilePath}", filePath);
                return null;
            }

            // Parse the header lines
            var crashLogInfo = ParseCrashLogHeader(filePath, lines[0], lines[1]);
            if (crashLogInfo == null)
            {
                _logger.LogDebug("Could not parse crash log header: {FilePath}", filePath);
                return null;
            }

            // Validate against supported combinations
            if (!IsSupportedCombination(crashLogInfo))
            {
                _logger.LogDebug("Unsupported combination {Game}|{CrashGen}: {FilePath}",
                    crashLogInfo.GameName, crashLogInfo.CrashGenerator, filePath);
                return null;
            }

            _logger.LogDebug("Valid crash log detected: {FilePath} ({Game}|{CrashGen})",
                filePath, crashLogInfo.GameName, crashLogInfo.CrashGenerator);

            return crashLogInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating crash log: {FilePath}", filePath);
            return null;
        }
    }

    /// <inheritdoc />
    public IEnumerable<SupportedCombination> GetSupportedCombinations()
    {
        return _supportedCombinations.Where(c => c.IsEnabled);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<CrashLogInfo>> GetValidCrashLogsAsync(IEnumerable<string> filePaths)
    {
        var validLogs = new List<CrashLogInfo>();
        var tasks = filePaths.Select(async filePath =>
        {
            var info = await GetCrashLogInfoAsync(filePath);
            return info;
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(info => info != null)!;
    }

    /// <summary>
    /// Reads the first few lines of a file for header parsing
    /// </summary>
    private static async Task<List<string>> ReadFileHeaderAsync(string filePath, int maxLines = 5)
    {
        var lines = new List<string>();

        using var reader = new StreamReader(filePath);
        for (var i = 0; i < maxLines && !reader.EndOfStream; i++)
        {
            var line = await reader.ReadLineAsync();
            if (line != null) lines.Add(line.Trim());
        }

        return lines;
    }

    /// <summary>
    /// Parses crash log header information from the first two lines
    /// </summary>
    private CrashLogInfo? ParseCrashLogHeader(string filePath, string gameLine, string crashGenLine)
    {
        // Parse game line (e.g., "Fallout 4 v1.2.72")
        var gameMatch = GameLinePattern.Match(gameLine);
        if (!gameMatch.Success)
        {
            _logger.LogDebug("Could not parse game line: {GameLine}", gameLine);
            return null;
        }

        var gameName = gameMatch.Groups[1].Value.Trim();
        var gameVersion = gameMatch.Groups[2].Value.Trim();

        // Parse crash generator line (e.g., "Buffout 4 v1.31.1 Feb 28 2023 00:32:02")
        var crashGenMatch = CrashGenLinePattern.Match(crashGenLine);
        if (!crashGenMatch.Success)
        {
            _logger.LogDebug("Could not parse crash generator line: {CrashGenLine}", crashGenLine);
            return null;
        }

        var crashGenerator = crashGenMatch.Groups[1].Value.Trim();
        var crashGenVersion = crashGenMatch.Groups[2].Value.Trim();

        // Detect VR version
        var isVrVersion = gameName.Contains("VR", StringComparison.OrdinalIgnoreCase);

        return new CrashLogInfo
        {
            FilePath = filePath,
            GameName = gameName,
            GameVersion = gameVersion,
            CrashGenerator = crashGenerator,
            CrashGeneratorVersion = crashGenVersion,
            GameLine = gameLine,
            CrashGeneratorLine = crashGenLine,
            IsVrVersion = isVrVersion
        };
    }

    /// <summary>
    /// Checks if a crash log combination is supported
    /// </summary>
    private bool IsSupportedCombination(CrashLogInfo crashLogInfo)
    {
        return _supportedCombinations.Any(combo =>
            combo.IsEnabled &&
            string.Equals(combo.GameName, crashLogInfo.GameName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(combo.CrashGenerator, crashLogInfo.CrashGenerator, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Loads supported combinations from configuration
    /// </summary>
    private List<SupportedCombination> LoadSupportedCombinations()
    {
        try
        {
            // Try to load from configuration first
            var combinations = _config.GetValue<List<Dictionary<string, object>>>(
                YamlStore.Main, "CLASSIC_Info.supported_crash_log_combinations");

            if (combinations?.Any() == true)
                return combinations.Select(dict => new SupportedCombination
                {
                    GameName = dict.GetValueOrDefault("game_name", "").ToString() ?? "",
                    CrashGenerator = dict.GetValueOrDefault("crash_generator", "").ToString() ?? "",
                    SupportsVr = bool.Parse(dict.GetValueOrDefault("supports_vr", false).ToString() ?? "false"),
                    Description = dict.GetValueOrDefault("description", "").ToString() ?? "",
                    IsEnabled = bool.Parse(dict.GetValueOrDefault("is_enabled", true).ToString() ?? "true")
                }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load crash log combinations from configuration, using defaults");
        }

        // Return default combinations if configuration loading fails
        return GetDefaultSupportedCombinations();
    }

    /// <summary>
    /// Gets the default supported crash log combinations
    /// </summary>
    private static List<SupportedCombination> GetDefaultSupportedCombinations()
    {
        return new List<SupportedCombination>
        {
            new()
            {
                GameName = "Fallout 4",
                CrashGenerator = "Buffout 4",
                SupportsVr = true,
                Description = "Fallout 4 with Buffout 4 crash logger",
                IsEnabled = true
            },
            new()
            {
                GameName = "Fallout4VR",
                CrashGenerator = "Buffout 4",
                SupportsVr = true,
                Description = "Fallout 4 VR with Buffout 4 crash logger",
                IsEnabled = true
            },
            new()
            {
                GameName = "The Elder Scrolls V: Skyrim Special Edition",
                CrashGenerator = "Crash Logger SSE",
                SupportsVr = false,
                Description = "Skyrim Special Edition with Crash Logger SSE",
                IsEnabled = false // Not implemented yet
            },
            new()
            {
                GameName = "SkyrimVR",
                CrashGenerator = "Crash Logger VR",
                SupportsVr = true,
                Description = "Skyrim VR with Crash Logger VR",
                IsEnabled = false // Not implemented yet
            }
        };
    }

    [GeneratedRegex(@"^(.+?)\s+v?(\d+\.\d+\.\d+.*?)$", RegexOptions.Compiled)]
    private static partial Regex InitializeGameLineRegex();

    [GeneratedRegex(@"^(.+?)\s+v?(\d+\.\d+\.\d+.*?)(?:\s+.+)?$", RegexOptions.Compiled)]
    private static partial Regex InitializeCrashGenLineRegex();
}
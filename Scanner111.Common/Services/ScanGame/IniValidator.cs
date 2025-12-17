using System.Collections.Concurrent;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Scans and validates INI configuration files in a game installation directory.
/// </summary>
/// <remarks>
/// <para>
/// This service scans for INI and CONF files, caches their contents, detects duplicates,
/// and identifies known problematic settings. It operates in read-only mode and never
/// modifies configuration files.
/// </para>
/// <para>
/// The scanner checks for:
/// <list type="bullet">
/// <item>Console command settings that may slow startup</item>
/// <item>VSync settings across various mod configuration files</item>
/// <item>Known mod-specific problematic settings</item>
/// <item>Duplicate configuration files</item>
/// </list>
/// </para>
/// </remarks>
public sealed class IniValidator : IIniValidator
{
    private readonly IniFileCache _cache;

    /// <summary>
    /// VSync settings to check across various configuration files.
    /// Format: (filename, section, setting name).
    /// </summary>
    private static readonly (string FileName, string Section, string Setting)[] VSyncSettings =
    [
        ("dxvk.conf", "{GameName}.exe", "dxgi.syncInterval"),
        ("enblocal.ini", "ENGINE", "ForceVSync"),
        ("longloadingtimesfix.ini", "Limiter", "EnableVSync"),
        ("reshade.ini", "APP", "ForceVsync"),
        ("fallout4_test.ini", "CreationKit", "VSyncRender"),
        ("highfpsphysicsfix.ini", "Main", "EnableVSync"),
    ];

    /// <summary>
    /// Known configuration issues to detect.
    /// </summary>
    private static readonly KnownIssueDefinition[] KnownIssues =
    [
        new(
            "espexplorer.ini",
            "General",
            "HotKey",
            value => value.Contains("; F10", StringComparison.OrdinalIgnoreCase),
            "0x79",
            "Hotkey is commented out and won't work. Change to hex code 0x79 for F10.",
            ConfigIssueSeverity.Warning),
        new(
            "epo.ini",
            "Particles",
            "iMaxDesired",
            value => int.TryParse(value, out var i) && i > 5000,
            "5000",
            "High particle count can cause performance issues and crashes.",
            ConfigIssueSeverity.Warning),
        new(
            "f4ee.ini",
            "CharGen",
            "bUnlockHeadParts",
            value => value == "0",
            "1",
            "Head parts are locked. Set to 1 to unlock all head parts.",
            ConfigIssueSeverity.Info),
        new(
            "f4ee.ini",
            "CharGen",
            "bUnlockTints",
            value => value == "0",
            "1",
            "Face tints are locked. Set to 1 to unlock all face tints.",
            ConfigIssueSeverity.Info),
        new(
            "highfpsphysicsfix.ini",
            "Limiter",
            "LoadingScreenFPS",
            value => float.TryParse(value, out var f) && f < 600.0f,
            "600.0",
            "Loading screen FPS is too low. Increase to 600.0 to prevent physics issues during loading.",
            ConfigIssueSeverity.Warning),
    ];

    /// <summary>
    /// Directories to whitelist for duplicate detection.
    /// </summary>
    private static readonly HashSet<string> DuplicateWhitelistDirs = new(StringComparer.OrdinalIgnoreCase)
    {
        "F4EE"
    };

    /// <summary>
    /// File name fragments to whitelist for duplicate detection.
    /// </summary>
    private static readonly string[] DuplicateWhitelistFragments = ["F4EE"];

    /// <summary>
    /// Console command setting name to check for startup slowdown.
    /// </summary>
    private const string ConsoleCommandSetting = "sStartingConsoleCommand";
    private const string ConsoleCommandSection = "General";

    /// <summary>
    /// Initializes a new instance of the <see cref="IniValidator"/> class.
    /// </summary>
    public IniValidator()
    {
        _cache = new IniFileCache();
    }

    /// <inheritdoc/>
    public async Task<IniScanResult> ScanAsync(
        string gameRootPath,
        string gameName,
        CancellationToken cancellationToken = default)
    {
        return await ScanWithProgressAsync(gameRootPath, gameName, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<IniScanResult> ScanWithProgressAsync(
        string gameRootPath,
        string gameName,
        IProgress<IniScanProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(gameRootPath))
        {
            return new IniScanResult();
        }

        // Clear cache for fresh scan
        _cache.Clear();

        // Find all INI/CONF files
        var configFiles = await FindConfigFilesAsync(gameRootPath, cancellationToken).ConfigureAwait(false);

        // Load all files into cache
        var filesScanned = 0;
        var issuesFound = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(configFiles, options, async (filePath, ct) =>
        {
            await _cache.LoadAsync(filePath, ct).ConfigureAwait(false);
            var scanned = Interlocked.Increment(ref filesScanned);
            progress?.Report(new IniScanProgress(filePath, scanned, issuesFound));
        }).ConfigureAwait(false);

        // Collect issues
        var configIssues = new ConcurrentBag<ConfigIssue>();
        var consoleCommandIssues = new ConcurrentBag<ConsoleCommandIssue>();
        var vsyncIssues = new ConcurrentBag<VSyncIssue>();

        // Check known issues in parallel
        await Parallel.ForEachAsync(_cache.CachedFiles, options, async (file, ct) =>
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();

            CheckKnownIssues(file.FileNameLower, file.FilePath, configIssues);
            CheckConsoleCommand(file.FileNameLower, file.FilePath, gameName, consoleCommandIssues);
            CheckVSyncSettings(file.FileNameLower, file.FilePath, gameName, vsyncIssues);
        }).ConfigureAwait(false);

        // Build duplicate file issues
        var duplicateIssues = BuildDuplicateIssues();

        // Update issue count
        issuesFound = configIssues.Count + consoleCommandIssues.Count +
                      vsyncIssues.Count + duplicateIssues.Count;
        progress?.Report(new IniScanProgress("Scan complete", filesScanned, issuesFound));

        return new IniScanResult
        {
            TotalFilesScanned = filesScanned,
            ConfigIssues = configIssues.ToList(),
            ConsoleCommandIssues = consoleCommandIssues.ToList(),
            VSyncIssues = vsyncIssues.ToList(),
            DuplicateFileIssues = duplicateIssues
        };
    }

    /// <inheritdoc/>
    public T? GetValue<T>(string fileNameLower, string section, string setting) where T : struct
    {
        return _cache.GetValue<T>(fileNameLower, section, setting);
    }

    /// <inheritdoc/>
    public string? GetStringValue(string fileNameLower, string section, string setting)
    {
        return _cache.GetStringValue(fileNameLower, section, setting);
    }

    /// <inheritdoc/>
    public bool HasSetting(string fileNameLower, string section, string setting)
    {
        return _cache.HasSetting(fileNameLower, section, setting);
    }

    /// <inheritdoc/>
    public void ClearCache()
    {
        _cache.Clear();
    }

    private static async Task<List<string>> FindConfigFilesAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        var result = new List<string>();

        await Task.Run(() =>
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                MatchCasing = MatchCasing.CaseInsensitive
            };

            try
            {
                foreach (var file in Directory.EnumerateFiles(rootPath, "*.*", options))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileName = Path.GetFileName(file);
                    var fileNameLower = fileName.ToLowerInvariant();

                    // Check if file matches our criteria
                    if (ShouldIncludeFile(file, fileNameLower))
                    {
                        result.Add(file);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Partial results are fine
            }
            catch (DirectoryNotFoundException)
            {
                // Directory was deleted during enumeration
            }
        }, cancellationToken).ConfigureAwait(false);

        return result;
    }

    private static bool ShouldIncludeFile(string filePath, string fileNameLower)
    {
        // Include .ini and .conf files
        if (fileNameLower.EndsWith(".ini", StringComparison.Ordinal) ||
            fileNameLower.EndsWith(".conf", StringComparison.Ordinal))
        {
            // Check if in whitelisted directory or contains whitelisted fragment
            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
            var dirParts = directory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Check directory whitelist
            if (dirParts.Any(d => DuplicateWhitelistDirs.Contains(d)))
            {
                return true;
            }

            // Check filename fragments
            if (DuplicateWhitelistFragments.Any(f =>
                    fileNameLower.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            // Include all INI files at root level or in standard locations
            return true;
        }

        // Special case: dxvk.conf
        if (fileNameLower == "dxvk.conf")
        {
            return true;
        }

        return false;
    }

    private void CheckKnownIssues(
        string fileNameLower,
        string filePath,
        ConcurrentBag<ConfigIssue> issues)
    {
        foreach (var issue in KnownIssues)
        {
            if (!string.Equals(fileNameLower, issue.FileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var currentValue = _cache.GetStringValue(fileNameLower, issue.Section, issue.Setting);
            if (currentValue == null)
            {
                continue;
            }

            if (issue.IsProblematic(currentValue))
            {
                issues.Add(new ConfigIssue(
                    filePath,
                    Path.GetFileName(filePath),
                    issue.Section,
                    issue.Setting,
                    currentValue,
                    issue.RecommendedValue,
                    issue.Description,
                    issue.Severity));
            }
        }
    }

    private void CheckConsoleCommand(
        string fileNameLower,
        string filePath,
        string gameName,
        ConcurrentBag<ConsoleCommandIssue> issues)
    {
        // Only check game INI files
        var gameNameLower = gameName.ToLowerInvariant();
        if (!fileNameLower.StartsWith(gameNameLower, StringComparison.Ordinal))
        {
            return;
        }

        if (!_cache.HasSetting(fileNameLower, ConsoleCommandSection, ConsoleCommandSetting))
        {
            return;
        }

        var value = _cache.GetStringValue(fileNameLower, ConsoleCommandSection, ConsoleCommandSetting);
        if (!string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new ConsoleCommandIssue(filePath, Path.GetFileName(filePath), value));
        }
    }

    private void CheckVSyncSettings(
        string fileNameLower,
        string filePath,
        string gameName,
        ConcurrentBag<VSyncIssue> issues)
    {
        foreach (var (fileName, section, setting) in VSyncSettings)
        {
            if (!string.Equals(fileNameLower, fileName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Handle dynamic section name with game name placeholder
            var actualSection = section.Replace("{GameName}", gameName, StringComparison.OrdinalIgnoreCase);

            var value = _cache.GetValue<bool>(fileNameLower, actualSection, setting);
            if (value.HasValue && value.Value)
            {
                issues.Add(new VSyncIssue(
                    filePath,
                    Path.GetFileName(filePath),
                    actualSection,
                    setting,
                    IsEnabled: true));
            }
        }
    }

    private List<DuplicateFileIssue> BuildDuplicateIssues()
    {
        var issues = new List<DuplicateFileIssue>();

        foreach (var (fileNameLower, duplicatePaths) in _cache.DuplicateFiles)
        {
            var originalPath = _cache.GetFilePath(fileNameLower);
            if (originalPath == null)
            {
                continue;
            }

            foreach (var duplicatePath in duplicatePaths)
            {
                issues.Add(new DuplicateFileIssue(
                    originalPath,
                    duplicatePath,
                    Path.GetFileName(originalPath),
                    DuplicateSimilarityType.ExactMatch));
            }
        }

        return issues;
    }

    /// <summary>
    /// Definition for a known configuration issue.
    /// </summary>
    private sealed record KnownIssueDefinition(
        string FileName,
        string Section,
        string Setting,
        Func<string, bool> IsProblematic,
        string RecommendedValue,
        string Description,
        ConfigIssueSeverity Severity = ConfigIssueSeverity.Warning);
}

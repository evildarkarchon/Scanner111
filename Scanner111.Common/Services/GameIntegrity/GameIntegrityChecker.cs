using System.Security.Cryptography;
using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Models.ScanGame;

namespace Scanner111.Common.Services.GameIntegrity;

/// <summary>
/// Checks game installation integrity by validating executable version and installation location.
/// </summary>
/// <remarks>
/// <para>
/// This service validates:
/// <list type="bullet">
/// <item>Executable SHA-256 hash against known good versions</item>
/// <item>Installation location (warns about Program Files)</item>
/// <item>Steam INI presence (outdated installation detection)</item>
/// </list>
/// </para>
/// <para>
/// The checker operates in read-only mode and never modifies any files.
/// </para>
/// </remarks>
public sealed class GameIntegrityChecker : IGameIntegrityChecker
{
    private static readonly string[] RestrictedPaths = ["Program Files", "Program Files (x86)"];

    /// <inheritdoc/>
    public async Task<GameIntegrityResult> CheckIntegrityAsync(
        GameIntegrityConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        var issues = new List<GameIntegrityIssue>();

        // Build executable path
        string? executablePath = null;
        if (!string.IsNullOrEmpty(configuration.GameRootPath))
        {
            var exeName = configuration.GameType.GetExecutableName();
            if (!string.IsNullOrEmpty(exeName))
            {
                executablePath = Path.Combine(configuration.GameRootPath, exeName);
            }
        }

        // Check executable version
        var (versionStatus, computedHash, matchedVersion) = await CheckExecutableVersionAsync(
            executablePath,
            configuration.ExecutableHashOld,
            configuration.ExecutableHashNew,
            cancellationToken).ConfigureAwait(false);

        var executableFound = versionStatus != ExecutableVersionStatus.NotFound &&
                              versionStatus != ExecutableVersionStatus.NotChecked;

        // Add version-related issues
        AddVersionIssues(issues, versionStatus, configuration, matchedVersion);

        // Check installation location
        var locationStatus = CheckInstallationLocation(configuration.GameRootPath);
        if (locationStatus == InstallationLocationStatus.RestrictedLocation)
        {
            var gameName = !string.IsNullOrEmpty(configuration.GameDisplayName)
                ? configuration.GameDisplayName
                : configuration.GameType.GetDisplayName();

            var warning = configuration.RestrictedLocationWarning ??
                $"Your {gameName} game files are installed in the Program Files folder. " +
                "This may cause permission issues. Consider moving the game to a non-restricted location.";

            issues.Add(new GameIntegrityIssue(
                GameIntegrityIssueType.RestrictedInstallLocation,
                warning,
                ConfigIssueSeverity.Warning,
                "Move the game installation to a folder outside of Program Files."));
        }

        // Check for Steam INI (build path if not provided)
        var steamIniPath = configuration.SteamIniPath;
        if (string.IsNullOrEmpty(steamIniPath) && !string.IsNullOrEmpty(configuration.GameRootPath))
        {
            steamIniPath = Path.Combine(configuration.GameRootPath, "steam_api64.ini");
        }

        var steamIniDetected = CheckSteamIniExists(steamIniPath);
        if (steamIniDetected)
        {
            var gameName = !string.IsNullOrEmpty(configuration.GameDisplayName)
                ? configuration.GameDisplayName
                : configuration.GameType.GetDisplayName();

            issues.Add(new GameIntegrityIssue(
                GameIntegrityIssueType.SteamIniPresent,
                $"Steam INI file detected. Your {gameName} installation may be outdated.",
                ConfigIssueSeverity.Error,
                "Update your game through Steam or reinstall from a clean source."));
        }

        return new GameIntegrityResult
        {
            GameType = configuration.GameType,
            ExecutablePath = executablePath,
            ExecutableFound = executableFound,
            VersionStatus = versionStatus,
            ComputedHash = computedHash,
            MatchedVersion = matchedVersion,
            LocationStatus = locationStatus,
            SteamIniDetected = steamIniDetected,
            Issues = issues
        };
    }

    /// <inheritdoc/>
    public async Task<(ExecutableVersionStatus Status, string? ComputedHash, string? MatchedVersion)> CheckExecutableVersionAsync(
        string? executablePath,
        string? expectedHashOld,
        string? expectedHashNew,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return (ExecutableVersionStatus.NotChecked, null, null);
        }

        if (!File.Exists(executablePath))
        {
            return (ExecutableVersionStatus.NotFound, null, null);
        }

        var computedHash = await ComputeFileHashAsync(executablePath, cancellationToken).ConfigureAwait(false);
        if (computedHash == null)
        {
            return (ExecutableVersionStatus.HashError, null, null);
        }

        // Check against known hashes
        if (!string.IsNullOrEmpty(expectedHashNew) &&
            string.Equals(computedHash, expectedHashNew, StringComparison.OrdinalIgnoreCase))
        {
            return (ExecutableVersionStatus.LatestVersion, computedHash, "new");
        }

        if (!string.IsNullOrEmpty(expectedHashOld) &&
            string.Equals(computedHash, expectedHashOld, StringComparison.OrdinalIgnoreCase))
        {
            return (ExecutableVersionStatus.Outdated, computedHash, "old");
        }

        // Hash doesn't match any known version
        return (ExecutableVersionStatus.Unknown, computedHash, null);
    }

    /// <inheritdoc/>
    public InstallationLocationStatus CheckInstallationLocation(string? gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return InstallationLocationStatus.PathNotProvided;
        }

        foreach (var restricted in RestrictedPaths)
        {
            if (gamePath.Contains(restricted, StringComparison.OrdinalIgnoreCase))
            {
                return InstallationLocationStatus.RestrictedLocation;
            }
        }

        return InstallationLocationStatus.RecommendedLocation;
    }

    /// <inheritdoc/>
    public bool CheckSteamIniExists(string? steamIniPath)
    {
        if (string.IsNullOrWhiteSpace(steamIniPath))
        {
            return false;
        }

        return File.Exists(steamIniPath);
    }

    /// <inheritdoc/>
    public async Task<string?> ComputeFileHashAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920, // Larger buffer for large executables
                useAsync: true);

            var hashBytes = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Adds version-related issues to the issues list.
    /// </summary>
    private static void AddVersionIssues(
        List<GameIntegrityIssue> issues,
        ExecutableVersionStatus status,
        GameIntegrityConfiguration config,
        string? matchedVersion)
    {
        var gameName = !string.IsNullOrEmpty(config.GameDisplayName)
            ? config.GameDisplayName
            : config.GameType.GetDisplayName();

        switch (status)
        {
            case ExecutableVersionStatus.NotFound:
                issues.Add(new GameIntegrityIssue(
                    GameIntegrityIssueType.ExecutableNotFound,
                    $"Game executable not found for {gameName}.",
                    ConfigIssueSeverity.Error,
                    "Verify the game installation path is correct."));
                break;

            case ExecutableVersionStatus.Outdated:
                var oldVersion = config.GameVersionOld ?? "older";
                var newVersion = config.GameVersionNew ?? "latest";
                issues.Add(new GameIntegrityIssue(
                    GameIntegrityIssueType.OutdatedVersion,
                    $"Your {gameName} executable is version {oldVersion}. " +
                    $"The latest version is {newVersion}.",
                    ConfigIssueSeverity.Warning,
                    "Update your game through Steam or your game launcher."));
                break;

            case ExecutableVersionStatus.Unknown:
                issues.Add(new GameIntegrityIssue(
                    GameIntegrityIssueType.UnknownVersion,
                    $"Your {gameName} executable does not match any known version.",
                    ConfigIssueSeverity.Warning,
                    "This may be a modified executable or an unsupported version."));
                break;

            case ExecutableVersionStatus.HashError:
                issues.Add(new GameIntegrityIssue(
                    GameIntegrityIssueType.ExecutableReadError,
                    $"Could not read the {gameName} executable to verify version.",
                    ConfigIssueSeverity.Warning,
                    "Check file permissions or close any programs using the executable."));
                break;
        }
    }
}

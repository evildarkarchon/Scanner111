using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Services.GamePath;

/// <summary>
/// Detects game installation paths using registry lookups and XSE log parsing.
/// </summary>
/// <remarks>
/// <para>
/// This service locates game installations using multiple detection strategies in order of reliability:
/// </para>
/// <list type="number">
/// <item><description>Windows Registry (Bethesda Softworks key) - Most reliable on Windows</description></item>
/// <item><description>GOG Galaxy registry key - For GOG-installed games</description></item>
/// <item><description>XSE log file parsing - Extracts path from "plugin directory" line</description></item>
/// </list>
/// <para>
/// Registry access is only available on Windows. On other platforms, only XSE log parsing is available.
/// </para>
/// </remarks>
public sealed class GamePathDetector : IGamePathDetector
{
    private readonly ILogger<GamePathDetector> _logger;

    /// <summary>
    /// GOG Galaxy registry keys for supported games.
    /// </summary>
    private static readonly IReadOnlyDictionary<GameType, string> GogGameIds = new Dictionary<GameType, string>
    {
        [GameType.Fallout4] = "1998527297",
        [GameType.SkyrimSE] = "1711230643"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GamePathDetector"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public GamePathDetector(ILogger<GamePathDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<GamePathResult> DetectGamePathAsync(
        GameType gameType,
        string? xseLogPath = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (gameType == GameType.Unknown)
        {
            return GamePathResult.Failure(gameType, "Cannot detect path for unknown game type");
        }

        // Try registry first on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryPath = await FindFromRegistryAsync(gameType, cancellationToken).ConfigureAwait(false);
            if (registryPath != null)
            {
                return GamePathResult.Success(gameType, registryPath, GamePathDetectionMethod.Registry);
            }

            // Try GOG registry
            if (GogGameIds.ContainsKey(gameType))
            {
                var gogPath = await FindFromGogRegistryAsync(gameType, cancellationToken).ConfigureAwait(false);
                if (gogPath != null)
                {
                    return GamePathResult.Success(gameType, gogPath, GamePathDetectionMethod.GogRegistry);
                }
            }
        }

        // Try XSE log if path provided
        if (!string.IsNullOrWhiteSpace(xseLogPath))
        {
            var xsePath = await FindFromXseLogAsync(gameType, xseLogPath, cancellationToken).ConfigureAwait(false);
            if (xsePath != null)
            {
                return GamePathResult.Success(gameType, xsePath, GamePathDetectionMethod.XseLog);
            }
        }

        return GamePathResult.Failure(gameType, "Could not detect game installation path");
    }

    /// <inheritdoc/>
    [SupportedOSPlatform("windows")]
    public Task<string?> FindFromRegistryAsync(
        GameType gameType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult<string?>(null);
        }

        var registryKeyName = gameType.GetRegistryKeyName();
        if (string.IsNullOrEmpty(registryKeyName))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var path = ReadBethesdaRegistryPath(registryKeyName);
            if (!string.IsNullOrEmpty(path) && ValidateGamePath(gameType, path))
            {
                return Task.FromResult<string?>(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read game path from Bethesda registry: {GameType}", gameType);
        }

        return Task.FromResult<string?>(null);
    }

    /// <inheritdoc/>
    public async Task<string?> FindFromXseLogAsync(
        GameType gameType,
        string xseLogPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(xseLogPath) || !File.Exists(xseLogPath))
        {
            return null;
        }

        try
        {
            var xseAcronymBase = gameType.GetXseAcronymBase();
            if (string.IsNullOrEmpty(xseAcronymBase))
            {
                return null;
            }

            // Read the file and look for the plugin directory line
            await using var stream = new FileStream(
                xseLogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) != null)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (line.StartsWith("plugin directory", StringComparison.OrdinalIgnoreCase))
                {
                    var path = ExtractPathFromPluginDirectory(line, xseAcronymBase);
                    if (!string.IsNullOrEmpty(path) && ValidateGamePath(gameType, path))
                    {
                        return path;
                    }
                }
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to read XSE log for game path extraction: {XseLogPath}", xseLogPath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to XSE log: {XseLogPath}", xseLogPath);
        }

        return null;
    }

    /// <inheritdoc/>
    public bool ValidateGamePath(GameType gameType, string gamePath)
    {
        if (string.IsNullOrWhiteSpace(gamePath))
        {
            return false;
        }

        if (!Directory.Exists(gamePath))
        {
            return false;
        }

        var executableName = gameType.GetExecutableName();
        if (string.IsNullOrEmpty(executableName))
        {
            return false;
        }

        var exePath = Path.Combine(gamePath, executableName);
        return File.Exists(exePath);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GamePathResult>> DetectAllInstalledGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<GamePathResult>();
        var gameTypes = new[]
        {
            GameType.Fallout4,
            GameType.Fallout4VR,
            GameType.SkyrimSE,
            GameType.SkyrimVR
        };

        foreach (var gameType in gameTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await DetectGamePathAsync(gameType, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (result.Found)
            {
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Attempts to find the game path from the GOG Galaxy registry.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private Task<string?> FindFromGogRegistryAsync(
        GameType gameType,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult<string?>(null);
        }

        if (!GogGameIds.TryGetValue(gameType, out var gogId))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var path = ReadGogRegistryPath(gogId);
            if (!string.IsNullOrEmpty(path) && ValidateGamePath(gameType, path))
            {
                return Task.FromResult<string?>(path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read game path from GOG registry: {GameType}", gameType);
        }

        return Task.FromResult<string?>(null);
    }

    /// <summary>
    /// Reads the game installation path from the Bethesda Softworks registry key.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string? ReadBethesdaRegistryPath(string gameKeyName)
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            $@"SOFTWARE\WOW6432Node\Bethesda Softworks\{gameKeyName}");

        return key?.GetValue("installed path") as string;
    }

    /// <summary>
    /// Reads the game installation path from the GOG Galaxy registry key.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string? ReadGogRegistryPath(string gogGameId)
    {
        using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
            $@"SOFTWARE\WOW6432Node\GOG.com\Games\{gogGameId}");

        return key?.GetValue("path") as string;
    }

    /// <summary>
    /// Extracts the game path from an XSE log "plugin directory" line.
    /// </summary>
    /// <param name="line">The log line containing the plugin directory path.</param>
    /// <param name="xseAcronymBase">The base XSE acronym (e.g., "F4SE", "SKSE").</param>
    /// <returns>The extracted game path, or <c>null</c> if extraction failed.</returns>
    /// <remarks>
    /// XSE log lines are formatted like:
    /// <code>plugin directory = C:\Steam\steamapps\common\Fallout 4\Data\F4SE\Plugins</code>
    /// This method extracts the path up to and including the game folder.
    /// </remarks>
    private static string? ExtractPathFromPluginDirectory(string line, string xseAcronymBase)
    {
        // Expected format: "plugin directory = C:\path\to\game\Data\F4SE\Plugins"
        var equalsIndex = line.IndexOf('=');
        if (equalsIndex < 0 || equalsIndex >= line.Length - 1)
        {
            return null;
        }

        var pathPart = line[(equalsIndex + 1)..].Trim();

        // Remove the trailing \Data\XSE\Plugins part
        var suffixToRemove = $@"\Data\{xseAcronymBase}\Plugins";
        if (pathPart.EndsWith(suffixToRemove, StringComparison.OrdinalIgnoreCase))
        {
            return pathPart[..^suffixToRemove.Length];
        }

        // Try alternate patterns (forward slashes, lowercase)
        var normalizedPath = pathPart.Replace('/', '\\');
        if (normalizedPath.EndsWith(suffixToRemove, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath[..^suffixToRemove.Length];
        }

        return null;
    }
}

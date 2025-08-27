using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;

namespace Scanner111.Core.Discovery;

/// <summary>
///     Service for discovering game installation paths with thread-safe caching.
/// </summary>
public sealed class GamePathDiscoveryService : IGamePathDiscoveryService, IDisposable
{
    private readonly ConcurrentDictionary<string, CachedDiscoveryResult> _cache;
    private readonly TimeSpan _cacheExpiration;
    private readonly SemaphoreSlim _discoverySemaphore;
    private readonly ILogger<GamePathDiscoveryService> _logger;
    private readonly IPathValidationService _pathValidation;
    private readonly IAsyncYamlSettingsCore _yamlCore;

    public GamePathDiscoveryService(
        IPathValidationService pathValidation,
        IAsyncYamlSettingsCore yamlCore,
        ILogger<GamePathDiscoveryService> logger)
    {
        _pathValidation = pathValidation ?? throw new ArgumentNullException(nameof(pathValidation));
        _yamlCore = yamlCore ?? throw new ArgumentNullException(nameof(yamlCore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = new ConcurrentDictionary<string, CachedDiscoveryResult>();
        _discoverySemaphore = new SemaphoreSlim(1, 1);
        _cacheExpiration = TimeSpan.FromMinutes(5);
    }

    public void Dispose()
    {
        _discoverySemaphore?.Dispose();
    }

    public async Task<PathDiscoveryResult> DiscoverGamePathAsync(GameInfo gameInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameInfo);

        var stopwatch = Stopwatch.StartNew();
        var cacheKey = GetCacheKey(gameInfo);

        // Check cache first
        var cached = GetCachedResult(gameInfo);
        if (cached != null)
        {
            _logger.LogDebug("Returning cached game path discovery result for {GameName}", gameInfo.GameName);
            return cached;
        }

        await _discoverySemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Double-check cache after acquiring semaphore
            cached = GetCachedResult(gameInfo);
            if (cached != null) return cached;

            _logger.LogInformation("Starting game path discovery for {GameName} {VR}",
                gameInfo.GameName, gameInfo.IsVR ? "VR" : "");

            // Try configured path first
            var configuredPath = await TryGetConfiguredPathAsync(gameInfo, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(configuredPath))
                if (await ValidateGamePathAsync(gameInfo, configuredPath, cancellationToken).ConfigureAwait(false))
                {
                    var paths = CreateGamePaths(gameInfo, configuredPath);
                    var result = PathDiscoveryResult.Success(paths, DiscoveryMethod.ConfiguredPath,
                        stopwatch.ElapsedMilliseconds, "Found via configured settings");
                    CacheResult(cacheKey, result);
                    return result;
                }

            // Try registry discovery on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var registryResult =
                    await TryDiscoverViaRegistryAsync(gameInfo, cancellationToken).ConfigureAwait(false);
                if (registryResult?.IsSuccess == true)
                {
                    CacheResult(cacheKey, registryResult);
                    return registryResult;
                }
            }

            // Try Script Extender log discovery
            var xseLogPath = await GetScriptExtenderLogPathAsync(gameInfo, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(xseLogPath))
            {
                var xseResult = await TryDiscoverViaScriptExtenderLogAsync(gameInfo, xseLogPath, cancellationToken)
                    .ConfigureAwait(false);
                if (xseResult?.IsSuccess == true)
                {
                    CacheResult(cacheKey, xseResult);
                    return xseResult;
                }
            }

            // Try Steam discovery
            var steamResult = await TryDiscoverViaSteamAsync(gameInfo, cancellationToken).ConfigureAwait(false);
            if (steamResult?.IsSuccess == true)
            {
                CacheResult(cacheKey, steamResult);
                return steamResult;
            }

            // All discovery methods failed
            var failureResult = PathDiscoveryResult.Failure(
                DiscoveryMethod.Unknown,
                "Unable to discover game path through any automatic method",
                stopwatch.ElapsedMilliseconds);

            _logger.LogWarning("Failed to discover game path for {GameName}", gameInfo.GameName);
            return failureResult;
        }
        finally
        {
            _discoverySemaphore.Release();
        }
    }

    public async Task<PathDiscoveryResult?> TryDiscoverViaRegistryAsync(GameInfo gameInfo,
        CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return null;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Attempting registry discovery for {GameName}", gameInfo.GameName);

            // Try Bethesda registry key
            var bethesdaKey = gameInfo.RegistryKeyPath ??
                              $@"SOFTWARE\WOW6432Node\Bethesda Softworks\{gameInfo.GameName}{(gameInfo.IsVR ? "VR" : "")}";

            using var key = Registry.LocalMachine.OpenSubKey(bethesdaKey);
            if (key != null)
            {
                var path = key.GetValue("installed path") as string;
                if (!string.IsNullOrWhiteSpace(path) &&
                    await ValidateGamePathAsync(gameInfo, path, cancellationToken).ConfigureAwait(false))
                {
                    var paths = CreateGamePaths(gameInfo, path);
                    return PathDiscoveryResult.Success(paths, DiscoveryMethod.Registry,
                        stopwatch.ElapsedMilliseconds, "Found via Bethesda registry");
                }
            }

            // Try GOG registry key if applicable
            if (!string.IsNullOrWhiteSpace(gameInfo.GogId))
            {
                using var gogKey =
                    Registry.LocalMachine.OpenSubKey($@"SOFTWARE\WOW6432Node\GOG.com\Games\{gameInfo.GogId}");
                if (gogKey != null)
                {
                    var path = gogKey.GetValue("path") as string;
                    if (!string.IsNullOrWhiteSpace(path) &&
                        await ValidateGamePathAsync(gameInfo, path, cancellationToken).ConfigureAwait(false))
                    {
                        var paths = CreateGamePaths(gameInfo, path);
                        return PathDiscoveryResult.Success(paths, DiscoveryMethod.Registry,
                            stopwatch.ElapsedMilliseconds, "Found via GOG registry");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registry discovery for {GameName}", gameInfo.GameName);
        }

        return null;
    }

    public async Task<PathDiscoveryResult?> TryDiscoverViaScriptExtenderLogAsync(
        GameInfo gameInfo,
        string scriptExtenderLogPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(scriptExtenderLogPath)) return null;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Attempting Script Extender log discovery from {LogPath}", scriptExtenderLogPath);

            if (!File.Exists(scriptExtenderLogPath)) return null;

            var content = await File.ReadAllTextAsync(scriptExtenderLogPath, cancellationToken).ConfigureAwait(false);

            // Look for plugin directory line
            var pattern = @"plugin directory\s*=\s*(.+?)\\Data\\[A-Z0-9]+\\Plugins";
            var match = Regex.Match(content, pattern, RegexOptions.IgnoreCase);

            if (match.Success)
            {
                var gamePath = match.Groups[1].Value.Trim();

                if (await ValidateGamePathAsync(gameInfo, gamePath, cancellationToken).ConfigureAwait(false))
                {
                    var paths = CreateGamePaths(gameInfo, gamePath);
                    return PathDiscoveryResult.Success(paths, DiscoveryMethod.ScriptExtenderLog,
                        stopwatch.ElapsedMilliseconds, $"Found via {gameInfo.ScriptExtenderAcronym} log");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Script Extender log at {LogPath}", scriptExtenderLogPath);
        }

        return null;
    }

    public async Task<PathDiscoveryResult?> TryDiscoverViaSteamAsync(GameInfo gameInfo,
        CancellationToken cancellationToken = default)
    {
        if (gameInfo.SteamId == null) return null;

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Attempting Steam discovery for {GameName} (AppId: {SteamId})",
                gameInfo.GameName, gameInfo.SteamId);

            // Common Steam installation paths
            var steamPaths = GetPotentialSteamPaths();

            foreach (var steamPath in steamPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var gamePath = Path.Combine(steamPath, "steamapps", "common", gameInfo.GameName);
                if (await ValidateGamePathAsync(gameInfo, gamePath, cancellationToken).ConfigureAwait(false))
                {
                    var paths = CreateGamePaths(gameInfo, gamePath);
                    return PathDiscoveryResult.Success(paths, DiscoveryMethod.SteamLibrary,
                        stopwatch.ElapsedMilliseconds, "Found via Steam library");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Steam discovery for {GameName}", gameInfo.GameName);
        }

        return null;
    }

    public async Task<bool> ValidateGamePathAsync(GameInfo gameInfo, string gamePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gamePath)) return false;

        try
        {
            // Check directory exists
            if (!await _pathValidation.DirectoryExistsAsync(gamePath, cancellationToken).ConfigureAwait(false))
                return false;

            // Check executable exists
            var exePath = Path.Combine(gamePath, gameInfo.ExecutableName);
            if (!await _pathValidation.FileExistsAsync(exePath, cancellationToken).ConfigureAwait(false)) return false;

            _logger.LogDebug("Successfully validated game path at {GamePath}", gamePath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating game path {GamePath}", gamePath);
            return false;
        }
    }

    public PathDiscoveryResult? GetCachedResult(GameInfo gameInfo)
    {
        var cacheKey = GetCacheKey(gameInfo);

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.CachedAt < _cacheExpiration) return cached.Result;

            // Remove expired entry
            _cache.TryRemove(cacheKey, out _);
        }

        return null;
    }

    public void ClearCache()
    {
        _cache.Clear();
        _logger.LogDebug("Game path discovery cache cleared");
    }

    private async Task<string?> TryGetConfiguredPathAsync(GameInfo gameInfo, CancellationToken cancellationToken)
    {
        try
        {
            var settingKey = $"Game{(gameInfo.IsVR ? "VR" : "")}_Info.Root_Folder_Game";
            return await _yamlCore.GetSettingAsync<string>(
                YamlStore.GameLocal, settingKey, null, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading configured game path from settings");
            return null;
        }
    }

    private async Task<string?> GetScriptExtenderLogPathAsync(GameInfo gameInfo, CancellationToken cancellationToken)
    {
        try
        {
            var settingKey = $"Game{(gameInfo.IsVR ? "VR" : "")}_Info.Docs_File_XSE";
            return await _yamlCore.GetSettingAsync<string>(
                YamlStore.GameLocal, settingKey, null, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static GamePaths CreateGamePaths(GameInfo gameInfo, string gamePath)
    {
        var paths = new GamePaths
        {
            GameRootPath = gamePath,
            ExecutablePath = Path.Combine(gamePath, gameInfo.ExecutableName),
            ScriptExtenderPluginsPath = Path.Combine(gamePath, "Data", gameInfo.ScriptExtenderBase, "Plugins"),
            SteamApiIniPath = Path.Combine(gamePath, "steam_api.ini")
        };

        return paths;
    }

    private static List<string> GetPotentialSteamPaths()
    {
        var paths = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            paths.Add(@"C:\Program Files (x86)\Steam");
            paths.Add(@"C:\Program Files\Steam");
            paths.Add(@"D:\Steam");
            paths.Add(@"E:\Steam");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var home = Environment.GetEnvironmentVariable("HOME");
            if (!string.IsNullOrWhiteSpace(home))
            {
                paths.Add(Path.Combine(home, ".local", "share", "Steam"));
                paths.Add(Path.Combine(home, ".steam", "steam"));
            }
        }

        return paths;
    }

    private static string GetCacheKey(GameInfo gameInfo)
    {
        return $"{gameInfo.GameName}_{(gameInfo.IsVR ? "VR" : "Standard")}";
    }

    private void CacheResult(string key, PathDiscoveryResult result)
    {
        _cache[key] = new CachedDiscoveryResult(result, DateTimeOffset.UtcNow);
    }

    private sealed record CachedDiscoveryResult(PathDiscoveryResult Result, DateTimeOffset CachedAt);
}
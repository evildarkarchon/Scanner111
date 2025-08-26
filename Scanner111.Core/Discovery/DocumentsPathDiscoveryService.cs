using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;

namespace Scanner111.Core.Discovery;

/// <summary>
///     Service for discovering game documents folder paths.
/// </summary>
public sealed class DocumentsPathDiscoveryService : IDocumentsPathDiscoveryService
{
    private readonly ILogger<DocumentsPathDiscoveryService> _logger;
    private readonly IPathValidationService _pathValidation;
    private readonly IYamlSettingsCache _yamlSettings;

    public DocumentsPathDiscoveryService(
        IPathValidationService pathValidation,
        IYamlSettingsCache yamlSettings,
        ILogger<DocumentsPathDiscoveryService> logger)
    {
        _pathValidation = pathValidation ?? throw new ArgumentNullException(nameof(pathValidation));
        _yamlSettings = yamlSettings ?? throw new ArgumentNullException(nameof(yamlSettings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PathDiscoveryResult> DiscoverDocumentsPathAsync(GameInfo gameInfo,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameInfo);

        _logger.LogInformation("Starting documents path discovery for {GameName}", gameInfo.GameName);

        // Try configured path first
        var configuredPath = await GetConfiguredDocumentsPathAsync(gameInfo, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(configuredPath) &&
            await ValidateDocumentsPathAsync(gameInfo, configuredPath, cancellationToken).ConfigureAwait(false))
        {
            var paths = new GamePaths
            {
                GameRootPath = string.Empty, // Will be filled by GamePathDiscoveryService
                ExecutablePath = string.Empty,
                DocumentsPath = configuredPath,
                GameIniPath = Path.Combine(configuredPath, $"{gameInfo.DocumentsFolderName}.ini"),
                GameCustomIniPath = Path.Combine(configuredPath, $"{gameInfo.DocumentsFolderName}Custom.ini"),
                ScriptExtenderLogPath = Path.Combine(configuredPath, gameInfo.ScriptExtenderBase,
                    $"{gameInfo.ScriptExtenderAcronym.ToLower()}.log"),
                PapyrusLogPath = Path.Combine(configuredPath, "Logs", "Script", "Papyrus.0.log")
            };

            return PathDiscoveryResult.Success(paths, DiscoveryMethod.ConfiguredPath, 0,
                "Found via configured settings");
        }

        // Try platform-specific discovery
        string? discoveredPath = null;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            discoveredPath = await DiscoverWindowsDocumentsPathAsync(gameInfo, cancellationToken).ConfigureAwait(false);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            // For Linux, we need the Steam library path which should come from GamePathDiscoveryService
            _logger.LogWarning("Linux documents discovery requires Steam library path");

        if (!string.IsNullOrWhiteSpace(discoveredPath) &&
            await ValidateDocumentsPathAsync(gameInfo, discoveredPath, cancellationToken).ConfigureAwait(false))
        {
            var paths = new GamePaths
            {
                GameRootPath = string.Empty,
                ExecutablePath = string.Empty,
                DocumentsPath = discoveredPath,
                GameIniPath = Path.Combine(discoveredPath, $"{gameInfo.DocumentsFolderName}.ini"),
                GameCustomIniPath = Path.Combine(discoveredPath, $"{gameInfo.DocumentsFolderName}Custom.ini"),
                ScriptExtenderLogPath = Path.Combine(discoveredPath, gameInfo.ScriptExtenderBase,
                    $"{gameInfo.ScriptExtenderAcronym.ToLower()}.log"),
                PapyrusLogPath = Path.Combine(discoveredPath, "Logs", "Script", "Papyrus.0.log")
            };

            return PathDiscoveryResult.Success(paths, DiscoveryMethod.Registry, 0, "Found via Windows registry");
        }

        return PathDiscoveryResult.Failure(DiscoveryMethod.Unknown,
            "Unable to discover documents path", 0);
    }

    public Task<string?> DiscoverWindowsDocumentsPathAsync(GameInfo gameInfo,
        CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return Task.FromResult<string?>(null);

        try
        {
            string documentsPath;

            // Try to get from registry
            using (var key = Registry.CurrentUser.OpenSubKey(
                       @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders"))
            {
                var personal = key?.GetValue("Personal") as string;
                documentsPath = personal ??
                                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                    "Documents");
            }

            // Build game documents path
            var gameDocs = Path.Combine(documentsPath, "My Games", gameInfo.DocumentsFolderName);

            // Create directory if it doesn't exist
            if (!Directory.Exists(gameDocs)) Directory.CreateDirectory(gameDocs);

            return Task.FromResult<string?>(gameDocs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering Windows documents path");
            return Task.FromResult<string?>(null);
        }
    }

    public Task<string?> DiscoverLinuxDocumentsPathAsync(
        GameInfo gameInfo,
        string steamLibraryPath,
        CancellationToken cancellationToken = default)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || gameInfo.SteamId == null)
            return Task.FromResult<string?>(null);

        try
        {
            // Build Proton compatibility path
            var protonPath = Path.Combine(
                steamLibraryPath,
                "steamapps",
                "compatdata",
                gameInfo.SteamId.ToString(),
                "pfx",
                "drive_c",
                "users",
                "steamuser",
                "My Documents",
                "My Games",
                gameInfo.DocumentsFolderName
            );

            if (Directory.Exists(protonPath)) return Task.FromResult<string?>(protonPath);

            return Task.FromResult<string?>(null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering Linux documents path");
            return Task.FromResult<string?>(null);
        }
    }

    public async Task<bool> ValidateDocumentsPathAsync(
        GameInfo gameInfo,
        string documentsPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentsPath)) return false;

        var validation = await _pathValidation.ValidatePathAsync(documentsPath, true, true, cancellationToken)
            .ConfigureAwait(false);

        return validation.IsValid && validation.CanRead && validation.CanWrite;
    }

    public async Task<List<IniValidationResult>> ValidateIniFilesAsync(
        GameInfo gameInfo,
        string documentsPath,
        CancellationToken cancellationToken = default)
    {
        var results = new List<IniValidationResult>();

        // Check main INI
        var mainIniPath = Path.Combine(documentsPath, $"{gameInfo.DocumentsFolderName}.ini");
        var mainIniResult =
            await ValidateIniFileAsync(mainIniPath, $"{gameInfo.DocumentsFolderName}.ini", false, cancellationToken)
                .ConfigureAwait(false);
        results.Add(mainIniResult);

        // Check custom INI
        var customIniPath = Path.Combine(documentsPath, $"{gameInfo.DocumentsFolderName}Custom.ini");
        var customIniResult = await ValidateIniFileAsync(customIniPath, $"{gameInfo.DocumentsFolderName}Custom.ini",
                true, cancellationToken)
            .ConfigureAwait(false);
        results.Add(customIniResult);

        return results;
    }

    public async Task<bool> EnsureArchiveInvalidationAsync(
        GameInfo gameInfo,
        string customIniPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var lines = new List<string>();

            if (File.Exists(customIniPath))
                lines.AddRange(await File.ReadAllLinesAsync(customIniPath, cancellationToken).ConfigureAwait(false));

            // Check if Archive section exists
            var hasArchiveSection = lines.Any(l => l.Trim().Equals("[Archive]", StringComparison.OrdinalIgnoreCase));

            if (!hasArchiveSection) lines.Add("[Archive]");

            // Ensure required settings are present
            EnsureIniSetting(lines, "bInvalidateOlderFiles", "1");
            EnsureIniSetting(lines, "sResourceDataDirsFinal", "");

            await File.WriteAllLinesAsync(customIniPath, lines, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Archive invalidation configured in {IniPath}", customIniPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring archive invalidation");
            return false;
        }
    }

    private Task<string?> GetConfiguredDocumentsPathAsync(GameInfo gameInfo, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var settingKey = $"Game{(gameInfo.IsVR ? "VR" : "")}_Info.Root_Folder_Docs";
                return _yamlSettings.GetSetting<string>(YamlStore.GameLocal, settingKey);
            }
            catch
            {
                return null;
            }
        }, cancellationToken);
    }

    private async Task<IniValidationResult> ValidateIniFileAsync(
        string iniPath,
        string fileName,
        bool checkArchiveInvalidation,
        CancellationToken cancellationToken)
    {
        var result = new IniValidationResult
        {
            FileName = fileName,
            FilePath = iniPath,
            Exists = File.Exists(iniPath)
        };

        if (!result.Exists)
        {
            result = result with { NeedsCreation = true };
            result.ValidationMessages.Add($"{fileName} is missing and needs to be created");
            return result;
        }

        try
        {
            var content = await File.ReadAllTextAsync(iniPath, cancellationToken).ConfigureAwait(false);

            // Basic validation - check if it's parseable
            result = result with { IsValid = !string.IsNullOrWhiteSpace(content) };

            if (checkArchiveInvalidation)
            {
                var hasArchiveInvalidation = content.Contains("[Archive]", StringComparison.OrdinalIgnoreCase) &&
                                             content.Contains("bInvalidateOlderFiles",
                                                 StringComparison.OrdinalIgnoreCase);
                result = result with { HasArchiveInvalidation = hasArchiveInvalidation };

                if (!hasArchiveInvalidation) result.ValidationMessages.Add("Archive invalidation is not configured");
            }
        }
        catch (Exception ex)
        {
            result = result with { IsValid = false, NeedsRepair = true };
            result.ValidationMessages.Add($"Error reading INI file: {ex.Message}");
        }

        return result;
    }

    private static void EnsureIniSetting(List<string> lines, string key, string value)
    {
        var settingLine = $"{key}={value}";
        var existingIndex = lines.FindIndex(l => l.TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            lines[existingIndex] = settingLine;
        }
        else
        {
            // Add after [Archive] section
            var archiveIndex = lines.FindIndex(l => l.Trim().Equals("[Archive]", StringComparison.OrdinalIgnoreCase));
            if (archiveIndex >= 0)
                lines.Insert(archiveIndex + 1, settingLine);
            else
                lines.Add(settingLine);
        }
    }
}
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Scanner111.Common.Models.DocsPath;
using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Services.DocsPath;

/// <summary>
/// Detects game documents paths using registry lookups and environment fallbacks.
/// </summary>
/// <remarks>
/// <para>
/// This service locates game documents folders using multiple detection strategies
/// in order of reliability:
/// </para>
/// <list type="number">
/// <item><description>Cached user settings (if valid)</description></item>
/// <item><description>Windows Registry Shell Folders key (most reliable on Windows)</description></item>
/// <item><description>Environment.SpecialFolder.MyDocuments fallback</description></item>
/// </list>
/// <para>
/// Registry access is only available on Windows. On other platforms, only environment
/// fallback is available.
/// </para>
/// </remarks>
public sealed class DocsPathDetector : IDocsPathDetector
{
    private readonly ILogger<DocsPathDetector> _logger;

    /// <summary>
    /// Registry key for Windows Shell Folders.
    /// </summary>
    private const string ShellFoldersRegistryPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Folders";

    /// <summary>
    /// Registry value name for the Personal (Documents) folder.
    /// </summary>
    private const string PersonalValueName = "Personal";

    /// <summary>
    /// Initializes a new instance of the <see cref="DocsPathDetector"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public DocsPathDetector(ILogger<DocsPathDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<DocsPathResult> DetectDocsPathAsync(
        GameType gameType,
        CancellationToken cancellationToken = default)
    {
        return await DetectDocsPathAsync(gameType, cachedPath: null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<DocsPathResult> DetectDocsPathAsync(
        GameType gameType,
        string? cachedPath,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        if (gameType == GameType.Unknown)
        {
            return DocsPathResult.Failure(gameType, "Cannot detect path for unknown game type");
        }

        var myGamesFolderName = gameType.GetMyGamesFolderName();
        if (string.IsNullOrEmpty(myGamesFolderName))
        {
            return DocsPathResult.Failure(gameType, "Game type does not have a My Games folder name");
        }

        // Try cached path first
        if (!string.IsNullOrWhiteSpace(cachedPath) && ValidateDocsPath(gameType, cachedPath))
        {
            return DocsPathResult.Success(gameType, cachedPath, DocsPathDetectionMethod.Cache);
        }

        // Try registry on Windows
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryDocsFolder = await FindWindowsDocumentsFolderAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrEmpty(registryDocsFolder))
            {
                var docsPath = Path.Combine(registryDocsFolder, "My Games", myGamesFolderName);
                if (ValidateDocsPath(gameType, docsPath))
                {
                    return DocsPathResult.Success(gameType, docsPath, DocsPathDetectionMethod.Registry);
                }
            }
        }

        // Fallback to Environment.SpecialFolder
        var envDocsFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (!string.IsNullOrEmpty(envDocsFolder))
        {
            var docsPath = Path.Combine(envDocsFolder, "My Games", myGamesFolderName);
            if (ValidateDocsPath(gameType, docsPath))
            {
                return DocsPathResult.Success(gameType, docsPath, DocsPathDetectionMethod.EnvironmentFallback);
            }
        }

        return DocsPathResult.Failure(gameType, "Could not detect game documents path");
    }

    /// <inheritdoc/>
    public GeneratedDocsPaths GeneratePaths(GameType gameType, string docsRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(docsRootPath);

        var xseAcronymBase = gameType.GetXseAcronymBase();
        var xseLogFileName = gameType.GetXseLogFileName();
        var myGamesFolderName = gameType.GetMyGamesFolderName();

        // Determine the INI base name (same as My Games folder name for most games)
        var iniBaseName = GetIniBaseName(gameType);

        return new GeneratedDocsPaths
        {
            RootPath = docsRootPath,
            XseFolderPath = Path.Combine(docsRootPath, xseAcronymBase),
            PapyrusLogPath = Path.Combine(docsRootPath, "Logs", "Script", "Papyrus.0.log"),
            WryeBashModCheckerPath = Path.Combine(docsRootPath, "ModChecker.html"),
            XseLogPath = Path.Combine(docsRootPath, xseAcronymBase, xseLogFileName),
            MainIniPath = Path.Combine(docsRootPath, $"{iniBaseName}.ini"),
            CustomIniPath = Path.Combine(docsRootPath, $"{iniBaseName}Custom.ini"),
            PrefsIniPath = Path.Combine(docsRootPath, $"{iniBaseName}Prefs.ini")
        };
    }

    /// <inheritdoc/>
    public bool ValidateDocsPath(GameType gameType, string docsPath)
    {
        if (string.IsNullOrWhiteSpace(docsPath))
        {
            return false;
        }

        // The documents folder must exist
        return Directory.Exists(docsPath);
    }

    /// <inheritdoc/>
    [SupportedOSPlatform("windows")]
    public Task<string?> FindWindowsDocumentsFolderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult<string?>(null);
        }

        try
        {
            var path = ReadDocumentsPathFromRegistry();
            return Task.FromResult(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Documents folder from registry");
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>
    /// Reads the Documents folder path from the Windows Registry.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static string? ReadDocumentsPathFromRegistry()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(ShellFoldersRegistryPath);
        return key?.GetValue(PersonalValueName) as string;
    }

    /// <summary>
    /// Gets the base name for INI files based on the game type.
    /// </summary>
    /// <param name="gameType">The game type.</param>
    /// <returns>The INI base name (e.g., "Fallout4", "Skyrim").</returns>
    /// <remarks>
    /// INI files use the game's base name without "VR" suffix or "Special Edition" modifier.
    /// For example, both Skyrim SE and Skyrim VR use "Skyrim" as the INI base name.
    /// </remarks>
    private static string GetIniBaseName(GameType gameType) => gameType switch
    {
        GameType.Fallout4 or GameType.Fallout4VR => "Fallout4",
        GameType.SkyrimSE or GameType.SkyrimVR => "Skyrim",
        _ => string.Empty
    };
}

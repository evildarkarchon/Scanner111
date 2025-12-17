using Scanner111.Common.Services.Settings;

namespace Scanner111.Common.Services.PathValidation;

/// <summary>
/// Implementation of <see cref="IPathValidator"/> that provides file system path validation.
/// Thread-safe and suitable for use as a singleton service.
/// </summary>
public class PathValidator : IPathValidator
{
    private readonly IUserSettingsService _userSettings;

    /// <summary>
    /// Mapping of game names to their executable file names (non-VR).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> GameExecutables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Fallout4", "Fallout4.exe" },
        { "SkyrimSE", "SkyrimSE.exe" },
        { "Skyrim", "TESV.exe" },
        { "Starfield", "Starfield.exe" }
    };

    /// <summary>
    /// Mapping of game names to their VR executable file names.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> GameVrExecutables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Fallout4", "Fallout4VR.exe" },
        { "SkyrimSE", "SkyrimVR.exe" }
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PathValidator"/> class.
    /// </summary>
    /// <param name="userSettings">The user settings service for reading and writing settings.</param>
    public PathValidator(IUserSettingsService userSettings)
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings));
    }

    /// <inheritdoc/>
    public bool IsValidPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return System.IO.Path.Exists(path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool IsDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return Directory.Exists(path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool IsFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            return File.Exists(path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool ContainsFile(string? directoryPath, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        try
        {
            var filePath = System.IO.Path.Combine(directoryPath, fileName);
            return File.Exists(filePath);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public bool IsRestrictedPath(string? path)
    {
        return RestrictedPathChecker.IsRestrictedPath(path);
    }

    /// <inheritdoc/>
    public PathValidationResult ValidatePath(string? path, PathValidationOptions? options = null)
    {
        options ??= new PathValidationOptions();

        // Check null/empty
        if (string.IsNullOrWhiteSpace(path))
        {
            return PathValidationResult.Failure(
                PathValidationError.NullOrEmpty,
                "Path is null or empty.");
        }

        try
        {
            // Normalize path
            var normalizedPath = System.IO.Path.GetFullPath(path);

            // Check restricted paths if enabled
            if (options.CheckRestricted && RestrictedPathChecker.IsRestrictedPath(normalizedPath))
            {
                return PathValidationResult.Failure(
                    PathValidationError.RestrictedPath,
                    $"Path '{path}' is in a restricted system location.");
            }

            // Check existence
            var exists = System.IO.Path.Exists(normalizedPath);
            if (!exists)
            {
                return PathValidationResult.Failure(
                    PathValidationError.DoesNotExist,
                    $"Path '{path}' does not exist.");
            }

            // Check directory requirement
            if (options.RequireDirectory)
            {
                if (!Directory.Exists(normalizedPath))
                {
                    return PathValidationResult.Failure(
                        PathValidationError.NotADirectory,
                        $"Path '{path}' is not a directory.");
                }

                // Check required files
                if (options.RequiredFiles is { Count: > 0 })
                {
                    var missingFiles = options.RequiredFiles
                        .Where(f => !File.Exists(System.IO.Path.Combine(normalizedPath, f)))
                        .ToList();

                    if (missingFiles.Count > 0)
                    {
                        return PathValidationResult.Failure(
                            PathValidationError.MissingRequiredFiles,
                            $"Directory '{path}' is missing required files: {string.Join(", ", missingFiles)}");
                    }
                }
            }

            // Check file requirement
            if (options.RequireFile && !File.Exists(normalizedPath))
            {
                return PathValidationResult.Failure(
                    PathValidationError.NotAFile,
                    $"Path '{path}' is not a file.");
            }

            return PathValidationResult.Success();
        }
        catch (UnauthorizedAccessException)
        {
            return PathValidationResult.Failure(
                PathValidationError.AccessDenied,
                $"Access denied to path '{path}'.");
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException or NotSupportedException)
        {
            return PathValidationResult.Failure(
                PathValidationError.InvalidPath,
                $"Path '{path}' is invalid: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<SettingsValidationResult> ValidateAllSettingsPathsAsync(
        string gameName,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        var results = new Dictionary<string, PathValidationResult>();
        var invalidated = new List<string>();

        var settings = await _userSettings.GetCurrentAsync(ct).ConfigureAwait(false);
        var isVr = settings.IsVrMode;

        // Validate custom scan path
        if (!string.IsNullOrEmpty(settings.CustomScanPath))
        {
            var result = ValidateCustomScanPath(settings.CustomScanPath);
            results["CustomScanPath"] = result;
            if (!result.IsValid)
            {
                invalidated.Add("CustomScanPath");
                await _userSettings.SetCustomScanPathAsync(null, ct).ConfigureAwait(false);
            }
        }

        // Validate game root path
        if (!string.IsNullOrEmpty(settings.GameRootPath))
        {
            var result = ValidateGameRootPath(settings.GameRootPath, gameName, isVr);
            results["GameRootPath"] = result;
            if (!result.IsValid)
            {
                invalidated.Add("GameRootPath");
                await _userSettings.SetGameRootPathAsync(null, ct).ConfigureAwait(false);
            }
        }

        // Validate documents path
        if (!string.IsNullOrEmpty(settings.DocumentsPath))
        {
            var result = ValidateDocumentsPath(settings.DocumentsPath);
            results["DocumentsPath"] = result;
            if (!result.IsValid)
            {
                invalidated.Add("DocumentsPath");
                await _userSettings.SetDocumentsPathAsync(null, ct).ConfigureAwait(false);
            }
        }

        // Validate mods folder path
        if (!string.IsNullOrEmpty(settings.ModsFolderPath))
        {
            var result = ValidateModsFolderPath(settings.ModsFolderPath);
            results["ModsFolderPath"] = result;
            if (!result.IsValid)
            {
                invalidated.Add("ModsFolderPath");
                await _userSettings.SetModsFolderPathAsync(null, ct).ConfigureAwait(false);
            }
        }

        // Validate INI folder path
        if (!string.IsNullOrEmpty(settings.IniFolderPath))
        {
            var result = ValidateIniFolderPath(settings.IniFolderPath);
            results["IniFolderPath"] = result;
            if (!result.IsValid)
            {
                invalidated.Add("IniFolderPath");
                await _userSettings.SetIniFolderPathAsync(null, ct).ConfigureAwait(false);
            }
        }

        return invalidated.Count == 0
            ? SettingsValidationResult.AllSucceeded(results)
            : SettingsValidationResult.WithFailures(invalidated, results);
    }

    /// <inheritdoc/>
    public PathValidationResult ValidateGameRootPath(string? path, string gameName, bool isVrMode = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameName);

        // Get the executable name for this game
        var executableName = GetGameExecutable(gameName, isVrMode);
        if (executableName is null)
        {
            return PathValidationResult.Failure(
                PathValidationError.InvalidPath,
                $"Unknown game: {gameName}");
        }

        var options = new PathValidationOptions
        {
            RequireDirectory = true,
            RequiredFiles = new[] { executableName },
            CheckRestricted = false // Game installs may be in Program Files
        };

        return ValidatePath(path, options);
    }

    /// <inheritdoc/>
    public PathValidationResult ValidateDocumentsPath(string? path)
    {
        var options = new PathValidationOptions
        {
            RequireDirectory = true,
            CheckRestricted = false // Documents folder may be in special locations
        };

        return ValidatePath(path, options);
    }

    /// <inheritdoc/>
    public PathValidationResult ValidateModsFolderPath(string? path)
    {
        var options = new PathValidationOptions
        {
            RequireDirectory = true,
            CheckRestricted = true
        };

        return ValidatePath(path, options);
    }

    /// <inheritdoc/>
    public PathValidationResult ValidateIniFolderPath(string? path)
    {
        var options = new PathValidationOptions
        {
            RequireDirectory = true,
            CheckRestricted = false // INI folder may be in documents
        };

        return ValidatePath(path, options);
    }

    /// <inheritdoc/>
    public PathValidationResult ValidateCustomScanPath(string? path)
    {
        var options = new PathValidationOptions
        {
            RequireDirectory = true,
            CheckRestricted = true
        };

        return ValidatePath(path, options);
    }

    /// <summary>
    /// Gets the executable file name for a game.
    /// </summary>
    /// <param name="gameName">The game name.</param>
    /// <param name="isVrMode">True if VR mode.</param>
    /// <returns>The executable name, or null if unknown game.</returns>
    private static string? GetGameExecutable(string gameName, bool isVrMode)
    {
        if (isVrMode && GameVrExecutables.TryGetValue(gameName, out var vrExe))
        {
            return vrExe;
        }

        return GameExecutables.TryGetValue(gameName, out var exe) ? exe : null;
    }
}

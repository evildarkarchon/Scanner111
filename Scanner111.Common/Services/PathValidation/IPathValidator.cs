namespace Scanner111.Common.Services.PathValidation;

/// <summary>
/// Service for validating file system paths.
/// Provides methods to check path existence, directory/file status,
/// restricted location detection, and comprehensive validation.
/// </summary>
public interface IPathValidator
{
    /// <summary>
    /// Checks if a path exists on the file system.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path exists (as file or directory), false otherwise.</returns>
    bool IsValidPath(string? path);

    /// <summary>
    /// Checks if a path exists and is a directory.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path exists and is a directory, false otherwise.</returns>
    bool IsDirectory(string? path);

    /// <summary>
    /// Checks if a path exists and is a file.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path exists and is a file, false otherwise.</returns>
    bool IsFile(string? path);

    /// <summary>
    /// Checks if a directory contains a specific file.
    /// </summary>
    /// <param name="directoryPath">The directory path to check.</param>
    /// <param name="fileName">The file name to look for.</param>
    /// <returns>True if the file exists in the directory, false otherwise.</returns>
    bool ContainsFile(string? directoryPath, string fileName);

    /// <summary>
    /// Checks if a path is in a restricted system location.
    /// </summary>
    /// <param name="path">The path to check.</param>
    /// <returns>True if the path is restricted, false otherwise.</returns>
    /// <remarks>
    /// Restricted paths include Windows system directories like
    /// C:\Windows, Program Files, ProgramData, etc.
    /// Null or empty paths are considered restricted (fail-safe behavior).
    /// </remarks>
    bool IsRestrictedPath(string? path);

    /// <summary>
    /// Performs comprehensive path validation with configurable options.
    /// </summary>
    /// <param name="path">The path to validate.</param>
    /// <param name="options">Validation options. If null, uses default options.</param>
    /// <returns>A <see cref="PathValidationResult"/> with validation details.</returns>
    PathValidationResult ValidatePath(string? path, PathValidationOptions? options = null);

    /// <summary>
    /// Validates all user settings paths and clears any invalid paths.
    /// </summary>
    /// <param name="gameName">The name of the game for game-specific validation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="SettingsValidationResult"/> with validation details.</returns>
    Task<SettingsValidationResult> ValidateAllSettingsPathsAsync(
        string gameName,
        CancellationToken ct = default);

    /// <summary>
    /// Validates a game root path (must contain the game executable).
    /// </summary>
    /// <param name="path">The game root path to validate.</param>
    /// <param name="gameName">The game name (e.g., "Fallout4", "SkyrimSE").</param>
    /// <param name="isVrMode">True if validating for VR variant.</param>
    /// <returns>A <see cref="PathValidationResult"/> with validation details.</returns>
    PathValidationResult ValidateGameRootPath(string? path, string gameName, bool isVrMode = false);

    /// <summary>
    /// Validates a documents path (must be an existing directory).
    /// </summary>
    /// <param name="path">The documents path to validate.</param>
    /// <returns>A <see cref="PathValidationResult"/> with validation details.</returns>
    PathValidationResult ValidateDocumentsPath(string? path);

    /// <summary>
    /// Validates a mods folder path (must be an existing directory).
    /// </summary>
    /// <param name="path">The mods folder path to validate.</param>
    /// <returns>A <see cref="PathValidationResult"/> with validation details.</returns>
    PathValidationResult ValidateModsFolderPath(string? path);

    /// <summary>
    /// Validates an INI folder path (must be an existing directory).
    /// </summary>
    /// <param name="path">The INI folder path to validate.</param>
    /// <returns>A <see cref="PathValidationResult"/> with validation details.</returns>
    PathValidationResult ValidateIniFolderPath(string? path);

    /// <summary>
    /// Validates a custom scan path (must exist and not be restricted).
    /// </summary>
    /// <param name="path">The custom scan path to validate.</param>
    /// <returns>A <see cref="PathValidationResult"/> with validation details.</returns>
    PathValidationResult ValidateCustomScanPath(string? path);
}

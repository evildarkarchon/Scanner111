namespace Scanner111.Common.Services.PathValidation;

/// <summary>
/// Specifies the type of error encountered during path validation.
/// </summary>
public enum PathValidationError
{
    /// <summary>
    /// No error - validation succeeded.
    /// </summary>
    None,

    /// <summary>
    /// The path was null, empty, or whitespace.
    /// </summary>
    NullOrEmpty,

    /// <summary>
    /// The path does not exist on the file system.
    /// </summary>
    DoesNotExist,

    /// <summary>
    /// The path exists but is not a directory when a directory was required.
    /// </summary>
    NotADirectory,

    /// <summary>
    /// The path exists but is not a file when a file was required.
    /// </summary>
    NotAFile,

    /// <summary>
    /// The directory exists but is missing one or more required files.
    /// </summary>
    MissingRequiredFiles,

    /// <summary>
    /// The path is in a restricted system location.
    /// </summary>
    RestrictedPath,

    /// <summary>
    /// Access to the path was denied due to permissions.
    /// </summary>
    AccessDenied,

    /// <summary>
    /// The path string is invalid (contains illegal characters, etc.).
    /// </summary>
    InvalidPath
}

/// <summary>
/// Options for customizing path validation behavior.
/// </summary>
public record PathValidationOptions
{
    /// <summary>
    /// Gets a value indicating whether the path must be a directory.
    /// </summary>
    public bool RequireDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether the path must be a file.
    /// </summary>
    public bool RequireFile { get; init; }

    /// <summary>
    /// Gets the list of file names that must exist within the directory.
    /// Only applicable when <see cref="RequireDirectory"/> is true.
    /// </summary>
    public IReadOnlyList<string>? RequiredFiles { get; init; }

    /// <summary>
    /// Gets a value indicating whether to check if the path is in a restricted location.
    /// Defaults to true.
    /// </summary>
    public bool CheckRestricted { get; init; } = true;

    /// <summary>
    /// Gets the default options for directory validation.
    /// </summary>
    public static PathValidationOptions Directory => new() { RequireDirectory = true };

    /// <summary>
    /// Gets the default options for file validation.
    /// </summary>
    public static PathValidationOptions File => new() { RequireFile = true };
}

/// <summary>
/// Represents the result of a path validation operation.
/// </summary>
public record PathValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation succeeded.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the error type if validation failed.
    /// </summary>
    public PathValidationError Error { get; init; }

    /// <summary>
    /// Gets a human-readable error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful <see cref="PathValidationResult"/>.</returns>
    public static PathValidationResult Success() => new()
    {
        IsValid = true,
        Error = PathValidationError.None
    };

    /// <summary>
    /// Creates a failed validation result with the specified error.
    /// </summary>
    /// <param name="error">The type of validation error.</param>
    /// <param name="message">A human-readable error message.</param>
    /// <returns>A failed <see cref="PathValidationResult"/>.</returns>
    public static PathValidationResult Failure(PathValidationError error, string message) => new()
    {
        IsValid = false,
        Error = error,
        ErrorMessage = message
    };
}

/// <summary>
/// Represents the result of validating all user settings paths.
/// </summary>
public record SettingsValidationResult
{
    /// <summary>
    /// Gets a value indicating whether all validated paths were valid.
    /// </summary>
    public bool AllValid { get; init; }

    /// <summary>
    /// Gets the list of setting names whose paths were invalidated and cleared.
    /// </summary>
    public IReadOnlyList<string> InvalidatedSettings { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the detailed validation results for each setting, keyed by setting name.
    /// </summary>
    public IReadOnlyDictionary<string, PathValidationResult> Results { get; init; }
        = new Dictionary<string, PathValidationResult>();

    /// <summary>
    /// Creates a result indicating all settings were valid.
    /// </summary>
    /// <param name="results">The validation results for each setting.</param>
    /// <returns>A successful <see cref="SettingsValidationResult"/>.</returns>
    public static SettingsValidationResult AllSucceeded(Dictionary<string, PathValidationResult> results) => new()
    {
        AllValid = true,
        Results = results
    };

    /// <summary>
    /// Creates a result indicating some settings had invalid paths.
    /// </summary>
    /// <param name="invalidatedSettings">The names of settings that were invalidated.</param>
    /// <param name="results">The validation results for each setting.</param>
    /// <returns>A <see cref="SettingsValidationResult"/> with failures.</returns>
    public static SettingsValidationResult WithFailures(
        IReadOnlyList<string> invalidatedSettings,
        Dictionary<string, PathValidationResult> results) => new()
    {
        AllValid = false,
        InvalidatedSettings = invalidatedSettings,
        Results = results
    };
}

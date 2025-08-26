namespace Scanner111.Core.Models;

/// <summary>
///     Represents the result of a path discovery operation.
///     Thread-safe immutable model.
/// </summary>
public sealed record PathDiscoveryResult
{
    /// <summary>
    ///     Gets a value indicating whether the discovery was successful.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    ///     Gets the discovered paths if successful.
    /// </summary>
    public GamePaths? Paths { get; init; }

    /// <summary>
    ///     Gets the discovery method used.
    /// </summary>
    public required DiscoveryMethod Method { get; init; }

    /// <summary>
    ///     Gets the error message if discovery failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    ///     Gets additional details about the discovery process.
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    ///     Gets the time taken for discovery in milliseconds.
    /// </summary>
    public long ElapsedMilliseconds { get; init; }

    /// <summary>
    ///     Creates a successful discovery result.
    /// </summary>
    public static PathDiscoveryResult Success(GamePaths paths, DiscoveryMethod method, long elapsedMs,
        string? details = null)
    {
        return new PathDiscoveryResult
        {
            IsSuccess = true,
            Paths = paths,
            Method = method,
            Details = details,
            ElapsedMilliseconds = elapsedMs
        };
    }

    /// <summary>
    ///     Creates a failed discovery result.
    /// </summary>
    public static PathDiscoveryResult Failure(DiscoveryMethod method, string errorMessage, long elapsedMs)
    {
        return new PathDiscoveryResult
        {
            IsSuccess = false,
            Method = method,
            ErrorMessage = errorMessage,
            ElapsedMilliseconds = elapsedMs
        };
    }
}

/// <summary>
///     Represents the method used for path discovery.
/// </summary>
public enum DiscoveryMethod
{
    /// <summary>
    ///     Discovery via Windows registry.
    /// </summary>
    Registry,

    /// <summary>
    ///     Discovery via Script Extender log file.
    /// </summary>
    ScriptExtenderLog,

    /// <summary>
    ///     Discovery via Steam library configuration.
    /// </summary>
    SteamLibrary,

    /// <summary>
    ///     Discovery via GOG Galaxy configuration.
    /// </summary>
    GogGalaxy,

    /// <summary>
    ///     Discovery via configured settings.
    /// </summary>
    ConfiguredPath,

    /// <summary>
    ///     Manual user input.
    /// </summary>
    Manual,

    /// <summary>
    ///     Discovery method unknown or not applicable.
    /// </summary>
    Unknown
}

/// <summary>
///     Represents the result of a path validation operation.
/// </summary>
public sealed record PathValidationResult
{
    /// <summary>
    ///     Gets a value indicating whether the path is valid.
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    ///     Gets the validated path.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the path exists.
    /// </summary>
    public bool Exists { get; init; }

    /// <summary>
    ///     Gets a value indicating whether read access is available.
    /// </summary>
    public bool CanRead { get; init; }

    /// <summary>
    ///     Gets a value indicating whether write access is available.
    /// </summary>
    public bool CanWrite { get; init; }

    /// <summary>
    ///     Gets the validation error message if validation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    ///     Gets additional validation details.
    /// </summary>
    public List<string> ValidationIssues { get; init; } = new();

    /// <summary>
    ///     Creates a successful validation result.
    /// </summary>
    public static PathValidationResult Success(string path, bool canRead, bool canWrite)
    {
        return new PathValidationResult
        {
            IsValid = true,
            Path = path,
            Exists = true,
            CanRead = canRead,
            CanWrite = canWrite
        };
    }

    /// <summary>
    ///     Creates a failed validation result.
    /// </summary>
    public static PathValidationResult Failure(string? path, string errorMessage, List<string>? issues = null)
    {
        return new PathValidationResult
        {
            IsValid = false,
            Path = path,
            ErrorMessage = errorMessage,
            ValidationIssues = issues ?? new List<string>()
        };
    }
}
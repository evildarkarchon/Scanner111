namespace Scanner111.Common.Models.GamePath;

/// <summary>
/// Represents the result of a game path detection operation.
/// </summary>
public record GamePathResult
{
    /// <summary>
    /// Gets the game type that was detected or searched for.
    /// </summary>
    public GameType GameType { get; init; }

    /// <summary>
    /// Gets whether the game installation was found.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Gets the detected game installation path, or <c>null</c> if not found.
    /// </summary>
    public string? GamePath { get; init; }

    /// <summary>
    /// Gets the method used to detect the game path.
    /// </summary>
    public GamePathDetectionMethod DetectionMethod { get; init; }

    /// <summary>
    /// Gets the path to the game's Data folder, or <c>null</c> if not found.
    /// </summary>
    public string? DataPath => Found && GamePath != null
        ? Path.Combine(GamePath, "Data")
        : null;

    /// <summary>
    /// Gets the path to the game executable, or <c>null</c> if not found.
    /// </summary>
    public string? ExecutablePath => Found && GamePath != null
        ? Path.Combine(GamePath, GameType.GetExecutableName())
        : null;

    /// <summary>
    /// Gets any error message if detection failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful game path result.
    /// </summary>
    public static GamePathResult Success(
        GameType gameType,
        string gamePath,
        GamePathDetectionMethod detectionMethod) => new()
    {
        GameType = gameType,
        Found = true,
        GamePath = gamePath,
        DetectionMethod = detectionMethod
    };

    /// <summary>
    /// Creates a failed game path result.
    /// </summary>
    public static GamePathResult Failure(
        GameType gameType,
        string? errorMessage = null) => new()
    {
        GameType = gameType,
        Found = false,
        DetectionMethod = GamePathDetectionMethod.NotFound,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Represents the method used to detect a game path.
/// </summary>
public enum GamePathDetectionMethod
{
    /// <summary>
    /// Game path was not found.
    /// </summary>
    NotFound = 0,

    /// <summary>
    /// Game path was found in the Windows registry (Bethesda Softworks key).
    /// </summary>
    Registry,

    /// <summary>
    /// Game path was found in the GOG Galaxy registry key.
    /// </summary>
    GogRegistry,

    /// <summary>
    /// Game path was extracted from the XSE log file.
    /// </summary>
    XseLog,

    /// <summary>
    /// Game path was retrieved from cached settings.
    /// </summary>
    Cache,

    /// <summary>
    /// Game path was provided manually by the user.
    /// </summary>
    Manual
}

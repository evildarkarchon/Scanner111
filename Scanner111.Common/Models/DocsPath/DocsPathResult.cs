using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Models.DocsPath;

/// <summary>
/// Represents the result of a documents path detection operation.
/// </summary>
public record DocsPathResult
{
    /// <summary>
    /// Gets the game type that was detected or searched for.
    /// </summary>
    public GameType GameType { get; init; }

    /// <summary>
    /// Gets whether the documents path was found.
    /// </summary>
    public bool Found { get; init; }

    /// <summary>
    /// Gets the detected documents path, or <c>null</c> if not found.
    /// </summary>
    public string? DocsPath { get; init; }

    /// <summary>
    /// Gets the method used to detect the documents path.
    /// </summary>
    public DocsPathDetectionMethod DetectionMethod { get; init; }

    /// <summary>
    /// Gets any error message if detection failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets whether the documents path is located in a OneDrive folder.
    /// </summary>
    /// <remarks>
    /// OneDrive folder synchronization can cause issues with game save files and INI configurations.
    /// This property performs a case-insensitive check for "onedrive" in the path.
    /// </remarks>
    public bool IsOneDrivePath => Found && DocsPath != null &&
        DocsPath.Contains("onedrive", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a successful documents path result.
    /// </summary>
    /// <param name="gameType">The game type that was detected.</param>
    /// <param name="docsPath">The detected documents path.</param>
    /// <param name="detectionMethod">The method used to detect the path.</param>
    /// <returns>A successful <see cref="DocsPathResult"/>.</returns>
    public static DocsPathResult Success(
        GameType gameType,
        string docsPath,
        DocsPathDetectionMethod detectionMethod) => new()
    {
        GameType = gameType,
        Found = true,
        DocsPath = docsPath,
        DetectionMethod = detectionMethod
    };

    /// <summary>
    /// Creates a failed documents path result.
    /// </summary>
    /// <param name="gameType">The game type that was searched for.</param>
    /// <param name="errorMessage">Optional error message describing the failure.</param>
    /// <returns>A failed <see cref="DocsPathResult"/>.</returns>
    public static DocsPathResult Failure(
        GameType gameType,
        string? errorMessage = null) => new()
    {
        GameType = gameType,
        Found = false,
        DetectionMethod = DocsPathDetectionMethod.NotFound,
        ErrorMessage = errorMessage
    };
}

/// <summary>
/// Represents the method used to detect a documents path.
/// </summary>
public enum DocsPathDetectionMethod
{
    /// <summary>
    /// Documents path was not found.
    /// </summary>
    NotFound = 0,

    /// <summary>
    /// Documents path was retrieved from cached settings.
    /// </summary>
    Cache,

    /// <summary>
    /// Documents path was found via Windows Registry Shell Folders key.
    /// </summary>
    Registry,

    /// <summary>
    /// Documents path was found via Environment.SpecialFolder.MyDocuments.
    /// </summary>
    EnvironmentFallback,

    /// <summary>
    /// Documents path was provided manually by the user.
    /// </summary>
    Manual
}

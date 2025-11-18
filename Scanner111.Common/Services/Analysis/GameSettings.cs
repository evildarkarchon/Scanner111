namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Represents expected game and crash logger settings.
/// </summary>
public record GameSettings
{
    /// <summary>
    /// Gets the name of the game (e.g., "Fallout 4", "Skyrim Special Edition").
    /// </summary>
    public string GameName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the recommended settings for the crash logger.
    /// Key is the setting name, value is the recommended value.
    /// </summary>
    public IReadOnlyDictionary<string, string> RecommendedSettings { get; init; } =
        new Dictionary<string, string>();

    /// <summary>
    /// Gets the latest version of the crash logger (Buffout 4, Crash Logger, etc.).
    /// </summary>
    public string LatestCrashLoggerVersion { get; init; } = string.Empty;

    /// <summary>
    /// Creates default settings for Fallout 4 with Buffout 4.
    /// </summary>
    /// <returns>Default <see cref="GameSettings"/> for Fallout 4.</returns>
    public static GameSettings CreateFallout4Defaults()
    {
        return new GameSettings
        {
            GameName = "Fallout 4",
            LatestCrashLoggerVersion = "1.28.6",
            RecommendedSettings = new Dictionary<string, string>
            {
                { "MemoryManager", "false" },
                { "AutoScanning", "true" },
                { "F4SE", "true" }
            }
        };
    }

    /// <summary>
    /// Creates default settings for Skyrim SE with Crash Logger.
    /// </summary>
    /// <returns>Default <see cref="GameSettings"/> for Skyrim SE.</returns>
    public static GameSettings CreateSkyrimDefaults()
    {
        return new GameSettings
        {
            GameName = "Skyrim Special Edition",
            LatestCrashLoggerVersion = "1.0.0",
            RecommendedSettings = new Dictionary<string, string>
            {
                { "AutoScanning", "true" },
                { "SKSE", "true" }
            }
        };
    }
}

using Microsoft.Extensions.Options;

namespace Scanner111.ClassicLib;

/// <summary>
/// Implementation of IGameRegistry that manages game-related information.
/// </summary>
public class GameRegistry : IGameRegistry
{
    private readonly GameOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameRegistry"/> class.
    /// </summary>
    /// <param name="options">Game options configuration.</param>
    public GameRegistry(IOptions<GameOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc />
    public string GetGame()
    {
        return !string.IsNullOrEmpty(_options.Game) ? _options.Game : "Fallout4";
    }

    /// <inheritdoc />
    public string GetVR()
    {
        return _options.VR ?? string.Empty;
    }
}

/// <summary>
/// Options for game configuration.
/// </summary>
public class GameOptions
{
    /// <summary>
    /// Gets or sets the game identifier.
    /// </summary>
    public string Game { get; set; } = "Fallout4";

    /// <summary>
    /// Gets or sets the VR status.
    /// </summary>
    public string? VR { get; set; }
}
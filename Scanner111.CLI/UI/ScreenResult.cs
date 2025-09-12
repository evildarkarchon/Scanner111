namespace Scanner111.CLI.UI;

/// <summary>
/// Represents the result of a screen interaction.
/// </summary>
public class ScreenResult
{
    /// <summary>
    /// Gets or sets the next action to take.
    /// </summary>
    public MenuAction NextAction { get; set; } = MenuAction.None;
    
    /// <summary>
    /// Gets or sets any data to pass to the next screen.
    /// </summary>
    public object? Data { get; set; }
    
    /// <summary>
    /// Gets a result indicating the user wants to go back.
    /// </summary>
    public static ScreenResult Back => new() { NextAction = MenuAction.Back };
    
    /// <summary>
    /// Gets a result indicating the user wants to exit.
    /// </summary>
    public static ScreenResult Exit => new() { NextAction = MenuAction.Exit };
}

/// <summary>
/// Represents possible menu actions.
/// </summary>
public enum MenuAction
{
    None,
    Back,
    Exit,
    Analyze,
    ViewResults,
    Configure,
    Help,
    Monitor,
    SessionHistory,
    Export,
    Refresh
}
namespace Scanner111.CLI.UI;

/// <summary>
/// Represents a screen in the terminal user interface.
/// </summary>
public interface IScreen
{
    /// <summary>
    /// Gets the title of the screen.
    /// </summary>
    string Title { get; }
    
    /// <summary>
    /// Displays the screen and handles user interaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the screen interaction.</returns>
    Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default);
}
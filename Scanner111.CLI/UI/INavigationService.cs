namespace Scanner111.CLI.UI;

/// <summary>
/// Service for managing screen navigation in the terminal UI.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Gets the current screen.
    /// </summary>
    IScreen? CurrentScreen { get; }
    
    /// <summary>
    /// Gets whether there are screens in the navigation history to go back to.
    /// </summary>
    bool CanGoBack { get; }
    
    /// <summary>
    /// Navigates to a specific screen type.
    /// </summary>
    /// <typeparam name="TScreen">The type of screen to navigate to.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NavigateToAsync<TScreen>(CancellationToken cancellationToken = default) where TScreen : IScreen;
    
    /// <summary>
    /// Navigates to a specific screen type with initialization data.
    /// </summary>
    /// <typeparam name="TScreen">The type of screen to navigate to.</typeparam>
    /// <param name="data">Data to pass to the screen.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NavigateToAsync<TScreen>(object data, CancellationToken cancellationToken = default) where TScreen : IScreen;
    
    /// <summary>
    /// Navigates to a screen instance.
    /// </summary>
    /// <param name="screen">The screen to navigate to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task NavigateToAsync(IScreen screen, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Goes back to the previous screen.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task GoBackAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clears the navigation history.
    /// </summary>
    void ClearHistory();
    
    /// <summary>
    /// Exits the application.
    /// </summary>
    void Exit();
}
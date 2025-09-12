using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Scanner111.CLI.UI;

/// <summary>
/// Service for managing screen navigation in the terminal UI.
/// </summary>
public class NavigationService : INavigationService
{
    private readonly Stack<IScreen> _screenStack = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly IAnsiConsole _console;
    private readonly ILogger<NavigationService> _logger;
    private CancellationTokenSource? _applicationCts;
    private object? _navigationData;
    
    /// <summary>
    /// Gets the current screen.
    /// </summary>
    public IScreen? CurrentScreen => _screenStack.TryPeek(out var screen) ? screen : null;
    
    /// <summary>
    /// Gets whether there are screens in the navigation history to go back to.
    /// </summary>
    public bool CanGoBack => _screenStack.Count > 1;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationService"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <param name="console">The Spectre.Console instance.</param>
    /// <param name="logger">The logger.</param>
    public NavigationService(
        IServiceProvider serviceProvider, 
        IAnsiConsole console,
        ILogger<NavigationService> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _applicationCts = new CancellationTokenSource();
    }
    
    /// <summary>
    /// Navigates to a specific screen type.
    /// </summary>
    /// <typeparam name="TScreen">The type of screen to navigate to.</typeparam>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task NavigateToAsync<TScreen>(CancellationToken cancellationToken = default) 
        where TScreen : IScreen
    {
        await NavigateToAsync<TScreen>(null, cancellationToken).ConfigureAwait(false);
    }
    
    /// <summary>
    /// Navigates to a specific screen type with initialization data.
    /// </summary>
    /// <typeparam name="TScreen">The type of screen to navigate to.</typeparam>
    /// <param name="data">Data to pass to the screen.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task NavigateToAsync<TScreen>(object? data, CancellationToken cancellationToken = default) 
        where TScreen : IScreen
    {
        try
        {
            _logger.LogDebug("Navigating to screen: {ScreenType}", typeof(TScreen).Name);
            
            var screen = _serviceProvider.GetRequiredService<TScreen>();
            _navigationData = data;
            
            await NavigateToAsync(screen, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to screen: {ScreenType}", typeof(TScreen).Name);
            throw;
        }
    }
    
    /// <summary>
    /// Navigates to a screen instance.
    /// </summary>
    /// <param name="screen">The screen to navigate to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task NavigateToAsync(IScreen screen, CancellationToken cancellationToken = default)
    {
        if (screen == null)
            throw new ArgumentNullException(nameof(screen));
        
        _screenStack.Push(screen);
        
        try
        {
            // If the screen implements IDataReceiver, pass the navigation data
            if (screen is IDataReceiver dataReceiver && _navigationData != null)
            {
                dataReceiver.SetData(_navigationData);
                _navigationData = null; // Clear after use
            }
            
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, 
                _applicationCts?.Token ?? CancellationToken.None);
            
            var result = await screen.DisplayAsync(linkedCts.Token).ConfigureAwait(false);
            
            // Handle the screen result
            await HandleScreenResult(result, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Screen display was cancelled: {ScreenTitle}", screen.Title);
            await GoBackAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying screen: {ScreenTitle}", screen.Title);
            _console.WriteException(ex);
            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            await GoBackAsync(cancellationToken).ConfigureAwait(false);
        }
    }
    
    /// <summary>
    /// Goes back to the previous screen.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GoBackAsync(CancellationToken cancellationToken = default)
    {
        if (_screenStack.Count > 1)
        {
            var currentScreen = _screenStack.Pop();
            _logger.LogDebug("Going back from screen: {ScreenTitle}", currentScreen.Title);
            
            var previousScreen = _screenStack.Peek();
            _screenStack.Pop(); // Remove it so NavigateToAsync will push it again
            
            await NavigateToAsync(previousScreen, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogDebug("Cannot go back - at root screen");
            Exit();
        }
    }
    
    /// <summary>
    /// Clears the navigation history.
    /// </summary>
    public void ClearHistory()
    {
        _logger.LogDebug("Clearing navigation history");
        _screenStack.Clear();
    }
    
    /// <summary>
    /// Exits the application.
    /// </summary>
    public void Exit()
    {
        _logger.LogInformation("Exiting application");
        _applicationCts?.Cancel();
        Environment.Exit(0);
    }
    
    /// <summary>
    /// Handles the result from a screen interaction.
    /// </summary>
    /// <param name="result">The screen result.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task HandleScreenResult(ScreenResult result, CancellationToken cancellationToken)
    {
        if (result == null)
        {
            await GoBackAsync(cancellationToken).ConfigureAwait(false);
            return;
        }
        
        _navigationData = result.Data;
        
        switch (result.NextAction)
        {
            case MenuAction.Back:
                await GoBackAsync(cancellationToken).ConfigureAwait(false);
                break;
                
            case MenuAction.Exit:
                Exit();
                break;
                
            case MenuAction.Analyze:
                await NavigateToAsync<Screens.AnalysisScreen>(result.Data, cancellationToken).ConfigureAwait(false);
                break;
                
            case MenuAction.ViewResults:
                await NavigateToAsync<Screens.ResultsScreen>(result.Data, cancellationToken).ConfigureAwait(false);
                break;
                
            case MenuAction.Configure:
                await NavigateToAsync<Screens.ConfigurationScreen>(result.Data, cancellationToken).ConfigureAwait(false);
                break;
                
            case MenuAction.Help:
                await NavigateToAsync<Screens.HelpScreen>(result.Data, cancellationToken).ConfigureAwait(false);
                break;
                
            case MenuAction.Monitor:
                await NavigateToAsync<Screens.LogMonitorScreen>(result.Data, cancellationToken).ConfigureAwait(false);
                break;
                
            case MenuAction.SessionHistory:
                await NavigateToAsync<Screens.SessionHistoryScreen>(result.Data, cancellationToken).ConfigureAwait(false);
                break;
                
            case MenuAction.None:
            default:
                // Stay on current screen or go back
                if (_screenStack.Count > 0)
                {
                    _screenStack.Pop(); // Remove current screen
                    if (_screenStack.Count > 0)
                    {
                        var previousScreen = _screenStack.Pop();
                        await NavigateToAsync(previousScreen, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        Exit();
                    }
                }
                break;
        }
    }
}

/// <summary>
/// Interface for screens that can receive data.
/// </summary>
public interface IDataReceiver
{
    /// <summary>
    /// Sets the data for the screen.
    /// </summary>
    /// <param name="data">The data to set.</param>
    void SetData(object data);
}
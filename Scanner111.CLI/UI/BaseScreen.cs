using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Scanner111.CLI.UI;

/// <summary>
/// Base class for all terminal UI screens.
/// </summary>
public abstract class BaseScreen : IScreen
{
    /// <summary>
    /// Gets the Spectre.Console instance.
    /// </summary>
    protected readonly IAnsiConsole Console;
    
    /// <summary>
    /// Gets the service provider.
    /// </summary>
    protected readonly IServiceProvider Services;
    
    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected readonly ILogger Logger;
    
    /// <summary>
    /// Gets the title of the screen.
    /// </summary>
    public abstract string Title { get; }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="BaseScreen"/> class.
    /// </summary>
    /// <param name="console">The Spectre.Console instance.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    protected BaseScreen(IAnsiConsole console, IServiceProvider services, ILogger logger)
    {
        Console = console ?? throw new ArgumentNullException(nameof(console));
        Services = services ?? throw new ArgumentNullException(nameof(services));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Displays the screen and handles user interaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>The result of the screen interaction.</returns>
    public abstract Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Draws the screen header with the title.
    /// </summary>
    protected virtual void DrawHeader()
    {
        Console.Clear();
        var rule = new Rule($"[bold yellow]{Title}[/]")
            .RuleStyle("cyan")
            .LeftJustified();
        Console.Write(rule);
        Console.WriteLine();
    }
    
    /// <summary>
    /// Draws the screen footer with common navigation hints.
    /// </summary>
    protected virtual void DrawFooter()
    {
        Console.WriteLine();
        var rule = new Rule()
            .RuleStyle("dim");
        Console.Write(rule);
        Console.MarkupLine("[dim]Press [yellow]ESC[/] to go back • [yellow]F1[/] for help • [yellow]Q[/] to quit[/]");
    }
    
    /// <summary>
    /// Shows an error message to the user.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected void ShowError(string message)
    {
        Logger.LogError(message);
        Console.MarkupLine($"[red]✗[/] {message}");
    }
    
    /// <summary>
    /// Shows a success message to the user.
    /// </summary>
    /// <param name="message">The success message.</param>
    protected void ShowSuccess(string message)
    {
        Logger.LogInformation(message);
        Console.MarkupLine($"[green]✓[/] {message}");
    }
    
    /// <summary>
    /// Shows a warning message to the user.
    /// </summary>
    /// <param name="message">The warning message.</param>
    protected void ShowWarning(string message)
    {
        Logger.LogWarning(message);
        Console.MarkupLine($"[yellow]⚠[/] {message}");
    }
    
    /// <summary>
    /// Prompts the user for confirmation.
    /// </summary>
    /// <param name="prompt">The confirmation prompt.</param>
    /// <returns>True if the user confirms; otherwise, false.</returns>
    protected bool Confirm(string prompt)
    {
        return Console.Confirm(prompt);
    }
    
    /// <summary>
    /// Waits for a key press with optional timeout.
    /// </summary>
    /// <param name="prompt">The prompt to display.</param>
    /// <param name="timeout">Optional timeout in milliseconds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The key that was pressed, or null if timed out or cancelled.</returns>
    protected async Task<ConsoleKeyInfo?> WaitForKeyAsync(
        string prompt = "Press any key to continue...", 
        int? timeout = null,
        CancellationToken cancellationToken = default)
    {
        Console.MarkupLine($"[dim]{prompt}[/]");
        
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue)
        {
            cts.CancelAfter(timeout.Value);
        }
        
        try
        {
            return await Task.Run(() =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (System.Console.KeyAvailable)
                    {
                        return System.Console.ReadKey(true);
                    }
                    Thread.Sleep(50);
                }
                return (ConsoleKeyInfo?)null;
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
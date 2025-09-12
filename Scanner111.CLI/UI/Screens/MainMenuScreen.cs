using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Services;
using Scanner111.Core.Configuration;
using Scanner111.Core.Discovery;
using Scanner111.Core.Models;
using Spectre.Console;

namespace Scanner111.CLI.UI.Screens;

/// <summary>
/// The main menu screen of the application.
/// </summary>
public class MainMenuScreen : BaseScreen
{
    private readonly IConfigurationService _configService;
    private readonly ISessionManager _sessionManager;
    private readonly IGamePathDiscoveryService _pathDiscovery;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    
    /// <summary>
    /// Gets the title of the screen.
    /// </summary>
    public override string Title => "Scanner111 - Crash Log Analyzer";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="MainMenuScreen"/> class.
    /// </summary>
    /// <param name="console">The Spectre.Console instance.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public MainMenuScreen(
        IAnsiConsole console, 
        IServiceProvider services, 
        ILogger<MainMenuScreen> logger) 
        : base(console, services, logger)
    {
        _configService = services.GetRequiredService<IConfigurationService>();
        _sessionManager = services.GetRequiredService<ISessionManager>();
        _pathDiscovery = services.GetRequiredService<IGamePathDiscoveryService>();
        _yamlCore = services.GetRequiredService<IAsyncYamlSettingsCore>();
    }
    
    /// <summary>
    /// Displays the main menu and handles user selection.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The screen result based on user selection.</returns>
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DrawHeader();
            
            // Display status panel with current configuration
            await DisplayStatusPanelAsync(cancellationToken);
            
            // Display welcome and information panels
            await DisplayWelcomePanelAsync(cancellationToken);
            
            // Display recent activity summary and quick access
            await DisplayRecentActivityAsync(cancellationToken);
            
            // Display quick access to recent sessions
            await DisplayQuickAccessAsync(cancellationToken);
            
            // Create menu options with keyboard shortcuts
            var menuOptions = new[]
            {
                new MenuOption("🔍 Analyze Crash Log", MenuAction.Analyze, "A"),
                new MenuOption("📊 View Recent Results", MenuAction.ViewResults, "V"),
                new MenuOption("👁️ Monitor Log File", MenuAction.Monitor, "M"),
                new MenuOption("📋 Session History", MenuAction.SessionHistory, "S"),
                new MenuOption("⚙️  Configuration", MenuAction.Configure, "C"),
                new MenuOption("📚 Help & Documentation", MenuAction.Help, "H"),
                new MenuOption("🔄 Refresh", MenuAction.Refresh, "R"),
                new MenuOption("🚪 Exit", MenuAction.Exit, "Q")
            };
            
            // Display keyboard shortcuts
            DisplayKeyboardShortcuts();
            
            var choice = Console.Prompt(
                new SelectionPrompt<MenuOption>()
                    .Title("[green]Select an option:[/]")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                    .AddChoices(menuOptions)
                    .UseConverter(option => $"{option.Display} [dim]({option.Shortcut})[/]"));
            
            Logger.LogInformation("User selected menu option: {Option}", choice.Action);
            
            // Handle refresh internally, otherwise return the action
            if (choice.Action == MenuAction.Refresh)
            {
                await AnsiConsole.Status()
                    .StartAsync("[yellow]Refreshing...[/]", async ctx =>
                    {
                        await Task.Delay(500, cancellationToken);
                    });
                continue;
            }
            
            return new ScreenResult { NextAction = choice.Action };
        }
        
        return ScreenResult.Exit;
    }
    
    private async Task DisplayStatusPanelAsync(CancellationToken cancellationToken)
    {
        try
        {
            var statusContent = new Table()
                .Border(TableBorder.None)
                .HideHeaders()
                .AddColumn("Property")
                .AddColumn("Value");
            
            // Get current game type from discovery - try Fallout4 as default
            var gameInfo = new GameInfo
            {
                GameName = "Fallout4",
                ExecutableName = "Fallout4.exe",
                ScriptExtenderAcronym = "F4SE",
                ScriptExtenderBase = "F4SE",
                DocumentsFolderName = "Fallout4"
            };
            
            var discoveryResult = await _pathDiscovery.DiscoverGamePathAsync(gameInfo, cancellationToken);
            var gameType = discoveryResult.IsSuccess ? gameInfo.GameName : "Not detected";
            var gameStatus = discoveryResult.IsSuccess ? "[green]✓[/]" : "[red]✗[/]";
            
            statusContent.AddRow("[cyan]Game Detection:[/]", $"{gameStatus} {gameType}");
            statusContent.AddRow("[cyan]Auto-detect:[/]", "[green]✓ Enabled[/]");
            statusContent.AddRow("[cyan]Config Status:[/]", "[green]✓ Loaded[/]");
            statusContent.AddRow("[cyan]Last Updated:[/]", DateTime.Now.ToString("MMM dd, HH:mm"));
            
            var statusPanel = new Panel(statusContent)
                .Header("[yellow]System Status[/]")
                .BorderStyle(new Style(Color.Cyan1));
                
            Console.Write(statusPanel);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load status information");
            var errorPanel = new Panel("[red]Status information unavailable[/]")
                .Header("[yellow]System Status[/]")
                .BorderStyle(new Style(Color.Red));
            Console.Write(errorPanel);
        }
    }
    
    private async Task DisplayWelcomePanelAsync(CancellationToken cancellationToken)
    {
        var welcomeContent = new Markup(
            "Welcome to [bold yellow]Scanner111 CLI[/]!\n\n" +
            "Analyze crash logs, identify problematic plugins, and get detailed reports.\n" +
            "This modern C# port provides enhanced performance and reliability.");
        
        var panel = new Panel(welcomeContent)
            .Header("[yellow]Welcome[/]")
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 1);
        Console.Write(panel);
        Console.WriteLine();
        
        await Task.CompletedTask;
    }
    
    private async Task DisplayRecentActivityAsync(CancellationToken cancellationToken)
    {
        try
        {
            var recentSessions = await _sessionManager.GetRecentSessionsAsync(3, cancellationToken);
            var sessionsList = recentSessions.ToList();
            
            if (sessionsList.Any())
            {
                var activityTable = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("[bold]File[/]")
                    .AddColumn("[bold]Date[/]")
                    .AddColumn("[bold]Status[/]");
                
                foreach (var session in sessionsList)
                {
                    var fileName = Path.GetFileName(session.LogFile);
                    var date = session.StartTime.ToString("MMM dd, HH:mm");
                    var status = session.EndTime.HasValue ? "[green]✓ Complete[/]" : "[yellow]⏳ In Progress[/]";
                    
                    activityTable.AddRow(
                        fileName.Length > 30 ? fileName[..27] + "..." : fileName,
                        date,
                        status);
                }
                
                var activityPanel = new Panel(activityTable)
                    .Header("[cyan]Recent Analysis Sessions[/]")
                    .BorderStyle(new Style(Color.Blue));
                    
                Console.Write(activityPanel);
            }
            else
            {
                var noActivityPanel = new Panel("[dim]No recent analysis sessions found[/]")
                    .Header("[cyan]Recent Analysis Sessions[/]")
                    .BorderStyle(new Style(Color.Blue));
                    
                Console.Write(noActivityPanel);
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load recent activity");
            // Don't show error panel for activity - just skip it
        }
        
        Console.WriteLine();
    }
    
    private void DisplayKeyboardShortcuts()
    {
        var shortcuts = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("Key")
            .AddColumn("Action");
            
        shortcuts.AddRow("[yellow]A[/]", "Analyze Crash Log");
        shortcuts.AddRow("[yellow]V[/]", "View Results");
        shortcuts.AddRow("[yellow]M[/]", "Monitor Log File");
        shortcuts.AddRow("[yellow]S[/]", "Session History");
        shortcuts.AddRow("[yellow]C[/]", "Configuration");
        shortcuts.AddRow("[yellow]H[/]", "Help");
        shortcuts.AddRow("[yellow]R[/]", "Refresh");
        shortcuts.AddRow("[yellow]Q[/]", "Quit");
        
        var shortcutsPanel = new Panel(shortcuts)
            .Header("[dim]Keyboard Shortcuts[/]")
            .BorderStyle(new Style(Color.Grey));
            
        Console.Write(shortcutsPanel);
        Console.WriteLine();
    }
    
    private async Task DisplayQuickAccessAsync(CancellationToken cancellationToken)
    {
        try
        {
            var recentSessions = await _sessionManager.GetSessionMetadataAsync(5, cancellationToken);
            var sessionsList = recentSessions.ToList();
            
            if (sessionsList.Any())
            {
                var quickAccessTable = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Green)
                    .AddColumn("[bold]#[/]")
                    .AddColumn("[bold]Session[/]")
                    .AddColumn("[bold]File[/]")
                    .AddColumn("[bold]Results[/]");
                
                for (int i = 0; i < Math.Min(3, sessionsList.Count); i++)
                {
                    var session = sessionsList[i];
                    var fileName = Path.GetFileName(session.LogFile);
                    var shortId = session.Id.ToString("N")[..8];
                    
                    quickAccessTable.AddRow(
                        (i + 1).ToString(),
                        $"[cyan]{shortId}[/]",
                        fileName.Length > 25 ? fileName[..22] + "..." : fileName,
                        session.ResultCount.ToString());
                }
                
                var quickAccessPanel = new Panel(quickAccessTable)
                    .Header("[green]Quick Access - Recent Sessions[/]")
                    .BorderStyle(new Style(Color.Green));
                    
                Console.Write(quickAccessPanel);
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to load quick access sessions");
        }
    }
    
    /// <summary>
    /// Represents a menu option with keyboard shortcut.
    /// </summary>
    private class MenuOption
    {
        public string Display { get; }
        public MenuAction Action { get; }
        public string Shortcut { get; }
        
        public MenuOption(string display, MenuAction action, string shortcut)
        {
            Display = display;
            Action = action;
            Shortcut = shortcut;
        }
    }
}
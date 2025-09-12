using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Services;
using Scanner111.Core.Analysis;
using Spectre.Console;

namespace Scanner111.CLI.UI.Screens;

/// <summary>
/// Screen for displaying help and documentation.
/// </summary>
public class HelpScreen : BaseScreen
{
    private readonly IAnalyzerRegistry _analyzerRegistry;
    /// <summary>
    /// Gets the title of the screen.
    /// </summary>
    public override string Title => "Help & Documentation";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="HelpScreen"/> class.
    /// </summary>
    /// <param name="console">The Spectre.Console instance.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public HelpScreen(
        IAnsiConsole console,
        IServiceProvider services,
        ILogger<HelpScreen> logger)
        : base(console, services, logger)
    {
        _analyzerRegistry = services.GetRequiredService<IAnalyzerRegistry>();
    }
    
    /// <summary>
    /// Displays the help screen.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The screen result.</returns>
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DrawHeader();
            
            // Display help overview
            DisplayHelpOverview();
            
            var topics = new[]
            {
                "🚀 Quick Start Guide - Get started in minutes",
                "🔧 Analyzer Documentation - Detailed analyzer information", 
                "💻 Command Line Usage - CLI reference and examples",
                "⌨️ Keyboard Shortcuts - Navigation and hotkeys",
                "🔍 Troubleshooting Guide - Common issues and solutions",
                "📊 System Requirements - Prerequisites and compatibility",
                "📁 File Formats - Supported log formats and structures",
                "🚑 Performance Tips - Optimize analysis speed",
                "📞 Getting Help - Support resources and community",
                "ℹ️ About Scanner111 - Version and project information",
                "🔙 Back to Menu"
            };
            
            var choice = Console.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select a help topic:[/]")
                    .PageSize(12)
                    .AddChoices(topics)
                    .UseConverter(topic => topic.Split('-')[0].Trim()));
            
            try
            {
                switch (choice)
                {
                    case var s when s.Contains("Quick Start"):
                        await ShowQuickStartGuideAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("Analyzer"):
                        await ShowAnalyzerDocumentationAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("Command Line"):
                        await ShowCommandLineUsageAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("Keyboard"):
                        await ShowKeyboardShortcutsAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("Troubleshooting"):
                        await ShowTroubleshootingAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("System Requirements"):
                        await ShowSystemRequirementsAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("File Formats"):
                        await ShowFileFormatsAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("Performance"):
                        await ShowPerformanceTipsAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("Getting Help"):
                        await ShowGettingHelpAsync(cancellationToken);
                        break;
                        
                    case var s when s.Contains("About"):
                        await ShowAboutAsync(cancellationToken);
                        break;
                        
                    default:
                        return ScreenResult.Back;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error displaying help topic: {Topic}", choice);
                ShowError($"Error displaying help: {ex.Message}");
                await WaitForKeyAsync(cancellationToken: cancellationToken);
            }
        }
        
        return ScreenResult.Back;
    }
    
    private void DisplayHelpOverview()
    {
        var overviewPanel = new Panel(
            new Markup(
                "Welcome to [bold yellow]Scanner111 Help System[/]!\n\n" +
                "Scanner111 is a powerful crash log analyzer that helps identify and diagnose \n" +
                "game stability issues. Choose a topic below to get detailed information.\n\n" +
                "💡 [dim]Tip: Use the search feature in topics to quickly find specific information[/]"))
            .Header("[cyan]Help & Documentation[/]")
            .BorderStyle(new Style(Color.Cyan1))
;
        
        Console.Write(overviewPanel);
        Console.WriteLine();
    }
    
    private async Task ShowQuickStartGuideAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        // Step-by-step guide with interactive elements
        var steps = new[]
        {
            new { Title = "Getting Started", Icon = "🚀" },
            new { Title = "Analyzing Your First Log", Icon = "🔍" },
            new { Title = "Understanding Results", Icon = "📊" },
            new { Title = "Advanced Features", Icon = "⚙️" }
        };
        
        var selectedStep = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Quick Start Guide - Select a section:[/]")
                .AddChoices(steps.Select(s => $"{s.Icon} {s.Title}"))
                .UseConverter(s => s));
        
        Console.Clear();
        DrawHeader();
        
        if (selectedStep.Contains("Getting Started"))
        {
            ShowGettingStartedSection();
        }
        else if (selectedStep.Contains("Analyzing"))
        {
            ShowAnalyzingSection();
        }
        else if (selectedStep.Contains("Understanding"))
        {
            ShowUnderstandingResultsSection();
        }
        else if (selectedStep.Contains("Advanced"))
        {
            ShowAdvancedFeaturesSection();
        }
        
        await WaitForKeyAsync("Press any key to return to help menu...", cancellationToken: cancellationToken);
    }
    
    private void ShowGettingStartedSection()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Getting Started with Scanner111[/]\n\n" +
                "[cyan]1. Installation Verification[/]\n" +
                "   ✓ Ensure Scanner111.CLI.exe is in your PATH\n" +
                "   ✓ Verify all dependencies are installed\n" +
                "   ✓ Test with: [dim]scanner111 --version[/]\n\n" +
                "[cyan]2. Initial Configuration[/]\n" +
                "   • Set your default game type\n" +
                "   • Configure auto-path detection\n" +
                "   • Choose your preferred theme\n\n" +
                "[cyan]3. Locating Crash Logs[/]\n" +
                "   • Windows: [dim]%USERPROFILE%\\Documents\\My Games\\[Game]\\[/]\n" +
                "   • Steam: [dim]Steam\\steamapps\\common\\[Game]\\[/]\n" +
                "   • Look for .log or .txt files with recent timestamps"))
            .Header("[🚀 Getting Started]")
            .BorderStyle(new Style(Color.Green))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowAnalyzingSection()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Analyzing Your First Crash Log[/]\n\n" +
                "[cyan]Step 1: Launch Interactive Mode[/]\n" +
                "   • Run: [dim]scanner111 interactive[/] or just [dim]scanner111[/]\n" +
                "   • Navigate to 'Analyze Crash Log'\n\n" +
                "[cyan]Step 2: Select Your Log File[/]\n" +
                "   • Choose from: Manual path, Recent files, or Auto-search\n" +
                "   • The system validates log format automatically\n\n" +
                "[cyan]Step 3: Choose Analysis Type[/]\n" +
                "   • [green]Quick Analysis:[/] Essential analyzers only (faster)\n" +
                "   • [yellow]Comprehensive:[/] All available analyzers (thorough)\n" +
                "   • [blue]Custom:[/] Select specific analyzers\n\n" +
                "[cyan]Step 4: Monitor Progress[/]\n" +
                "   • Watch real-time progress bars\n" +
                "   • See live status updates\n" +
                "   • Cancel anytime with Ctrl+C"))
            .Header("[🔍 Analyzing Your First Log]")
            .BorderStyle(new Style(Color.Blue))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowUnderstandingResultsSection()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Understanding Analysis Results[/]\n\n" +
                "[cyan]Severity Levels[/]\n" +
                "   🚨 [red bold]Critical:[/] Immediate action required\n" +
                "   ❌ [red]Error:[/] Significant issues found\n" +
                "   ⚠️ [yellow]Warning:[/] Potential problems\n" +
                "   ℹ️ [cyan]Info:[/] General information\n\n" +
                "[cyan]Result Navigation[/]\n" +
                "   • Use pagination to browse long results\n" +
                "   • Search within results using keywords\n" +
                "   • Filter by severity level\n" +
                "   • Export in multiple formats\n\n" +
                "[cyan]Key Information to Look For[/]\n" +
                "   • Plugin conflicts and missing masters\n" +
                "   • Memory allocation failures\n" +
                "   • Configuration issues\n" +
                "   • File path problems"))
            .Header("[📊 Understanding Results]")
            .BorderStyle(new Style(Color.Yellow))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowAdvancedFeaturesSection()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Advanced Features & Tips[/]\n\n" +
                "[cyan]Batch Processing[/]\n" +
                "   • Analyze multiple files: [dim]scanner111 analyze -f *.log[/]\n" +
                "   • Use wildcards and directory scanning\n\n" +
                "[cyan]Custom Analyzer Selection[/]\n" +
                "   • CLI: [dim]scanner111 analyze -f log.txt -a Plugin,Memory[/]\n" +
                "   • Create analyzer presets in configuration\n\n" +
                "[cyan]Output Customization[/]\n" +
                "   • Templates: Customize report formatting\n" +
                "   • Filters: Exclude low-priority findings\n" +
                "   • Automation: JSON output for scripting\n\n" +
                "[cyan]Performance Optimization[/]\n" +
                "   • Adjust parallel analyzer limits\n" +
                "   • Use SSD storage for large logs\n" +
                "   • Configure memory limits"))
            .Header("[⚙️ Advanced Features]")
            .BorderStyle(new Style(Color.Red))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private async Task ShowAnalyzerDocumentationAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        Console.MarkupLine("[yellow]Loading analyzer information...[/]");
        
        try
        {
            var analyzers = await _analyzerRegistry.GetAllAsync(cancellationToken);
            var analyzerList = analyzers.ToList();
            
            if (!analyzerList.Any())
            {
                ShowWarning("No analyzers available");
                return;
            }
            
            // Interactive analyzer selection
            var analyzerNames = analyzerList.Select(a => $"{GetAnalyzerIcon(a.Name)} {a.Name}").ToList();
            analyzerNames.Add("📄 View All Analyzers Summary");
            analyzerNames.Add("🔙 Back to Help Menu");
            
            var selection = Console.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Select an analyzer for detailed information:[/]")
                    .AddChoices(analyzerNames));
            
            Console.Clear();
            DrawHeader();
            
            if (selection.Contains("View All"))
            {
                ShowAllAnalyzersOverview(analyzerList);
            }
            else if (!selection.Contains("Back"))
            {
                var selectedName = selection.Split(' ').Last();
                var selectedAnalyzer = analyzerList.FirstOrDefault(a => a.Name == selectedName);
                if (selectedAnalyzer != null)
                {
                    ShowDetailedAnalyzerInfo(selectedAnalyzer);
                }
            }
            else
            {
                return;
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load analyzer information: {ex.Message}");
            Logger.LogError(ex, "Failed to load analyzer registry");
        }
        
        await WaitForKeyAsync("Press any key to continue...", cancellationToken: cancellationToken);
    }
    
    private void ShowAllAnalyzersOverview(List<IAnalyzer> analyzers)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[cyan]Available Analyzers Overview[/]")
            .AddColumn("[bold]Analyzer[/]")
            .AddColumn("[bold]Primary Function[/]")
            .AddColumn("[bold]Performance[/]")
            .AddColumn("[bold]Key Detections[/]");
        
        foreach (var analyzer in analyzers.OrderBy(a => a.Name))
        {
            var info = GetAnalyzerDetails(analyzer.Name);
            table.AddRow(
                $"{GetAnalyzerIcon(analyzer.Name)} [yellow]{analyzer.Name}[/]",
                info.Function,
                info.Performance,
                info.KeyDetections);
        }
        
        Console.Write(table);
        Console.WriteLine();
        
        var summaryPanel = new Panel(
            new Markup(
                "[cyan]Usage Recommendations:[/]\n" +
                "• [green]Quick Analysis:[/] Use Plugin, Memory, and Settings analyzers\n" +
                "• [yellow]Thorough Analysis:[/] Enable all analyzers for comprehensive results\n" +
                "• [blue]Targeted Analysis:[/] Select specific analyzers based on crash symptoms"))
            .Header("[yellow]Analyzer Recommendations[/]")
            .BorderStyle(new Style(Color.Green));
        
        Console.Write(summaryPanel);
    }
    
    private void ShowDetailedAnalyzerInfo(IAnalyzer analyzer)
    {
        var details = GetAnalyzerDetails(analyzer.Name);
        
        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(8),
                new Layout("Content")
            );
        
        // Header with basic info
        var headerTable = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .AddColumn("Property")
            .AddColumn("Value");
        
        headerTable.AddRow("[cyan]Name:[/]", $"{GetAnalyzerIcon(analyzer.Name)} [yellow]{analyzer.Name}[/]");
        headerTable.AddRow("[cyan]Function:[/]", details.Function);
        headerTable.AddRow("[cyan]Performance:[/]", details.Performance);
        headerTable.AddRow("[cyan]Recommended For:[/]", details.RecommendedFor);
        
        layout["Header"].Update(new Panel(headerTable).Header("[yellow]Analyzer Information[/]"));
        
        // Detailed content
        var contentText = new Markup(
            $"[cyan]What This Analyzer Detects:[/]\n{details.KeyDetections}\n\n" +
            $"[cyan]How It Works:[/]\n{details.HowItWorks}\n\n" +
            $"[cyan]Example Findings:[/]\n{details.ExampleFindings}\n\n" +
            $"[cyan]Configuration Options:[/]\n{details.ConfigOptions}");
        
        layout["Content"].Update(new Panel(contentText).Header("[cyan]Detailed Information[/]"));
        
        Console.Write(layout);
    }
    
    private string GetAnalyzerIcon(string analyzerName)
    {
        return analyzerName switch
        {
            "PluginAnalyzer" => "🔌",
            "MemoryAnalyzer" => "💾",
            "SettingsAnalyzer" => "⚙️",
            "PathAnalyzer" => "📁",
            "FcxModeAnalyzer" => "🎮",
            _ => "📊"
        };
    }
    
    private (string Function, string Performance, string KeyDetections, string RecommendedFor, string HowItWorks, string ExampleFindings, string ConfigOptions) GetAnalyzerDetails(string analyzerName)
    {
        return analyzerName switch
        {
            "PluginAnalyzer" => (
                "Analyzes plugin conflicts and dependencies",
                "[yellow]●●○[/] Medium - Depends on plugin count",
                "• Missing master files\n• Load order conflicts\n• Plugin version mismatches\n• Circular dependencies",
                "Mod-heavy setups, CTDs on startup",
                "Parses plugin headers, checks dependencies, validates load order against game requirements",
                "• [red]Error:[/] MissingMaster.esp requires Master.esm\n• [yellow]Warning:[/] Plugin load order may cause issues",
                "Plugin blacklists, dependency validation rules"
            ),
            "MemoryAnalyzer" => (
                "Monitors memory allocation and usage patterns", 
                "[green]●○○[/] Fast - Lightweight analysis",
                "• Out of memory errors\n• Memory leaks\n• Stack overflow\n• Heap corruption",
                "Performance issues, random crashes",
                "Examines memory addresses, allocation patterns, and system resource usage from crash dumps",
                "• [red]Critical:[/] Out of memory - 8GB process limit exceeded\n• [yellow]Warning:[/] Memory fragmentation detected",
                "Memory limits, allocation tracking thresholds"
            ),
            "SettingsAnalyzer" => (
                "Validates game configuration and INI settings",
                "[green]●○○[/] Fast - Quick INI parsing",
                "• Invalid INI values\n• Performance bottlenecks\n• Compatibility issues\n• Deprecated settings",
                "Configuration problems, stability issues",
                "Parses INI files, validates against known good values, checks for conflicting settings",
                "• [yellow]Warning:[/] iMaxAnisotropy=16 may cause performance issues\n• [red]Error:[/] Invalid graphics adapter specified",
                "Setting validation rules, performance thresholds"
            ),
            "PathAnalyzer" => (
                "Verifies file and directory paths",
                "[green]●○○[/] Fast - File system checks",
                "• Missing required files\n• Invalid file paths\n• Permission issues\n• Broken symbolic links",
                "File not found errors, mod installation issues",
                "Validates file paths in logs, checks file existence, verifies permissions and accessibility",
                "• [red]Error:[/] Required file not found: Data\\Scripts\\main.psc\n• [yellow]Warning:[/] Path length exceeds Windows limit",
                "Search paths, file validation rules"
            ),
            "FcxModeAnalyzer" => (
                "Specialized analysis for FCX mode configurations",
                "[red]●●●[/] Slow - Complex validation",
                "• FCX compatibility issues\n• Mode-specific errors\n• Configuration conflicts\n• Version mismatches",
                "FCX-enabled setups, specialized configurations",
                "Deep analysis of FCX-specific settings, compatibility checks, and mode-related crash patterns",
                "• [red]Critical:[/] FCX mode incompatible with current configuration\n• [yellow]Warning:[/] FCX settings may affect performance",
                "FCX mode rules, compatibility matrices"
            ),
            _ => (
                "General purpose analyzer",
                "[yellow]●●○[/] Medium",
                "Various crash log patterns",
                "General troubleshooting",
                "Analyzes log patterns and identifies common issues",
                "Standard log analysis results",
                "Configurable analysis rules"
            )
        };
    }
    
    private void ShowCommandLineUsage()
    {
        Console.Clear();
        DrawHeader();
        
        var panel = new Panel(
            new Markup(
                "[yellow]Command Line Usage[/]\n\n" +
                "[cyan]analyze[/] - Analyze a crash log file\n" +
                "  -f, --file       [red]Required[/]. Path to the crash log file\n" +
                "  -a, --analyzers  Comma-separated list of analyzers\n" +
                "  -o, --output     Output file path for the report\n" +
                "  -F, --format     Output format (Markdown, Html, Json, Text)\n" +
                "  -v, --verbose    Enable verbose output\n\n" +
                "[cyan]interactive[/] - Launch interactive TUI mode\n" +
                "  -t, --theme      Color theme (Default, Dark, Light, HighContrast)\n" +
                "  -d, --debug      Enable debug mode\n\n" +
                "[cyan]config[/] - Manage configuration settings\n" +
                "  -l, --list       List all configuration settings\n" +
                "  -g, --get        Get a specific configuration value\n" +
                "  -s, --set        Set a configuration value\n" +
                "  -v, --value      Value to set\n" +
                "  -r, --reset      Reset all settings to defaults\n\n" +
                "[cyan]Examples:[/]\n" +
                "  scanner111 analyze -f crash.log\n" +
                "  scanner111 analyze -f crash.log -a Plugin,Memory -o report.md\n" +
                "  scanner111 config --list\n" +
                "  scanner111 interactive --theme Dark"))
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 1);
        
        Console.Write(panel);
    }
    
    private void ShowKeyboardShortcuts()
    {
        Console.Clear();
        DrawHeader();
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Key")
            .AddColumn("Action")
            .AddColumn("Context");
        
        table.AddRow("[yellow]↑/↓[/]", "Navigate menu", "All menus");
        table.AddRow("[yellow]Enter[/]", "Select option", "All menus");
        table.AddRow("[yellow]Space[/]", "Toggle selection", "Multi-select menus");
        table.AddRow("[yellow]ESC[/]", "Go back", "All screens");
        table.AddRow("[yellow]F1[/]", "Show help", "All screens");
        table.AddRow("[yellow]F5[/]", "Refresh", "Results screen");
        table.AddRow("[yellow]Ctrl+C[/]", "Cancel operation", "During analysis");
        table.AddRow("[yellow]Q[/]", "Quit application", "Main menu");
        table.AddRow("[yellow]S[/]", "Quick save", "Results screen");
        table.AddRow("[yellow]O[/]", "Open file", "Main menu");
        
        Console.Write(table);
    }
    
    private async Task ShowTroubleshootingAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var categories = new[]
        {
            "📁 File and Path Issues",
            "📊 Performance Problems", 
            "⚙️ Installation and Setup",
            "💾 Export and Output Issues",
            "🔍 Analysis Problems",
            "📞 Getting Support"
        };
        
        var category = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Troubleshooting Categories - Select an area:[/]")
                .AddChoices(categories)
                .UseConverter(c => c));
        
        Console.Clear();
        DrawHeader();
        
        if (category.Contains("File and Path"))
            ShowFilePathTroubleshooting();
        else if (category.Contains("Performance"))
            ShowPerformanceTroubleshooting();
        else if (category.Contains("Installation"))
            ShowInstallationTroubleshooting();
        else if (category.Contains("Export"))
            ShowExportTroubleshooting();
        else if (category.Contains("Analysis"))
            ShowAnalysisTroubleshooting();
        else if (category.Contains("Support"))
            ShowSupportResources();
        
        await WaitForKeyAsync("Press any key to return...", cancellationToken: cancellationToken);
    }
    
    private void ShowFilePathTroubleshooting()
    {
        var content = new Panel(
            new Markup(
                "[yellow]File and Path Issues[/]\n\n" +
                "[red]🚨 File not found error[/]\n" +
                "   ✓ Verify the file path is correct and complete\n" +
                "   ✓ Use forward slashes (/) or double backslashes (\\\\)\n" +
                "   ✓ Check file permissions - ensure read access\n" +
                "   ✓ Try absolute paths instead of relative paths\n" +
                "   ✓ Ensure the file hasn't been moved or deleted\n\n" +
                "[red]🔒 Permission denied error[/]\n" +
                "   ✓ Run as administrator (Windows) or with sudo (Linux)\n" +
                "   ✓ Check file is not locked by another application\n" +
                "   ✓ Verify antivirus isn't blocking access\n\n" +
                "[red]📄 Invalid log format[/]\n" +
                "   ✓ Ensure file has .log or .txt extension\n" +
                "   ✓ Check the file contains actual crash log data\n" +
                "   ✓ Verify file size is reasonable (> 1KB)\n" +
                "   ✓ Try opening file in text editor to verify content"))
            .Header("[📁 File and Path Troubleshooting]")
            .BorderStyle(new Style(Color.Red))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowPerformanceTroubleshooting()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Performance Optimization[/]\n\n" +
                "[red]🕰️ Analysis takes too long[/]\n" +
                "   ✓ Large files (>100MB) naturally take longer\n" +
                "   ✓ Reduce parallel analyzers (try 2-4 instead of 8)\n" +
                "   ✓ Use Quick Analysis instead of Comprehensive\n" +
                "   ✓ Close other applications to free up resources\n" +
                "   ✓ Monitor CPU and memory usage during analysis\n\n" +
                "[red]💾 High memory usage[/]\n" +
                "   ✓ Split very large log files (>500MB) into chunks\n" +
                "   ✓ Increase virtual memory/swap space\n" +
                "   ✓ Close browser and other memory-intensive apps\n\n" +
                "[red]🔥 CPU throttling/overheating[/]\n" +
                "   ✓ Reduce MaxParallelAnalyzers in settings\n" +
                "   ✓ Take breaks between analyses\n" +
                "   ✓ Ensure proper system cooling"))
            .Header("[📊 Performance Troubleshooting]")
            .BorderStyle(new Style(Color.Yellow))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowInstallationTroubleshooting()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Installation and Setup Issues[/]\n\n" +
                "[red]📦 No analyzers available[/]\n" +
                "   ✓ Verify Scanner111.Core.dll is in the same directory\n" +
                "   ✓ Check all dependencies are installed (.NET 8.0)\n" +
                "   ✓ Re-download and extract all files\n" +
                "   ✓ Check Windows Defender hasn't quarantined files\n\n" +
                "[red]🚫 Application won't start[/]\n" +
                "   ✓ Install .NET 8.0 Runtime from Microsoft\n" +
                "   ✓ Verify Windows version compatibility (Win 10+)\n" +
                "   ✓ Run Windows compatibility troubleshooter\n" +
                "   ✓ Check event logs for detailed error information\n\n" +
                "[red]⚙️ Configuration errors[/]\n" +
                "   ✓ Delete settings.json to reset configuration\n" +
                "   ✓ Check YAML syntax if manually editing config\n" +
                "   ✓ Use 'Reset to Defaults' in Configuration menu"))
            .Header("[⚙️ Installation Troubleshooting]")
            .BorderStyle(new Style(Color.Blue))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowExportTroubleshooting()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Export and Output Problems[/]\n\n" +
                "[red]💾 Export fails[/]\n" +
                "   ✓ Check destination folder has write permissions\n" +
                "   ✓ Ensure sufficient disk space is available\n" +
                "   ✓ Close the output file if it's open elsewhere\n" +
                "   ✓ Try a different output format (MD, HTML, JSON)\n" +
                "   ✓ Use a different file name or location\n\n" +
                "[red]📄 Corrupted output files[/]\n" +
                "   ✓ Check if analysis was interrupted\n" +
                "   ✓ Verify system stability during export\n" +
                "   ✓ Try exporting smaller result sets\n\n" +
                "[red]📝 Format issues[/]\n" +
                "   • [green]Markdown:[/] Best for documentation\n" +
                "   • [blue]HTML:[/] Best for sharing and viewing\n" +
                "   • [yellow]JSON:[/] Best for automation/scripting\n" +
                "   • [cyan]Text:[/] Most compatible, basic formatting"))
            .Header("[💾 Export Troubleshooting]")
            .BorderStyle(new Style(Color.Green))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowAnalysisTroubleshooting()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Analysis-Specific Problems[/]\n\n" +
                "[red]🔍 No results found[/]\n" +
                "   ✓ Verify the log file contains actual crash data\n" +
                "   ✓ Try different analyzers - some may be more sensitive\n" +
                "   ✓ Check if log is truncated or incomplete\n" +
                "   ✓ Enable verbose output for detailed diagnostics\n\n" +
                "[red]🤔 Results seem incorrect[/]\n" +
                "   ✓ Cross-reference with manual log inspection\n" +
                "   ✓ Try Comprehensive analysis for more data\n" +
                "   ✓ Check if log format matches expected game version\n\n" +
                "[red]❌ Analyzer crashes[/]\n" +
                "   ✓ Enable debug logging to identify problematic analyzer\n" +
                "   ✓ Try running analyzers individually\n" +
                "   ✓ Check system resources (RAM, CPU)\n" +
                "   ✓ Report persistent issues via support channels"))
            .Header("[🔍 Analysis Troubleshooting]")
            .BorderStyle(new Style(Color.Red))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowSupportResources()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Getting Help and Support[/]\n\n" +
                "[cyan]📁 Local Diagnostics[/]\n" +
                "   • Log files: [dim]%LOCALAPPDATA%\\Scanner111\\Logs[/]\n" +
                "   • Configuration: [dim]%LOCALAPPDATA%\\Scanner111\\Settings[/]\n" +
                "   • Enable verbose logging: [dim]--verbose[/] flag\n" +
                "   • Debug mode: [dim]--debug[/] flag\n\n" +
                "[cyan]🌐 Online Resources[/]\n" +
                "   • Project repository: GitHub.com/Scanner111\n" +
                "   • Documentation wiki: docs.scanner111.org\n" +
                "   • Community forums: community.scanner111.org\n" +
                "   • Issue tracker: Submit bug reports and requests\n\n" +
                "[cyan]📝 Preparing Bug Reports[/]\n" +
                "   ✓ Include log files from recent analysis\n" +
                "   ✓ Describe steps to reproduce the issue\n" +
                "   ✓ Specify Scanner111 version (scanner111 --version)\n" +
                "   ✓ Include system information (OS, .NET version)\n" +
                "   ✓ Attach sample crash log if possible"))
            .Header("[📞 Support Resources]")
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private async Task ShowCommandLineUsageAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var sections = new[]
        {
            "📖 Command Overview",
            "🔍 Analyze Command",
            "🎮 Interactive Mode", 
            "⚙️ Config Command",
            "📝 Usage Examples"
        };
        
        var section = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Command Line Reference - Select a section:[/]")
                .AddChoices(sections));
        
        Console.Clear();
        DrawHeader();
        
        if (section.Contains("Overview"))
            ShowCommandOverview();
        else if (section.Contains("Analyze"))
            ShowAnalyzeCommand();
        else if (section.Contains("Interactive"))
            ShowInteractiveMode();
        else if (section.Contains("Config"))
            ShowConfigCommand();
        else if (section.Contains("Examples"))
            ShowUsageExamples();
        
        await WaitForKeyAsync("Press any key to continue...", cancellationToken: cancellationToken);
    }
    
    private void ShowCommandOverview()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Scanner111 Command Line Interface[/]\n\n" +
                "[cyan]Basic Syntax:[/]\n" +
                "   scanner111 <command> [options]\n\n" +
                "[cyan]Available Commands:[/]\n" +
                "   [green]analyze[/]      Analyze crash log files\n" +
                "   [green]interactive[/]  Launch TUI (Terminal UI) mode\n" +
                "   [green]config[/]       Manage configuration settings\n" +
                "   [green]version[/]      Show version information\n" +
                "   [green]help[/]         Display help information\n\n" +
                "[cyan]Global Options:[/]\n" +
                "   -v, --verbose      Enable verbose output\n" +
                "   -q, --quiet        Suppress non-essential output\n" +
                "   --debug            Enable debug mode\n" +
                "   --no-color         Disable colored output\n" +
                "   -h, --help         Show help for command"))
            .Header("[📖 Command Overview]")
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowAnalyzeCommand()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Analyze Command Reference[/]\n\n" +
                "[cyan]Syntax:[/]\n" +
                "   scanner111 analyze -f <file> [options]\n\n" +
                "[cyan]Required Options:[/]\n" +
                "   -f, --file <path>        Path to crash log file\n\n" +
                "[cyan]Optional Parameters:[/]\n" +
                "   -a, --analyzers <list>   Comma-separated analyzer names\n" +
                "                           (Plugin,Memory,Settings,Path,FcxMode)\n" +
                "   -o, --output <path>      Output file path for report\n" +
                "   -F, --format <format>    Output format (Markdown,Html,Json,Text)\n" +
                "   -t, --timeout <seconds>  Analysis timeout (default: 300)\n" +
                "   --parallel <count>       Max parallel analyzers (1-16)\n" +
                "   --quick                  Use quick analysis preset\n" +
                "   --comprehensive          Use comprehensive analysis preset\n\n" +
                "[cyan]Filter Options:[/]\n" +
                "   --min-severity <level>   Minimum severity (Info,Warning,Error,Critical)\n" +
                "   --exclude-empty          Exclude analyzers with no findings\n" +
                "   --include-debug          Include debug-level information"))
            .Header("[🔍 Analyze Command Details]")
            .BorderStyle(new Style(Color.Green))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowInteractiveMode()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Interactive TUI Mode[/]\n\n" +
                "[cyan]Launch Interactive Mode:[/]\n" +
                "   scanner111 interactive [options]\n" +
                "   scanner111              (shortcut - no command needed)\n\n" +
                "[cyan]Interactive Options:[/]\n" +
                "   -t, --theme <theme>      Color theme (Default,Dark,Light,HighContrast)\n" +
                "   --start-screen <screen>  Initial screen (menu,analyze,config,help)\n" +
                "   --auto-detect           Enable automatic path detection\n\n" +
                "[cyan]Features in Interactive Mode:[/]\n" +
                "   • Rich terminal interface with colors and formatting\n" +
                "   • Real-time progress indicators and live updates\n" +
                "   • Interactive file selection with search\n" +
                "   • Paginated results with filtering and search\n" +
                "   • Guided configuration with validation\n" +
                "   • Comprehensive help system (you're using it now!)\n\n" +
                "[cyan]Navigation:[/]\n" +
                "   • Use arrow keys to navigate menus\n" +
                "   • Press Enter to select options\n" +
                "   • Use ESC to go back or cancel operations\n" +
                "   • Press F1 for context-sensitive help"))
            .Header("[🎮 Interactive Mode Guide]")
            .BorderStyle(new Style(Color.Blue))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowConfigCommand()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Configuration Management[/]\n\n" +
                "[cyan]List All Settings:[/]\n" +
                "   scanner111 config --list\n" +
                "   scanner111 config -l\n\n" +
                "[cyan]Get Specific Setting:[/]\n" +
                "   scanner111 config --get <key>\n" +
                "   scanner111 config -g DefaultGame\n\n" +
                "[cyan]Set Configuration Value:[/]\n" +
                "   scanner111 config --set <key> --value <value>\n" +
                "   scanner111 config -s MaxParallelAnalyzers -v 4\n\n" +
                "[cyan]Reset to Defaults:[/]\n" +
                "   scanner111 config --reset\n\n" +
                "[cyan]Common Configuration Keys:[/]\n" +
                "   • DefaultGame              Game type (Skyrim,SkyrimSE,Fallout4,etc)\n" +
                "   • AutoDetectPaths         Enable automatic path detection\n" +
                "   • MaxParallelAnalyzers    Concurrent analyzer limit (1-16)\n" +
                "   • DefaultReportFormat     Output format preference\n" +
                "   • Theme                   UI color theme\n" +
                "   • ShowTimestamps          Include timestamps in output\n" +
                "   • VerboseOutput           Enable detailed logging\n" +
                "   • LogDirectory            Custom log file location"))
            .Header("[⚙️ Configuration Commands]")
            .BorderStyle(new Style(Color.Yellow))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private void ShowUsageExamples()
    {
        var content = new Panel(
            new Markup(
                "[yellow]Practical Usage Examples[/]\n\n" +
                "[cyan]Basic Analysis:[/]\n" +
                "   scanner111 analyze -f crash.log\n" +
                "   scanner111 analyze -f \"C:\\Games\\crash log.txt\"\n\n" +
                "[cyan]Targeted Analysis:[/]\n" +
                "   scanner111 analyze -f crash.log -a Plugin,Memory\n" +
                "   scanner111 analyze -f crash.log --quick\n\n" +
                "[cyan]Custom Output:[/]\n" +
                "   scanner111 analyze -f crash.log -o report.html -F Html\n" +
                "   scanner111 analyze -f crash.log -o results.json -F Json\n\n" +
                "[cyan]Batch Processing:[/]\n" +
                "   for %f in (*.log) do scanner111 analyze -f \"%f\" -o \"%f-report.md\"\n" +
                "   find . -name \"*.log\" -exec scanner111 analyze -f {} \\;\n\n" +
                "[cyan]Configuration Examples:[/]\n" +
                "   scanner111 config -s DefaultGame -v SkyrimSE\n" +
                "   scanner111 config -s MaxParallelAnalyzers -v 6\n" +
                "   scanner111 config -s Theme -v Dark\n\n" +
                "[cyan]Performance Optimization:[/]\n" +
                "   scanner111 analyze -f large.log --parallel 2 --timeout 600\n" +
                "   scanner111 analyze -f crash.log --min-severity Warning"))
            .Header("[📝 Usage Examples]")
            .BorderStyle(new Style(Color.Red))
            .Padding(1, 1);
        
        Console.Write(content);
    }
    
    private async Task ShowKeyboardShortcutsAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var layout = new Layout("Root")
            .SplitColumns(
                new Layout("General"),
                new Layout("Specific")
            );
        
        // General shortcuts
        var generalTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[cyan]General Navigation[/]")
            .AddColumn("[bold]Key[/]")
            .AddColumn("[bold]Action[/]");
        
        generalTable.AddRow("[yellow]↑/↓[/]", "Navigate menu items");
        generalTable.AddRow("[yellow]Enter[/]", "Select/confirm option");
        generalTable.AddRow("[yellow]Space[/]", "Toggle in multi-select");
        generalTable.AddRow("[yellow]ESC[/]", "Go back/cancel");
        generalTable.AddRow("[yellow]Tab[/]", "Navigate between panels");
        generalTable.AddRow("[yellow]F1[/]", "Context help");
        generalTable.AddRow("[yellow]F5[/]", "Refresh current screen");
        generalTable.AddRow("[yellow]Ctrl+C[/]", "Cancel operation");
        generalTable.AddRow("[yellow]Q[/]", "Quit from main menu");
        
        layout["General"].Update(new Panel(generalTable).Expand());
        
        // Screen-specific shortcuts
        var specificTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[cyan]Screen-Specific[/]")
            .AddColumn("[bold]Key[/]")
            .AddColumn("[bold]Context[/]")
            .AddColumn("[bold]Action[/]");
        
        specificTable.AddRow("[yellow]A[/]", "Main Menu", "Quick analyze");
        specificTable.AddRow("[yellow]V[/]", "Main Menu", "View results");
        specificTable.AddRow("[yellow]C[/]", "Main Menu", "Configuration");
        specificTable.AddRow("[yellow]H[/]", "Main Menu", "Help");
        specificTable.AddRow("[yellow]R[/]", "Main Menu", "Refresh");
        specificTable.AddRow("[yellow]S[/]", "Results", "Search results");
        specificTable.AddRow("[yellow]F[/]", "Results", "Filter by severity");
        specificTable.AddRow("[yellow]E[/]", "Results", "Export options");
        specificTable.AddRow("[yellow]PgUp/PgDn[/]", "Results", "Page navigation");
        specificTable.AddRow("[yellow]Home/End[/]", "Results", "First/last page");
        
        layout["Specific"].Update(new Panel(specificTable).Expand());
        
        Console.Write(layout);
        Console.WriteLine();
        
        var tipPanel = new Panel(
            "💡 [dim]Pro Tip: Most screens support additional hotkeys - look for underlined letters in menu options![/]")
            .Header("[yellow]Keyboard Tips[/]")
            .BorderStyle(new Style(Color.Green))
;
        
        Console.Write(tipPanel);
        
        await WaitForKeyAsync("Press any key to continue...", cancellationToken: cancellationToken);
    }
    
    private async Task ShowSystemRequirementsAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var requirements = new Panel(
            new Markup(
                "[yellow]System Requirements[/]\n\n" +
                "[cyan]Minimum Requirements:[/]\n" +
                "   • Operating System: Windows 10 (1903+) or Windows 11\n" +
                "   • .NET Runtime: .NET 8.0 or later\n" +
                "   • Memory: 512 MB RAM available\n" +
                "   • Storage: 100 MB free disk space\n" +
                "   • Terminal: Windows Terminal (recommended)\n\n" +
                "[cyan]Recommended Specifications:[/]\n" +
                "   • Memory: 2 GB RAM for large log files (>100 MB)\n" +
                "   • CPU: Multi-core processor for parallel analysis\n" +
                "   • Storage: SSD for better I/O performance\n" +
                "   • Terminal: Windows Terminal with modern font\n\n" +
                "[cyan]Supported Log File Formats:[/]\n" +
                "   • Skyrim/Skyrim SE crash logs (.log, .txt)\n" +
                "   • Fallout 4 crash logs (.log, .txt)\n" +
                "   • Fallout New Vegas crash logs (.txt)\n" +
                "   • Generic crash dumps (limited support)\n\n" +
                "[cyan]Platform Compatibility:[/]\n" +
                "   • ✅ Windows 10/11 (Primary platform)\n" +
                "   • ⚠️  Linux (via .NET 8.0 - limited testing)\n" +
                "   • ❌ macOS (Not supported - Windows-specific game focus)"))
            .Header("[💻 System Requirements]")
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 1);
        
        Console.Write(requirements);
        
        await WaitForKeyAsync("Press any key to continue...", cancellationToken: cancellationToken);
    }
    
    private async Task ShowFileFormatsAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var formats = new Panel(
            new Markup(
                "[yellow]Supported File Formats and Structures[/]\n\n" +
                "[cyan]Input Formats (Crash Logs):[/]\n" +
                "   • .log files - Standard game crash logs\n" +
                "   • .txt files - Text-based crash reports\n" +
                "   • .dmp files - Memory dump files (limited support)\n\n" +
                "[cyan]Output Formats (Analysis Reports):[/]\n" +
                "   • [green]Markdown (.md):[/] Human-readable with formatting\n" +
                "   • [blue]HTML (.html):[/] Web-viewable with styling\n" +
                "   • [yellow]JSON (.json):[/] Machine-readable structured data\n" +
                "   • [cyan]Plain Text (.txt):[/] Simple unformatted output\n\n" +
                "[cyan]Log Structure Requirements:[/]\n" +
                "   ✅ Must contain timestamp information\n" +
                "   ✅ Should include stack trace or error details\n" +
                "   ✅ Plugin/mod loading information helpful\n" +
                "   ✅ System information recommended\n\n" +
                "[cyan]Common Log Locations:[/]\n" +
                "   • Skyrim SE: Documents\\My Games\\Skyrim Special Edition\\\n" +
                "   • Fallout 4: Documents\\My Games\\Fallout4\\\n" +
                "   • Steam: steamapps\\common\\[GameName]\\\n" +
                "   • Mod Organizer: [MO2 Instance]\\logs\\"))
            .Header("[📁 File Formats Guide]")
            .BorderStyle(new Style(Color.Blue))
            .Padding(1, 1);
        
        Console.Write(formats);
        
        await WaitForKeyAsync("Press any key to continue...", cancellationToken: cancellationToken);
    }
    
    private async Task ShowPerformanceTipsAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var tips = new Panel(
            new Markup(
                "[yellow]Performance Optimization Tips[/]\n\n" +
                "[cyan]🚀 Speed Up Analysis:[/]\n" +
                "   • Use Quick Analysis for initial diagnosis\n" +
                "   • Limit parallel analyzers on slower systems (2-4)\n" +
                "   • Close unnecessary applications during analysis\n" +
                "   • Use SSD storage for large log files\n" +
                "   • Pre-filter logs to remove irrelevant sections\n\n" +
                "[cyan]💾 Memory Management:[/]\n" +
                "   • Split very large log files (>500MB) into chunks\n" +
                "   • Increase virtual memory if analyzing huge files\n" +
                "   • Monitor memory usage during analysis\n" +
                "   • Use streaming analysis for continuous logs\n\n" +
                "[cyan]⚙️ Configuration Tuning:[/]\n" +
                "   • Set MaxParallelAnalyzers based on CPU cores\n" +
                "   • Adjust timeout values for complex analyses\n" +
                "   • Enable caching for repeated analyses\n" +
                "   • Use appropriate log levels (avoid debug in production)\n\n" +
                "[cyan]📊 Batch Processing:[/]\n" +
                "   • Process multiple files in sequence, not parallel\n" +
                "   • Use command-line mode for automation\n" +
                "   • Implement result caching for similar logs\n" +
                "   • Consider using filters to reduce output volume"))
            .Header("[🚀 Performance Tips]")
            .BorderStyle(new Style(Color.Green))
            .Padding(1, 1);
        
        Console.Write(tips);
        
        await WaitForKeyAsync("Press any key to continue...", cancellationToken: cancellationToken);
    }
    
    private async Task ShowGettingHelpAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var help = new Panel(
            new Markup(
                "[yellow]Getting Help and Support[/]\n\n" +
                "[cyan]📝 Self-Help Resources:[/]\n" +
                "   • Enable verbose logging: --verbose flag\n" +
                "   • Check application logs in %LOCALAPPDATA%\\Scanner111\\Logs\n" +
                "   • Try different analyzers if results seem incomplete\n" +
                "   • Use debug mode for detailed troubleshooting\n\n" +
                "[cyan]🌐 Online Documentation:[/]\n" +
                "   • Official Wiki: https://github.com/Scanner111/wiki\n" +
                "   • API Documentation: https://docs.scanner111.org\n" +
                "   • Video Tutorials: YouTube channel (Scanner111 Official)\n" +
                "   • Community Guides: Steam Community guides\n\n" +
                "[cyan]🤝 Community Support:[/]\n" +
                "   • Discord Server: Real-time community help\n" +
                "   • Reddit: r/Scanner111 for discussions\n" +
                "   • Forums: Official support forums\n" +
                "   • GitHub Issues: Bug reports and feature requests\n\n" +
                "[cyan]🐛 Reporting Issues:[/]\n" +
                "   • Include Scanner111 version (scanner111 --version)\n" +
                "   • Attach relevant log files\n" +
                "   • Describe steps to reproduce\n" +
                "   • Include system information (OS, .NET version)\n" +
                "   • Sanitize crash logs (remove personal info)"))
            .Header("[📞 Getting Help]")
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 1);
        
        Console.Write(help);
        
        await WaitForKeyAsync("Press any key to continue...", cancellationToken: cancellationToken);
    }
    
    private async Task ShowAboutAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var about = new Panel(
            new Markup(
                "[yellow]Scanner111[/] - Advanced Crash Log Analyzer\n" +
                "Version 1.0.0 - .NET 8.0\n\n" +
                "[cyan]🎯 Project Mission:[/]\n" +
                "Scanner111 is a modern, high-performance crash log analyzer\n" +
                "designed to help gamers and developers quickly diagnose\n" +
                "game stability issues. Born from the need for better\n" +
                "debugging tools in the modding community.\n\n" +
                "[cyan]✨ Key Features:[/]\n" +
                "   • Multi-threaded analysis engine\n" +
                "   • Rich terminal user interface\n" +
                "   • Comprehensive command-line interface\n" +
                "   • Multiple export formats (MD, HTML, JSON, TXT)\n" +
                "   • Extensible analyzer architecture\n" +
                "   • Real-time progress monitoring\n" +
                "   • Intelligent error detection and categorization\n\n" +
                "[cyan]🏗️ Technical Architecture:[/]\n" +
                "   • Built on .NET 8.0 with async-first design\n" +
                "   • Thread-safe operations throughout\n" +
                "   • Comprehensive unit and integration testing\n" +
                "   • Dependency injection and modular design\n" +
                "   • YAML-based configuration system\n" +
                "   • Cross-platform compatibility (Windows primary)\n\n" +
                "[dim]© 2024 Scanner111 Development Team\n" +
                "Licensed under MIT License[/]"))
            .Header("[ℹ️ About Scanner111]")
            .BorderStyle(new Style(Color.Cyan1))
            .Padding(1, 1);
        
        Console.Write(about);
        
        await WaitForKeyAsync("Press any key to return to help menu...", cancellationToken: cancellationToken);
    }
}
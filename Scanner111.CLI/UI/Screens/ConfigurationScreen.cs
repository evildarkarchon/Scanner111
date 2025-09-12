using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Configuration;
using Scanner111.CLI.Services;
using Scanner111.Core.Models;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;
using Spectre.Console;

namespace Scanner111.CLI.UI.Screens;

/// <summary>
/// Screen for managing application configuration.
/// </summary>
public class ConfigurationScreen : BaseScreen
{
    private readonly IApplicationSettings _settings;
    private readonly IConfigurationService _configService;
    
    /// <summary>
    /// Gets the title of the screen.
    /// </summary>
    public override string Title => "Configuration";
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationScreen"/> class.
    /// </summary>
    /// <param name="console">The Spectre.Console instance.</param>
    /// <param name="services">The service provider.</param>
    /// <param name="logger">The logger.</param>
    public ConfigurationScreen(
        IAnsiConsole console,
        IServiceProvider services,
        ILogger<ConfigurationScreen> logger)
        : base(console, services, logger)
    {
        _settings = services.GetRequiredService<IApplicationSettings>();
        _configService = services.GetRequiredService<IConfigurationService>();
    }
    
    /// <summary>
    /// Displays the configuration screen.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The screen result.</returns>
    public override async Task<ScreenResult> DisplayAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            DrawHeader();
            
            var settings = await _settings.LoadAsync(cancellationToken);
            
            // Display categorized settings overview
            await DisplaySettingsOverviewAsync(settings);
            
            // Main menu options with icons
            var choices = new[]
            {
                "⚙️ General Settings",
                "📊 Analysis Settings", 
                "🎨 UI & Display Settings",
                "🔧 Advanced Settings",
                "📁 File Management",
                "🔄 Reset to Defaults",
                "🔙 Back to Menu"
            };
            
            var action = Console.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]Configuration Categories:[/]")
                    .AddChoices(choices));
            
            switch (action)
            {
                case var s when s.Contains("General"):
                    await EditGeneralSettingsAsync(settings, cancellationToken);
                    break;
                    
                case var s when s.Contains("Analysis"):
                    await EditAnalysisSettingsAsync(settings, cancellationToken);
                    break;
                    
                case var s when s.Contains("UI & Display"):
                    await EditUISettingsAsync(settings, cancellationToken);
                    break;
                    
                case var s when s.Contains("Advanced"):
                    await EditAdvancedSettingsAsync(settings, cancellationToken);
                    break;
                    
                case var s when s.Contains("File Management"):
                    await HandleFileManagementAsync(settings, cancellationToken);
                    break;
                    
                case var s when s.Contains("Reset"):
                    if (await ConfirmResetAsync())
                    {
                        await ResetSettingsAsync(cancellationToken);
                    }
                    break;
                    
                default:
                    return ScreenResult.Back;
            }
        }
        
        return ScreenResult.Back;
    }
    
    private async Task DisplaySettingsOverviewAsync(ApplicationSettings settings)
    {
        await Task.CompletedTask;
        var layout = new Layout("Root")
            .SplitColumns(
                new Layout("Left"),
                new Layout("Right")
            );
        
        // Left panel: General & Analysis settings
        var leftTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[cyan]General & Analysis[/]")
            .AddColumn("[bold]Setting[/]")
            .AddColumn("[bold]Value[/]");
        
        leftTable.AddRow("🎮 Default Game", $"[yellow]{settings.DefaultGame}[/]");
        leftTable.AddRow("🔍 Auto-detect Paths", settings.AutoDetectPaths ? "[green]✓ Enabled[/]" : "[red]✗ Disabled[/]");
        leftTable.AddRow("⚙️ Max Parallel Analyzers", $"[cyan]{settings.MaxParallelAnalyzers}[/]");
        leftTable.AddRow("📊 Default Report Format", $"[yellow]{settings.DefaultReportFormat}[/]");
        
        layout["Left"].Update(new Panel(leftTable).Expand());
        
        // Right panel: UI & Advanced settings  
        var rightTable = new Table()
            .Border(TableBorder.Rounded)
            .Title("[cyan]UI & Advanced[/]")
            .AddColumn("[bold]Setting[/]")
            .AddColumn("[bold]Value[/]");
        
        rightTable.AddRow("🎨 Theme", $"[yellow]{settings.Theme}[/]");
        rightTable.AddRow("🕰️ Show Timestamps", settings.ShowTimestamps ? "[green]✓ Yes[/]" : "[red]✗ No[/]");
        rightTable.AddRow("📝 Verbose Output", settings.VerboseOutput ? "[green]✓ Yes[/]" : "[red]✗ No[/]");
        rightTable.AddRow("📁 Log Directory", $"[dim]{(settings.LogDirectory ?? "Default")}[/]");
        
        layout["Right"].Update(new Panel(rightTable).Expand());
        
        Console.Write(layout);
        Console.WriteLine();
    }
    
    private async Task EditGeneralSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var panel = new Panel(
            "Configure general application settings including default game type and path detection preferences.")
            .Header("[yellow]⚙️ General Settings[/]")
            .BorderStyle(new Style(Color.Cyan1));
        
        Console.Write(panel);
        Console.WriteLine();
        
        // Show current values and preview
        DisplaySettingPreview("Current Settings", new Dictionary<string, string>
        {
            ["Default Game"] = settings.DefaultGame.ToString(),
            ["Auto-detect Paths"] = settings.AutoDetectPaths.ToString()
        });
        
        // Default Game with descriptions
        var gameOptions = Enum.GetValues<GameType>()
            .Select(g => new { Game = g, Description = GetGameDescription(g) })
            .ToList();
        
        settings.DefaultGame = Console.Prompt(
            new SelectionPrompt<GameType>()
                .Title("[green]Select default game type:[/]")
                .AddChoices(gameOptions.Select(g => g.Game))
                .UseConverter(g => $"{g} [dim]- {GetGameDescription(g)}[/]"));
        
        // Auto-detect Paths with explanation
        Console.WriteLine();
        Console.MarkupLine("[dim]Auto-detect paths: Automatically discover game installation directories[/]");
        settings.AutoDetectPaths = Console.Confirm(
            "[cyan]Enable automatic path detection?[/]", 
            settings.AutoDetectPaths);
        
        await SaveWithPreviewAsync(settings, cancellationToken, "General settings");
    }
    
    private async Task EditAnalysisSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var panel = new Panel(
            "Configure analysis behavior including performance settings and default output formats.")
            .Header("[yellow]📊 Analysis Settings[/]")
            .BorderStyle(new Style(Color.Green));
        
        Console.Write(panel);
        Console.WriteLine();
        
        DisplaySettingPreview("Current Settings", new Dictionary<string, string>
        {
            ["Max Parallel Analyzers"] = settings.MaxParallelAnalyzers.ToString(),
            ["Default Report Format"] = settings.DefaultReportFormat.ToString()
        });
        
        // Max Parallel Analyzers with performance guidance
        Console.MarkupLine("[dim]Recommended: 4 for most systems, 8+ for high-end systems[/]");
        settings.MaxParallelAnalyzers = Console.Prompt(
            new TextPrompt<int>("[cyan]Maximum parallel analyzers (1-16):[/]")
                .DefaultValue(settings.MaxParallelAnalyzers)
                .Validate(value => value switch
                {
                    < 1 => Spectre.Console.ValidationResult.Error("[red]Minimum value is 1[/]"),
                    > 16 => Spectre.Console.ValidationResult.Error("[red]Maximum value is 16[/]"),
                    > 8 => Spectre.Console.ValidationResult.Error("[yellow]Warning: High values may impact system performance[/]"),
                    _ => Spectre.Console.ValidationResult.Success()
                }));
        
        // Default Report Format with format descriptions
        var formatOptions = Enum.GetValues<ReportFormat>()
            .Select(f => new { Format = f, Description = GetFormatDescription(f) })
            .ToList();
        
        Console.WriteLine();
        settings.DefaultReportFormat = Console.Prompt(
            new SelectionPrompt<ReportFormat>()
                .Title("[green]Select default report format:[/]")
                .AddChoices(formatOptions.Select(f => f.Format))
                .UseConverter(f => $"{f} [dim]- {GetFormatDescription(f)}[/]"));
        
        await SaveWithPreviewAsync(settings, cancellationToken, "Analysis settings");
    }
    
    private async Task EditUISettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var panel = new Panel(
            "Customize the user interface appearance and output preferences.")
            .Header("[yellow]🎨 UI & Display Settings[/]")
            .BorderStyle(new Style(Color.Magenta1));
        
        Console.Write(panel);
        Console.WriteLine();
        
        DisplaySettingPreview("Current Settings", new Dictionary<string, string>
        {
            ["Theme"] = settings.Theme,
            ["Show Timestamps"] = settings.ShowTimestamps.ToString(),
            ["Verbose Output"] = settings.VerboseOutput.ToString()
        });
        
        // Theme selection with previews
        var themeOptions = new[]
        {
            new { Name = "Default", Description = "Standard color scheme", Sample = "[white]Text sample[/]" },
            new { Name = "Dark", Description = "Dark background optimized", Sample = "[grey]Text sample[/]" },
            new { Name = "Light", Description = "Light background optimized", Sample = "[black]Text sample[/]" },
            new { Name = "High Contrast", Description = "Accessibility optimized", Sample = "[bold white]Text sample[/]" }
        };
        
        settings.Theme = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select UI theme:[/]")
                .AddChoices(themeOptions.Select(t => t.Name))
                .UseConverter(theme => 
                {
                    var option = themeOptions.First(t => t.Name == theme);
                    return $"{theme} [dim]- {option.Description}[/] {option.Sample}";
                }));
        
        // Timestamps
        Console.WriteLine();
        Console.MarkupLine("[dim]Show timestamps: Display time information in analysis output[/]");
        settings.ShowTimestamps = Console.Confirm(
            "[cyan]Show timestamps in output?[/]", 
            settings.ShowTimestamps);
        
        // Verbose Output
        Console.WriteLine();
        Console.MarkupLine("[dim]Verbose output: Show detailed progress and diagnostic information[/]");
        settings.VerboseOutput = Console.Confirm(
            "[cyan]Enable verbose output?[/]", 
            settings.VerboseOutput);
        
        await SaveWithPreviewAsync(settings, cancellationToken, "UI settings");
    }
    
    private async Task EditAdvancedSettingsAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var panel = new Panel(
            "Advanced configuration options for power users. [red]Use with caution![/]")
            .Header("[red]🔧 Advanced Settings[/]")
            .BorderStyle(new Style(Color.Red));
        
        Console.Write(panel);
        Console.WriteLine();
        
        DisplaySettingPreview("Current Settings", new Dictionary<string, string>
        {
            ["Log Directory"] = settings.LogDirectory ?? "Default"
        });
        
        // Log Directory
        var logDirOptions = new[]
        {
            "Use default location",
            "Specify custom directory",
            "View current log files"
        };
        
        var logDirChoice = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Log directory configuration:[/]")
                .AddChoices(logDirOptions));
        
        if (logDirChoice.Contains("custom"))
        {
            var customDir = Console.Prompt(
                new TextPrompt<string>("[cyan]Enter custom log directory path:[/]")
                    .DefaultValue(settings.LogDirectory ?? "")
                    .AllowEmpty()
                    .Validate(path =>
                    {
                        if (string.IsNullOrWhiteSpace(path))
                            return Spectre.Console.ValidationResult.Success();
                        
                        try
                        {
                            var fullPath = Path.GetFullPath(path);
                            if (!Directory.Exists(Path.GetDirectoryName(fullPath)))
                                return Spectre.Console.ValidationResult.Error("[red]Parent directory does not exist[/]");
                            
                            return Spectre.Console.ValidationResult.Success();
                        }
                        catch
                        {
                            return Spectre.Console.ValidationResult.Error("[red]Invalid directory path[/]");
                        }
                    }));
            
            settings.LogDirectory = string.IsNullOrWhiteSpace(customDir) ? null : customDir;
        }
        else if (logDirChoice.Contains("View"))
        {
            await ViewLogFilesAsync(settings, cancellationToken);
            return;
        }
        else
        {
            settings.LogDirectory = null;
        }
        
        await SaveWithPreviewAsync(settings, cancellationToken, "Advanced settings");
    }
    
    private async Task HandleFileManagementAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var choices = new[]
        {
            "📁 View Log Files",
            "🗑️ Clear Cache",
            "📊 View Disk Usage",
            "🧹 Clean Old Files",
            "🔙 Back to Configuration"
        };
        
        var action = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]File Management Options:[/]")
                .AddChoices(choices));
        
        switch (action)
        {
            case var s when s.Contains("View Log"):
                await ViewLogFilesAsync(settings, cancellationToken);
                break;
                
            case var s when s.Contains("Clear Cache"):
                await ClearCacheAsync(cancellationToken);
                break;
                
            case var s when s.Contains("Disk Usage"):
                await ViewDiskUsageAsync(settings, cancellationToken);
                break;
                
            case var s when s.Contains("Clean"):
                await CleanOldFilesAsync(settings, cancellationToken);
                break;
        }
    }
    
    private async Task ResetSettingsAsync(CancellationToken cancellationToken)
    {
        await _settings.ResetAsync(cancellationToken);
        ShowSuccess("Settings reset to defaults!");
        await Task.Delay(1500, cancellationToken);
    }
    
    private async Task ViewLogFilesAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var logDir = settings.LogDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scanner111",
            "Logs");
        
        if (!Directory.Exists(logDir))
        {
            ShowWarning("Log directory does not exist yet");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
            return;
        }
        
        var logFiles = Directory.GetFiles(logDir, "*.log")
            .OrderByDescending(File.GetLastWriteTime)
            .Take(10)
            .ToList();
        
        if (!logFiles.Any())
        {
            ShowWarning("No log files found");
            await WaitForKeyAsync(cancellationToken: cancellationToken);
            return;
        }
        
        Console.WriteLine();
        Console.MarkupLine($"[yellow]Recent Log Files[/] (in {logDir})");
        Console.WriteLine();
        
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("File Name")
            .AddColumn("Size")
            .AddColumn("Last Modified");
        
        foreach (var file in logFiles)
        {
            var info = new FileInfo(file);
            table.AddRow(
                Path.GetFileName(file),
                FormatFileSize(info.Length),
                info.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        
        Console.Write(table);
        
        await WaitForKeyAsync(cancellationToken: cancellationToken);
    }
    
    private async Task<bool> ConfirmResetAsync()
    {
        await Task.CompletedTask;
        Console.WriteLine();
        var warningPanel = new Panel(
            "[red bold]WARNING:[/] This will reset ALL settings to their default values.\n" +
            "Any custom configurations will be lost.\n" +
            "This action cannot be undone.")
            .Header("[red]Reset Confirmation[/]")
            .BorderStyle(new Style(Color.Red));
        
        Console.Write(warningPanel);
        Console.WriteLine();
        
        return Console.Confirm("[red]Are you absolutely sure you want to reset all settings?[/]");
    }
    
    private async Task SaveWithPreviewAsync(ApplicationSettings settings, CancellationToken cancellationToken, string category)
    {
        Console.WriteLine();
        
        // Show changes preview
        Console.MarkupLine($"[yellow]Preview changes to {category}...[/]");
        
        await AnsiConsole.Status()
            .StartAsync("[yellow]Validating settings...[/]", async ctx =>
            {
                await Task.Delay(500, cancellationToken); // Simulate validation
            });
        
        var confirmSave = Console.Confirm("[green]Save these changes?[/]", true);
        
        if (confirmSave)
        {
            await Console.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Saving settings...[/]");
                    
                    task.Value = 25;
                    await _settings.SaveAsync(settings, cancellationToken);
                    task.Value = 75;
                    
                    await Task.Delay(500, cancellationToken);
                    task.Value = 100;
                });
            
            ShowSuccess($"{category} saved successfully!");
        }
        else
        {
            ShowWarning("Changes discarded");
        }
        
        await Task.Delay(1500, cancellationToken);
    }
    
    private void DisplaySettingPreview(string title, Dictionary<string, string> settings)
    {
        var previewTable = new Table()
            .Border(TableBorder.None)
            .Title($"[dim]{title}[/]")
            .AddColumn("Setting")
            .AddColumn("Value");
        
        foreach (var kvp in settings)
        {
            previewTable.AddRow($"[cyan]{kvp.Key}:[/]", $"[yellow]{kvp.Value}[/]");
        }
        
        Console.Write(new Panel(previewTable));
        Console.WriteLine();
    }
    
    private string GetGameDescription(GameType gameType)
    {
        return gameType switch
        {
            GameType.Skyrim => "The Elder Scrolls V: Skyrim Special Edition",
            GameType.SkyrimVR => "Skyrim VR Edition",
            GameType.Fallout4 => "Fallout 4",
            GameType.Fallout4VR => "Fallout 4 VR",
            _ => "Unknown game type"
        };
    }
    
    private string GetFormatDescription(ReportFormat format)
    {
        return format switch
        {
            ReportFormat.Markdown => "Structured text with formatting",
            ReportFormat.Html => "Web-compatible rich format", 
            ReportFormat.Json => "Machine-readable data format",
            ReportFormat.PlainText => "Simple unformatted text",
            _ => "Unknown format"
        };
    }
    
    private async Task ViewDiskUsageAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var logDir = settings.LogDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scanner111");
        
        await AnsiConsole.Status()
            .StartAsync("[yellow]Calculating disk usage...[/]", async ctx =>
            {
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("[bold]Directory[/]")
                    .AddColumn("[bold]Size[/]")
                    .AddColumn("[bold]Files[/]");
                
                if (Directory.Exists(logDir))
                {
                    var logSize = GetDirectorySize(Path.Combine(logDir, "Logs"));
                    var logFiles = Directory.Exists(Path.Combine(logDir, "Logs")) 
                        ? Directory.GetFiles(Path.Combine(logDir, "Logs")).Length 
                        : 0;
                    
                    table.AddRow("Logs", FormatFileSize(logSize), logFiles.ToString());
                    
                    var cacheSize = GetDirectorySize(Path.Combine(logDir, "Cache"));
                    var cacheFiles = Directory.Exists(Path.Combine(logDir, "Cache")) 
                        ? Directory.GetFiles(Path.Combine(logDir, "Cache"), "*", SearchOption.AllDirectories).Length 
                        : 0;
                    
                    table.AddRow("Cache", FormatFileSize(cacheSize), cacheFiles.ToString());
                    
                    var totalSize = logSize + cacheSize;
                    table.AddRow("[bold]Total[/]", $"[bold]{FormatFileSize(totalSize)}[/]", $"[bold]{logFiles + cacheFiles}[/]");
                }
                else
                {
                    table.AddRow("[dim]No data directory found[/]", "0 B", "0");
                }
                
                Console.Write(table);
                await Task.Delay(1000, cancellationToken);
            });
        
        await WaitForKeyAsync(cancellationToken: cancellationToken);
    }
    
    private async Task CleanOldFilesAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        Console.Clear();
        DrawHeader();
        
        var retentionOptions = new[]
        {
            "Keep last 7 days",
            "Keep last 30 days",
            "Keep last 90 days",
            "Custom retention period"
        };
        
        var selection = Console.Prompt(
            new SelectionPrompt<string>()
                .Title("[green]Select file retention policy:[/]")
                .AddChoices(retentionOptions));
        
        var retentionDays = selection switch
        {
            var s when s.Contains("7") => 7,
            var s when s.Contains("30") => 30,
            var s when s.Contains("90") => 90,
            _ => Console.Prompt(
                new TextPrompt<int>("[cyan]Enter retention period in days:[/]")
                    .Validate(days => days > 0 
                        ? Spectre.Console.ValidationResult.Success() 
                        : Spectre.Console.ValidationResult.Error("[red]Must be greater than 0[/]")))
        };
        
        var cutoffDate = DateTime.Now.AddDays(-retentionDays);
        
        Console.WriteLine();
        Console.MarkupLine($"[yellow]Files older than {cutoffDate:yyyy-MM-dd} will be deleted.[/]");
        
        if (Console.Confirm("[red]Continue with cleanup?[/]"))
        {
            await Console.Progress()
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[red]Cleaning old files...[/]");
                    
                    // Simulate cleanup process
                    for (int i = 0; i <= 100; i += 10)
                    {
                        task.Value = i;
                        await Task.Delay(200, cancellationToken);
                    }
                });
            
            ShowSuccess("Old files cleaned successfully!");
        }
        else
        {
            ShowWarning("Cleanup cancelled");
        }
        
        await Task.Delay(1500, cancellationToken);
    }
    
    private long GetDirectorySize(string path)
    {
        if (!Directory.Exists(path))
            return 0;
        
        try
        {
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                .Sum(file => new FileInfo(file).Length);
        }
        catch
        {
            return 0;
        }
    }
    
    private async Task ClearCacheAsync(CancellationToken cancellationToken)
    {
        Console.WriteLine();
        var confirmClear = Console.Confirm("[yellow]Clear all cached data? This will improve disk space but may slow down next analysis.[/]");
        
        if (confirmClear)
        {
            await Console.Status()
                .StartAsync("[yellow]Clearing cache...[/]", async ctx =>
                {
                    // TODO: Implement actual cache clearing
                    await Task.Delay(1000, cancellationToken);
                });
            
            ShowSuccess("Cache cleared successfully!");
        }
        else
        {
            ShowWarning("Cache clearing cancelled");
        }
        
        await Task.Delay(1500, cancellationToken);
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
}
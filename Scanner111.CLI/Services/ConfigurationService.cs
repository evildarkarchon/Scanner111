using Microsoft.Extensions.Logging;
using Scanner111.CLI.Configuration;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Spectre.Console;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for managing configuration settings.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ICliSettings _settings;
    private readonly IAnsiConsole _console;
    private readonly ILogger<ConfigurationService> _logger;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
    /// </summary>
    public ConfigurationService(
        ICliSettings settings,
        IAnsiConsole console,
        ILogger<ConfigurationService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _console = console ?? throw new ArgumentNullException(nameof(console));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Lists all configuration settings.
    /// </summary>
    public async Task ListSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settings.LoadAsync(cancellationToken);
            
            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Setting")
                .AddColumn("Value")
                .AddColumn("Type");
            
            table.AddRow("DefaultGame", settings.DefaultGame.ToString(), "GameType");
            table.AddRow("AutoDetectPaths", settings.AutoDetectPaths.ToString(), "bool");
            table.AddRow("MaxParallelAnalyzers", settings.MaxParallelAnalyzers.ToString(), "int");
            table.AddRow("DefaultReportFormat", settings.DefaultReportFormat.ToString(), "ReportFormat");
            table.AddRow("Theme", settings.Theme, "string");
            table.AddRow("ShowTimestamps", settings.ShowTimestamps.ToString(), "bool");
            table.AddRow("VerboseOutput", settings.VerboseOutput.ToString(), "bool");
            table.AddRow("LogDirectory", settings.LogDirectory ?? "Default", "string");
            
            _console.Write(table);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list settings");
            _console.MarkupLine($"[red]✗[/] Failed to list settings: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets a specific configuration value.
    /// </summary>
    public async Task GetSettingAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var settings = await _settings.LoadAsync(cancellationToken);
            
            var value = key.ToLowerInvariant() switch
            {
                "defaultgame" => settings.DefaultGame.ToString(),
                "autodetectpaths" => settings.AutoDetectPaths.ToString(),
                "maxparallelanalyzers" => settings.MaxParallelAnalyzers.ToString(),
                "defaultreportformat" => settings.DefaultReportFormat.ToString(),
                "theme" => settings.Theme,
                "showtimestamps" => settings.ShowTimestamps.ToString(),
                "verboseoutput" => settings.VerboseOutput.ToString(),
                "logdirectory" => settings.LogDirectory ?? "Default",
                _ => null
            };
            
            if (value != null)
            {
                _console.MarkupLine($"[cyan]{key}[/] = [yellow]{value}[/]");
            }
            else
            {
                _console.MarkupLine($"[red]✗[/] Unknown setting: {key}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get setting {Key}", key);
            _console.MarkupLine($"[red]✗[/] Failed to get setting: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Sets a configuration value.
    /// </summary>
    public async Task SetSettingAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(value))
            {
                _console.MarkupLine("[red]✗[/] Value cannot be empty");
                return;
            }
            
            var settings = await _settings.LoadAsync(cancellationToken);
            var updated = false;
            
            switch (key.ToLowerInvariant())
            {
                case "defaultgame":
                    if (Enum.TryParse<GameType>(value, true, out var gameType))
                    {
                        settings.DefaultGame = gameType;
                        updated = true;
                    }
                    else
                    {
                        _console.MarkupLine($"[red]✗[/] Invalid GameType: {value}");
                    }
                    break;
                    
                case "autodetectpaths":
                    if (bool.TryParse(value, out var autoDetect))
                    {
                        settings.AutoDetectPaths = autoDetect;
                        updated = true;
                    }
                    else
                    {
                        _console.MarkupLine($"[red]✗[/] Invalid boolean: {value}");
                    }
                    break;
                    
                case "maxparallelanalyzers":
                    if (int.TryParse(value, out var maxAnalyzers) && maxAnalyzers >= 1 && maxAnalyzers <= 10)
                    {
                        settings.MaxParallelAnalyzers = maxAnalyzers;
                        updated = true;
                    }
                    else
                    {
                        _console.MarkupLine($"[red]✗[/] Invalid value (must be 1-10): {value}");
                    }
                    break;
                    
                case "defaultreportformat":
                    if (Enum.TryParse<ReportFormat>(value, true, out var format))
                    {
                        settings.DefaultReportFormat = format;
                        updated = true;
                    }
                    else
                    {
                        _console.MarkupLine($"[red]✗[/] Invalid ReportFormat: {value}");
                    }
                    break;
                    
                case "theme":
                    if (new[] { "Default", "Dark", "Light", "High Contrast" }.Contains(value))
                    {
                        settings.Theme = value;
                        updated = true;
                    }
                    else
                    {
                        _console.MarkupLine($"[red]✗[/] Invalid theme: {value}");
                    }
                    break;
                    
                case "showtimestamps":
                    if (bool.TryParse(value, out var showTimestamps))
                    {
                        settings.ShowTimestamps = showTimestamps;
                        updated = true;
                    }
                    else
                    {
                        _console.MarkupLine($"[red]✗[/] Invalid boolean: {value}");
                    }
                    break;
                    
                case "verboseoutput":
                    if (bool.TryParse(value, out var verbose))
                    {
                        settings.VerboseOutput = verbose;
                        updated = true;
                    }
                    else
                    {
                        _console.MarkupLine($"[red]✗[/] Invalid boolean: {value}");
                    }
                    break;
                    
                case "logdirectory":
                    settings.LogDirectory = value;
                    updated = true;
                    break;
                    
                default:
                    _console.MarkupLine($"[red]✗[/] Unknown setting: {key}");
                    break;
            }
            
            if (updated)
            {
                await _settings.SaveAsync(settings, cancellationToken);
                _console.MarkupLine($"[green]✓[/] Setting updated: {key} = {value}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set setting {Key} to {Value}", key, value);
            _console.MarkupLine($"[red]✗[/] Failed to set setting: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Resets all settings to defaults.
    /// </summary>
    public async Task ResetSettingsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _settings.ResetAsync(cancellationToken);
            _console.MarkupLine("[green]✓[/] All settings reset to defaults");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            _console.MarkupLine($"[red]✗[/] Failed to reset settings: {ex.Message}");
        }
    }
}
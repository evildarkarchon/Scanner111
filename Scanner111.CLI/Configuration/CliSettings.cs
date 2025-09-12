using Microsoft.Extensions.Logging;
using Scanner111.Core.Configuration;
using Scanner111.Core.Models;

namespace Scanner111.CLI.Configuration;

/// <summary>
/// CLI-specific settings that extend the core application settings.
/// </summary>
public class CliSpecificSettings
{
    /// <summary>
    /// Gets or sets whether to use colored console output.
    /// </summary>
    public bool UseColoredOutput { get; set; } = true;
    
    /// <summary>
    /// Gets or sets whether to clear console before running commands.
    /// </summary>
    public bool ClearConsoleOnRun { get; set; }
    
    /// <summary>
    /// Gets or sets the default command when none is specified.
    /// </summary>
    public string? DefaultCommand { get; set; }
    
    /// <summary>
    /// Gets or sets whether to show progress bars.
    /// </summary>
    public bool ShowProgressBars { get; set; } = true;
    
    /// <summary>
    /// Gets or sets CLI-specific key bindings.
    /// </summary>
    public Dictionary<string, string> KeyBindings { get; set; } = new();
}

/// <summary>
/// CLI settings manager implementation that extends the core ApplicationSettingsManager.
/// </summary>
public class CliSettingsManager : ApplicationSettingsManager, ICliSettings
{
    private readonly ILogger<CliSettingsManager> _logger;
    private CliSpecificSettings? _cliSettings;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="CliSettingsManager"/> class.
    /// </summary>
    public CliSettingsManager(ILogger<CliSettingsManager> logger)
        : base(logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Gets CLI-specific settings.
    /// </summary>
    public async Task<CliSpecificSettings> GetCliSettingsAsync(CancellationToken cancellationToken = default)
    {
        if (_cliSettings != null)
        {
            return _cliSettings;
        }
        
        // Load CLI-specific settings from extended settings
        var appSettings = await LoadAsync(cancellationToken).ConfigureAwait(false);
        
        _cliSettings = new CliSpecificSettings();
        
        // Load from extended settings if available
        if (appSettings.ExtendedSettings.TryGetValue("CLI", out var cliSettingsObj))
        {
            if (cliSettingsObj is System.Text.Json.JsonElement jsonElement)
            {
                try
                {
                    var json = jsonElement.GetRawText();
                    _cliSettings = System.Text.Json.JsonSerializer.Deserialize<CliSpecificSettings>(json) ?? new CliSpecificSettings();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize CLI settings, using defaults");
                }
            }
        }
        
        return _cliSettings;
    }
    
    /// <summary>
    /// Updates CLI-specific settings.
    /// </summary>
    public async Task UpdateCliSettingsAsync(CliSpecificSettings settings, CancellationToken cancellationToken = default)
    {
        _cliSettings = settings;
        
        // Save to extended settings
        var appSettings = await LoadAsync(cancellationToken).ConfigureAwait(false);
        appSettings.ExtendedSettings["CLI"] = settings;
        await SaveAsync(appSettings, cancellationToken).ConfigureAwait(false);
        
        _logger.LogInformation("CLI-specific settings updated successfully");
    }
}
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Configuration;

/// <summary>
/// Application settings manager implementation.
/// </summary>
public class ApplicationSettingsManager : IApplicationSettings, IDisposable
{
    private readonly string _settingsPath;
    private readonly ILogger<ApplicationSettingsManager> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);
    private readonly ConcurrentDictionary<string, object> _runtimeSettings = new();
    private ApplicationSettings? _cachedSettings;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationSettingsManager"/> class.
    /// </summary>
    public ApplicationSettingsManager(ILogger<ApplicationSettingsManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scanner111");
        
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "settings.json");
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }
    
    /// <summary>
    /// Initializes a new instance with a custom settings path.
    /// </summary>
    public ApplicationSettingsManager(string settingsPath, ILogger<ApplicationSettingsManager> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));
        
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
    }
    
    /// <summary>
    /// Loads the settings from storage.
    /// </summary>
    public async Task<ApplicationSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedSettings != null)
            {
                return _cachedSettings;
            }
            
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath, cancellationToken).ConfigureAwait(false);
                _cachedSettings = JsonSerializer.Deserialize<ApplicationSettings>(json, _jsonOptions) ?? GetDefaultSettings();
                _logger.LogDebug("Settings loaded from {Path}", _settingsPath);
            }
            else
            {
                _cachedSettings = GetDefaultSettings();
                await SaveInternalAsync(_cachedSettings, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Created default settings at {Path}", _settingsPath);
            }
            
            // Apply any runtime overrides
            foreach (var kvp in _runtimeSettings)
            {
                ApplyRuntimeSetting(_cachedSettings, kvp.Key, kvp.Value);
            }
            
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
            _cachedSettings = GetDefaultSettings();
            return _cachedSettings;
        }
        finally
        {
            _settingsLock.Release();
        }
    }
    
    /// <summary>
    /// Saves the settings to storage.
    /// </summary>
    public async Task SaveAsync(ApplicationSettings settings, CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveInternalAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _settingsLock.Release();
        }
    }
    
    private async Task SaveInternalAsync(ApplicationSettings settings, CancellationToken cancellationToken)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json, cancellationToken).ConfigureAwait(false);
            _cachedSettings = settings;
            _logger.LogInformation("Settings saved successfully to {Path}", _settingsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            throw;
        }
    }
    
    /// <summary>
    /// Resets settings to defaults.
    /// </summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var defaultSettings = GetDefaultSettings();
            await SaveInternalAsync(defaultSettings, cancellationToken).ConfigureAwait(false);
            _runtimeSettings.Clear();
            _logger.LogInformation("Settings reset to defaults");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            throw;
        }
        finally
        {
            _settingsLock.Release();
        }
    }
    
    /// <summary>
    /// Gets a specific setting value.
    /// </summary>
    public T GetSetting<T>(string key, T defaultValue = default!)
    {
        if (_runtimeSettings.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        
        if (_cachedSettings == null)
        {
            return defaultValue;
        }
        
        return GetSettingFromObject(_cachedSettings, key, defaultValue);
    }
    
    /// <summary>
    /// Sets a specific setting value.
    /// </summary>
    public async Task SetSettingAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        _runtimeSettings[key] = value!;
        
        var settings = await LoadAsync(cancellationToken).ConfigureAwait(false);
        ApplyRuntimeSetting(settings, key, value!);
        await SaveAsync(settings, cancellationToken).ConfigureAwait(false);
    }
    
    private static ApplicationSettings GetDefaultSettings()
    {
        return new ApplicationSettings
        {
            DefaultGame = GameType.Fallout4,
            AutoDetectPaths = true,
            MaxParallelAnalyzers = 4,
            DefaultReportFormat = Reporting.ReportFormat.Markdown,
            Theme = "Default",
            ShowTimestamps = false,
            VerboseOutput = false,
            DebugMode = false,
            AnalysisTimeoutSeconds = 300,
            EnableCaching = true,
            CacheExpirationMinutes = 60,
            EnableFcxModeAnalysis = true,
            LogDirectory = null,
            CustomGamePaths = new Dictionary<string, string>(),
            ExtendedSettings = new Dictionary<string, object>()
        };
    }
    
    private static T GetSettingFromObject<T>(ApplicationSettings settings, string key, T defaultValue)
    {
        var property = typeof(ApplicationSettings).GetProperty(key);
        if (property != null && property.CanRead)
        {
            var value = property.GetValue(settings);
            if (value is T typedValue)
            {
                return typedValue;
            }
        }
        
        // Check extended settings
        if (settings.ExtendedSettings.TryGetValue(key, out var extendedValue))
        {
            if (extendedValue is T typedExtendedValue)
            {
                return typedExtendedValue;
            }
            
            // Try to convert
            try
            {
                if (extendedValue is JsonElement jsonElement)
                {
                    var json = jsonElement.GetRawText();
                    var converted = JsonSerializer.Deserialize<T>(json);
                    if (converted != null)
                    {
                        return converted;
                    }
                }
            }
            catch
            {
                // Conversion failed, return default
            }
        }
        
        return defaultValue;
    }
    
    private static void ApplyRuntimeSetting(ApplicationSettings settings, string key, object value)
    {
        var property = typeof(ApplicationSettings).GetProperty(key);
        if (property != null && property.CanWrite)
        {
            try
            {
                property.SetValue(settings, value);
                return;
            }
            catch
            {
                // Property set failed, try extended settings
            }
        }
        
        // Store in extended settings
        settings.ExtendedSettings[key] = value;
    }
    
    /// <summary>
    /// Disposes resources used by the settings manager.
    /// </summary>
    public void Dispose()
    {
        _settingsLock?.Dispose();
    }
}
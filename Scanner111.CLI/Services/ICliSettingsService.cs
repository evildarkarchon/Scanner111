using Scanner111.CLI.Models;
using Scanner111.Core.Models;

/// <summary>
/// Provides functionality for managing and persisting CLI-related settings, including legacy and application-wide configurations.
/// </summary>
public interface ICliSettingsService
{
    Task<ApplicationSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(ApplicationSettings settings);
    Task SaveSettingAsync(string key, object value);
    ApplicationSettings GetDefaultSettings();

    // Backward compatibility methods for existing CLI code
    Task<CliSettings> LoadCliSettingsAsync();
    Task SaveCliSettingsAsync(CliSettings settings);
}
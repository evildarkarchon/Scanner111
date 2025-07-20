using System.Threading.Tasks;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Service for managing unified application settings
/// </summary>
public interface IApplicationSettingsService
{
    Task<ApplicationSettings> LoadSettingsAsync();
    Task SaveSettingsAsync(ApplicationSettings settings);
    Task SaveSettingAsync(string key, object value);
    ApplicationSettings GetDefaultSettings();
}
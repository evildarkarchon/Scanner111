using Scanner111.Plugins.Interface.Models;

namespace Scanner111.Plugins.Interface.Services;

public interface IPluginHost
{
    Task<string> GetApplicationDirectoryAsync();
    Task<string> GetPluginsDirectoryAsync();
    Task<string> GetConfigDirectoryAsync();
    Task<string> GetLogsDirectoryAsync();
    Task<T?> LoadConfigurationAsync<T>(string configName) where T : class, new();
    Task SaveConfigurationAsync<T>(string configName, T configuration) where T : class;
    Task LogInformationAsync(string message);
    Task LogWarningAsync(string message);
    Task LogErrorAsync(string message, Exception? exception = null);
    Task<IEnumerable<GameInfo>> GetSupportedGamesAsync();
    Task<GameInfo?> GetGameInfoAsync(string gameId);
}
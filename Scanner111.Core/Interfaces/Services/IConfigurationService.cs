namespace Scanner111.Core.Interfaces.Services;

public interface IConfigurationService
{
    Task<T?> LoadConfigurationAsync<T>(string configName) where T : class, new();
    Task SaveConfigurationAsync<T>(string configName, T configuration) where T : class;
    Task<bool> ConfigurationExistsAsync(string configName);
    Task DeleteConfigurationAsync(string configName);
}
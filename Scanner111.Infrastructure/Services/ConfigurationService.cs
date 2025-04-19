using System.Text.Json;
using Scanner111.Core.Interfaces.Services;

namespace Scanner111.Infrastructure.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly string _configDirectory;
    
    public ConfigurationService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
        _configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configurations");
    }
    
    public async Task<T?> LoadConfigurationAsync<T>(string configName) where T : class, new()
    {
        try
        {
            var filePath = GetConfigFilePath(configName);
            
            if (!await _fileSystemService.FileExistsAsync(filePath))
                return null;
                
            var json = await _fileSystemService.ReadAllTextAsync(filePath);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception)
        {
            return null;
        }
    }
    
    public async Task SaveConfigurationAsync<T>(string configName, T configuration) where T : class
    {
        var filePath = GetConfigFilePath(configName);
        var directoryPath = Path.GetDirectoryName(filePath);
        
        if (directoryPath != null && !await _fileSystemService.DirectoryExistsAsync(directoryPath))
            await _fileSystemService.CreateDirectoryAsync(directoryPath);
            
        var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions { WriteIndented = true });
        await _fileSystemService.WriteAllTextAsync(filePath, json);
    }
    
    public async Task<bool> ConfigurationExistsAsync(string configName)
    {
        var filePath = GetConfigFilePath(configName);
        return await _fileSystemService.FileExistsAsync(filePath);
    }
    
    public async Task DeleteConfigurationAsync(string configName)
    {
        var filePath = GetConfigFilePath(configName);
        if (await _fileSystemService.FileExistsAsync(filePath))
            await _fileSystemService.DeleteFileAsync(filePath);
    }
    
    private string GetConfigFilePath(string configName)
    {
        return Path.Combine(_configDirectory, $"{configName}.json");
    }
}
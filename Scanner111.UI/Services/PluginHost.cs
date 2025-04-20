using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Plugins.Interface.Models;
using Scanner111.Plugins.Interface.Services;

namespace Scanner111.UI.Services;

public class PluginHost(
    IFileSystemService fileSystemService,
    IConfigurationService configurationService,
    IGameDetectionService gameDetectionService)
    : IPluginHost
{
    public async Task<string> GetApplicationDirectoryAsync()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        return await Task.FromResult(appDir);
    }
    
    public async Task<string> GetPluginsDirectoryAsync()
    {
        var pluginsDir = Path.Combine(await GetApplicationDirectoryAsync(), "Plugins");
        if (!await fileSystemService.DirectoryExistsAsync(pluginsDir))
            await fileSystemService.CreateDirectoryAsync(pluginsDir);
        return pluginsDir;
    }
    
    public async Task<string> GetConfigDirectoryAsync()
    {
        var configDir = Path.Combine(await GetApplicationDirectoryAsync(), "Configurations");
        if (!await fileSystemService.DirectoryExistsAsync(configDir))
            await fileSystemService.CreateDirectoryAsync(configDir);
        return configDir;
    }
    
    public async Task<string> GetLogsDirectoryAsync()
    {
        var logsDir = Path.Combine(await GetApplicationDirectoryAsync(), "Logs");
        if (!await fileSystemService.DirectoryExistsAsync(logsDir))
            await fileSystemService.CreateDirectoryAsync(logsDir);
        return logsDir;
    }
    
    public async Task<T?> LoadConfigurationAsync<T>(string configName) where T : class, new()
    {
        return await configurationService.LoadConfigurationAsync<T>(configName);
    }
    
    public async Task SaveConfigurationAsync<T>(string configName, T configuration) where T : class
    {
        await configurationService.SaveConfigurationAsync(configName, configuration);
    }
    
    public async Task LogInformationAsync(string message)
    {
        // In a real application, this would use a proper logging framework
        var logFile = Path.Combine(await GetLogsDirectoryAsync(), "plugin.log");
        var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [INFO] {message}{Environment.NewLine}";
        await fileSystemService.WriteAllTextAsync(logFile, logLine);
    }
    
    public async Task LogWarningAsync(string message)
    {
        // In a real application, this would use a proper logging framework
        var logFile = Path.Combine(await GetLogsDirectoryAsync(), "plugin.log");
        var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [WARN] {message}{Environment.NewLine}";
        await fileSystemService.WriteAllTextAsync(logFile, logLine);
    }
    
    public async Task LogErrorAsync(string message, Exception? exception = null)
    {
        // In a real application, this would use a proper logging framework
        var logFile = Path.Combine(await GetLogsDirectoryAsync(), "plugin.log");
        var exceptionMessage = exception != null ? $" Exception: {exception.Message}" : string.Empty;
        var logLine = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [ERROR] {message}{exceptionMessage}{Environment.NewLine}";
        await fileSystemService.WriteAllTextAsync(logFile, logLine);
    }
    
    public async Task<IEnumerable<GameInfo>> GetSupportedGamesAsync()
    {
        var detectedGames = await gameDetectionService.DetectInstalledGamesAsync();
        
        return detectedGames.Select(g => new GameInfo
        {
            Id = g.Id,
            Name = g.Name,
            ExecutableNames = [g.ExecutableName],
            Version = g.Version,
            InstallPath = g.InstallPath,
            DocumentsPath = g.DocumentsPath,
            IsInstalled = !string.IsNullOrEmpty(g.InstallPath),
            IsSupported = true
        });
    }
    
    public async Task<GameInfo?> GetGameInfoAsync(string gameId)
    {
        var games = await GetSupportedGamesAsync();
        return games.FirstOrDefault(g => g.Id == gameId);
    }
}
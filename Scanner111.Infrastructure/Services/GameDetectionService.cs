using System.Diagnostics;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Core.Models;

namespace Scanner111.Infrastructure.Services;

public class GameDetectionService : IGameDetectionService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IPluginSystemService _pluginSystemService;
    
    public GameDetectionService(
        IFileSystemService fileSystemService,
        IPluginSystemService pluginSystemService)
    {
        _fileSystemService = fileSystemService;
        _pluginSystemService = pluginSystemService;
    }
    
    public async Task<Game?> DetectGameAsync(string possibleInstallPath)
    {
        if (!await _fileSystemService.DirectoryExistsAsync(possibleInstallPath))
            return null;
            
        var plugins = await _pluginSystemService.DiscoverPluginsAsync();
        
        foreach (var plugin in plugins)
        {
            var game = await plugin.DetectGameAsync(possibleInstallPath);
            if (game != null)
                return game;
        }
        
        return null;
    }
    
    public async Task<IEnumerable<Game>> DetectInstalledGamesAsync()
    {
        var result = new List<Game>();
        var plugins = await _pluginSystemService.DiscoverPluginsAsync();
        
        // Common Steam installation paths
        var possiblePaths = new List<string>
        {
            @"C:\Program Files (x86)\Steam\steamapps\common",
            @"C:\Program Files\Steam\steamapps\common",
            @"D:\Steam\steamapps\common",
            @"E:\Steam\steamapps\common"
        };
        
        // Common GOG installation paths
        possiblePaths.AddRange(new[]
        {
            @"C:\Program Files (x86)\GOG Galaxy\Games",
            @"C:\Program Files\GOG Galaxy\Games",
            @"D:\GOG Galaxy\Games",
            @"E:\GOG Galaxy\Games"
        });
        
        // Look in all possible paths
        foreach (var basePath in possiblePaths)
        {
            if (!await _fileSystemService.DirectoryExistsAsync(basePath))
                continue;
                
            var gameDirs = await _fileSystemService.GetDirectoriesAsync(basePath, "*", false);
            
            foreach (var gameDir in gameDirs)
            {
                var game = await DetectGameAsync(gameDir);
                if (game != null && !result.Any(g => g.Id == game.Id))
                    result.Add(game);
            }
        }
        
        return result;
    }
    
    public async Task<string?> FindGameDocumentsPathAsync(Game game)
    {
        // Check if game already has a documents path
        if (!string.IsNullOrEmpty(game.DocumentsPath) && 
            await _fileSystemService.DirectoryExistsAsync(game.DocumentsPath))
            return game.DocumentsPath;
            
        // Try to find the documents path based on the game
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var possiblePaths = new List<string>();
        
        switch (game.Id)
        {
            case "Fallout4":
                possiblePaths.Add(Path.Combine(documentsPath, "My Games", "Fallout4"));
                break;
            case "Fallout4VR":
                possiblePaths.Add(Path.Combine(documentsPath, "My Games", "Fallout4VR"));
                break;
            case "SkyrimSE":
                possiblePaths.Add(Path.Combine(documentsPath, "My Games", "Skyrim Special Edition"));
                break;
            default:
                return null;
        }
        
        foreach (var path in possiblePaths)
        {
            if (await _fileSystemService.DirectoryExistsAsync(path))
                return path;
        }
        
        return null;
    }
    
    public async Task<string?> FindGameInstallPathAsync(string gameName)
    {
        // Common Steam installation paths
        var possiblePaths = new List<string>
        {
            @"C:\Program Files (x86)\Steam\steamapps\common",
            @"C:\Program Files\Steam\steamapps\common",
            @"D:\Steam\steamapps\common",
            @"E:\Steam\steamapps\common"
        };
        
        // Common GOG installation paths
        possiblePaths.AddRange(new[]
        {
            @"C:\Program Files (x86)\GOG Galaxy\Games",
            @"C:\Program Files\GOG Galaxy\Games",
            @"D:\GOG Galaxy\Games",
            @"E:\GOG Galaxy\Games"
        });
        
        // Define common folder names for each game
        var folderNames = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        folderNames["Fallout4"] = new List<string> { "Fallout 4" };
        folderNames["Fallout4VR"] = new List<string> { "Fallout 4 VR" };
        folderNames["SkyrimSE"] = new List<string> { "Skyrim Special Edition" };
        
        if (!folderNames.TryGetValue(gameName, out var gamefolderNames))
            return null;
            
        foreach (var basePath in possiblePaths)
        {
            if (!await _fileSystemService.DirectoryExistsAsync(basePath))
                continue;
                
            foreach (var folderName in gamefolderNames)
            {
                var fullPath = Path.Combine(basePath, folderName);
                if (await _fileSystemService.DirectoryExistsAsync(fullPath))
                    return fullPath;
            }
        }
        
        return null;
    }
    
    public async Task<string?> GetGameVersionAsync(string exePath)
    {
        if (!await _fileSystemService.FileExistsAsync(exePath))
            return null;
            
        try
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(exePath);
            return versionInfo.FileVersion;
        }
        catch
        {
            return null;
        }
    }
}
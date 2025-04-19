using Scanner111.Plugins.Interface.Models;

namespace Scanner111.Plugins.Interface.Services;

public interface IGamePluginBase
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Version { get; }
    string[] SupportedGameIds { get; }
    
    Task InitializeAsync(IPluginHost host);
    Task ShutdownAsync();
    Task<bool> CanHandleGameAsync(GameInfo game);
    Task<GameInfo?> DetectGameAsync(string possibleInstallPath);
    Task<bool> ValidateGameFilesAsync(GameInfo game);
}
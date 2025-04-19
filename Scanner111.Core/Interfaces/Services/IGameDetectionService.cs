using Scanner111.Core.Models;

namespace Scanner111.Core.Interfaces.Services;

public interface IGameDetectionService
{
    Task<Game?> DetectGameAsync(string possibleInstallPath);
    Task<IEnumerable<Game>> DetectInstalledGamesAsync();
    Task<string?> FindGameDocumentsPathAsync(Game game);
    Task<string?> FindGameInstallPathAsync(string gameName);
    Task<string?> GetGameVersionAsync(string exePath);
}
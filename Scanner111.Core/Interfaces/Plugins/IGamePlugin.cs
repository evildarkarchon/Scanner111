using Scanner111.Core.Models;

namespace Scanner111.Core.Interfaces.Plugins;

public interface IGamePlugin
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    string Version { get; }
    string[] SupportedGameIds { get; }
    string GameExecutable { get; }
    Task<bool> CanHandleGameAsync(Game game);
    Task<Game?> DetectGameAsync(string possibleInstallPath);
    Task<CrashLog> AnalyzeCrashLogAsync(string logContent, Game game);
    Task<IEnumerable<ModIssue>> AnalyzePluginsAsync(IEnumerable<Plugin> plugins);
    Task<IEnumerable<string>> GetRequiredFilesAsync();
    Task<Dictionary<string, string>> GetGameConfigurationAsync(Game game);
    Task<bool> ValidateGameFilesAsync(Game game);
}
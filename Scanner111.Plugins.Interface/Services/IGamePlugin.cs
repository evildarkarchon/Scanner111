using Scanner111.Plugins.Interface.Models;

namespace Scanner111.Plugins.Interface.Services;

public interface IGamePlugin : IGamePluginBase
{
    Task<CrashLogInfo> AnalyzeCrashLogAsync(string logContent, GameInfo game);
    Task<IEnumerable<ModIssueInfo>> AnalyzePluginsAsync(IEnumerable<PluginInfo> plugins);
    Task<IEnumerable<string>> GetRequiredFilesAsync();
    Task<Dictionary<string, string>> GetGameConfigurationAsync(GameInfo game);
    Task<string> GetCrashGeneratorNameAsync();
    Task<Dictionary<string, string>> ExtractLoadedPluginsAsync(string logContent);
    Task<List<string>> ExtractCallStackAsync(string logContent);
    Task<List<string>> DetectIssuesAsync(string logContent);

}
using Scanner111.Core.Models;

namespace Scanner111.Core.Interfaces.Services;

public interface IPluginService
{
    Task<IEnumerable<Plugin>> LoadPluginsAsync(string gameId);
    Task<IEnumerable<ModIssue>> AnalyzePluginCompatibilityAsync(IEnumerable<Plugin> plugins);
    Task<IEnumerable<ModIssue>> GetPluginIssuesAsync(string pluginName);
    Task<bool> HasPluginWithIssuesAsync(string gameId);
}
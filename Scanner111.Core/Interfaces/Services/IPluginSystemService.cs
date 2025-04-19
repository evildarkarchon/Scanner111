using Scanner111.Core.Interfaces.Plugins;

namespace Scanner111.Core.Interfaces.Services;

public interface IPluginSystemService
{
    Task InitializeAsync();
    Task<IEnumerable<IGamePlugin>> DiscoverPluginsAsync();
    Task<IGamePlugin?> GetPluginForGameAsync(string gameId);
    Task RegisterPluginAsync(IGamePlugin plugin);
    Task UnregisterPluginAsync(string pluginId);
}
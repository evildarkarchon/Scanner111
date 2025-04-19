using Scanner111.Core.Interfaces.Plugins;
using Scanner111.Core.Interfaces.Services;

namespace Scanner111.Infrastructure.Services;

public class PluginSystemService : IPluginSystemService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly List<IGamePlugin> _plugins = new();
    private bool _isInitialized;
    
    public PluginSystemService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }
    
    public async Task InitializeAsync()
    {
        if (_isInitialized)
            return;
            
        _plugins.Clear();
        
        // In a full implementation, this would load plugins from DLLs dynamically
        // For this example, we'll assume plugins are added manually through the RegisterPluginAsync method
        
        _isInitialized = true;
        await Task.CompletedTask;
    }
    
    public async Task<IEnumerable<IGamePlugin>> DiscoverPluginsAsync()
    {
        if (!_isInitialized)
            await InitializeAsync();
            
        return _plugins;
    }
    
    public async Task<IGamePlugin?> GetPluginForGameAsync(string gameId)
    {
        var plugins = await DiscoverPluginsAsync();
        return plugins.FirstOrDefault(p => p.Id == gameId);
    }
    
    public async Task RegisterPluginAsync(IGamePlugin plugin)
    {
        if (!_isInitialized)
            await InitializeAsync();
            
        if (_plugins.Any(p => p.Id == plugin.Id))
            return;
            
        _plugins.Add(plugin);
    }
    
    public async Task UnregisterPluginAsync(string pluginId)
    {
        if (!_isInitialized)
            await InitializeAsync();
            
        var plugin = _plugins.FirstOrDefault(p => p.Id == pluginId);
        if (plugin != null)
            _plugins.Remove(plugin);
            
        await Task.CompletedTask;
    }
}
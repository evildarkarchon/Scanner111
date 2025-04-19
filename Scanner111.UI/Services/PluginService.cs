using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Scanner111.Core.Interfaces.Plugins;
using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Core.Models;

namespace Scanner111.UI.Services;

public class PluginService : IPluginService
{
    private readonly IPluginRepository _pluginRepository;
    private readonly IPluginSystemService _pluginSystemService;
    
    public PluginService(
        IPluginRepository pluginRepository,
        IPluginSystemService pluginSystemService)
    {
        _pluginRepository = pluginRepository;
        _pluginSystemService = pluginSystemService;
    }
    
    public async Task<IEnumerable<Plugin>> LoadPluginsAsync(string gameId)
    {
        var gamePlugin = await _pluginSystemService.GetPluginForGameAsync(gameId);
        if (gamePlugin == null)
            return Enumerable.Empty<Plugin>();
            
        // This would normally scan the game's load order and mod directory
        // For simplicity, we'll just return some sample plugins
        var plugins = new List<Plugin>
        {
            new Plugin
            {
                Name = "Fallout4.esm",
                FileName = "Fallout4.esm",
                FilePath = "Data\\Fallout4.esm",
                LoadOrderId = "00",
                Type = PluginType.Esm,
                IsEnabled = true,
                IsOfficial = true,
                IsMaster = true,
                HasIssues = false
            },
            new Plugin
            {
                Name = "DLCRobot.esm",
                FileName = "DLCRobot.esm",
                FilePath = "Data\\DLCRobot.esm",
                LoadOrderId = "01",
                Type = PluginType.Esm,
                IsEnabled = true,
                IsOfficial = true,
                IsMaster = true,
                HasIssues = false
            },
            new Plugin
            {
                Name = "Unofficial Fallout 4 Patch.esp",
                FileName = "Unofficial Fallout 4 Patch.esp",
                FilePath = "Data\\Unofficial Fallout 4 Patch.esp",
                LoadOrderId = "02",
                Type = PluginType.Esp,
                IsEnabled = true,
                IsOfficial = false,
                IsMaster = false,
                HasIssues = false
            },
            new Plugin
            {
                Name = "ClassicHolsteredWeapons.esp",
                FileName = "ClassicHolsteredWeapons.esp",
                FilePath = "Data\\ClassicHolsteredWeapons.esp",
                LoadOrderId = "03",
                Type = PluginType.Esp,
                IsEnabled = true,
                IsOfficial = false,
                IsMaster = false,
                HasIssues = true
            }
        };
        
        // Save to repository
        foreach (var plugin in plugins)
        {
            if (!await _pluginRepository.ExistsAsync(plugin.Name))
                await _pluginRepository.AddAsync(plugin);
        }
        
        return plugins;
    }
    
    public async Task<IEnumerable<ModIssue>> AnalyzePluginCompatibilityAsync(IEnumerable<Plugin> plugins)
    {
        var issues = new List<ModIssue>();
        
        // Check for known issues with specific plugins
        foreach (var plugin in plugins)
        {
            var pluginIssues = await GetPluginIssuesAsync(plugin.Name);
            issues.AddRange(pluginIssues);
        }
        
        // Check for conflicts between plugins
        await CheckPluginConflictsAsync(plugins, issues);
        
        return issues;
    }
    
    public async Task<IEnumerable<ModIssue>> GetPluginIssuesAsync(string pluginName)
    {
        // This would normally query a database of known issues
        // For simplicity, we'll just return some sample issues for known problematic plugins
        var issues = new List<ModIssue>();
        
        if (pluginName.Equals("ClassicHolsteredWeapons.esp", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ModIssue
            {
                Id = Guid.NewGuid().ToString(),
                PluginName = pluginName,
                Description = "Classic Holstered Weapons can cause crashes with certain body/skeleton mods",
                Severity = 5,
                IssueType = ModIssueType.Conflict,
                Solution = "Use a compatibility patch or disable one of the conflicting mods",
                PatchLinks = new List<string> { "https://www.nexusmods.com/fallout4/articles/2496" }
            });
        }
        
        return await Task.FromResult(issues);
    }
    
    public async Task<bool> HasPluginWithIssuesAsync(string gameId)
    {
        var plugins = await _pluginRepository.GetWithIssuesAsync(gameId);
        return plugins.Any();
    }
    
    private async Task CheckPluginConflictsAsync(IEnumerable<Plugin> plugins, List<ModIssue> issues)
    {
        // Check for conflicts between plugins
        var pluginList = plugins.ToList();
        
        // Example: Check if Classic Holstered Weapons and any body mod are both enabled
        var hasClassicHolsteredWeapons = pluginList.Any(p => 
            p.Name.Equals("ClassicHolsteredWeapons.esp", StringComparison.OrdinalIgnoreCase) && p.IsEnabled);
            
        var hasBodyMod = pluginList.Any(p => 
            (p.Name.Contains("CBBE", StringComparison.OrdinalIgnoreCase) || 
             p.Name.Contains("BodyTalk", StringComparison.OrdinalIgnoreCase) ||
             p.Name.Contains("AtomicBeauty", StringComparison.OrdinalIgnoreCase)) && 
            p.IsEnabled);
            
        if (hasClassicHolsteredWeapons && hasBodyMod)
        {
            issues.Add(new ModIssue
            {
                Id = Guid.NewGuid().ToString(),
                PluginName = "ClassicHolsteredWeapons.esp",
                Description = "Classic Holstered Weapons may conflict with body mods",
                Severity = 4,
                IssueType = ModIssueType.Conflict,
                Solution = "Use a compatibility patch or disable one of the conflicting mods",
                PatchLinks = new List<string> { "https://www.nexusmods.com/fallout4/articles/2496" }
            });
        }
        
        await Task.CompletedTask;
    }
}
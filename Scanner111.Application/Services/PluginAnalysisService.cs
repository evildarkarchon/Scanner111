using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Core.Models;
using Scanner111.Application.DTOs;

namespace Scanner111.Application.Services;

public class PluginAnalysisService
{
    private readonly IPluginRepository _pluginRepository;
    private readonly IPluginService _pluginService;
    private readonly IGameRepository _gameRepository;
    
    public PluginAnalysisService(
        IPluginRepository pluginRepository,
        IPluginService pluginService,
        IGameRepository gameRepository)
    {
        _pluginRepository = pluginRepository;
        _pluginService = pluginService;
        _gameRepository = gameRepository;
    }
    
    public async Task<IEnumerable<PluginDto>> GetPluginsForGameAsync(string gameId)
    {
        var plugins = await _pluginRepository.GetByGameIdAsync(gameId);
        return plugins.Select(p => new PluginDto
        {
            Name = p.Name,
            FileName = p.FileName,
            LoadOrderId = p.LoadOrderId,
            Type = p.Type.ToString(),
            IsEnabled = p.IsEnabled,
            IsOfficial = p.IsOfficial,
            HasIssues = p.HasIssues
        });
    }
    
    public async Task<IEnumerable<ModIssueDto>> GetIssuesForPluginAsync(string pluginName)
    {
        var issues = await _pluginService.GetPluginIssuesAsync(pluginName);
        return issues.Select(i => new ModIssueDto
        {
            Id = i.Id,
            PluginName = i.PluginName,
            Description = i.Description,
            Severity = i.Severity,
            IssueType = i.IssueType.ToString(),
            Solution = i.Solution,
            PatchLinks = i.PatchLinks
        });
    }
    
    public async Task<IEnumerable<ModIssueDto>> AnalyzeLoadOrderAsync(string gameId)
    {
        var plugins = await _pluginRepository.GetEnabledAsync(gameId);
        var issues = await _pluginService.AnalyzePluginCompatibilityAsync(plugins);
        
        return issues.Select(i => new ModIssueDto
        {
            Id = i.Id,
            PluginName = i.PluginName,
            Description = i.Description,
            Severity = i.Severity,
            IssueType = i.IssueType.ToString(),
            Solution = i.Solution,
            PatchLinks = i.PatchLinks
        });
    }
    
    public async Task LoadPluginsAsync(string gameId)
    {
        await _pluginService.LoadPluginsAsync(gameId);
    }
}
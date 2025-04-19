using Scanner111.Core.Interfaces.Repositories;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Core.Models;
using Scanner111.Application.DTOs;

namespace Scanner111.Application.Services;

public class CrashLogService
{
    private readonly ICrashLogRepository _crashLogRepository;
    private readonly ILogAnalyzerService _logAnalyzerService;
    private readonly IFileSystemService _fileSystemService;
    private readonly IGameRepository _gameRepository;
    
    public CrashLogService(
        ICrashLogRepository crashLogRepository,
        ILogAnalyzerService logAnalyzerService,
        IFileSystemService fileSystemService,
        IGameRepository gameRepository)
    {
        _crashLogRepository = crashLogRepository;
        _logAnalyzerService = logAnalyzerService;
        _fileSystemService = fileSystemService;
        _gameRepository = gameRepository;
    }
    
    public async Task<IEnumerable<CrashLogDto>> GetAllCrashLogsAsync()
    {
        var crashLogs = await _crashLogRepository.GetAllAsync();
        return await MapToDtosAsync(crashLogs);
    }
    
    public async Task<IEnumerable<CrashLogDto>> GetCrashLogsByGameAsync(string gameId)
    {
        var crashLogs = await _crashLogRepository.GetByGameIdAsync(gameId);
        return await MapToDtosAsync(crashLogs);
    }
    
    public async Task<CrashLogDetailDto?> GetCrashLogDetailAsync(string id)
    {
        var crashLog = await _crashLogRepository.GetByIdAsync(id);
        if (crashLog == null)
            return null;
            
        var game = await _gameRepository.GetByIdAsync(crashLog.GameId);
        
        var result = new CrashLogDetailDto
        {
            Id = crashLog.Id,
            FileName = crashLog.FileName,
            FilePath = crashLog.FilePath,
            CrashTime = crashLog.CrashTime,
            GameName = game?.Name ?? "Unknown",
            GameVersion = crashLog.GameVersion,
            CrashGenVersion = crashLog.CrashGenVersion,
            MainError = crashLog.MainError,
            IsAnalyzed = crashLog.IsAnalyzed,
            IsSolved = crashLog.IsSolved,
            IssueCount = crashLog.DetectedIssues.Count,
            PluginCount = crashLog.LoadedPlugins.Count,
            CallStack = crashLog.CallStack,
            RawContent = await _fileSystemService.ReadAllTextAsync(crashLog.FilePath)
        };
        
        // Map plugins
        foreach (var plugin in crashLog.LoadedPlugins)
        {
            result.LoadedPlugins.Add(new PluginDto
            {
                Name = plugin.Key,
                LoadOrderId = plugin.Value,
                FileName = plugin.Key,
                Type = DeterminePluginType(plugin.Key),
                IsEnabled = true,
                IsOfficial = IsOfficialPlugin(plugin.Key)
            });
        }
        
        // Map issues
        foreach (var issue in crashLog.DetectedIssues)
        {
            // In a real app, this would fetch detailed issue information from a repository
            result.DetectedIssues.Add(new ModIssueDto
            {
                Id = Guid.NewGuid().ToString(),
                Description = issue,
                Severity = 3,
                IssueType = "Crash",
                PluginName = DetermineAffectedPlugin(issue, crashLog.LoadedPlugins.Keys.ToList())
            });
        }
        
        return result;
    }
    
    public async Task<string> AnalyzeCrashLogAsync(string filePath)
    {
        var crashLog = await _logAnalyzerService.AnalyzeCrashLogAsync(filePath);
        await _crashLogRepository.AddAsync(crashLog);
        return crashLog.Id;
    }
    
    public async Task<IEnumerable<string>> ScanForCrashLogsAsync(string folderPath)
    {
        var crashLogFiles = await _fileSystemService.GetFilesAsync(folderPath, "crash-*.log", true);
        var results = new List<string>();
        
        foreach (var file in crashLogFiles)
        {
            var crashLog = await _logAnalyzerService.AnalyzeCrashLogAsync(file);
            await _crashLogRepository.AddAsync(crashLog);
            results.Add(crashLog.Id);
        }
        
        return results;
    }
    
    private async Task<IEnumerable<CrashLogDto>> MapToDtosAsync(IEnumerable<CrashLog> crashLogs)
    {
        var result = new List<CrashLogDto>();
        
        foreach (var crashLog in crashLogs)
        {
            var game = await _gameRepository.GetByIdAsync(crashLog.GameId);
            
            result.Add(new CrashLogDto
            {
                Id = crashLog.Id,
                FileName = crashLog.FileName,
                FilePath = crashLog.FilePath,
                CrashTime = crashLog.CrashTime,
                GameName = game?.Name ?? "Unknown",
                MainError = crashLog.MainError,
                IsAnalyzed = crashLog.IsAnalyzed,
                IsSolved = crashLog.IsSolved,
                IssueCount = crashLog.DetectedIssues.Count,
                PluginCount = crashLog.LoadedPlugins.Count
            });
        }
        
        return result;
    }
    
    private string DeterminePluginType(string pluginName)
    {
        if (pluginName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            return "DLL";
        if (pluginName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase))
            return "ESM";
        if (pluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            return "ESL";
        return "ESP";
    }
    
    private bool IsOfficialPlugin(string pluginName)
    {
        var officialPlugins = new[]
        {
            "Fallout4.esm",
            "DLCRobot.esm",
            "DLCworkshop01.esm",
            "DLCCoast.esm",
            "DLCworkshop02.esm",
            "DLCworkshop03.esm",
            "DLCNukaWorld.esm",
            "Fallout4VR.esm"
        };
        
        return officialPlugins.Contains(pluginName, StringComparer.OrdinalIgnoreCase);
    }
    
    private string DetermineAffectedPlugin(string issue, List<string> loadedPlugins)
    {
        foreach (var plugin in loadedPlugins)
        {
            if (issue.Contains(plugin, StringComparison.OrdinalIgnoreCase))
                return plugin;
        }
        
        return "Unknown";
    }
}
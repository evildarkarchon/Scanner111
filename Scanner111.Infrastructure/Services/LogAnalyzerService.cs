// Scanner111.Infrastructure/Services/LogAnalyzerService.cs
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Scanner111.Core.Interfaces.Services;
using Scanner111.Core.Models;

namespace Scanner111.Infrastructure.Services;

public class LogAnalyzerService : ILogAnalyzerService
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IGameDetectionService _gameDetectionService;
    
    public LogAnalyzerService(
        IFileSystemService fileSystemService,
        IGameDetectionService gameDetectionService)
    {
        _fileSystemService = fileSystemService;
        _gameDetectionService = gameDetectionService;
    }
    
    public async Task<CrashLog> AnalyzeCrashLogAsync(string filePath)
    {
        if (!await _fileSystemService.FileExistsAsync(filePath))
            throw new FileNotFoundException("Crash log file not found.", filePath);
            
        var content = await _fileSystemService.ReadAllTextAsync(filePath);
        var fileName = Path.GetFileName(filePath);
        
        // Extract basic crash log information
        var crashLog = new CrashLog
        {
            Id = Guid.NewGuid().ToString(),
            FileName = fileName,
            FilePath = filePath,
            CrashTime = ExtractCrashTime(fileName, content),
            GameId = await DetermineGameId(content),
            GameVersion = ExtractGameVersion(content),
            CrashGenVersion = ExtractCrashGenVersion(content),
            MainError = ExtractMainError(content),
            IsAnalyzed = true,
            IsSolved = false
        };
        
        // Extract plugins
        var plugins = await ExtractPluginsFromContentAsync(content);
        foreach (var plugin in plugins)
        {
            crashLog.Plugins.Add(new CrashLogPlugin
            {
                Id = Guid.NewGuid().ToString(),
                CrashLogId = crashLog.Id,
                PluginName = plugin.Key,
                LoadOrderId = plugin.Value,
                CrashLog = crashLog
            });
        }
        
        // Extract call stack
        var callStack = await ExtractCallStackFromContentAsync(content);
        for (int i = 0; i < callStack.Count; i++)
        {
            crashLog.CallStackEntries.Add(new CrashLogCallStack
            {
                Id = Guid.NewGuid().ToString(),
                CrashLogId = crashLog.Id,
                Order = i,
                Entry = callStack[i],
                CrashLog = crashLog
            });
        }
        
        // Extract issues
        var issues = DetectIssues(content);
        foreach (var issue in issues)
        {
            crashLog.DetectedIssues.Add(new CrashLogIssue
            {
                Id = Guid.NewGuid().ToString(),
                CrashLogId = crashLog.Id,
                Description = issue,
                CrashLog = crashLog
            });
        }
        
        return crashLog;
    }
    
    public async Task<IEnumerable<string>> ExtractPluginsFromLogAsync(string filePath)
    {
        var content = await _fileSystemService.ReadAllTextAsync(filePath);
        var plugins = await ExtractPluginsFromContentAsync(content);
        return plugins.Keys;
    }
    
    public async Task<IEnumerable<string>> ExtractErrorsFromLogAsync(string filePath)
    {
        var content = await _fileSystemService.ReadAllTextAsync(filePath);
        return DetectIssues(content);
    }
    
    public async Task<IEnumerable<string>> ExtractCallStackFromLogAsync(string filePath)
    {
        var content = await _fileSystemService.ReadAllTextAsync(filePath);
        return await ExtractCallStackFromContentAsync(content);
    }
    
    public async Task GenerateReportAsync(CrashLog crashLog, string outputPath)
    {
        // Basic report generation - in a real application, this would be much more sophisticated
        var report = new StringBuilder();
        
        report.AppendLine($"# Crash Log Analysis Report: {crashLog.FileName}");
        report.AppendLine();
        report.AppendLine($"**Date:** {crashLog.CrashTime:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"**Game:** {crashLog.GameId} {crashLog.GameVersion}");
        report.AppendLine($"**CrashGen:** {crashLog.CrashGenVersion}");
        report.AppendLine();
        report.AppendLine("## Main Error");
        report.AppendLine(crashLog.MainError);
        report.AppendLine();
        report.AppendLine("## Detected Issues");
        
        foreach (var issue in crashLog.DetectedIssues)
        {
            report.AppendLine($"- {issue.Description}");
        }
        
        report.AppendLine();
        report.AppendLine("## Loaded Plugins");
        
        foreach (var plugin in crashLog.Plugins)
        {
            report.AppendLine($"- [{plugin.LoadOrderId}] {plugin.PluginName}");
        }
        
        report.AppendLine();
        report.AppendLine("## Call Stack");
        
        foreach (var entry in crashLog.CallStackEntries.OrderBy(e => e.Order))
        {
            report.AppendLine(entry.Entry);
        }
        
        await _fileSystemService.WriteAllTextAsync(outputPath, report.ToString());
    }
    
    private DateTime ExtractCrashTime(string fileName, string content)
    {
        // Try to extract date from filename (e.g., "crash-2023-01-01-12-30-45.log")
        var datePattern = @"crash-(\d{4}-\d{2}-\d{2}-\d{2}-\d{2}-\d{2})";
        var match = Regex.Match(fileName, datePattern);
        
        if (match.Success)
        {
            var dateString = match.Groups[1].Value;
            if (DateTime.TryParseExact(
                dateString,
                "yyyy-MM-dd-HH-mm-ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
            {
                return date;
            }
        }
        
        // Fall back to file creation time
        return File.GetCreationTime(fileName);
    }
    
    private async Task<string> DetermineGameId(string content)
    {
        // In a real application, this would be more sophisticated
        if (content.Contains("Fallout4.exe") || content.Contains("Fallout 4"))
            return "Fallout4";
        if (content.Contains("Fallout4VR.exe") || content.Contains("Fallout 4 VR"))
            return "Fallout4VR";
        if (content.Contains("SkyrimSE.exe") || content.Contains("Skyrim Special Edition"))
            return "SkyrimSE";
        
        // Default to Fallout 4 for this example
        return "Fallout4";
    }
    
    private string ExtractGameVersion(string content)
    {
        // In a real application, this would be more sophisticated
        var versionPattern = @"(?:Fallout 4|Skyrim Special Edition|Fallout 4 VR)[^\d]*(\d+\.\d+\.\d+(?:\.\d+)?)";
        var match = Regex.Match(content, versionPattern);
        
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
    
    private string ExtractCrashGenVersion(string content)
    {
        // In a real application, this would be more sophisticated
        var versionPattern = @"(?:Buffout 4|Crash Logger)[^\d]*(?:v|version)?[^\d]*(\d+\.\d+\.\d+(?:\.\d+)?)";
        var match = Regex.Match(content, versionPattern, RegexOptions.IgnoreCase);
        
        return match.Success ? $"v{match.Groups[1].Value}" : "Unknown";
    }
    
    private string ExtractMainError(string content)
    {
        // In a real application, this would be more sophisticated
        var errorPattern = @"Unhandled exception[^\n]*\n([^\n]+)";
        var match = Regex.Match(content, errorPattern);
        
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown error";
    }
    
    private async Task<Dictionary<string, string>> ExtractPluginsFromContentAsync(string content)
    {
        // In a real application, this would be more sophisticated
        var result = new Dictionary<string, string>();
        var pluginSection = false;
        
        using var reader = new StringReader(content);
        string? line;
        
        while ((line = await Task.Run(() => reader.ReadLine())) != null)
        {
            if (line.Trim() == "PLUGINS:")
            {
                pluginSection = true;
                continue;
            }
            
            if (pluginSection && string.IsNullOrWhiteSpace(line))
                break;
                
            if (pluginSection)
            {
                var match = Regex.Match(line, @"\s*\[(FE:?[0-9A-F]+|[0-9A-F]+)\]\s*(.+?\.es[pml])");
                if (match.Success)
                {
                    var loadOrderId = match.Groups[1].Value;
                    var pluginName = match.Groups[2].Value;
                    result[pluginName] = loadOrderId;
                }
                else if (line.Contains(".dll"))
                {
                    var dllName = line.Trim();
                    result[dllName] = "DLL";
                }
            }
        }
        
        return result;
    }
    
    private async Task<List<string>> ExtractCallStackFromContentAsync(string content)
    {
        // In a real application, this would be more sophisticated
        var result = new List<string>();
        var callStackSection = false;
        
        using var reader = new StringReader(content);
        string? line;
        
        while ((line = await Task.Run(() => reader.ReadLine())) != null)
        {
            if (line.Trim() == "PROBABLE CALL STACK:")
            {
                callStackSection = true;
                continue;
            }
            
            if (callStackSection && line.Trim() == "MODULES:")
                break;
                
            if (callStackSection && !string.IsNullOrWhiteSpace(line))
            {
                result.Add(line.Trim());
            }
        }
        
        return result;
    }
    
    private List<string> DetectIssues(string content)
    {
        // In a real application, this would be much more sophisticated
        var issues = new List<string>();
        
        // Check for common crash patterns
        if (content.Contains("EXCEPTION_STACK_OVERFLOW"))
            issues.Add("Stack Overflow Crash");
            
        if (content.Contains("EXCEPTION_ACCESS_VIOLATION"))
            issues.Add("Memory Access Violation");
            
        if (content.Contains("Plugin Limit"))
            issues.Add("Plugin Limit Crash");
            
        if (content.Contains("BSTextureStreamerLocalHeap") || content.Contains("TextureLoad"))
            issues.Add("Texture Crash");
            
        if (content.Contains("AnimationFileData") || content.Contains("AnimData"))
            issues.Add("Animation Corruption Crash");
        
        return issues;
    }
}
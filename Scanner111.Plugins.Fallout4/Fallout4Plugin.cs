// Scanner111.Plugins.Fallout4/Fallout4Plugin.cs - Fixed for new CrashLog entity model
using System.Diagnostics;
using Scanner111.Core.Models;
using Scanner111.Plugins.Interface.Attributes;
using Scanner111.Plugins.Interface.Models;
using Scanner111.Plugins.Interface.Services;
using System.Text.RegularExpressions;
using Scanner111.Application.Interfaces.Services;

using CorePlugin = Scanner111.Core.Interfaces.Plugins.IGamePlugin;
using ModIssueType = Scanner111.Plugins.Interface.Models.ModIssueType;
using PluginInterface = Scanner111.Plugins.Interface.Services.IGamePlugin;

namespace Scanner111.Plugins.Fallout4;

[GamePlugin(
    "fallout4",
    "Fallout 4 Plugin",
    "Provides support for Fallout 4 crash log analysis",
    "1.0.0",
    "Fallout4", "Fallout4VR")]
[GameSupport("Fallout4", "Fallout 4", "Fallout4.exe")]
[GameSupport("Fallout4VR", "Fallout 4 VR", "Fallout4VR.exe")]
public class Fallout4Plugin : CorePlugin, PluginInterface
{
    private IPluginHost? _host;
    private string _pluginsDirectory = string.Empty;
    private string _configDirectory = string.Empty;
    private string _logsDirectory = string.Empty;
    private string _classicYamlPath = string.Empty;
    
    public string Id => "fallout4";
    public string Name => "Fallout 4 Plugin";
    public string Description => "Provides support for Fallout 4 crash log analysis";
    public string Version => "1.0.0";
    public string[] SupportedGameIds => new[] { "Fallout4", "Fallout4VR" };
    public string GameExecutable => "Fallout4.exe"; // Implementation for CorePlugin
    
    private IYamlCompatibilityService _yamlService;
    private Dictionary<string, string>? _crashSuspects;
    private Dictionary<string, List<string>>? _crashStackCheck;
    private Dictionary<string, string>? _modsFrequent;
    private Dictionary<string, string>? _modsConflict;
    private Dictionary<string, string>? _modsSolutions;
    
    public Fallout4Plugin(IYamlCompatibilityService yamlService)
    {
        _yamlService = yamlService;
    }
    
    // CorePlugin implementation methods
    public async Task<bool> CanHandleGameAsync(Game game)
    {
        return SupportedGameIds.Contains(game.Id) && await Task.FromResult(true);
    }

    public Task<bool> CanHandleGameAsync(GameInfo game)
    {
        throw new NotImplementedException();
    }

    Task<GameInfo?> IGamePluginBase.DetectGameAsync(string possibleInstallPath)
    {
        throw new NotImplementedException();
    }

    public async Task<Game?> DetectGameAsync(string possibleInstallPath)
    {
        if (string.IsNullOrEmpty(possibleInstallPath) || !Directory.Exists(possibleInstallPath))
            return null;
            
        // Check for Fallout 4
        var fallout4ExePath = Path.Combine(possibleInstallPath, "Fallout4.exe");
        if (File.Exists(fallout4ExePath))
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(fallout4ExePath);
            return new Game
            {
                Id = "Fallout4",
                Name = "Fallout 4",
                ExecutableName = "Fallout4.exe",
                Version = versionInfo.FileVersion ?? "Unknown",
                InstallPath = possibleInstallPath
            };
        }
        
        // Check for Fallout 4 VR
        var fallout4VrExePath = Path.Combine(possibleInstallPath, "Fallout4VR.exe");
        if (File.Exists(fallout4VrExePath))
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(fallout4VrExePath);
            return new Game
            {
                Id = "Fallout4VR",
                Name = "Fallout 4 VR",
                ExecutableName = "Fallout4VR.exe",
                Version = versionInfo.FileVersion ?? "Unknown",
                InstallPath = possibleInstallPath
            };
        }
        
        return null;
    }
    
    public async Task<CrashLog> AnalyzeCrashLogAsync(string logContent, Game game)
    {
        // Ensure YAML data is loaded
        if (_crashSuspects == null || _crashStackCheck == null)
        {
            await LoadYamlDataAsync();
        }
        
        var crashLogId = Guid.NewGuid().ToString();
        var crashLog = new CrashLog
        {
            Id = crashLogId,
            GameId = game.Id,
            GameVersion = ExtractGameVersion(logContent),
            CrashGenVersion = ExtractCrashGenVersion(logContent),
            MainError = ExtractMainError(logContent),
            CrashTime = ExtractCrashTime(logContent),
            IsAnalyzed = true,
            IsSolved = false
        };
        
        // Extract plugins and add them to the crash log
        var plugins = await ExtractPluginsFromContentAsync(logContent);
        foreach (var plugin in plugins)
        {
            crashLog.Plugins.Add(new CrashLogPlugin
            {
                Id = Guid.NewGuid().ToString(),
                CrashLogId = crashLogId,
                PluginName = plugin.Key,
                LoadOrderId = plugin.Value,
                CrashLog = crashLog
            });
        }
        
        // Extract call stack and add it to the crash log
        var callStack = await ExtractCallStackAsync(logContent);
        for (int i = 0; i < callStack.Count; i++)
        {
            crashLog.CallStackEntries.Add(new CrashLogCallStack
            {
                Id = Guid.NewGuid().ToString(),
                CrashLogId = crashLogId,
                Order = i,
                Entry = callStack[i],
                CrashLog = crashLog
            });
        }
        
        // Detect issues and add them to the crash log
        var issues = await DetectIssuesAsync(logContent);
        foreach (var issue in issues)
        {
            crashLog.DetectedIssues.Add(new CrashLogIssue
            {
                Id = Guid.NewGuid().ToString(),
                CrashLogId = crashLogId,
                Description = issue,
                CrashLog = crashLog
            });
        }
        
        return crashLog;
    }
    
    public async Task<IEnumerable<ModIssue>> AnalyzePluginsAsync(IEnumerable<Plugin> plugins)
    {
        if (_host == null)
            throw new InvalidOperationException("Plugin not initialized");
            
        await _host.LogInformationAsync("Analyzing plugins...");
        
        var issues = new List<ModIssue>();
        var knownIssues = await LoadKnownPluginIssuesAsync();
        
        foreach (var plugin in plugins)
        {
            var matchingIssues = knownIssues.Where(i => i.PluginName.Equals(plugin.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            issues.AddRange(matchingIssues);
            
            // Check for incompatibilities between plugins
            await CheckCorePluginIncompatibilitiesAsync(plugin, plugins, issues);
        }
        
        // Check for load order issues
        await CheckCoreLoadOrderIssuesAsync(plugins, issues);
        
        return issues;
    }
    
    public async Task<Dictionary<string, string>> GetGameConfigurationAsync(Game game)
    {
        if (_host == null)
            throw new InvalidOperationException("Plugin not initialized");
            
        await _host.LogInformationAsync("Getting game configuration...");
        
        var config = new Dictionary<string, string>();
        
        // This is a simplified implementation that would normally read from INI files
        config["GamePath"] = game.InstallPath;
        config["GameVersion"] = game.Version;
        config["ModsPath"] = Path.Combine(game.InstallPath, "Data");
        
        return config;
    }
    
    public async Task<bool> ValidateGameFilesAsync(Game game)
    {
        if (_host == null)
            throw new InvalidOperationException("Plugin not initialized");
            
        await _host.LogInformationAsync("Validating game files...");
        
        var requiredFiles = await GetRequiredFilesAsync();
        var missingFiles = new List<string>();
        
        foreach (var file in requiredFiles)
        {
            var filePath = Path.Combine(game.InstallPath, file);
            if (!File.Exists(filePath))
                missingFiles.Add(file);
        }
        
        if (missingFiles.Any())
        {
            await _host.LogWarningAsync($"Missing files: {string.Join(", ", missingFiles)}");
            return false;
        }
        
        return true;
    }
    
    // PluginInterface implementation methods
    async Task<bool> PluginInterface.CanHandleGameAsync(GameInfo game)
    {
        return SupportedGameIds.Contains(game.Id) && await Task.FromResult(true);
    }
    
    async Task<GameInfo?> PluginInterface.DetectGameAsync(string possibleInstallPath)
    {
        if (string.IsNullOrEmpty(possibleInstallPath) || !Directory.Exists(possibleInstallPath))
            return null;
            
        // Check for Fallout 4
        var fallout4ExePath = Path.Combine(possibleInstallPath, "Fallout4.exe");
        if (File.Exists(fallout4ExePath))
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(fallout4ExePath);
            return new GameInfo
            {
                Id = "Fallout4",
                Name = "Fallout 4",
                ExecutableNames = new[] { "Fallout4.exe" },
                Version = versionInfo.FileVersion ?? "Unknown",
                InstallPath = possibleInstallPath,
                IsInstalled = true,
                IsSupported = true
            };
        }
        
        // Check for Fallout 4 VR
        var fallout4VrExePath = Path.Combine(possibleInstallPath, "Fallout4VR.exe");
        if (File.Exists(fallout4VrExePath))
        {
            var versionInfo = FileVersionInfo.GetVersionInfo(fallout4VrExePath);
            return new GameInfo
            {
                Id = "Fallout4VR",
                Name = "Fallout 4 VR",
                ExecutableNames = new[] { "Fallout4VR.exe" },
                Version = versionInfo.FileVersion ?? "Unknown",
                InstallPath = possibleInstallPath,
                IsInstalled = true,
                IsSupported = true
            };
        }
        
        return null;
    }
    
    async Task<CrashLogInfo> PluginInterface.AnalyzeCrashLogAsync(string logContent, GameInfo game)
    {
        // Ensure YAML data is loaded
        if (_crashSuspects == null || _crashStackCheck == null)
        {
            await LoadYamlDataAsync();
        }
        
        var crashLog = new CrashLogInfo
        {
            Id = Guid.NewGuid().ToString(),
            GameId = game.Id,
            GameVersion = ExtractGameVersion(logContent),
            CrashGenVersion = ExtractCrashGenVersion(logContent),
            MainError = ExtractMainError(logContent),
            CrashTime = ExtractCrashTime(logContent),
            LoadedPlugins = await ExtractLoadedPluginsAsync(logContent),
            CallStack = await ExtractCallStackAsync(logContent),
            DetectedIssues = await DetectIssuesAsync(logContent),
            IsAnalyzed = true,
            IsSolved = false
        };
        
        return crashLog;
    }
    
    async Task<IEnumerable<ModIssueInfo>> PluginInterface.AnalyzePluginsAsync(IEnumerable<PluginInfo> plugins)
    {
        if (_host == null)
            throw new InvalidOperationException("Plugin not initialized");
            
        await _host.LogInformationAsync("Analyzing plugins...");
        
        var issues = new List<ModIssueInfo>();
        var knownIssues = await LoadKnownIssuesAsync();
        
        foreach (var plugin in plugins)
        {
            var matchingIssues = knownIssues.Where(i => i.PluginName.Equals(plugin.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            issues.AddRange(matchingIssues);
            
            // Check for incompatibilities between plugins
            await CheckPluginIncompatibilitiesAsync(plugin, plugins, issues);
        }
        
        // Check for load order issues
        await CheckLoadOrderIssuesAsync(plugins, issues);
        
        return issues;
    }
    
    public async Task InitializeAsync(IPluginHost host)
    {
        _host = host;
        var configDir = await host.GetConfigDirectoryAsync();
        _classicYamlPath = Path.Combine(configDir, "databases", "CLASSIC Fallout4.yaml");
        
        // Check if YAML file exists, otherwise copy from embedded resource
        if (!File.Exists(_classicYamlPath))
        {
            await CopyDefaultYamlFileAsync();
        }
        
        // Load YAML data
        await LoadYamlDataAsync();
        
        await host.LogInformationAsync("Fallout 4 Plugin initialized");
    }
    
    public async Task ShutdownAsync()
    {
        if (_host != null)
            await _host.LogInformationAsync("Fallout 4 Plugin shutdown");
    }
    
    public async Task<IEnumerable<string>> GetRequiredFilesAsync()
    {
        // List of files that should be present for a correctly working game
        var requiredFiles = new List<string>
        {
            "Fallout4.exe",
            "Fallout4.esm",
            "f4se_loader.exe",
            "Data\\F4SE\\Plugins\\Buffout4.dll"
        };
        
        return await Task.FromResult(requiredFiles);
    }
    
    public async Task<Dictionary<string, string>> GetGameConfigurationAsync(GameInfo game)
    {
        if (_host == null)
            throw new InvalidOperationException("Plugin not initialized");
            
        await _host.LogInformationAsync("Getting game configuration...");
        
        var config = new Dictionary<string, string>();
        
        // This is a simplified implementation that would normally read from INI files
        config["GamePath"] = game.InstallPath;
        config["GameVersion"] = game.Version;
        config["ModsPath"] = Path.Combine(game.InstallPath, "Data");
        
        return config;
    }
    
    public async Task<bool> ValidateGameFilesAsync(GameInfo game)
    {
        if (_host == null)
            throw new InvalidOperationException("Plugin not initialized");
            
        await _host.LogInformationAsync("Validating game files...");
        
        var requiredFiles = await GetRequiredFilesAsync();
        var missingFiles = new List<string>();
        
        foreach (var file in requiredFiles)
        {
            var filePath = Path.Combine(game.InstallPath, file);
            if (!File.Exists(filePath))
                missingFiles.Add(file);
        }
        
        if (missingFiles.Any())
        {
            await _host.LogWarningAsync($"Missing files: {string.Join(", ", missingFiles)}");
            return false;
        }
        
        return true;
    }
    
    public async Task<string> GetCrashGeneratorNameAsync()
    {
        return await Task.FromResult("Buffout 4");
    }
    
    public async Task<Dictionary<string, string>> ExtractLoadedPluginsAsync(string logContent)
    {
        var result = new Dictionary<string, string>();
        var pluginSection = false;
        
        using var reader = new StringReader(logContent);
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

    // Interface implementation for IGamePlugin
    Task<List<string>> IGamePlugin.ExtractCallStackAsync(string logContent)
    {
        return ExtractCallStackAsync(logContent);
    }

    // Interface implementation for IGamePlugin
    Task<List<string>> IGamePlugin.DetectIssuesAsync(string logContent)
    {
        return DetectIssuesAsync(logContent);
    }

    #region Private Methods
    
    private async Task CopyDefaultYamlFileAsync()
    {
        if (_host == null) return;
        
        // In a real app, you'd include the default YAML as an embedded resource
        // and extract it here
        var directory = Path.GetDirectoryName(_classicYamlPath);
        if (!Directory.Exists(directory) && directory != null)
            Directory.CreateDirectory(directory);
            
        // For now, creating a minimal YAML file
        var defaultYaml = @"
Game_Info:
  Main_Root_Name: Fallout 4
  Main_Docs_Name: Fallout4
  Main_SteamID: 377160
  XSE_Acronym: F4SE
";
        
        await File.WriteAllTextAsync(_classicYamlPath, defaultYaml);
    }
    
    private async Task LoadYamlDataAsync()
    {
        // Load crash suspects
        _crashSuspects = await _yamlService.LoadCrashSuspectsAsync(_classicYamlPath, "Crashlog_Error_Check");
        
        // Load crash stack check
        _crashStackCheck = await _yamlService.LoadCrashStackCheckAsync(_classicYamlPath);
        
        // Load mods data
        _modsFrequent = await _yamlService.LoadModsListAsync(_classicYamlPath, "Mods_FREQ");
        _modsConflict = await _yamlService.LoadModsListAsync(_classicYamlPath, "Mods_CONF");
        _modsSolutions = await _yamlService.LoadModsListAsync(_classicYamlPath, "Mods_SOLU");
    }
    
    private DateTime ExtractCrashTime(string logContent)
    {
        // Try to extract the crash time from the log content
        var match = Regex.Match(logContent, @"Time: (\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2})");
        if (match.Success)
        {
            if (DateTime.TryParse(match.Groups[1].Value, out var dateTime))
                return dateTime;
        }
        
        // Default to current time if not found
        return DateTime.Now;
    }
    
    private string ExtractGameVersion(string logContent)
    {
        var match = Regex.Match(logContent, @"Fallout 4[^\d]*(\d+\.\d+\.\d+(?:\.\d+)?)");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
    
    private string ExtractCrashGenVersion(string logContent)
    {
        var match = Regex.Match(logContent, @"Buffout 4[^\d]*(?:v|version)?[^\d]*(\d+\.\d+\.\d+(?:\.\d+)?)");
        return match.Success ? $"v{match.Groups[1].Value}" : "Unknown";
    }
    
    private string ExtractMainError(string logContent)
    {
        var match = Regex.Match(logContent, @"Unhandled exception[^\n]*\n([^\n]+)");
        return match.Success ? match.Groups[1].Value.Trim() : "Unknown error";
    }
    
    // This method directly calls ExtractLoadedPluginsAsync for consistency
    private async Task<Dictionary<string, string>> ExtractPluginsFromContentAsync(string logContent)
    {
        // This method is used for the Core plugin interface
        return await ExtractLoadedPluginsAsync(logContent);
    }
    
    private async Task<List<string>> ExtractCallStackAsync(string logContent)
    {
        var result = new List<string>();
        var callStackSection = false;
        
        using var reader = new StringReader(logContent);
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
    
    private async Task<List<string>> DetectIssuesAsync(string logContent)
    {
        var issues = new List<string>();
        
        // Check for common crash patterns
        if (logContent.Contains("EXCEPTION_STACK_OVERFLOW"))
            issues.Add("Stack Overflow Crash");
            
        if (logContent.Contains("EXCEPTION_ACCESS_VIOLATION"))
            issues.Add("Memory Access Violation");
            
        if (logContent.Contains("Plugin Limit"))
            issues.Add("Plugin Limit Crash");
            
        if (logContent.Contains("BSTextureStreamerLocalHeap") || logContent.Contains("TextureLoad"))
            issues.Add("Texture Crash");
            
        if (logContent.Contains("AnimationFileData") || logContent.Contains("AnimData"))
            issues.Add("Animation Corruption Crash");
        
        return await Task.FromResult(issues);
    }
    
    private async Task<List<ModIssueInfo>> LoadKnownIssuesAsync()
    {
        // This would normally load from a database or configuration file
        var issues = new List<ModIssueInfo>
        {
            new ModIssueInfo
            {
                Id = Guid.NewGuid().ToString(),
                PluginName = "ClassicHolsteredWeapons.esp",
                Description = "Classic Holstered Weapons can cause crashes with certain body/skeleton mods",
                Severity = 5,
                IssueType = ModIssueType.Conflict,
                Solution = "Use a compatibility patch or disable one of the conflicting mods",
                PatchLinks = new List<string> { "https://www.nexusmods.com/fallout4/articles/2496" }
            },
            new ModIssueInfo
            {
                Id = Guid.NewGuid().ToString(),
                PluginName = "WeaponsFramework.esm",
                Description = "Weapons Framework can conflict with Tactical Reload",
                Severity = 4,
                IssueType = ModIssueType.Conflict,
                Solution = "Use a compatibility patch or disable one of the conflicting mods",
                PatchLinks = new List<string> { "https://www.nexusmods.com/fallout4/articles/3769" }
            }
        };
        
        return await Task.FromResult(issues);
    }
    
    private async Task<List<ModIssue>> LoadKnownPluginIssuesAsync()
    {
        // This would normally load from a database or configuration file
        var issues = new List<ModIssue>
        {
            new ModIssue
            {
                Id = Guid.NewGuid().ToString(),
                PluginName = "ClassicHolsteredWeapons.esp",
                Description = "Classic Holstered Weapons can cause crashes with certain body/skeleton mods",
                Severity = 5,
                IssueType = Core.Models.ModIssueType.Conflict,
                Solution = "Use a compatibility patch or disable one of the conflicting mods",
                PatchLinks = new List<string> { "https://www.nexusmods.com/fallout4/articles/2496" }
            },
            new ModIssue
            {
                Id = Guid.NewGuid().ToString(),
                PluginName = "WeaponsFramework.esm",
                Description = "Weapons Framework can conflict with Tactical Reload",
                Severity = 4,
                IssueType = Core.Models.ModIssueType.Conflict,
                Solution = "Use a compatibility patch or disable one of the conflicting mods",
                PatchLinks = new List<string> { "https://www.nexusmods.com/fallout4/articles/3769" }
            }
        };
        
        return await Task.FromResult(issues);
    }
    
    private async Task CheckPluginIncompatibilitiesAsync(
        PluginInfo plugin,
        IEnumerable<PluginInfo> allPlugins,
        List<ModIssueInfo> issues)
    {
        // Common incompatibilities in Fallout 4
        var incompatibilities = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "ClassicHolsteredWeapons.esp", ("cbp.dll", "CBP Physics conflicts with Classic Holstered Weapons") },
            { "ExtendedWeaponSystem.esp", ("tacticalreload.esp", "Extended Weapon Systems conflicts with Tactical Reload") },
            { "bostonfpsfix.esp", ("prp.esp", "Boston FPS Fix conflicts with Previs Repair Pack") }
        };
        
        if (incompatibilities.TryGetValue(plugin.Name, out var incompatibleInfo))
        {
            var (incompatiblePlugin, description) = incompatibleInfo;
            
            if (allPlugins.Any(p => p.Name.Equals(incompatiblePlugin, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ModIssueInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    PluginName = plugin.Name,
                    Description = description,
                    Severity = 4,
                    IssueType = ModIssueType.Conflict,
                    Solution = "Use only one of these mods, not both at the same time"
                });
            }
        }
        
        await Task.CompletedTask;
    }
    
    private async Task CheckCorePluginIncompatibilitiesAsync(
        Plugin plugin,
        IEnumerable<Plugin> allPlugins,
        List<ModIssue> issues)
    {
        // Similar to the above but for Core plugin interface
        var incompatibilities = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            { "ClassicHolsteredWeapons.esp", ("cbp.dll", "CBP Physics conflicts with Classic Holstered Weapons") },
            { "ExtendedWeaponSystem.esp", ("tacticalreload.esp", "Extended Weapon Systems conflicts with Tactical Reload") },
            { "bostonfpsfix.esp", ("prp.esp", "Boston FPS Fix conflicts with Previs Repair Pack") }
        };
        
        if (incompatibilities.TryGetValue(plugin.Name, out var incompatibleInfo))
        {
            var (incompatiblePlugin, description) = incompatibleInfo;
            
            if (allPlugins.Any(p => p.Name.Equals(incompatiblePlugin, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ModIssue
                {
                    Id = Guid.NewGuid().ToString(),
                    PluginName = plugin.Name,
                    Description = description,
                    Severity = 4,
                    IssueType = Core.Models.ModIssueType.Conflict,
                    Solution = "Use only one of these mods, not both at the same time"
                });
            }
        }
        
        await Task.CompletedTask;
    }
    
    private async Task CheckLoadOrderIssuesAsync(
        IEnumerable<PluginInfo> plugins,
        List<ModIssueInfo> issues)
    {
        // This would check for load order issues between plugins
        var prpPlugin = plugins.FirstOrDefault(p => p.Name.Equals("prp.esp", StringComparison.OrdinalIgnoreCase));
        var previsPlugins = plugins.Where(p => p.Name.Contains("_PREVIS", StringComparison.OrdinalIgnoreCase)).ToList();
        
        if (prpPlugin != null && previsPlugins.Any())
        {
            issues.Add(new ModIssueInfo
            {
                Id = Guid.NewGuid().ToString(),
                PluginName = "Multiple Previs Plugins",
                Description = "Multiple plugins modifying previs/precombines detected",
                Severity = 3,
                IssueType = ModIssueType.Conflict,
                Solution = "Ensure PRP.esp loads after all other previs mods, or use compatible patches"
            });
        }
        
        await Task.CompletedTask;
    }
    
    private async Task CheckCoreLoadOrderIssuesAsync(
        IEnumerable<Plugin> plugins,
        List<ModIssue> issues)
    {
        // Similar to the above but for Core plugin interface
        var prpPlugin = plugins.FirstOrDefault(p => p.Name.Equals("prp.esp", StringComparison.OrdinalIgnoreCase));
        var previsPlugins = plugins.Where(p => p.Name.Contains("_PREVIS", StringComparison.OrdinalIgnoreCase)).ToList();
        
        if (prpPlugin != null && previsPlugins.Any())
        {
            issues.Add(new ModIssue
            {
                Id = Guid.NewGuid().ToString(),
                PluginName = "Multiple Previs Plugins",
                Description = "Multiple plugins modifying previs/precombines detected",
                Severity = 3,
                IssueType = Core.Models.ModIssueType.Conflict,
                Solution = "Ensure PRP.esp loads after all other previs mods, or use compatible patches"
            });
        }
        
        await Task.CompletedTask;
    }
    
    #endregion
}
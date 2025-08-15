using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using YamlDotNet.RepresentationModel;

namespace Scanner111.Core.FCX;

public class ModCompatibilityService : IModCompatibilityService
{
    private readonly string _dataPath;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly ILogger<ModCompatibilityService> _logger;
    private readonly IYamlSettingsProvider _yamlSettings;
    private YamlMappingNode? _compatibilityData;

    public ModCompatibilityService(
        ILogger<ModCompatibilityService> logger,
        IYamlSettingsProvider yamlSettings,
        IApplicationSettingsService appSettings)
    {
        _logger = logger;
        _yamlSettings = yamlSettings;
        _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "FCX", "ModCompatibility.yaml");
    }

    public async Task<ModCompatibilityInfo?> GetCompatibilityInfoAsync(string modName, GameType gameType,
        string gameVersion)
    {
        await EnsureDataLoadedAsync().ConfigureAwait(false);

        if (_compatibilityData == null)
            return null;

        var gameKey = gameType == GameType.Fallout4 ? "fallout4" : "skyrimse";

        try
        {
            if (!_compatibilityData.Children.TryGetValue(gameKey, out var gameNode) ||
                !(gameNode is YamlMappingNode gameMapping))
                return null;

            if (!gameMapping.Children.TryGetValue("compatibility", out var compatNode) ||
                !(compatNode is YamlMappingNode compatMapping))
                return null;

            if (!compatMapping.Children.TryGetValue("version_requirements", out var reqNode) ||
                !(reqNode is YamlSequenceNode requirements))
                return null;

            // Search for the mod in version requirements
            foreach (var req in requirements.Children.OfType<YamlMappingNode>())
            {
                var modNameNode = req.Children.TryGetValue("mod", out var modNode) ? modNode as YamlScalarNode : null;
                if (modNameNode?.Value?.Equals(modName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    var minVersion = req.Children.TryGetValue("min_version", out var minNode)
                        ? (minNode as YamlScalarNode)?.Value
                        : null;
                    var maxVersion = req.Children.TryGetValue("max_version", out var maxNode)
                        ? (maxNode as YamlScalarNode)?.Value
                        : null;
                    var notes = req.Children.TryGetValue("notes", out var notesNode)
                        ? (notesNode as YamlScalarNode)?.Value
                        : null;

                    var isCompatible = IsVersionCompatible(gameVersion, minVersion, maxVersion);

                    return new ModCompatibilityInfo
                    {
                        ModName = modName,
                        MinVersion = minVersion != "null" ? minVersion : null,
                        MaxVersion = maxVersion != "null" ? maxVersion : null,
                        Notes = notes,
                        IsCompatible = isCompatible,
                        RecommendedAction =
                            !isCompatible ? GetRecommendedAction(gameVersion, minVersion, maxVersion) : null
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking mod compatibility for {Mod}", modName);
        }

        return null;
    }

    public async Task<List<ModCompatibilityIssue>> GetKnownIssuesAsync(GameType gameType, string gameVersion)
    {
        await EnsureDataLoadedAsync().ConfigureAwait(false);

        var issues = new List<ModCompatibilityIssue>();

        if (_compatibilityData == null)
            return issues;

        var gameKey = gameType == GameType.Fallout4 ? "fallout4" : "skyrimse";

        try
        {
            if (!_compatibilityData.Children.TryGetValue(gameKey, out var gameNode) ||
                !(gameNode is YamlMappingNode gameMapping))
                return issues;

            if (!gameMapping.Children.TryGetValue("compatibility", out var compatNode) ||
                !(compatNode is YamlMappingNode compatMapping))
                return issues;

            if (!compatMapping.Children.TryGetValue("known_issues", out var issuesNode) ||
                !(issuesNode is YamlSequenceNode knownIssues))
                return issues;

            foreach (var issue in knownIssues.Children.OfType<YamlMappingNode>())
            {
                var affectedVersionsNode = issue.Children.TryGetValue("affected_versions", out var affNode)
                    ? affNode as YamlSequenceNode
                    : null;
                var affectedVersions = affectedVersionsNode?.Children
                    .OfType<YamlScalarNode>()
                    .Select(n => n.Value ?? "")
                    .ToList() ?? new List<string>();

                // Check if this issue affects the current game version
                if (affectedVersions.Contains("all") || affectedVersions.Contains(gameVersion))
                    issues.Add(new ModCompatibilityIssue
                    {
                        ModName = issue.Children.TryGetValue("mod", out var modNode)
                            ? (modNode as YamlScalarNode)?.Value ?? ""
                            : "",
                        AffectedVersions = affectedVersions,
                        Issue = issue.Children.TryGetValue("issue", out var issueNode)
                            ? (issueNode as YamlScalarNode)?.Value ?? ""
                            : "",
                        Solution = issue.Children.TryGetValue("solution", out var solNode)
                            ? (solNode as YamlScalarNode)?.Value
                            : null
                    });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting known issues for {GameType} version {Version}", gameType, gameVersion);
        }

        return issues;
    }

    public async Task<List<XsePluginRequirement>> GetXseRequirementsAsync(GameType gameType, string gameVersion)
    {
        await EnsureDataLoadedAsync().ConfigureAwait(false);

        var requirements = new List<XsePluginRequirement>();

        if (_compatibilityData == null)
            return requirements;

        var gameKey = gameType == GameType.Fallout4 ? "fallout4" : "skyrimse";

        try
        {
            if (!_compatibilityData.Children.TryGetValue(gameKey, out var gameNode) ||
                !(gameNode is YamlMappingNode gameMapping))
                return requirements;

            if (!gameMapping.Children.TryGetValue("compatibility", out var compatNode) ||
                !(compatNode is YamlMappingNode compatMapping))
                return requirements;

            if (!compatMapping.Children.TryGetValue("xse_plugins", out var pluginsNode) ||
                !(pluginsNode is YamlSequenceNode xsePlugins))
                return requirements;

            foreach (var plugin in xsePlugins.Children.OfType<YamlMappingNode>())
            {
                var gameVersionsNode = plugin.Children.TryGetValue("game_versions", out var gvNode)
                    ? gvNode as YamlSequenceNode
                    : null;
                var compatibleVersions = gameVersionsNode?.Children
                    .OfType<YamlScalarNode>()
                    .Select(n => n.Value ?? "")
                    .ToList() ?? new List<string>();

                requirements.Add(new XsePluginRequirement
                {
                    PluginName = plugin.Children.TryGetValue("plugin", out var plNode)
                        ? (plNode as YamlScalarNode)?.Value ?? ""
                        : "",
                    RequiredXseVersion =
                        plugin.Children.TryGetValue(gameType == GameType.Fallout4 ? "required_f4se" : "required_skse",
                            out var xseNode)
                            ? (xseNode as YamlScalarNode)?.Value ?? ""
                            : "",
                    CompatibleGameVersions = compatibleVersions
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting XSE requirements for {GameType} version {Version}", gameType,
                gameVersion);
        }

        return requirements;
    }

    private async Task EnsureDataLoadedAsync()
    {
        if (_compatibilityData != null)
            return;

        await _loadLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_compatibilityData != null)
                return;

            if (!File.Exists(_dataPath))
            {
                _logger.LogWarning("Mod compatibility data not found at {Path}", _dataPath);
                return;
            }

            var yamlContent = await File.ReadAllTextAsync(_dataPath).ConfigureAwait(false);
            var yaml = new YamlStream();
            using (var reader = new StringReader(yamlContent))
            {
                yaml.Load(reader);
            }

            if (yaml.Documents.Count > 0 && yaml.Documents[0].RootNode is YamlMappingNode root)
                _compatibilityData = root;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load mod compatibility data");
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private bool IsVersionCompatible(string gameVersion, string? minVersion, string? maxVersion)
    {
        try
        {
            var current = Version.Parse(gameVersion);

            if (!string.IsNullOrEmpty(minVersion) && minVersion != "null")
            {
                var min = Version.Parse(minVersion);
                if (current < min)
                    return false;
            }

            if (!string.IsNullOrEmpty(maxVersion) && maxVersion != "null")
            {
                var max = Version.Parse(maxVersion);
                if (current > max)
                    return false;
            }

            return true;
        }
        catch
        {
            // If we can't parse versions, assume compatible
            return true;
        }
    }

    private string GetRecommendedAction(string gameVersion, string? minVersion, string? maxVersion)
    {
        try
        {
            var current = Version.Parse(gameVersion);

            if (!string.IsNullOrEmpty(minVersion) && minVersion != "null")
            {
                var min = Version.Parse(minVersion);
                if (current < min)
                    return $"This mod requires game version {minVersion} or higher. Consider updating your game.";
            }

            if (!string.IsNullOrEmpty(maxVersion) && maxVersion != "null")
            {
                var max = Version.Parse(maxVersion);
                if (current > max)
                    return
                        $"This mod is not compatible with game versions newer than {maxVersion}. Check for mod updates or use an older game version.";
            }
        }
        catch
        {
            // Fallback message
        }

        return "Check the mod page for version compatibility information.";
    }
}
using System.Collections.Generic;
using System.Threading.Tasks;
using Scanner111.Core.Models;

namespace Scanner111.Core.FCX
{
    public interface IModCompatibilityService
    {
        Task<ModCompatibilityInfo?> GetCompatibilityInfoAsync(string modName, GameType gameType, string gameVersion);
        Task<List<ModCompatibilityIssue>> GetKnownIssuesAsync(GameType gameType, string gameVersion);
        Task<List<XsePluginRequirement>> GetXseRequirementsAsync(GameType gameType, string gameVersion);
    }

    public class ModCompatibilityInfo
    {
        public string ModName { get; set; } = string.Empty;
        public string? MinVersion { get; set; }
        public string? MaxVersion { get; set; }
        public string? Notes { get; set; }
        public bool IsCompatible { get; set; }
        public string? RecommendedAction { get; set; }
    }

    public class ModCompatibilityIssue
    {
        public string ModName { get; set; } = string.Empty;
        public List<string> AffectedVersions { get; set; } = new();
        public string Issue { get; set; } = string.Empty;
        public string? Solution { get; set; }
    }

    public class XsePluginRequirement
    {
        public string PluginName { get; set; } = string.Empty;
        public string RequiredXseVersion { get; set; } = string.Empty;
        public List<string> CompatibleGameVersions { get; set; } = new();
    }
}
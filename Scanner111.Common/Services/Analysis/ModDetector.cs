using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Models.Reporting;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Detects and evaluates mods using configuration mappings and crash log plugins.
/// </summary>
public class ModDetector : IModDetector
{
    private readonly ConcurrentDictionary<string, Regex> _patternCache = new();

    /// <summary>
    /// Gets or sets the mod configuration used for detection.
    /// </summary>
    public ModConfiguration Configuration { get; set; } = ModConfiguration.Empty;

    /// <inheritdoc/>
    public async Task<ModDetectionResult> DetectAsync(
        IReadOnlyList<PluginInfo> plugins,
        IReadOnlySet<string> xseModules,
        GpuType? gpuType = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        // Build plugin lookup - map plugin name to FormID prefix
        var pluginLookup = plugins
            .ToDictionary(p => p.PluginName.ToLowerInvariant(), p => p.FormIdPrefix, StringComparer.OrdinalIgnoreCase);

        // Detect single problematic mods
        var problematicMods = new List<DetectedMod>();
        problematicMods.AddRange(DetectSingleMods(Configuration.FrequentCrashMods, pluginLookup, ModCategory.FrequentCrashes));
        problematicMods.AddRange(DetectSingleMods(Configuration.SolutionMods, pluginLookup, ModCategory.HasSolution));
        problematicMods.AddRange(DetectSingleMods(Configuration.OpcPatchedMods, pluginLookup, ModCategory.OpcPatched));

        // Detect mod conflicts
        var conflicts = DetectConflicts(Configuration.ConflictingMods, pluginLookup);

        // Check important mods
        var allPluginText = string.Join(" ", pluginLookup.Keys.Concat(xseModules.Select(m => m.ToLowerInvariant())));
        var importantMods = CheckImportantMods(Configuration.ImportantMods, allPluginText, gpuType);

        return new ModDetectionResult
        {
            ProblematicMods = problematicMods,
            Conflicts = conflicts,
            ImportantMods = importantMods
        };
    }

    /// <inheritdoc/>
    public ReportFragment CreateReportFragment(ModDetectionResult result)
    {
        var lines = new List<string>();

        // Add problematic mods section
        if (result.ProblematicMods.Count > 0)
        {
            lines.Add("## Detected Mod Issues");
            lines.Add(string.Empty);

            foreach (var mod in result.ProblematicMods)
            {
                lines.Add($"**[!] FOUND : [{mod.PluginFormId}] {mod.ModName}**");
                lines.Add(string.Empty);

                // Add warning lines
                foreach (var warningLine in mod.Warning.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    lines.Add(warningLine.Trim());
                }
                lines.Add(string.Empty);
            }
        }

        // Add conflicts section
        if (result.Conflicts.Count > 0)
        {
            lines.Add("## Mod Conflicts Detected");
            lines.Add(string.Empty);

            foreach (var conflict in result.Conflicts)
            {
                lines.Add($"**[!] CAUTION : Conflicting mods detected**");
                lines.Add($"- {conflict.Mod1}");
                lines.Add($"- {conflict.Mod2}");
                lines.Add(string.Empty);

                foreach (var warningLine in conflict.Warning.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    lines.Add(warningLine.Trim());
                }
                lines.Add(string.Empty);
            }
        }

        // Add important mods section
        var importantModsWithStatus = result.ImportantMods.Where(m => m.IsInstalled || m.Warning != null).ToList();
        if (importantModsWithStatus.Count > 0)
        {
            lines.Add("## Important Mods Status");
            lines.Add(string.Empty);

            foreach (var mod in importantModsWithStatus)
            {
                if (mod.IsInstalled && !mod.HasGpuConcern)
                {
                    lines.Add($"✔️ {mod.DisplayName} is installed!");
                }
                else if (mod.IsInstalled && mod.HasGpuConcern)
                {
                    lines.Add($"❓ {mod.DisplayName} is installed, BUT IT SEEMS YOU DON'T HAVE A COMPATIBLE GPU?");
                    lines.Add("IF THIS IS CORRECT, COMPLETELY UNINSTALL THIS MOD TO AVOID ANY PROBLEMS!");
                }
                else if (mod.Warning != null)
                {
                    lines.Add($"❌ {mod.DisplayName} is not installed!");
                    foreach (var warningLine in mod.Warning.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        lines.Add(warningLine.Trim());
                    }
                }
                lines.Add(string.Empty);
            }
        }

        return ReportFragment.FromLines(lines.ToArray());
    }

    private List<DetectedMod> DetectSingleMods(
        Dictionary<string, string> modMappings,
        Dictionary<string, string> pluginLookup,
        ModCategory category)
    {
        var detectedMods = new List<DetectedMod>();

        // Sort by pattern length (longest first) for most specific matches
        var sortedMappings = modMappings
            .OrderByDescending(kvp => kvp.Key.Length)
            .ToList();

        var matchedPlugins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (modPattern, warning) in sortedMappings)
        {
            var pattern = GetOrCompilePattern(modPattern);

            foreach (var (pluginName, formId) in pluginLookup)
            {
                if (matchedPlugins.Contains(pluginName))
                    continue;

                if (pattern.IsMatch(pluginName))
                {
                    matchedPlugins.Add(pluginName);

                    // Extract mod display name from warning (first line)
                    var warningLines = warning.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    var modName = warningLines.Length > 0 ? warningLines[0].Trim() : modPattern;

                    detectedMods.Add(new DetectedMod
                    {
                        ModName = modName,
                        MatchedPlugin = pluginName,
                        PluginFormId = formId,
                        Warning = warning,
                        Category = category
                    });

                    // Only match first plugin for each mod pattern
                    break;
                }
            }
        }

        return detectedMods;
    }

    private List<ModConflict> DetectConflicts(
        Dictionary<string, string> conflictMappings,
        Dictionary<string, string> pluginLookup)
    {
        var conflicts = new List<ModConflict>();

        // Build set of all unique mod names from conflict pairs
        var allModPatterns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pairMappings = new Dictionary<(string, string), string>();

        foreach (var (modPair, warning) in conflictMappings)
        {
            var parts = modPair.Split(" | ", 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;

            var mod1 = parts[0].ToLowerInvariant();
            var mod2 = parts[1].ToLowerInvariant();

            allModPatterns.Add(mod1);
            allModPatterns.Add(mod2);
            pairMappings[(mod1, mod2)] = warning;
        }

        if (allModPatterns.Count == 0)
            return conflicts;

        // Find which mods are present
        var modsPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var combinedPattern = BuildCombinedPattern(allModPatterns);

        foreach (var pluginName in pluginLookup.Keys)
        {
            var matches = combinedPattern.Matches(pluginName);
            foreach (Match match in matches)
            {
                modsPresent.Add(match.Value.ToLowerInvariant());
            }
        }

        // Check for conflicting pairs
        foreach (var ((mod1, mod2), warning) in pairMappings)
        {
            if (modsPresent.Contains(mod1) && modsPresent.Contains(mod2))
            {
                conflicts.Add(new ModConflict
                {
                    Mod1 = mod1,
                    Mod2 = mod2,
                    Warning = warning
                });
            }
        }

        return conflicts;
    }

    private List<ImportantModStatus> CheckImportantMods(
        Dictionary<string, string> importantMods,
        string allPluginText,
        GpuType? gpuType)
    {
        var results = new List<ImportantModStatus>();

        foreach (var (modEntry, warning) in importantMods)
        {
            var parts = modEntry.Split(" | ", 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) continue;

            var modId = parts[0];
            var displayName = parts[1];

            var pattern = GetOrCompilePattern(modId);
            var isInstalled = pattern.IsMatch(allPluginText);

            // Check GPU compatibility
            var gpuTypeString = gpuType?.ToString().ToLowerInvariant();
            var hasGpuConcern = false;

            if (isInstalled && gpuType.HasValue && !string.IsNullOrEmpty(warning))
            {
                // If the warning mentions a GPU type and user has that GPU type's rival
                var warningLower = warning.ToLowerInvariant();
                hasGpuConcern = warningLower.Contains(gpuTypeString ?? string.Empty);
            }

            // Determine if we should show a warning
            string? warningMessage = null;
            if (!isInstalled && !string.IsNullOrEmpty(warning))
            {
                // Only show warning for not installed if warning doesn't mention rival GPU
                if (gpuType.HasValue)
                {
                    var rivalGpu = gpuType == GpuType.Nvidia ? "amd" : "nvidia";
                    var warningLower = warning.ToLowerInvariant();
                    // Only show if the mod isn't specific to the rival GPU
                    if (!warningLower.Contains(rivalGpu))
                    {
                        warningMessage = warning;
                    }
                }
                else
                {
                    warningMessage = warning;
                }
            }

            results.Add(new ImportantModStatus
            {
                DisplayName = displayName,
                IsInstalled = isInstalled,
                HasGpuConcern = hasGpuConcern,
                Warning = warningMessage
            });
        }

        return results;
    }

    private Regex GetOrCompilePattern(string pattern)
    {
        return _patternCache.GetOrAdd(pattern.ToLowerInvariant(), p =>
            new Regex(Regex.Escape(p), RegexOptions.Compiled | RegexOptions.IgnoreCase));
    }

    private Regex BuildCombinedPattern(IEnumerable<string> patterns)
    {
        // Sort by length descending for most specific matches first
        var sortedPatterns = patterns
            .OrderByDescending(p => p.Length)
            .Select(p => Regex.Escape(p))
            .ToList();

        var combinedPattern = string.Join("|", sortedPatterns);
        return new Regex(combinedPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }
}

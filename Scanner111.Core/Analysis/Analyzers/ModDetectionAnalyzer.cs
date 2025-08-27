using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;

namespace Scanner111.Core.Analysis.Analyzers;

/// <summary>
///     Analyzes crash logs to detect problematic mods, mod conflicts, and missing important mods.
///     Implements functionality equivalent to Python's detectmods.py functions.
///     Thread-safe analyzer that processes mod databases and performs pattern matching.
/// </summary>
public sealed class ModDetectionAnalyzer : AnalyzerBase, IModDetectionAnalyzer
{
    private readonly IModDatabase _modDatabase;

    public ModDetectionAnalyzer(
        ILogger<ModDetectionAnalyzer> logger,
        IModDatabase modDatabase)
        : base(logger)
    {
        _modDatabase = modDatabase ?? throw new ArgumentNullException(nameof(modDatabase));
    }

    /// <inheritdoc />
    public override string Name => "ModDetectionAnalyzer";

    /// <inheritdoc />
    public override string DisplayName => "Mod Detection Analysis";

    /// <inheritdoc />
    public override int Priority => 35; // Run after plugin analysis but before FormID analysis

    /// <inheritdoc />
    public override TimeSpan Timeout => TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    protected override async Task<AnalysisResult> PerformAnalysisAsync(
        AnalysisContext context,
        CancellationToken cancellationToken)
    {
        LogDebug("Starting mod detection analysis");

        try
        {
            // Get crash log plugins from shared context
            if (!context.TryGetSharedData<IReadOnlyDictionary<string, string>>("CrashLogPlugins", out var crashLogPlugins) ||
                crashLogPlugins == null || crashLogPlugins.Count == 0)
            {
                LogDebug("No crash log plugins found in context");
                return CreateNoPluginsResult();
            }

            // Get detected GPU type if available
            context.TryGetSharedData<string>("DetectedGpuType", out var detectedGpuType);

            // Perform comprehensive mod detection analysis
            var modDetectionSettings = await PerformComprehensiveAnalysisAsync(
                crashLogPlugins, detectedGpuType, cancellationToken).ConfigureAwait(false);

            // Create report fragments
            var fragments = new List<ReportFragment>();

            // Add problematic mods fragment
            if (modDetectionSettings.DetectedWarnings.Count > 0)
            {
                var warningsFragment = CreateProblematicModsFragment(modDetectionSettings.DetectedWarnings);
                fragments.Add(warningsFragment);
            }

            // Add mod conflicts fragment
            if (modDetectionSettings.DetectedConflicts.Count > 0)
            {
                var conflictsFragment = CreateModConflictsFragment(modDetectionSettings.DetectedConflicts);
                fragments.Add(conflictsFragment);
            }

            // Add important mods fragment
            if (modDetectionSettings.ImportantMods.Count > 0)
            {
                var importantFragment = CreateImportantModsFragment(modDetectionSettings.ImportantMods);
                fragments.Add(importantFragment);
            }

            // Store detection results in context for other analyzers
            context.SetSharedData("ModDetectionSettings", modDetectionSettings);

            // Combine fragments or create info fragment
            var combinedFragment = fragments.Count > 0
                ? ReportFragment.CreateWithChildren("Mod Detection Analysis", fragments, 20)
                : ReportFragment.CreateInfo("Mod Detection Analysis", 
                    "No problematic mods, conflicts, or missing important mods detected.", 100);

            var result = new AnalysisResult(Name)
            {
                Success = true,
                Fragment = combinedFragment,
                Severity = DetermineSeverity(modDetectionSettings)
            };

            // Add metadata
            result.AddMetadata("ProblematicModsCount", modDetectionSettings.DetectedWarnings.Count.ToString());
            result.AddMetadata("ConflictsCount", modDetectionSettings.DetectedConflicts.Count.ToString());
            result.AddMetadata("ImportantModsCount", modDetectionSettings.ImportantMods.Count.ToString());
            result.AddMetadata("MissingImportantModsCount", 
                modDetectionSettings.ImportantMods.Count(m => m.GetStatus() == ModStatus.NotInstalled).ToString());

            LogInformation("Mod detection analysis completed with severity: {Severity}", result.Severity);
            return result;
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to perform mod detection analysis");
            return AnalysisResult.CreateFailure(Name, $"Mod detection analysis failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModWarning>> DetectProblematicModsAsync(
        IReadOnlyDictionary<string, string> crashLogPlugins,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(crashLogPlugins);

        if (crashLogPlugins.Count == 0)
            return Array.Empty<ModWarning>();

        var detectedWarnings = new List<ModWarning>();

        try
        {
            // Load different categories of problematic mods
            var categories = await _modDatabase.GetModWarningCategoriesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var category in categories)
            {
                var modWarningsDict = await _modDatabase.LoadModWarningsAsync(category, cancellationToken)
                    .ConfigureAwait(false);

                if (modWarningsDict.Count == 0)
                    continue;

                // Convert to lowercase for case-insensitive matching
                var crashLogPluginsLower = ConvertToLowercase(crashLogPlugins);

                // Sort mod names by length (longest first) for most specific matches
                var sortedMods = modWarningsDict.OrderByDescending(kvp => kvp.Key.Length).ToList();

                var modLookup = sortedMods.ToDictionary(kvp => kvp.Key.ToLowerInvariant(), kvp => kvp.Value,
                    StringComparer.OrdinalIgnoreCase);

                // Find matches in crash log plugins using flexible matching
                var modMatches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var (pluginName, pluginId) in crashLogPluginsLower)
                {
                    foreach (var (modName, warning) in sortedMods)
                    {
                        if (IsModNameMatch(modName, pluginName))
                        {
                            var matchedMod = modName.ToLowerInvariant();
                            if (!modMatches.ContainsKey(matchedMod))
                            {
                                modMatches[matchedMod] = pluginId;
                                break; // Found a match, stop searching for this plugin
                            }
                        }
                    }
                }

                // Create ModWarning objects for matches
                foreach (var (modName, pluginId) in modMatches)
                {
                    if (modLookup.TryGetValue(modName, out var warning))
                    {
                        ValidateWarning(modName, warning);
                        
                        var modWarning = ModWarning.Create(
                            modName,
                            warning,
                            AnalysisSeverity.Warning,
                            pluginId,
                            category);
                            
                        detectedWarnings.Add(modWarning);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to detect problematic mods");
        }

        LogDebug("Detected {Count} problematic mods", detectedWarnings.Count);
        return detectedWarnings.ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModConflict>> DetectModConflictsAsync(
        IReadOnlyDictionary<string, string> crashLogPlugins,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(crashLogPlugins);

        if (crashLogPlugins.Count == 0)
            return Array.Empty<ModConflict>();

        var detectedConflicts = new List<ModConflict>();

        try
        {
            var conflictsDict = await _modDatabase.LoadModConflictsAsync(cancellationToken).ConfigureAwait(false);
            if (conflictsDict.Count == 0)
                return Array.Empty<ModConflict>();

            // Convert to lowercase for case-insensitive matching
            var crashLogPluginsLower = ConvertToLowercase(crashLogPlugins);

            // Build set of unique mod names from conflict pairs
            var allModNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modPairsMap = new Dictionary<(string, string), string>();

            foreach (var (conflictPair, conflictWarning) in conflictsDict)
            {
                var parts = conflictPair.Split(" | ", 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var mod1 = parts[0].Trim().ToLowerInvariant();
                    var mod2 = parts[1].Trim().ToLowerInvariant();
                    allModNames.Add(mod1);
                    allModNames.Add(mod2);
                    modPairsMap[(mod1, mod2)] = conflictWarning;
                }
            }

            if (allModNames.Count == 0)
                return Array.Empty<ModConflict>();

            // Create regex pattern for efficient matching
            var modPatterns = allModNames.Select(Regex.Escape).ToArray();
            var combinedPattern = new Regex(string.Join("|", modPatterns), RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Find which mods are present in plugins
            var modsPresent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var foundPluginIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (pluginName, pluginId) in crashLogPluginsLower)
            {
                var matches = combinedPattern.Matches(pluginName);
                foreach (Match match in matches)
                {
                    var modName = match.Value.ToLowerInvariant();
                    modsPresent.Add(modName);
                    foundPluginIds[modName] = pluginId;
                }
            }

            // Check for conflicting pairs
            foreach (var ((mod1, mod2), conflictWarning) in modPairsMap)
            {
                if (modsPresent.Contains(mod1) && modsPresent.Contains(mod2))
                {
                    ValidateWarning($"{mod1} | {mod2}", conflictWarning);
                    
                    var foundIds = new List<string>();
                    if (foundPluginIds.TryGetValue(mod1, out var id1))
                        foundIds.Add(id1);
                    if (foundPluginIds.TryGetValue(mod2, out var id2) && id2 != id1)
                        foundIds.Add(id2);

                    var conflict = ModConflict.Create(
                        mod1,
                        mod2,
                        conflictWarning,
                        AnalysisSeverity.Warning,
                        foundIds);

                    detectedConflicts.Add(conflict);
                }
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to detect mod conflicts");
        }

        LogDebug("Detected {Count} mod conflicts", detectedConflicts.Count);
        return detectedConflicts.ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportantMod>> DetectImportantModsAsync(
        IReadOnlyDictionary<string, string> crashLogPlugins,
        string? detectedGpuType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(crashLogPlugins);

        var importantMods = new List<ImportantMod>();

        try
        {
            // Convert plugin names to lowercase for matching
            var pluginNamesLower = crashLogPlugins.Keys.Select(k => k.ToLowerInvariant()).ToList();
            var allPluginsText = string.Join(" ", pluginNamesLower);

            // Load different categories of important mods
            var categories = await _modDatabase.GetImportantModCategoriesAsync(cancellationToken).ConfigureAwait(false);

            foreach (var category in categories)
            {
                var importantModsDict = await _modDatabase.LoadImportantModsAsync(category, cancellationToken)
                    .ConfigureAwait(false);

                if (importantModsDict.Count == 0)
                    continue;

                // Build patterns for mod IDs
                var modPatterns = new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
                foreach (var modEntry in importantModsDict.Keys)
                {
                    var parts = modEntry.Split(" | ", 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var modId = parts[0].Trim();
                        var pattern = new Regex(Regex.Escape(modId.ToLowerInvariant()), RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        modPatterns[modEntry] = pattern;
                    }
                }

                // Check each important mod
                foreach (var (modEntry, recommendation) in importantModsDict)
                {
                    var parts = modEntry.Split(" | ", 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                        continue;

                    var modId = parts[0].Trim();
                    var displayName = parts[1].Trim();
                    
                    var isInstalled = modPatterns.TryGetValue(modEntry, out var pattern) && 
                                     pattern.IsMatch(allPluginsText);

                    // Check for GPU-specific requirements
                    var hasGpuRequirement = recommendation.Contains("nvidia", StringComparison.OrdinalIgnoreCase) ||
                                           recommendation.Contains("amd", StringComparison.OrdinalIgnoreCase);
                    
                    string? requiredGpuType = null;
                    if (hasGpuRequirement)
                    {
                        if (recommendation.Contains("nvidia", StringComparison.OrdinalIgnoreCase))
                            requiredGpuType = "nvidia";
                        else if (recommendation.Contains("amd", StringComparison.OrdinalIgnoreCase))
                            requiredGpuType = "amd";
                    }

                    var importantMod = ImportantMod.CreateWithGpuCheck(
                        modId,
                        displayName,
                        recommendation,
                        isInstalled,
                        requiredGpuType,
                        detectedGpuType,
                        category);

                    importantMods.Add(importantMod);
                }
            }
        }
        catch (Exception ex)
        {
            LogError(ex, "Failed to detect important mods");
        }

        LogDebug("Processed {Count} important mods", importantMods.Count);
        return importantMods.ToArray();
    }

    /// <inheritdoc />
    public async Task<ModDetectionSettings> PerformComprehensiveAnalysisAsync(
        IReadOnlyDictionary<string, string> crashLogPlugins,
        string? detectedGpuType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(crashLogPlugins);

        // Perform all three types of detection concurrently
        var warningsTask = DetectProblematicModsAsync(crashLogPlugins, cancellationToken);
        var conflictsTask = DetectModConflictsAsync(crashLogPlugins, cancellationToken);
        var importantTask = DetectImportantModsAsync(crashLogPlugins, detectedGpuType, cancellationToken);

        await Task.WhenAll(warningsTask, conflictsTask, importantTask).ConfigureAwait(false);

        var warnings = await warningsTask.ConfigureAwait(false);
        var conflicts = await conflictsTask.ConfigureAwait(false);
        var important = await importantTask.ConfigureAwait(false);

        var pluginsDict = crashLogPlugins.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        
        return ModDetectionSettings.FromDetectionData(
            crashLogPlugins: pluginsDict,
            detectedWarnings: warnings,
            detectedConflicts: conflicts,
            importantMods: important,
            detectedGpuType: detectedGpuType);
    }

    /// <inheritdoc />
    public override async Task<bool> CanAnalyzeAsync(AnalysisContext context)
    {
        var canAnalyze = await base.CanAnalyzeAsync(context).ConfigureAwait(false);
        if (!canAnalyze)
            return false;

        // Check if mod database is available
        var isAvailable = await _modDatabase.IsAvailableAsync(CancellationToken.None).ConfigureAwait(false);
        if (!isAvailable)
        {
            LogDebug("Mod database is not available, skipping mod detection analysis");
            return false;
        }

        // Check if we have crash log plugins in context
        var hasPlugins = context.TryGetSharedData<IReadOnlyDictionary<string, string>>("CrashLogPlugins", out var plugins) &&
                        plugins != null && plugins.Count > 0;

        LogDebug("CanAnalyze: {Result} (Database: {DatabaseAvailable}, Plugins: {HasPlugins})",
            hasPlugins, isAvailable, hasPlugins);

        return hasPlugins;
    }

    #region Private Helper Methods

    /// <summary>
    ///     Converts dictionary keys to lowercase for case-insensitive matching.
    /// </summary>
    private static Dictionary<string, string> ConvertToLowercase(IReadOnlyDictionary<string, string> data)
    {
        return data.ToDictionary(kvp => kvp.Key.ToLowerInvariant(), kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Validates that a mod has an associated warning message.
    /// </summary>
    private static void ValidateWarning(string modName, string warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
            throw new InvalidOperationException($"ERROR: {modName} has no warning in the database!");
    }

    /// <summary>
    ///     Determines if a mod name matches a plugin name using flexible matching rules.
    ///     Handles cases like "SCRAP EVERYTHING" matching "scrapeverything.esp".
    /// </summary>
    private static bool IsModNameMatch(string modName, string pluginName)
    {
        if (string.IsNullOrWhiteSpace(modName) || string.IsNullOrWhiteSpace(pluginName))
            return false;

        // Normalize both strings for comparison
        var normalizedModName = NormalizeModName(modName);
        var normalizedPluginName = NormalizePluginName(pluginName);

        // Don't match if the mod name is too short to be meaningful
        var cleanModName = normalizedModName.Replace(" ", "");
        if (cleanModName.Length < 4) 
            return false;

        // Strategy 1: Try exact match after normalization
        if (string.Equals(normalizedModName, normalizedPluginName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Strategy 2: Try removing spaces from mod name (e.g., "scrap everything" -> "scrapeverything")
        // BUT only if the original mod name has spaces AND doesn't contain generic words that create false positives
        var modNameNoSpaces = normalizedModName.Replace(" ", "");
        if (normalizedModName.Contains(' ') && // Must have had spaces originally
            string.Equals(modNameNoSpaces, normalizedPluginName, StringComparison.OrdinalIgnoreCase) &&
            !ContainsGenericModWords(normalizedModName)) // Avoid false positives like "nonexistent mod" = "nonexistentmod"
        {
            return true;
        }

        // Strategy 3: Check if mod name (no spaces) is a substantial part of plugin name
        // Only match if mod name takes up at least 85% of the plugin name to avoid false positives
        // AND the original mod name must have at least one space (indicating separate words that make sense to combine)
        if (normalizedPluginName.Contains(modNameNoSpaces, StringComparison.OrdinalIgnoreCase) && 
            normalizedModName.Contains(' ')) // Only apply space removal if there were actual spaces in the mod name
        {
            var lengthRatio = (double)modNameNoSpaces.Length / normalizedPluginName.Length;
            if (lengthRatio >= 0.85) // Very high similarity required to avoid false matches like "nonexistent mod" vs "nonexistentmod"
                return true;
        }

        // Strategy 4: Try with common word substitutions (only for exact matches)
        var substitutedModName = ApplyCommonSubstitutions(normalizedModName);
        if (substitutedModName != normalizedModName)
        {
            if (string.Equals(substitutedModName.Replace(" ", ""), normalizedPluginName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Normalizes a mod name for comparison by removing common prefixes/suffixes and standardizing format.
    /// </summary>
    private static string NormalizeModName(string modName)
    {
        return modName.ToLowerInvariant()
            .Trim()
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace("  ", " "); // Collapse multiple spaces
    }

    /// <summary>
    ///     Normalizes a plugin name for comparison by removing file extensions and common prefixes.
    /// </summary>
    private static string NormalizePluginName(string pluginName)
    {
        var normalized = pluginName.ToLowerInvariant().Trim();
        
        // Remove common file extensions
        var extensionsToRemove = new[] { ".esp", ".esl", ".esm", ".dll" };
        foreach (var ext in extensionsToRemove)
        {
            if (normalized.EndsWith(ext))
            {
                normalized = normalized[..^ext.Length];
                break;
            }
        }

        return normalized
            .Replace("-", " ")
            .Replace("_", " ")
            .Replace("  ", " "); // Collapse multiple spaces
    }

    /// <summary>
    ///     Applies common word substitutions to improve matching accuracy.
    /// </summary>
    private static string ApplyCommonSubstitutions(string modName)
    {
        var substituted = modName;
        
        // Common abbreviations and variations
        var substitutions = new Dictionary<string, string[]>
        {
            { "4", new[] { "four", "fo4" } },
            { "fo4", new[] { "4", "four" } },
            { "fallout", new[] { "fo", "fo4" } },
            { "weapon", new[] { "weapons", "wpn" } },
            { "armor", new[] { "armour", "armors", "armours" } },
            { "mod", new[] { "mods" } },
            { "fix", new[] { "fixes", "patch", "patches" } }
        };

        foreach (var (key, replacements) in substitutions)
        {
            if (substituted.Contains(key))
            {
                foreach (var replacement in replacements)
                {
                    var testName = substituted.Replace(key, replacement);
                    if (testName != substituted)
                        return testName; // Return first successful substitution
                }
            }
        }

        return substituted;
    }

    /// <summary>
    ///     Checks if the mod name contains generic words that often create false positive matches.
    /// </summary>
    private static bool ContainsGenericModWords(string modName)
    {
        var genericWords = new[] 
        {
            "mod", "plugin", "addon", "extension", "patch", "fix", "update", "version",
            "nonexistent", "unknown", "missing", "broken", "test", "sample", "example",
            "new", "old", "big", "small", "better", "improved", "enhanced", "ultimate"
        };

        var words = modName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Any(word => genericWords.Contains(word, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Creates a report fragment for problematic mods.
    /// </summary>
    private ReportFragment CreateProblematicModsFragment(IReadOnlyList<ModWarning> warnings)
    {
        var content = new StringBuilder();
        content.AppendLine("### Problematic Mods Detected\n");

        foreach (var warning in warnings.OrderBy(w => w.ModName))
        {
            content.AppendLine("```");
            content.AppendLine($"[!] FOUND : [{warning.PluginId ?? "Unknown"}] {warning.Warning}");
            if (!warning.Warning.EndsWith('\n'))
                content.AppendLine();
            content.AppendLine("```");
            content.AppendLine();
        }

        return ReportFragment.CreateWarning("Problematic Mods", content.ToString(), 25);
    }

    /// <summary>
    ///     Creates a report fragment for mod conflicts.
    /// </summary>
    private ReportFragment CreateModConflictsFragment(IReadOnlyList<ModConflict> conflicts)
    {
        var content = new StringBuilder();

        foreach (var conflict in conflicts)
        {
            content.AppendLine("[!] CAUTION : Conflicting mods detected");
            content.AppendLine(conflict.ConflictWarning);
            if (!conflict.ConflictWarning.EndsWith('\n'))
                content.AppendLine();
            content.AppendLine();
        }

        return ReportFragment.CreateWarning("Mod Conflicts", content.ToString(), 20);
    }

    /// <summary>
    ///     Creates a report fragment for important mods.
    /// </summary>
    private ReportFragment CreateImportantModsFragment(IReadOnlyList<ImportantMod> importantMods)
    {
        var content = new StringBuilder();
        content.AppendLine("### Checking for Important Mods\n");

        foreach (var mod in importantMods)
        {
            switch (mod.GetStatus())
            {
                case ModStatus.Installed:
                    content.AppendLine($"✔️ {mod.DisplayName} is installed!\n");
                    break;

                case ModStatus.InstalledWithGpuIssue:
                    content.AppendLine();
                    content.AppendLine($"❓ {mod.DisplayName} is installed, BUT IT SEEMS YOU DON'T HAVE AN {mod.RequiredGpuType?.ToUpperInvariant()} GPU?");
                    content.AppendLine("IF THIS IS CORRECT, COMPLETELY UNINSTALL THIS MOD TO AVOID ANY PROBLEMS! \n");
                    break;

                case ModStatus.NotInstalled when mod.HasGpuRequirement && 
                                              !string.Equals(mod.RequiredGpuType, mod.DetectedGpuType, StringComparison.OrdinalIgnoreCase):
                    content.AppendLine($"❌ {mod.DisplayName} is not installed!");
                    content.AppendLine(mod.Recommendation);
                    content.AppendLine();
                    break;

                case ModStatus.NotNeededForGpu:
                    // Skip - mod not needed for this GPU type
                    break;
            }
        }

        return ReportFragment.CreateInfo("Important Mods", content.ToString(), 30);
    }

    /// <summary>
    ///     Determines overall analysis severity based on detection results.
    /// </summary>
    private static AnalysisSeverity DetermineSeverity(ModDetectionSettings settings)
    {
        if (settings.DetectedWarnings.Any(w => w.Severity == AnalysisSeverity.Error) ||
            settings.DetectedConflicts.Any(c => c.Severity == AnalysisSeverity.Error))
        {
            return AnalysisSeverity.Error;
        }

        if (settings.DetectedWarnings.Count > 0 || settings.DetectedConflicts.Count > 0)
        {
            return AnalysisSeverity.Warning;
        }

        return AnalysisSeverity.Info;
    }

    /// <summary>
    ///     Creates a result when no plugins are available for analysis.
    /// </summary>
    private AnalysisResult CreateNoPluginsResult()
    {
        var fragment = ReportFragment.CreateInfo(
            "Mod Detection Analysis",
            "No crash log plugins found for mod detection analysis. This may indicate an issue with crash log parsing or an empty plugin list.",
            100);

        var result = AnalysisResult.CreateSuccess(Name, fragment);
        result.AddMetadata("ProblematicModsCount", "0");
        result.AddMetadata("ConflictsCount", "0");
        result.AddMetadata("ImportantModsCount", "0");
        result.AddMetadata("MissingImportantModsCount", "0");

        return result;
    }

    #endregion
}
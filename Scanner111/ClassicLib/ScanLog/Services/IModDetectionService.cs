using System;
using System.Collections.Generic;
using System.Linq;

namespace Scanner111.ClassicLib.ScanLog.Services;

/// <summary>
/// Service for detecting mods in crash logs.
/// Equivalent to Python's DetectMods module.
/// </summary>
public interface IModDetectionService
{
    /// <summary>
    /// Detects single mods based on YAML dictionary and crash log plugins.
    /// </summary>
    /// <param name="yamlDict">Dictionary of mod names to warnings.</param>
    /// <param name="crashlogPlugins">Plugins found in crash log.</param>
    /// <param name="autoscanReport">Report to update with findings.</param>
    /// <returns>True if any mods were detected.</returns>
    bool DetectModsSingle(Dictionary<string, string> yamlDict, Dictionary<string, string> crashlogPlugins,
        List<string> autoscanReport);

    /// <summary>
    /// Detects conflicting mod combinations.
    /// </summary>
    /// <param name="yamlDict">Dictionary of mod pairs to warnings.</param>
    /// <param name="crashlogPlugins">Plugins found in crash log.</param>
    /// <param name="autoscanReport">Report to update with findings.</param>
    /// <returns>True if any conflicts were detected.</returns>
    bool DetectModsDouble(Dictionary<string, string> yamlDict, Dictionary<string, string> crashlogPlugins,
        List<string> autoscanReport);

    /// <summary>
    /// Detects important mods and evaluates their compatibility.
    /// </summary>
    /// <param name="yamlDict">Dictionary of important mods to check.</param>
    /// <param name="crashlogPlugins">Plugins found in crash log.</param>
    /// <param name="autoscanReport">Report to update with findings.</param>
    /// <param name="gpuRival">GPU type for compatibility checking.</param>
    void DetectModsImportant(Dictionary<string, string> yamlDict, Dictionary<string, string> crashlogPlugins,
        List<string> autoscanReport, string? gpuRival);
}

/// <summary>
/// Implementation of mod detection service.
/// </summary>
public class ModDetectionService : IModDetectionService
{
    /// <summary>
    /// Detects single mods based on YAML dictionary.
    /// </summary>
    public bool DetectModsSingle(Dictionary<string, string> yamlDict, Dictionary<string, string> crashlogPlugins,
        List<string> autoscanReport)
    {
        var modsFound = false;
        var yamlDictLower = ConvertToLowercase(yamlDict);
        var crashlogPluginsLower = ConvertToLowercase(crashlogPlugins);

        foreach (var (modName, modWarning) in yamlDictLower)
        {
            if (crashlogPluginsLower.TryGetValue(modName, out var pluginId))
            {
                ValidateWarning(modName, modWarning);

                autoscanReport.Add($"🚨 [{pluginId}] {modWarning}\n");
                modsFound = true;
            }
        }

        return modsFound;
    }

    /// <summary>
    /// Detects conflicting mod combinations.
    /// </summary>
    public bool DetectModsDouble(Dictionary<string, string> yamlDict, Dictionary<string, string> crashlogPlugins,
        List<string> autoscanReport)
    {
        var modsFound = false;
        var yamlDictLower = ConvertToLowercase(yamlDict);
        var crashlogPluginsLower = ConvertToLowercase(crashlogPlugins);
        var pluginNames = crashlogPluginsLower.Keys.ToHashSet();

        foreach (var (modPair, modWarning) in yamlDictLower)
        {
            var mods = modPair.Split(" | ", StringSplitOptions.RemoveEmptyEntries);
            if (mods.Length == 2 && pluginNames.Contains(mods[0]) && pluginNames.Contains(mods[1]))
            {
                ValidateWarning(modPair, modWarning);

                var plugin1Id = crashlogPluginsLower[mods[0]];
                var plugin2Id = crashlogPluginsLower[mods[1]];

                autoscanReport.Add($"🚨 [{plugin1Id}] + [{plugin2Id}] {modWarning}\n");
                modsFound = true;
            }
        }

        return modsFound;
    }

    /// <summary>
    /// Detects important mods and evaluates compatibility.
    /// </summary>
    public void DetectModsImportant(Dictionary<string, string> yamlDict, Dictionary<string, string> crashlogPlugins,
        List<string> autoscanReport, string? gpuRival)
    {
        var yamlDictLower = ConvertToLowercase(yamlDict);
        var crashlogPluginsLower = ConvertToLowercase(crashlogPlugins);

        foreach (var (modName, modWarning) in yamlDictLower)
        {
            var isInstalled = crashlogPluginsLower.ContainsKey(modName);
            var warningText = modWarning;

            // Adjust warning based on GPU compatibility
            if (!string.IsNullOrEmpty(gpuRival) && !string.IsNullOrEmpty(warningText))
            {
                if (warningText.Contains("nvidia", StringComparison.OrdinalIgnoreCase) &&
                    gpuRival.Equals("amd", StringComparison.OrdinalIgnoreCase))
                {
                    warningText = warningText.Replace("recommended", "incompatible with AMD",
                        StringComparison.OrdinalIgnoreCase);
                }
                else if (warningText.Contains("amd", StringComparison.OrdinalIgnoreCase) &&
                         gpuRival.Equals("nvidia", StringComparison.OrdinalIgnoreCase))
                {
                    warningText = warningText.Replace("recommended", "incompatible with NVIDIA",
                        StringComparison.OrdinalIgnoreCase);
                }
            }

            var statusIcon = isInstalled ? "✅" : "❌";
            var statusText = isInstalled ? "INSTALLED" : "NOT INSTALLED";

            autoscanReport.Add($"{statusIcon} {statusText}: {modName} - {warningText}\n");
        }
    }

    /// <summary>
    /// Converts dictionary keys to lowercase for case-insensitive comparisons.
    /// </summary>
    private static Dictionary<string, string> ConvertToLowercase(Dictionary<string, string> data)
    {
        return data.ToDictionary(
            kvp => kvp.Key.ToLowerInvariant(),
            kvp => kvp.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that a mod has an associated warning message.
    /// </summary>
    private static void ValidateWarning(string modName, string warning)
    {
        if (string.IsNullOrEmpty(warning))
        {
            throw new InvalidOperationException(
                $"Mod '{modName}' has no warning defined but was found in crashlog plugins.");
        }
    }
}

namespace Scanner111.Core.Models;

/// <summary>
///     Represents mod detection settings and installed mods information.
///     Immutable for thread-safety during concurrent analyzer execution.
/// </summary>
public sealed record ModDetectionSettings
{
    /// <summary>
    ///     Gets the set of loaded XSE plugin modules (e.g., "achievements.dll", "f4ee.dll").
    ///     Case-insensitive for comparison.
    /// </summary>
    public IReadOnlySet<string> XseModules { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets whether X-Cell mod is installed.
    /// </summary>
    public bool HasXCell { get; init; }

    /// <summary>
    ///     Gets whether an outdated version of X-Cell is installed.
    /// </summary>
    public bool HasOldXCell { get; init; }

    /// <summary>
    ///     Gets whether Baka ScrapHeap mod is installed.
    /// </summary>
    public bool HasBakaScrapHeap { get; init; }

    /// <summary>
    ///     Gets whether FCX mode is enabled for extended file checking.
    /// </summary>
    public bool? FcxMode { get; init; }

    /// <summary>
    ///     Gets the dictionary of detected plugins from crash log.
    ///     Key: Plugin name, Value: Plugin info/version.
    /// </summary>
    public IReadOnlyDictionary<string, string> CrashLogPlugins { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets additional metadata about detected mods.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Creates a new instance with the specified XSE modules.
    /// </summary>
    public static ModDetectionSettings CreateWithXseModules(IEnumerable<string> xseModules)
    {
        ArgumentNullException.ThrowIfNull(xseModules);

        var modules = new HashSet<string>(xseModules, StringComparer.OrdinalIgnoreCase);

        return new ModDetectionSettings
        {
            XseModules = modules,
            // Auto-detect common mods based on their DLL names
            HasXCell = modules.Contains("x-cell-fo4.dll") || modules.Contains("x-cell-og.dll") ||
                       modules.Contains("x-cell-ng2.dll"),
            HasBakaScrapHeap = modules.Contains("bakascrapheap.dll") ||
                               modules.Contains("baka_scrapheap.dll")
        };
    }

    /// <summary>
    ///     Creates settings from raw detection data.
    /// </summary>
    public static ModDetectionSettings FromDetectionData(
        IEnumerable<string>? xseModules = null,
        IDictionary<string, string>? crashLogPlugins = null,
        bool? fcxMode = null,
        IDictionary<string, object>? metadata = null)
    {
        var modules = xseModules != null
            ? new HashSet<string>(xseModules, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var hasXCell = modules.Contains("x-cell-fo4.dll") || modules.Contains("x-cell-og.dll") ||
                       modules.Contains("x-cell-ng2.dll");
        var hasOldXCell = modules.Contains("x-cell-fo4.dll");
        var hasBakaScrapHeap = modules.Contains("bakascrapheap.dll") ||
                               modules.Contains("baka_scrapheap.dll");

        return new ModDetectionSettings
        {
            XseModules = modules,
            HasXCell = hasXCell,
            HasOldXCell = hasOldXCell,
            HasBakaScrapHeap = hasBakaScrapHeap,
            FcxMode = fcxMode,
            CrashLogPlugins = crashLogPlugins != null
                ? new Dictionary<string, string>(crashLogPlugins, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            Metadata = metadata != null
                ? new Dictionary<string, object>(metadata, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    ///     Checks if a specific XSE module is loaded.
    /// </summary>
    public bool HasXseModule(string moduleName)
    {
        ArgumentNullException.ThrowIfNull(moduleName);
        return XseModules.Contains(moduleName);
    }

    /// <summary>
    ///     Checks if a plugin is present in the crash log.
    /// </summary>
    public bool HasPlugin(string pluginName)
    {
        ArgumentNullException.ThrowIfNull(pluginName);

        // Check exact match first
        if (CrashLogPlugins.ContainsKey(pluginName))
            return true;

        // Check partial match (case-insensitive)
        var lowerPlugin = pluginName.ToLowerInvariant();
        foreach (var key in CrashLogPlugins.Keys)
            if (key.Contains(lowerPlugin, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}
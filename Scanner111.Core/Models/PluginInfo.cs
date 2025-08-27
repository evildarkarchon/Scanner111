namespace Scanner111.Core.Models;

/// <summary>
///     Represents plugin information with origin tracking and metadata.
///     Immutable for thread-safety during concurrent analyzer execution.
/// </summary>
public sealed record PluginInfo
{
    /// <summary>
    ///     Gets the plugin filename (e.g., "Skyrim.esm", "MyMod.esp").
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///     Gets the source marker indicating where this plugin was detected.
    ///     Examples: "LO" for loadorder.txt, "00" for plugin ID in crash logs.
    /// </summary>
    public required string Origin { get; init; }

    /// <summary>
    ///     Gets the optional load order index for this plugin.
    /// </summary>
    public int? Index { get; init; }

    /// <summary>
    ///     Gets whether this plugin should be ignored during analysis.
    /// </summary>
    public bool IsIgnored { get; init; }

    /// <summary>
    ///     Gets the plugin type based on file extension.
    /// </summary>
    public PluginType Type => DeterminePluginType(Name);

    /// <summary>
    ///     Gets whether this plugin appears to be a DLL-based plugin.
    /// </summary>
    public bool IsDllPlugin => Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Gets additional metadata about this plugin.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Creates a plugin info from loadorder.txt entry.
    /// </summary>
    /// <param name="name">Plugin name</param>
    /// <param name="index">Load order index</param>
    /// <param name="isIgnored">Whether to ignore this plugin</param>
    public static PluginInfo FromLoadOrder(string name, int index, bool isIgnored = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new PluginInfo
        {
            Name = name.Trim(),
            Origin = "LO",
            Index = index,
            IsIgnored = isIgnored
        };
    }

    /// <summary>
    ///     Creates a plugin info from crash log detection.
    /// </summary>
    /// <param name="name">Plugin name</param>
    /// <param name="pluginId">Plugin ID or origin marker</param>
    /// <param name="isIgnored">Whether to ignore this plugin</param>
    public static PluginInfo FromCrashLog(string name, string pluginId, bool isIgnored = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        return new PluginInfo
        {
            Name = name.Trim(),
            Origin = pluginId.Trim(),
            IsIgnored = isIgnored
        };
    }

    /// <summary>
    ///     Creates a plugin info with custom metadata.
    /// </summary>
    /// <param name="name">Plugin name</param>
    /// <param name="origin">Origin marker</param>
    /// <param name="metadata">Additional metadata</param>
    /// <param name="index">Optional load order index</param>
    /// <param name="isIgnored">Whether to ignore this plugin</param>
    public static PluginInfo WithMetadata(
        string name,
        string origin,
        IDictionary<string, object> metadata,
        int? index = null,
        bool isIgnored = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);

        return new PluginInfo
        {
            Name = name.Trim(),
            Origin = origin.Trim(),
            Index = index,
            IsIgnored = isIgnored,
            Metadata = metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase) ??
                       new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        };
    }

    /// <summary>
    ///     Determines the plugin type based on file extension.
    /// </summary>
    private static PluginType DeterminePluginType(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return PluginType.Unknown;

        var extension = Path.GetExtension(name).ToLowerInvariant();

        return extension switch
        {
            ".esm" => PluginType.Master,
            ".esp" => PluginType.Plugin,
            ".esl" => PluginType.Light,
            ".dll" => PluginType.Dynamic,
            _ => PluginType.Unknown
        };
    }

    /// <summary>
    ///     Returns a string representation of this plugin info.
    /// </summary>
    public override string ToString()
    {
        return Index.HasValue ? $"[{Index:D2}:{Origin}] {Name}" : $"[{Origin}] {Name}";
    }
}

/// <summary>
///     Defines the type of plugin based on file extension.
/// </summary>
public enum PluginType
{
    /// <summary>
    ///     Unknown or unsupported plugin type.
    /// </summary>
    Unknown,

    /// <summary>
    ///     Master file (.esm).
    /// </summary>
    Master,

    /// <summary>
    ///     Plugin file (.esp).
    /// </summary>
    Plugin,

    /// <summary>
    ///     Light plugin (.esl).
    /// </summary>
    Light,

    /// <summary>
    ///     Dynamic library (.dll).
    /// </summary>
    Dynamic
}
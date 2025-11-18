using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Checks plugin counts against engine limits.
/// </summary>
public class PluginLimitChecker
{
    private const int MaxFullPlugins = 254;
    private const int MaxLightPlugins = 4096;
    private const int WarningThresholdFull = 240; // ~95% of limit
    private const int WarningThresholdLight = 3900; // ~95% of limit

    /// <summary>
    /// Checks if plugin counts are within limits and generates warnings if approaching limits.
    /// </summary>
    /// <param name="plugins">The list of plugins to check.</param>
    /// <returns>A <see cref="PluginLimitResult"/> with warnings if applicable.</returns>
    public PluginLimitResult CheckLimits(IReadOnlyList<PluginInfo> plugins)
    {
        var fullPlugins = plugins.Count(p => !p.IsLightPlugin);
        var lightPlugins = plugins.Count(p => p.IsLightPlugin);

        var warnings = new List<string>();
        var approachingLimit = false;

        // Check full plugin limit
        if (fullPlugins >= MaxFullPlugins)
        {
            warnings.Add($"CRITICAL: Full plugin limit reached ({fullPlugins}/{MaxFullPlugins}). Game may crash or fail to load.");
            approachingLimit = true;
        }
        else if (fullPlugins >= WarningThresholdFull)
        {
            warnings.Add($"WARNING: Approaching full plugin limit ({fullPlugins}/{MaxFullPlugins}). " +
                        $"Consider converting some plugins to light plugins (.esl).");
            approachingLimit = true;
        }

        // Check light plugin limit
        if (lightPlugins >= MaxLightPlugins)
        {
            warnings.Add($"CRITICAL: Light plugin limit reached ({lightPlugins}/{MaxLightPlugins}). Game may crash or fail to load.");
            approachingLimit = true;
        }
        else if (lightPlugins >= WarningThresholdLight)
        {
            warnings.Add($"WARNING: Approaching light plugin limit ({lightPlugins}/{MaxLightPlugins}).");
            approachingLimit = true;
        }

        return new PluginLimitResult
        {
            FullPluginCount = fullPlugins,
            LightPluginCount = lightPlugins,
            ApproachingLimit = approachingLimit,
            Warnings = warnings
        };
    }
}

/// <summary>
/// Result of plugin limit checking.
/// </summary>
public record PluginLimitResult
{
    /// <summary>
    /// Gets the number of full (non-light) plugins.
    /// </summary>
    public int FullPluginCount { get; init; }

    /// <summary>
    /// Gets the number of light plugins.
    /// </summary>
    public int LightPluginCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the plugin limits are being approached or exceeded.
    /// </summary>
    public bool ApproachingLimit { get; init; }

    /// <summary>
    /// Gets the warning messages.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}

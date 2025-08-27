namespace Scanner111.Core.Models;

/// <summary>
/// Represents GPU information extracted from crash log system specifications.
/// Immutable data structure for thread-safe access across analyzers.
/// </summary>
public sealed class GpuInfo
{
    /// <summary>
    /// Gets the name of the primary GPU.
    /// </summary>
    public string Primary { get; init; } = "Unknown";

    /// <summary>
    /// Gets the name of the secondary GPU, if present.
    /// </summary>
    public string? Secondary { get; init; }

    /// <summary>
    /// Gets the manufacturer of the primary GPU (AMD, Nvidia, Intel, etc.).
    /// </summary>
    public string Manufacturer { get; init; } = "Unknown";

    /// <summary>
    /// Gets the rival manufacturer name for mod compatibility checks.
    /// Used to warn about AMD-specific mods on Nvidia systems and vice versa.
    /// </summary>
    public string? Rival { get; init; }

    /// <summary>
    /// Gets whether GPU information was successfully detected.
    /// </summary>
    public bool IsDetected => Primary != "Unknown" && Manufacturer != "Unknown";

    /// <summary>
    /// Creates a default GpuInfo instance with unknown values.
    /// </summary>
    public static GpuInfo Unknown => new()
    {
        Primary = "Unknown",
        Manufacturer = "Unknown",
        Secondary = null,
        Rival = null
    };

    /// <summary>
    /// Creates a GpuInfo instance for AMD graphics.
    /// </summary>
    public static GpuInfo CreateAmd(string primaryName, string? secondaryName = null) => new()
    {
        Primary = primaryName,
        Secondary = secondaryName,
        Manufacturer = "AMD",
        Rival = "nvidia"
    };

    /// <summary>
    /// Creates a GpuInfo instance for Nvidia graphics.
    /// </summary>
    public static GpuInfo CreateNvidia(string primaryName, string? secondaryName = null) => new()
    {
        Primary = primaryName,
        Secondary = secondaryName,
        Manufacturer = "Nvidia",
        Rival = "amd"
    };

    /// <summary>
    /// Creates a GpuInfo instance for Intel graphics.
    /// </summary>
    public static GpuInfo CreateIntel(string primaryName, string? secondaryName = null) => new()
    {
        Primary = primaryName,
        Secondary = secondaryName,
        Manufacturer = "Intel",
        Rival = null // Intel typically doesn't have specific mod compatibility issues
    };

    public override string ToString()
    {
        var parts = new List<string> { $"Primary: {Primary}", $"Manufacturer: {Manufacturer}" };
        
        if (!string.IsNullOrEmpty(Secondary))
            parts.Add($"Secondary: {Secondary}");
        
        if (!string.IsNullOrEmpty(Rival))
            parts.Add($"Rival: {Rival}");
        
        return string.Join(", ", parts);
    }
}
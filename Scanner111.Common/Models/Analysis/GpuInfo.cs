using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Models.Analysis;

/// <summary>
/// Represents GPU information extracted from a crash log's system specs.
/// </summary>
public record GpuInfo
{
    /// <summary>
    /// Gets the primary GPU name as reported in the crash log.
    /// </summary>
    public string PrimaryGpu { get; init; } = "Unknown";

    /// <summary>
    /// Gets the secondary GPU name, if present.
    /// </summary>
    public string? SecondaryGpu { get; init; }

    /// <summary>
    /// Gets the detected GPU manufacturer (AMD or Nvidia).
    /// Null if manufacturer could not be determined.
    /// </summary>
    public GpuType? Manufacturer { get; init; }

    /// <summary>
    /// Gets the rival GPU manufacturer for compatibility checking.
    /// If the user has Nvidia, this would be AMD and vice versa.
    /// </summary>
    public GpuType? RivalManufacturer { get; init; }

    /// <summary>
    /// Gets a value indicating whether GPU information was successfully detected.
    /// </summary>
    public bool IsDetected => Manufacturer.HasValue;

    /// <summary>
    /// Returns an empty GPU info indicating detection failed.
    /// </summary>
    public static GpuInfo Unknown { get; } = new();
}

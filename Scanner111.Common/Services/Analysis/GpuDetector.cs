using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Analysis;

/// <summary>
/// Detects GPU information from crash log system specifications.
/// </summary>
public class GpuDetector : IGpuDetector
{
    private const string SystemSpecsSegmentName = "SYSTEM SPECS";
    private const string GpuPrimaryPrefix = "GPU #1";
    private const string GpuSecondaryPrefix = "GPU #2";

    /// <inheritdoc/>
    public GpuInfo Detect(LogSegment? systemSpecsSegment)
    {
        if (systemSpecsSegment == null || systemSpecsSegment.Lines.Count == 0)
        {
            return GpuInfo.Unknown;
        }

        string? primaryGpu = null;
        string? secondaryGpu = null;
        GpuType? manufacturer = null;
        GpuType? rival = null;

        foreach (var line in systemSpecsSegment.Lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.Contains(GpuPrimaryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Extract full GPU name after the colon
                var colonIndex = trimmedLine.IndexOf(':');
                if (colonIndex >= 0 && colonIndex < trimmedLine.Length - 1)
                {
                    primaryGpu = trimmedLine[(colonIndex + 1)..].Trim();
                }

                // Determine manufacturer from the line
                if (trimmedLine.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
                    trimmedLine.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                {
                    manufacturer = GpuType.Amd;
                    rival = GpuType.Nvidia;
                }
                else if (trimmedLine.Contains("Nvidia", StringComparison.OrdinalIgnoreCase) ||
                         trimmedLine.Contains("GeForce", StringComparison.OrdinalIgnoreCase) ||
                         trimmedLine.Contains("GTX", StringComparison.OrdinalIgnoreCase) ||
                         trimmedLine.Contains("RTX", StringComparison.OrdinalIgnoreCase))
                {
                    manufacturer = GpuType.Nvidia;
                    rival = GpuType.Amd;
                }
            }
            else if (trimmedLine.Contains(GpuSecondaryPrefix, StringComparison.OrdinalIgnoreCase))
            {
                // Extract secondary GPU name after the colon
                var colonIndex = trimmedLine.IndexOf(':');
                if (colonIndex >= 0 && colonIndex < trimmedLine.Length - 1)
                {
                    secondaryGpu = trimmedLine[(colonIndex + 1)..].Trim();
                }
            }
        }

        return new GpuInfo
        {
            PrimaryGpu = primaryGpu ?? "Unknown",
            SecondaryGpu = secondaryGpu,
            Manufacturer = manufacturer,
            RivalManufacturer = rival
        };
    }

    /// <inheritdoc/>
    public GpuInfo DetectFromSegments(IReadOnlyList<LogSegment> segments)
    {
        var systemSpecsSegment = segments.FirstOrDefault(s =>
            s.Name.Equals(SystemSpecsSegmentName, StringComparison.OrdinalIgnoreCase));

        return Detect(systemSpecsSegment);
    }
}

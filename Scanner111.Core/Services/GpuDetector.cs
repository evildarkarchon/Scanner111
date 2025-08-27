using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services;

/// <summary>
/// Service for detecting and parsing GPU information from crash log system specifications.
/// Thread-safe implementation that extracts GPU details for mod compatibility analysis.
/// </summary>
public sealed partial class GpuDetector : IGpuDetector
{
    private readonly ILogger<GpuDetector> _logger;
    
    // Regex patterns for GPU detection - compiled for performance
    private static readonly Regex AmdPattern = AmdGpuRegex();
    private static readonly Regex NvidiaPattern = NvidiaGpuRegex();
    private static readonly Regex IntelPattern = IntelGpuRegex();

    public GpuDetector(ILogger<GpuDetector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public GpuInfo DetectGpuInfo(IReadOnlyList<string> systemSpecSegment)
    {
        if (systemSpecSegment == null || systemSpecSegment.Count == 0)
        {
            _logger.LogDebug("No system specification data provided for GPU detection");
            return GpuInfo.Unknown;
        }

        string? primaryGpu = null;
        string? secondaryGpu = null;
        string manufacturer = "Unknown";
        string? rival = null;

        try
        {
            foreach (var line in systemSpecSegment)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                // Check for primary GPU (GPU #1)
                if (line.Contains("GPU #1", StringComparison.OrdinalIgnoreCase))
                {
                    var gpuResult = ParseGpuLine(line);
                    if (gpuResult.HasValue)
                    {
                        (primaryGpu, manufacturer, rival) = gpuResult.Value;
                        _logger.LogDebug("Detected primary GPU: {Primary}, Manufacturer: {Manufacturer}", primaryGpu, manufacturer);
                    }
                }
                // Check for secondary GPU (GPU #2)
                else if (line.Contains("GPU #2", StringComparison.OrdinalIgnoreCase) && line.Contains(':', StringComparison.Ordinal))
                {
                    secondaryGpu = ExtractGpuName(line);
                    if (!string.IsNullOrEmpty(secondaryGpu))
                    {
                        _logger.LogDebug("Detected secondary GPU: {Secondary}", secondaryGpu);
                    }
                }
            }

            // Return appropriate GpuInfo based on detected manufacturer
            return manufacturer switch
            {
                "AMD" => GpuInfo.CreateAmd(primaryGpu ?? "AMD", secondaryGpu),
                "Nvidia" => GpuInfo.CreateNvidia(primaryGpu ?? "Nvidia", secondaryGpu),
                "Intel" => GpuInfo.CreateIntel(primaryGpu ?? "Intel", secondaryGpu),
                _ => GpuInfo.Unknown
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse GPU information from system specifications");
            return GpuInfo.Unknown;
        }
    }

    /// <inheritdoc />
    public bool IsGpuCompatible(GpuInfo gpuInfo, string modWarning)
    {
        if (gpuInfo == null || string.IsNullOrEmpty(modWarning) || !gpuInfo.IsDetected)
            return true; // Assume compatible if we can't determine

        if (string.IsNullOrEmpty(gpuInfo.Rival))
            return true; // No rival means no specific compatibility concerns

        // Check if the mod warning mentions the rival GPU type
        // This indicates the mod is designed for a different GPU manufacturer
        var lowerWarning = modWarning.ToLowerInvariant();
        var lowerRival = gpuInfo.Rival.ToLowerInvariant();

        return !lowerWarning.Contains(lowerRival, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a GPU line to extract GPU name, manufacturer, and rival information.
    /// </summary>
    private (string name, string manufacturer, string rival)? ParseGpuLine(string line)
    {
        // Check for AMD
        if (AmdPattern.IsMatch(line))
        {
            var name = ExtractGpuName(line) ?? "AMD";
            return (name, "AMD", "nvidia");
        }

        // Check for Nvidia
        if (NvidiaPattern.IsMatch(line))
        {
            var name = ExtractGpuName(line) ?? "Nvidia";
            return (name, "Nvidia", "amd");
        }

        // Check for Intel
        if (IntelPattern.IsMatch(line))
        {
            var name = ExtractGpuName(line) ?? "Intel";
            return (name, "Intel", null);
        }

        _logger.LogDebug("Unable to identify GPU manufacturer from line: {Line}", line);
        return null;
    }

    /// <summary>
    /// Extracts the full GPU name from a GPU specification line.
    /// </summary>
    private static string? ExtractGpuName(string line)
    {
        if (!line.Contains(':', StringComparison.Ordinal))
            return null;

        var colonIndex = line.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex >= 0 && colonIndex < line.Length - 1)
        {
            return line.Substring(colonIndex + 1).Trim();
        }

        return null;
    }

    [GeneratedRegex(@"\bAMD\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AmdGpuRegex();

    [GeneratedRegex(@"\bNvidia\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NvidiaGpuRegex();

    [GeneratedRegex(@"\bIntel\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex IntelGpuRegex();
}
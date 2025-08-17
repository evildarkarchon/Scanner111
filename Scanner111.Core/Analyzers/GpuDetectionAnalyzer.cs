using System.Collections.Concurrent;
using Scanner111.Core.Models;

namespace Scanner111.Core.Analyzers;

/// <summary>
///     Provides functionality for detecting GPU information from crash logs.
///     Extracts GPU manufacturer information from system specifications.
/// </summary>
public class GpuDetectionAnalyzer : IAnalyzer
{
    private static readonly ConcurrentDictionary<string, Regex> PatternCache = new();
    private readonly Regex _gpuPattern;

    /// <summary>
    ///     Initialize the GPU Detection analyzer
    /// </summary>
    public GpuDetectionAnalyzer()
    {
        // Pattern to match GPU #1 line in crash logs (cached)
        const string patternKey = "gpu_pattern";
        _gpuPattern = PatternCache.GetOrAdd(patternKey, _ => new Regex(
            @"^\s*GPU\s*#1:\s*(.+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        ));
    }

    /// <summary>
    ///     Name of the analyzer
    /// </summary>
    public string Name => "GPU Detection Analyzer";

    /// <summary>
    ///     Priority of the analyzer (lower values run first)
    /// </summary>
    public int Priority => 15;

    /// <summary>
    ///     Whether this analyzer can be run in parallel with others
    /// </summary>
    public bool CanRunInParallel => true;

    /// <summary>
    ///     Analyze a crash log for GPU information
    /// </summary>
    /// <param name="crashLog">Crash log to analyze</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>GPU detection analysis result</returns>
    public async Task<AnalysisResult> AnalyzeAsync(CrashLog crashLog, CancellationToken cancellationToken = default)
    {
        var gpuInfo = ExtractGpuInfo(crashLog.OriginalLines);
        var reportLines = new List<string>();
        var data = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(gpuInfo.Manufacturer))
        {
            data["GpuManufacturer"] = gpuInfo.Manufacturer;
            data["GpuModel"] = gpuInfo.Model ?? "Unknown";
            data["GpuFullInfo"] = gpuInfo.FullInfo ?? "Unknown";

            reportLines.Add($"GPU Manufacturer: {gpuInfo.Manufacturer}\n");
            if (!string.IsNullOrEmpty(gpuInfo.Model)) reportLines.Add($"GPU Model: {gpuInfo.Model}\n");

            reportLines.Add("\n");
        }
        else
        {
            reportLines.Add("* NO GPU INFORMATION FOUND *\n\n");
        }

        return new GenericAnalysisResult
        {
            AnalyzerName = Name,
            Data = data,
            ReportLines = reportLines,
            HasFindings = !string.IsNullOrEmpty(gpuInfo.Manufacturer)
        };
    }

    /// <summary>
    ///     Extracts GPU information from system specifications
    /// </summary>
    /// <param name="systemSpecs">System specifications lines from crash log</param>
    /// <returns>GPU information including manufacturer and model</returns>
    private GpuInfo ExtractGpuInfo(List<string> systemSpecs)
    {
        if (systemSpecs.Count == 0)
            return new GpuInfo();

        foreach (var line in systemSpecs)
        {
            var match = _gpuPattern.Match(line);
            if (match.Success)
            {
                var gpuFullInfo = match.Groups[1].Value.Trim();
                var manufacturer = ExtractManufacturer(gpuFullInfo);
                var model = ExtractModel(gpuFullInfo, manufacturer);

                return new GpuInfo
                {
                    Manufacturer = manufacturer,
                    Model = model,
                    FullInfo = gpuFullInfo
                };
            }
        }

        return new GpuInfo();
    }

    /// <summary>
    ///     Extracts GPU manufacturer from full GPU information string
    /// </summary>
    /// <param name="gpuFullInfo">Full GPU information string</param>
    /// <returns>GPU manufacturer name or empty string if not found</returns>
    private static string ExtractManufacturer(string gpuFullInfo)
    {
        if (string.IsNullOrEmpty(gpuFullInfo))
            return string.Empty;

        var gpuInfoLower = gpuFullInfo.ToLowerInvariant();

        // Check for known GPU manufacturers
        if (gpuInfoLower.Contains("nvidia") || gpuInfoLower.Contains("geforce") || gpuInfoLower.Contains("quadro") ||
            gpuInfoLower.Contains("tesla"))
            return "Nvidia";

        if (gpuInfoLower.Contains("amd") || gpuInfoLower.Contains("radeon") || gpuInfoLower.Contains("ryzen") ||
            gpuInfoLower.Contains("ati"))
            return "AMD";

        if (gpuInfoLower.Contains("intel") || gpuInfoLower.Contains("iris") || gpuInfoLower.Contains("uhd") ||
            gpuInfoLower.Contains("hd graphics"))
            return "Intel";

        // Check for other manufacturers
        if (gpuInfoLower.Contains("microsoft"))
            return "Microsoft";

        return string.Empty;
    }

    /// <summary>
    ///     Extracts GPU model from full GPU information string
    /// </summary>
    /// <param name="gpuFullInfo">Full GPU information string</param>
    /// <param name="manufacturer">GPU manufacturer</param>
    /// <returns>GPU model name or null if not extractable</returns>
    private static string? ExtractModel(string gpuFullInfo, string manufacturer)
    {
        if (string.IsNullOrEmpty(gpuFullInfo) || string.IsNullOrEmpty(manufacturer))
            return null;

        // For patterns like "Nvidia AD104 [GeForce RTX 4070]"
        var bracketMatch = Regex.Match(gpuFullInfo, @"\[([^\]]+)\]");
        if (bracketMatch.Success) return bracketMatch.Groups[1].Value.Trim();

        var modelText = gpuFullInfo;
        var manufacturerLower = manufacturer.ToLowerInvariant();
        var modelLower = modelText.ToLowerInvariant();

        // Remove manufacturer name prefix only
        if (modelLower.StartsWith(manufacturerLower + " "))
        {
            modelText = modelText.Substring(manufacturerLower.Length + 1).Trim();
            modelLower = modelText.ToLowerInvariant();
        }

        // Handle specific brand prefixes more carefully
        if (manufacturerLower == "nvidia")
        {
            // Only remove "GeForce" if it's followed by more specific model info
            if (modelLower.StartsWith("geforce "))
            {
                var afterGeforce = modelText.Substring(8).Trim();
                if (!string.IsNullOrWhiteSpace(afterGeforce) && afterGeforce.Length > 3) modelText = afterGeforce;
            }
        }
        else if (manufacturerLower == "amd")
        {
            // For AMD, keep "Radeon" as it's part of the product line
            // Don't remove it unless there's more specific info after it
        }

        return string.IsNullOrWhiteSpace(modelText) ? gpuFullInfo : modelText;
    }

    /// <summary>
    ///     Container for GPU information
    /// </summary>
    private class GpuInfo
    {
        public string Manufacturer { get; set; } = string.Empty;
        public string? Model { get; set; }
        public string? FullInfo { get; set; }
    }
}
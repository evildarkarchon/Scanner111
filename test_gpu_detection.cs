using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;

// Test the GPU detection with the sample crash log
var analyzer = new GpuDetectionAnalyzer();

// Create a test crash log using the sample data
var crashLog = new CrashLog
{
    OriginalLines = new List<string>
    {
        "Fallout 4 v1.10.163",
        "Buffout 4 v1.28.6",
        "",
        "SYSTEM SPECS:",
        "\tOS: Microsoft Windows 11 Pro v10.0.22621",
        "\tCPU: AuthenticAMD AMD Ryzen 7 7800X3D 8-Core Processor           ",
        "\tGPU #1: Nvidia AD104 [GeForce RTX 4070]",
        "\tGPU #2: AMD Raphael",
        "\tGPU #3: Microsoft Basic Render Driver",
        "\tPHYSICAL MEMORY: 15.62 GB/63.15 GB"
    },
    FilePath = "test.log"
};

// Run the analysis
var result = await analyzer.AnalyzeAsync(crashLog);

Console.WriteLine($"[DEBUG_LOG] GPU Detection Test Results:");
Console.WriteLine($"[DEBUG_LOG] Analyzer Name: {result.AnalyzerName}");
Console.WriteLine($"[DEBUG_LOG] Has Findings: {result.HasFindings}");
Console.WriteLine($"[DEBUG_LOG] Success: {result.Success}");

if (result is GenericAnalysisResult genericResult)
{
    Console.WriteLine($"[DEBUG_LOG] GPU Manufacturer: {genericResult.Data.GetValueOrDefault("GpuManufacturer", "Not Found")}");
    Console.WriteLine($"[DEBUG_LOG] GPU Model: {genericResult.Data.GetValueOrDefault("GpuModel", "Not Found")}");
    Console.WriteLine($"[DEBUG_LOG] GPU Full Info: {genericResult.Data.GetValueOrDefault("GpuFullInfo", "Not Found")}");
}

Console.WriteLine($"[DEBUG_LOG] Report Lines:");
foreach (var line in result.ReportLines)
{
    Console.WriteLine($"[DEBUG_LOG] {line.Trim()}");
}

Console.WriteLine($"[DEBUG_LOG] GPU Detection test completed successfully!");
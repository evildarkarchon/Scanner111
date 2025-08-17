using Scanner111.Core.Analyzers;
using Scanner111.Core.Models;

namespace Scanner111.Tests.Analyzers;

public class GpuDetectionAnalyzerTests
{
    private readonly GpuDetectionAnalyzer _analyzer;

    public GpuDetectionAnalyzerTests()
    {
        _analyzer = new GpuDetectionAnalyzer();
    }

    [Fact]
    public void Properties_ShouldHaveCorrectValues()
    {
        // Assert
        _analyzer.Name.Should().Be("GPU Detection Analyzer");
        _analyzer.Priority.Should().Be(15);
        _analyzer.CanRunInParallel.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_WithNvidiaGpu_ShouldDetectCorrectly()
    {
        // Arrange
        var crashLog = CreateTestCrashLog([
            "SYSTEM SPECS:",
            "	OS: Microsoft Windows 11 Pro v10.0.22621",
            "	CPU: AuthenticAMD AMD Ryzen 7 7800X3D 8-Core Processor           ",
            "	GPU #1: Nvidia AD104 [GeForce RTX 4070]",
            "	GPU #2: AMD Raphael",
            "	PHYSICAL MEMORY: 15.62 GB/63.15 GB"
        ]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.AnalyzerName.Should().Be("GPU Detection Analyzer");
        result.HasFindings.Should().BeTrue();
        result.Success.Should().BeTrue();

        var genericResult = result.Should().BeOfType<GenericAnalysisResult>().Subject;
        genericResult.Data.Should().ContainKey("GpuManufacturer");
        genericResult.Data["GpuManufacturer"].Should().Be("Nvidia");
        genericResult.Data.Should().ContainKey("GpuModel");
        genericResult.Data["GpuModel"].Should().Be("GeForce RTX 4070");
        genericResult.Data.Should().ContainKey("GpuFullInfo");
        genericResult.Data["GpuFullInfo"].Should().Be("Nvidia AD104 [GeForce RTX 4070]");

        result.ReportLines.Should().Contain("GPU Manufacturer: Nvidia\n");
        result.ReportLines.Should().Contain("GPU Model: GeForce RTX 4070\n");
    }

    [Fact]
    public async Task AnalyzeAsync_WithAmdGpu_ShouldDetectCorrectly()
    {
        // Arrange
        var crashLog = CreateTestCrashLog([
            "SYSTEM SPECS:",
            "	GPU #1: AMD Radeon RX 6800 XT",
            "	GPU #2: Intel UHD Graphics"
        ]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.HasFindings.Should().BeTrue();

        var genericResult = result.Should().BeOfType<GenericAnalysisResult>().Subject;
        genericResult.Data["GpuManufacturer"].Should().Be("AMD");
        genericResult.Data["GpuModel"].Should().Be("Radeon RX 6800 XT");
    }

    [Fact]
    public async Task AnalyzeAsync_WithIntelGpu_ShouldDetectCorrectly()
    {
        // Arrange
        var crashLog = CreateTestCrashLog([
            "SYSTEM SPECS:",
            "	GPU #1: Intel Iris Xe Graphics",
            "	GPU #2: Other GPU"
        ]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.HasFindings.Should().BeTrue();

        var genericResult = result.Should().BeOfType<GenericAnalysisResult>().Subject;
        genericResult.Data["GpuManufacturer"].Should().Be("Intel");
        genericResult.Data["GpuModel"].Should().Be("Iris Xe Graphics");
    }

    [Fact]
    public async Task AnalyzeAsync_WithMicrosoftGpu_ShouldDetectCorrectly()
    {
        // Arrange
        var crashLog = CreateTestCrashLog([
            "SYSTEM SPECS:",
            "	GPU #1: Microsoft Basic Render Driver",
            "	GPU #2: Other GPU"
        ]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.HasFindings.Should().BeTrue();

        var genericResult = result.Should().BeOfType<GenericAnalysisResult>().Subject;
        genericResult.Data["GpuManufacturer"].Should().Be("Microsoft");
        genericResult.Data["GpuModel"].Should().Be("Basic Render Driver");
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoGpuInfo_ShouldReturnNoFindings()
    {
        // Arrange
        var crashLog = CreateTestCrashLog([
            "SYSTEM SPECS:",
            "	OS: Microsoft Windows 11 Pro v10.0.22621",
            "	CPU: AMD Ryzen 7 7800X3D",
            "	PHYSICAL MEMORY: 15.62 GB/63.15 GB"
        ]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.HasFindings.Should().BeFalse();
        result.Success.Should().BeTrue();

        var genericResult = result.Should().BeOfType<GenericAnalysisResult>().Subject;
        genericResult.Data.Should().BeEmpty();

        result.ReportLines.Should().Contain("* NO GPU INFORMATION FOUND *\n\n");
    }

    [Fact]
    public async Task AnalyzeAsync_WithEmptyOriginalLines_ShouldReturnNoFindings()
    {
        // Arrange
        var crashLog = CreateTestCrashLog([]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.HasFindings.Should().BeFalse();
        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzeAsync_OnlyChecksGpu1_ShouldIgnoreOtherGpus()
    {
        // Arrange
        var crashLog = CreateTestCrashLog([
            "SYSTEM SPECS:",
            "	GPU #2: Nvidia GeForce RTX 4090",
            "	GPU #3: AMD Radeon RX 7900 XTX",
            "	GPU #1: Intel UHD Graphics 770"
        ]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.HasFindings.Should().BeTrue();

        var genericResult = result.Should().BeOfType<GenericAnalysisResult>().Subject;
        genericResult.Data["GpuManufacturer"].Should().Be("Intel");
        genericResult.Data["GpuModel"].Should().Be("UHD Graphics 770");
    }

    [Theory]
    [InlineData("GPU #1: GeForce RTX 4070", "Nvidia", "RTX 4070")]
    [InlineData("GPU #1: NVIDIA RTX 3080", "Nvidia", "RTX 3080")]
    [InlineData("GPU #1: AMD RX 6800", "AMD", "RX 6800")]
    [InlineData("GPU #1: Radeon Pro W7800", "AMD", "Radeon Pro W7800")]
    [InlineData("GPU #1: Intel HD Graphics 630", "Intel", "HD Graphics 630")]
    [InlineData("GPU #1: Intel UHD Graphics", "Intel", "UHD Graphics")]
    [InlineData("	GPU #1: Nvidia AD104 [GeForce RTX 4070]", "Nvidia", "GeForce RTX 4070")]
    public async Task AnalyzeAsync_WithVariousGpuFormats_ShouldDetectCorrectly(
        string gpuLine, string expectedManufacturer, string expectedModel)
    {
        // Arrange
        var crashLog = CreateTestCrashLog([gpuLine]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.HasFindings.Should().BeTrue();

        var genericResult = result.Should().BeOfType<GenericAnalysisResult>().Subject;
        genericResult.Data["GpuManufacturer"].Should().Be(expectedManufacturer);
        genericResult.Data["GpuModel"].Should().Be(expectedModel);
    }

    [Fact]
    public async Task AnalyzeAsync_WithCaseInsensitiveGpuLine_ShouldDetectCorrectly()
    {
        // Arrange
        var crashLog = CreateTestCrashLog([
            "	gpu #1: nvidia ad104 [geforce rtx 4070]"
        ]);

        // Act
        var result = await _analyzer.AnalyzeAsync(crashLog);

        // Assert
        result.Should().NotBeNull();
        result.HasFindings.Should().BeTrue();

        var genericResult = result.Should().BeOfType<GenericAnalysisResult>().Subject;
        genericResult.Data["GpuManufacturer"].Should().Be("Nvidia");
    }

    private static CrashLog CreateTestCrashLog(List<string> originalLines)
    {
        return new CrashLog
        {
            OriginalLines = originalLines,
            FilePath = "test.log"
        };
    }
}
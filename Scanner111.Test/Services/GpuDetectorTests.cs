using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Xunit;

namespace Scanner111.Test.Services;

/// <summary>
/// Unit tests for GpuDetector service to ensure proper GPU detection from system specifications.
/// </summary>
public class GpuDetectorTests
{
    private readonly ILogger<GpuDetector> _mockLogger;
    private readonly GpuDetector _gpuDetector;

    public GpuDetectorTests()
    {
        _mockLogger = Substitute.For<ILogger<GpuDetector>>();
        _gpuDetector = new GpuDetector(_mockLogger);
    }

    [Fact]
    public void DetectGpuInfo_WithEmptyInput_ReturnsUnknownGpu()
    {
        // Arrange
        var emptySystemSpecs = new List<string>();

        // Act
        var result = _gpuDetector.DetectGpuInfo(emptySystemSpecs);

        // Assert
        result.Should().NotBeNull();
        result.IsDetected.Should().BeFalse();
        result.Primary.Should().Be("Unknown");
        result.Manufacturer.Should().Be("Unknown");
        result.Rival.Should().BeNull();
    }

    [Fact]
    public void DetectGpuInfo_WithNvidiaGpu_ReturnsCorrectInfo()
    {
        // Arrange
        var systemSpecs = new List<string>
        {
            "System: Windows 10",
            "GPU #1: NVIDIA GeForce RTX 3080",
            "Memory: 16 GB"
        };

        // Act
        var result = _gpuDetector.DetectGpuInfo(systemSpecs);

        // Assert
        result.Should().NotBeNull();
        result.IsDetected.Should().BeTrue();
        result.Primary.Should().Be("NVIDIA GeForce RTX 3080");
        result.Manufacturer.Should().Be("Nvidia");
        result.Rival.Should().Be("amd");
    }

    [Fact]
    public void DetectGpuInfo_WithAmdGpu_ReturnsCorrectInfo()
    {
        // Arrange
        var systemSpecs = new List<string>
        {
            "CPU: AMD Ryzen 7 3700X",
            "GPU #1: AMD Radeon RX 6800 XT",
            "RAM: 32 GB"
        };

        // Act
        var result = _gpuDetector.DetectGpuInfo(systemSpecs);

        // Assert
        result.Should().NotBeNull();
        result.IsDetected.Should().BeTrue();
        result.Primary.Should().Be("AMD Radeon RX 6800 XT");
        result.Manufacturer.Should().Be("AMD");
        result.Rival.Should().Be("nvidia");
    }

    [Fact]
    public void DetectGpuInfo_WithIntelGpu_ReturnsCorrectInfo()
    {
        // Arrange
        var systemSpecs = new List<string>
        {
            "CPU: Intel Core i7-9700K",
            "GPU #1: Intel UHD Graphics 630",
            "Memory: 16 GB"
        };

        // Act
        var result = _gpuDetector.DetectGpuInfo(systemSpecs);

        // Assert
        result.Should().NotBeNull();
        result.IsDetected.Should().BeTrue();
        result.Primary.Should().Be("Intel UHD Graphics 630");
        result.Manufacturer.Should().Be("Intel");
        result.Rival.Should().BeNull(); // Intel doesn't have specific mod compatibility issues
    }

    [Fact]
    public void DetectGpuInfo_WithPrimaryAndSecondaryGpu_ReturnsCorrectInfo()
    {
        // Arrange
        var systemSpecs = new List<string>
        {
            "System: Windows 11",
            "GPU #1: NVIDIA GeForce RTX 4090",
            "GPU #2: Intel UHD Graphics 770",
            "Memory: 64 GB DDR5"
        };

        // Act
        var result = _gpuDetector.DetectGpuInfo(systemSpecs);

        // Assert
        result.Should().NotBeNull();
        result.IsDetected.Should().BeTrue();
        result.Primary.Should().Be("NVIDIA GeForce RTX 4090");
        result.Secondary.Should().Be("Intel UHD Graphics 770");
        result.Manufacturer.Should().Be("Nvidia");
        result.Rival.Should().Be("amd");
    }

    [Fact]
    public void DetectGpuInfo_WithUnrecognizedGpu_ReturnsUnknown()
    {
        // Arrange
        var systemSpecs = new List<string>
        {
            "CPU: Custom CPU",
            "GPU #1: Generic Graphics Device",
            "Memory: 8 GB"
        };

        // Act
        var result = _gpuDetector.DetectGpuInfo(systemSpecs);

        // Assert
        result.Should().NotBeNull();
        result.IsDetected.Should().BeFalse();
        result.Primary.Should().Be("Unknown");
        result.Manufacturer.Should().Be("Unknown");
        result.Rival.Should().BeNull();
    }

    [Theory]
    [InlineData("Some mod for nvidia cards", "nvidia", false)] // Nvidia mod on Nvidia system
    [InlineData("Some mod for amd cards", "nvidia", true)]    // AMD mod on Nvidia system - incompatible
    [InlineData("Generic mod with no gpu requirements", "nvidia", true)]  // Generic mod - compatible
    [InlineData("AMD specific feature mod", "amd", false)]     // AMD mod on AMD system
    public void IsGpuCompatible_WithVariousModWarnings_ReturnsExpectedCompatibility(
        string modWarning, 
        string rivalGpuType, 
        bool expectedCompatible)
    {
        // Arrange
        var gpuInfo = new GpuInfo 
        { 
            Primary = "Test GPU", 
            Manufacturer = "Test", 
            Rival = rivalGpuType 
        };

        // Act
        var result = _gpuDetector.IsGpuCompatible(gpuInfo, modWarning);

        // Assert
        result.Should().Be(expectedCompatible);
    }

    [Fact]
    public void IsGpuCompatible_WithUnknownGpu_ReturnsTrue()
    {
        // Arrange
        var unknownGpu = GpuInfo.Unknown;
        var modWarning = "Some AMD specific mod";

        // Act
        var result = _gpuDetector.IsGpuCompatible(unknownGpu, modWarning);

        // Assert
        result.Should().BeTrue(); // Assume compatible when GPU is unknown
    }

    [Fact]
    public void IsGpuCompatible_WithIntelGpu_ReturnsTrue()
    {
        // Arrange
        var intelGpu = GpuInfo.CreateIntel("Intel UHD Graphics");
        var modWarning = "Some GPU specific mod";

        // Act
        var result = _gpuDetector.IsGpuCompatible(intelGpu, modWarning);

        // Assert
        result.Should().BeTrue(); // Intel GPUs don't have specific compatibility issues
    }

    [Fact]
    public void DetectGpuInfo_WithMalformedLines_HandlesGracefully()
    {
        // Arrange
        var systemSpecs = new List<string>
        {
            "GPU #1", // Missing colon
            "GPU #1:", // Missing name after colon
            "GPU #1: ", // Empty name after colon
            "Not a GPU line",
            "GPU #1: NVIDIA GeForce GTX 1060", // Valid line
        };

        // Act
        var result = _gpuDetector.DetectGpuInfo(systemSpecs);

        // Assert
        result.Should().NotBeNull();
        result.IsDetected.Should().BeTrue();
        result.Primary.Should().Be("NVIDIA GeForce GTX 1060");
        result.Manufacturer.Should().Be("Nvidia");
    }

    [Fact]
    public void DetectGpuInfo_CaseInsensitive_DetectsCorrectly()
    {
        // Arrange
        var systemSpecs = new List<string>
        {
            "gpu #1: nvidia geforce rtx 3070", // lowercase
            "GPU #2: AMD RADEON RX 6700 XT"     // uppercase secondary
        };

        // Act
        var result = _gpuDetector.DetectGpuInfo(systemSpecs);

        // Assert
        result.Should().NotBeNull();
        result.IsDetected.Should().BeTrue();
        result.Primary.Should().Be("nvidia geforce rtx 3070");
        result.Secondary.Should().Be("AMD RADEON RX 6700 XT");
        result.Manufacturer.Should().Be("Nvidia");
        result.Rival.Should().Be("amd");
    }
}
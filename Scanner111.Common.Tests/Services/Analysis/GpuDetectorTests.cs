using FluentAssertions;
using Scanner111.Common.Models.Analysis;
using Scanner111.Common.Services.Analysis;

namespace Scanner111.Common.Tests.Services.Analysis;

/// <summary>
/// Tests for GpuDetector.
/// </summary>
public class GpuDetectorTests
{
    private readonly GpuDetector _detector;

    public GpuDetectorTests()
    {
        _detector = new GpuDetector();
    }

    [Fact]
    public void Detect_WithNvidiaGpu_DetectsManufacturer()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "GPU #1: Nvidia GA104 [GeForce RTX 3060 Ti Lite Hash Rate]",
            "GPU #2: Microsoft Basic Render Driver"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Nvidia);
        result.RivalManufacturer.Should().Be(GpuType.Amd);
        result.PrimaryGpu.Should().Be("Nvidia GA104 [GeForce RTX 3060 Ti Lite Hash Rate]");
        result.SecondaryGpu.Should().Be("Microsoft Basic Render Driver");
    }

    [Fact]
    public void Detect_WithAmdGpu_DetectsManufacturer()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "GPU #1: AMD Radeon RX 6800 XT"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Amd);
        result.RivalManufacturer.Should().Be(GpuType.Nvidia);
        result.PrimaryGpu.Should().Be("AMD Radeon RX 6800 XT");
        result.SecondaryGpu.Should().BeNull();
    }

    [Fact]
    public void Detect_WithRadeonKeyword_DetectsAmd()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "GPU #1: Radeon RX 580"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Amd);
    }

    [Fact]
    public void Detect_WithGeForceKeyword_DetectsNvidia()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "GPU #1: GeForce GTX 1080"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Nvidia);
    }

    [Fact]
    public void Detect_WithGtxKeyword_DetectsNvidia()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "GPU #1: EVGA GTX 970 SC"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Nvidia);
    }

    [Fact]
    public void Detect_WithRtxKeyword_DetectsNvidia()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "GPU #1: RTX 4090"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Nvidia);
    }

    [Fact]
    public void Detect_WithUnknownGpu_ReturnsUnknownManufacturer()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "GPU #1: Intel UHD Graphics 630"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeFalse();
        result.Manufacturer.Should().BeNull();
        result.RivalManufacturer.Should().BeNull();
        result.PrimaryGpu.Should().Be("Intel UHD Graphics 630");
    }

    [Fact]
    public void Detect_WithNullSegment_ReturnsUnknown()
    {
        // Act
        var result = _detector.Detect(null);

        // Assert
        result.Should().Be(GpuInfo.Unknown);
        result.IsDetected.Should().BeFalse();
    }

    [Fact]
    public void Detect_WithEmptySegment_ReturnsUnknown()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "SYSTEM SPECS",
            Lines = Array.Empty<string>()
        };

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.Should().Be(GpuInfo.Unknown);
    }

    [Fact]
    public void Detect_WithNoGpuLines_ReturnsUnknownPrimaryGpu()
    {
        // Arrange
        var segment = new LogSegment
        {
            Name = "SYSTEM SPECS",
            Lines = new[]
            {
                "OS: Microsoft Windows 10 Home v10.0.19041",
                "CPU: AuthenticAMD AMD Ryzen 7 5800X 8-Core Processor",
                "PHYSICAL MEMORY: 12.18 GB/15.93 GB"
            }
        };

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.PrimaryGpu.Should().Be("Unknown");
        result.SecondaryGpu.Should().BeNull();
        result.IsDetected.Should().BeFalse();
    }

    [Fact]
    public void Detect_IsCaseInsensitive()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "gpu #1: nvidia geforce rtx 3080"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Nvidia);
    }

    [Fact]
    public void DetectFromSegments_FindsSystemSpecsSegment()
    {
        // Arrange
        var segments = new[]
        {
            new LogSegment
            {
                Name = "Compatibility",
                Lines = new[] { "F4EE: true" }
            },
            new LogSegment
            {
                Name = "SYSTEM SPECS",
                Lines = new[]
                {
                    "SYSTEM SPECS:",
                    "OS: Microsoft Windows 10",
                    "GPU #1: AMD Radeon RX 6900 XT"
                }
            },
            new LogSegment
            {
                Name = "PROBABLE CALL STACK",
                Lines = new[] { "[0] 0x7FF..." }
            }
        };

        // Act
        var result = _detector.DetectFromSegments(segments);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Amd);
        result.PrimaryGpu.Should().Be("AMD Radeon RX 6900 XT");
    }

    [Fact]
    public void DetectFromSegments_WithNoSystemSpecsSegment_ReturnsUnknown()
    {
        // Arrange
        var segments = new[]
        {
            new LogSegment
            {
                Name = "PROBABLE CALL STACK",
                Lines = new[] { "[0] 0x7FF..." }
            }
        };

        // Act
        var result = _detector.DetectFromSegments(segments);

        // Assert
        result.Should().Be(GpuInfo.Unknown);
    }

    [Fact]
    public void DetectFromSegments_WithEmptySegments_ReturnsUnknown()
    {
        // Act
        var result = _detector.DetectFromSegments(Array.Empty<LogSegment>());

        // Assert
        result.Should().Be(GpuInfo.Unknown);
    }

    [Fact]
    public void Detect_WithWhitespaceInLine_HandlesCorrectly()
    {
        // Arrange
        var segment = CreateSystemSpecsSegment(
            "    GPU #1:   Nvidia GeForce RTX 3070   "
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.IsDetected.Should().BeTrue();
        result.Manufacturer.Should().Be(GpuType.Nvidia);
        result.PrimaryGpu.Should().Be("Nvidia GeForce RTX 3070");
    }

    [Fact]
    public void Detect_WithOnlySecondaryGpu_ReturnsUnknownPrimary()
    {
        // Arrange - unusual case where only GPU #2 is present
        var segment = CreateSystemSpecsSegment(
            "GPU #2: Microsoft Basic Render Driver"
        );

        // Act
        var result = _detector.Detect(segment);

        // Assert
        result.PrimaryGpu.Should().Be("Unknown");
        result.SecondaryGpu.Should().Be("Microsoft Basic Render Driver");
        result.IsDetected.Should().BeFalse(); // No primary GPU detected
    }

    private static LogSegment CreateSystemSpecsSegment(params string[] gpuLines)
    {
        var lines = new List<string>
        {
            "SYSTEM SPECS:",
            "OS: Microsoft Windows 10 Home v10.0.19041",
            "CPU: AuthenticAMD AMD Ryzen 7 5800X 8-Core Processor"
        };
        lines.AddRange(gpuLines);
        lines.Add("PHYSICAL MEMORY: 12.18 GB/15.93 GB");

        return new LogSegment
        {
            Name = "SYSTEM SPECS",
            Lines = lines
        };
    }
}

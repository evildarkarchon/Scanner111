using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis.SignalProcessing;

namespace Scanner111.Test.Analysis.SignalProcessing;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "SignalProcessing")]
public sealed class CallStackAnalyzerTests
{
    private readonly ILogger<CallStackAnalyzer> _logger;
    private readonly CallStackAnalyzer _sut;

    public CallStackAnalyzerTests()
    {
        _logger = Substitute.For<ILogger<CallStackAnalyzer>>();
        _sut = new CallStackAnalyzer(_logger);
    }

    private const string SampleCallStack = @"
[0] 0x7FF6A1B2C3D4 Fallout4.exe+0x1B2C3D4 -> BSResourceNiBinaryStream::Seek
[1] 0x7FF6A1B2C3D5 Fallout4.exe+0x1B2C3D5 -> BSResourceNiBinaryStream::Read
[2] 0x7FF6A1B2C3D6 nvwgf2umx.dll+0x123456 -> NvDriver::Render
[3] 0x7FF6A1B2C3D7 d3d11.dll+0x234567 -> D3D11CreateDevice
[4] 0x7FF6A1B2C3D8 Fallout4.exe+0x1B2C3D8 -> GameRenderer::Draw
";

    private const string RecursiveCallStack = @"
[0] 0x7FF6A1B2C3D4 Module.dll+0x1234 -> RecursiveFunc
[1] 0x7FF6A1B2C3D5 Module.dll+0x1235 -> RecursiveFunc
[2] 0x7FF6A1B2C3D6 Module.dll+0x1236 -> RecursiveFunc
[3] 0x7FF6A1B2C3D7 Module.dll+0x1237 -> RecursiveFunc
[4] 0x7FF6A1B2C3D8 Module.dll+0x1238 -> RecursiveFunc
";

    private static readonly string DeepCallStack = string.Join("\n", Enumerable.Range(0, 150)
        .Select(i => $"[{i}] 0x7FF6A1B2C{i:X3} Module{i % 5}.dll+0x{i:X4} -> Function{i}"));

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new CallStackAnalyzer(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void AnalyzeCallStack_EmptyString_ReturnsInvalidAnalysis()
    {
        // Act
        var analysis = _sut.AnalyzeCallStack("");

        // Assert
        analysis.IsValid.Should().BeFalse();
        analysis.TotalFrames.Should().Be(0);
        analysis.Frames.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeCallStack_NullString_ReturnsInvalidAnalysis()
    {
        // Act
        var analysis = _sut.AnalyzeCallStack(null!);

        // Assert
        analysis.IsValid.Should().BeFalse();
    }

    [Fact]
    public void AnalyzeCallStack_ValidStack_ParsesCorrectly()
    {
        // Act
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);

        // Assert
        analysis.IsValid.Should().BeTrue();
        analysis.TotalFrames.Should().Be(5);
        analysis.Frames.Should().HaveCount(5);
        
        // Check first frame
        var firstFrame = analysis.Frames[0];
        firstFrame.Index.Should().Be(0);
        firstFrame.Address.Should().Be("0x7FF6A1B2C3D4");
        firstFrame.Module.Should().Contain("Fallout4.exe");
        firstFrame.Function.Should().Be("BSResourceNiBinaryStream::Seek");
    }

    [Fact]
    public void AnalyzeCallStack_CountsModules()
    {
        // Act
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);

        // Assert
        analysis.ModuleCounts.Should().NotBeEmpty();
        analysis.ModuleCounts.Should().ContainKey("Fallout4.exe");
        analysis.ModuleCounts["Fallout4.exe"].Should().Be(3);
        analysis.ModuleCounts.Should().ContainKey("nvwgf2umx.dll");
        analysis.ModuleCounts.Should().ContainKey("d3d11.dll");
    }

    [Fact]
    public void AnalyzeCallStack_DetectsRecursion()
    {
        // Act
        var analysis = _sut.AnalyzeCallStack(RecursiveCallStack);

        // Assert
        analysis.RecursionDetected.Should().BeTrue();
        analysis.ProblemIndicators.Should().Contain(i => i.Contains("recursion"));
    }

    [Fact]
    public void AnalyzeCallStack_DetectsDirectRecursion()
    {
        // Arrange
        var directRecursion = @"
[0] 0x7FF6A1B2C3D4 Module.dll+0x1234 -> Function1
[1] 0x7FF6A1B2C3D5 Module.dll+0x1235 -> Function2
[2] 0x7FF6A1B2C3D6 Module.dll+0x1236 -> Function2
[3] 0x7FF6A1B2C3D7 Module.dll+0x1237 -> Function3
";

        // Act
        var analysis = _sut.AnalyzeCallStack(directRecursion);

        // Assert
        analysis.RecursionDetected.Should().BeTrue();
    }

    [Fact]
    public void AnalyzeCallStack_DeepStack_IdentifiesAsProblem()
    {
        // Act
        var analysis = _sut.AnalyzeCallStack(DeepCallStack);

        // Assert
        analysis.TotalFrames.Should().Be(150);
        analysis.ProblemIndicators.Should().Contain(i => i.Contains("deep call stack"));
    }

    [Fact]
    public void AnalyzeCallStack_FindsPatternClusters()
    {
        // Arrange
        var clusteredStack = @"
[0] 0x7FF6A1B2C3D4 ModuleA.dll+0x1234 -> FuncA1
[1] 0x7FF6A1B2C3D5 ModuleA.dll+0x1235 -> FuncA2
[2] 0x7FF6A1B2C3D6 ModuleA.dll+0x1236 -> FuncA3
[3] 0x7FF6A1B2C3D7 ModuleB.dll+0x1237 -> FuncB1
[4] 0x7FF6A1B2C3D8 ModuleC.dll+0x1238 -> FuncC1
[5] 0x7FF6A1B2C3D9 ModuleC.dll+0x1239 -> FuncC2
";

        // Act
        var analysis = _sut.AnalyzeCallStack(clusteredStack);

        // Assert
        analysis.PatternClusters.Should().NotBeEmpty();
        
        var moduleACluster = analysis.PatternClusters.FirstOrDefault(c => c.Module == "ModuleA.dll");
        moduleACluster.Should().NotBeNull();
        moduleACluster!.Size.Should().Be(3);
        moduleACluster.FrameIndices.Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public void AnalyzeCallStack_WithPatterns_FindsMatches()
    {
        // Arrange
        var patterns = new List<string> { "nvwgf2umx", "d3d11", "BSResource" };

        // Act
        var analysis = _sut.AnalyzeCallStack(SampleCallStack, patterns);

        // Assert
        analysis.PatternMatches.Should().NotBeEmpty();
        analysis.PatternMatches.Should().Contain(m => m.Pattern == "nvwgf2umx");
        analysis.PatternMatches.Should().Contain(m => m.Pattern == "d3d11");
        analysis.PatternMatches.Should().Contain(m => m.Pattern == "BSResource");
    }

    [Fact]
    public void AnalyzeCallStack_AnalyzesPatternDepths()
    {
        // Arrange
        var patterns = new List<string> { "Fallout4.exe" };

        // Act
        var analysis = _sut.AnalyzeCallStack(SampleCallStack, patterns);

        // Assert
        analysis.PatternDepths.Should().ContainKey("Fallout4.exe");
        analysis.PatternDepths["Fallout4.exe"].Should().Contain(new[] { 0, 1, 4 });
    }

    [Fact]
    public void AnalyzeCallStack_IdentifiesKnownProblemModules()
    {
        // Arrange - Stack with known problem module
        var problemStack = @"
[0] 0x7FF6A1B2C3D4 nvwgf2umx.dll+0x1234 -> NvFunc1
[1] 0x7FF6A1B2C3D5 nvwgf2umx.dll+0x1235 -> NvFunc2
[2] 0x7FF6A1B2C3D6 nvwgf2umx.dll+0x1236 -> NvFunc3
[3] 0x7FF6A1B2C3D7 nvwgf2umx.dll+0x1237 -> NvFunc4
";

        // Act
        var analysis = _sut.AnalyzeCallStack(problemStack);

        // Assert
        analysis.ProblemIndicators.Should().Contain(i => i.Contains("problematic module"));
        analysis.ProblemIndicators.Should().Contain(i => i.Contains("nvwgf2umx"));
    }

    [Fact]
    public void AnalyzeCallStack_DetectsDominantModule()
    {
        // Arrange - Stack dominated by one module
        var dominatedStack = @"
[0] 0x7FF6A1B2C3D4 Dominant.dll+0x1234 -> Func1
[1] 0x7FF6A1B2C3D5 Dominant.dll+0x1235 -> Func2
[2] 0x7FF6A1B2C3D6 Dominant.dll+0x1236 -> Func3
[3] 0x7FF6A1B2C3D7 Other.dll+0x1237 -> Func4
[4] 0x7FF6A1B2C3D8 Dominant.dll+0x1238 -> Func5
";

        // Act
        var analysis = _sut.AnalyzeCallStack(dominatedStack);

        // Assert
        analysis.ProblemIndicators.Should().Contain(i => i.Contains("dominated by"));
        analysis.ProblemIndicators.Should().Contain(i => i.Contains("Dominant.dll"));
    }

    [Fact]
    public void AnalyzeCallStack_CalculatesDepthStatistics()
    {
        // Act
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);

        // Assert
        analysis.DepthStatistics.Should().NotBeNull();
        analysis.DepthStatistics.MaxDepth.Should().Be(5);
        analysis.DepthStatistics.CriticalDepth.Should().BeGreaterThan(0);
        analysis.DepthStatistics.AverageModuleDepth.Should().NotBeEmpty();
    }

    [Fact]
    public void AnalyzeCallStack_NonStandardFormat_ParsesFallback()
    {
        // Arrange
        var nonStandardStack = @"
Frame 0: 0x12345678 in SomeModule.dll
Frame 1: 0x23456789 in AnotherModule.exe
Unknown format line with 0xABCDEF
";

        // Act
        var analysis = _sut.AnalyzeCallStack(nonStandardStack);

        // Assert
        analysis.IsValid.Should().BeTrue();
        analysis.Frames.Should().NotBeEmpty();
    }

    [Fact]
    public void FindOrderedSequence_EmptyFrames_ReturnsFalse()
    {
        // Arrange
        var frames = new List<StackFrame>();
        var sequence = new List<string> { "pattern1", "pattern2" };

        // Act
        var result = _sut.FindOrderedSequence(frames, sequence);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FindOrderedSequence_EmptyPattern_ReturnsFalse()
    {
        // Arrange
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);
        var sequence = new List<string>();

        // Act
        var result = _sut.FindOrderedSequence(analysis.Frames, sequence);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void FindOrderedSequence_ValidSequence_ReturnsTrue()
    {
        // Arrange
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);
        var sequence = new List<string> { "BSResource", "nvwgf2umx", "d3d11" };

        // Act
        var result = _sut.FindOrderedSequence(analysis.Frames, sequence);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void FindOrderedSequence_OutOfOrderSequence_ReturnsFalse()
    {
        // Arrange
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);
        var sequence = new List<string> { "d3d11", "nvwgf2umx", "BSResource" };

        // Act
        var result = _sut.FindOrderedSequence(analysis.Frames, sequence);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void AnalyzePatternStatistics_NoOccurrences_ReturnsZeroStats()
    {
        // Arrange
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);

        // Act
        var stats = _sut.AnalyzePatternStatistics(analysis.Frames, "NonExistentPattern");

        // Assert
        stats.Pattern.Should().Be("NonExistentPattern");
        stats.TotalOccurrences.Should().Be(0);
        stats.AverageDepth.Should().Be(0);
        stats.ClusteringCoefficient.Should().Be(0);
    }

    [Fact]
    public void AnalyzePatternStatistics_SingleOccurrence_CalculatesCorrectly()
    {
        // Arrange
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);

        // Act
        var stats = _sut.AnalyzePatternStatistics(analysis.Frames, "nvwgf2umx");

        // Assert
        stats.Pattern.Should().Be("nvwgf2umx");
        stats.TotalOccurrences.Should().Be(1);
        stats.FirstOccurrenceDepth.Should().Be(2);
        stats.LastOccurrenceDepth.Should().Be(2);
        stats.AverageDepth.Should().Be(2);
        stats.ClusteringCoefficient.Should().Be(0); // No clustering for single occurrence
    }

    [Fact]
    public void AnalyzePatternStatistics_MultipleOccurrences_CalculatesCorrectly()
    {
        // Arrange
        var analysis = _sut.AnalyzeCallStack(SampleCallStack);

        // Act
        var stats = _sut.AnalyzePatternStatistics(analysis.Frames, "Fallout4");

        // Assert
        stats.Pattern.Should().Be("Fallout4");
        stats.TotalOccurrences.Should().Be(3);
        stats.FirstOccurrenceDepth.Should().Be(0);
        stats.LastOccurrenceDepth.Should().Be(4);
        stats.AverageDepth.Should().BeApproximately(1.67, 0.1); // (0+1+4)/3
        stats.ClusteringCoefficient.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnalyzePatternStatistics_ClusteredPattern_HighClusteringCoefficient()
    {
        // Arrange - Clustered occurrences
        var clusteredStack = @"
[0] 0x7FF6A1B2C3D4 Pattern.dll+0x1234 -> Func1
[1] 0x7FF6A1B2C3D5 Pattern.dll+0x1235 -> Func2
[2] 0x7FF6A1B2C3D6 Pattern.dll+0x1236 -> Func3
[3] 0x7FF6A1B2C3D7 Other.dll+0x1237 -> Func4
[4] 0x7FF6A1B2C3D8 Other.dll+0x1238 -> Func5
";

        var analysis = _sut.AnalyzeCallStack(clusteredStack);

        // Act
        var stats = _sut.AnalyzePatternStatistics(analysis.Frames, "Pattern");

        // Assert
        stats.ClusteringCoefficient.Should().BeGreaterThan(0.3); // High clustering
    }

    [Fact]
    public void AnalyzePatternStatistics_SpreadPattern_LowClusteringCoefficient()
    {
        // Arrange - Spread occurrences
        var spreadStack = @"
[0] 0x7FF6A1B2C3D4 Pattern.dll+0x1234 -> Func1
[1] 0x7FF6A1B2C3D5 Other.dll+0x1235 -> Func2
[2] 0x7FF6A1B2C3D6 Other.dll+0x1236 -> Func3
[3] 0x7FF6A1B2C3D7 Pattern.dll+0x1237 -> Func4
[4] 0x7FF6A1B2C3D8 Other.dll+0x1238 -> Func5
[5] 0x7FF6A1B2C3D9 Other.dll+0x1239 -> Func6
[6] 0x7FF6A1B2C3DA Pattern.dll+0x123A -> Func7
";

        var analysis = _sut.AnalyzeCallStack(spreadStack);

        // Act
        var stats = _sut.AnalyzePatternStatistics(analysis.Frames, "Pattern");

        // Assert
        stats.ClusteringCoefficient.Should().BeLessThan(0.3); // Low clustering
    }

    [Fact]
    public void AnalyzeCallStack_ComplexRealWorldStack_HandlesCorrectly()
    {
        // Arrange - More complex real-world-like stack
        var complexStack = @"
[0] 0x00007FF6A1B2C3D4 Fallout4.exe+0x1B2C3D4
[1] 0x00007FF6A1B2C3D5 Fallout4.exe+0x1B2C3D5 -> BSResourceNiBinaryStream::Read
[2] 0x00007FFAB1234567 nvwgf2umx.dll+0x234567
[3] 0x00007FFAB1234568 nvwgf2umx.dll+0x234568 -> NvDriver::RenderFrame
[4] 0x00007FFAB1234569 d3d11.dll+0x234569 -> D3D11CreateDevice
[5] 0x00007FF6A1B2C3D6 Fallout4.exe+0x1B2C3D6
[6] 0x00007FFCD1234567 kernelbase.dll+0x234567 -> RaiseException
";

        // Act
        var analysis = _sut.AnalyzeCallStack(complexStack);

        // Assert
        analysis.IsValid.Should().BeTrue();
        analysis.TotalFrames.Should().Be(7);
        analysis.ModuleCounts.Should().HaveCountGreaterThanOrEqualTo(4);
        analysis.ProblemIndicators.Should().NotBeEmpty(); // Should identify kernelbase.dll
    }
}
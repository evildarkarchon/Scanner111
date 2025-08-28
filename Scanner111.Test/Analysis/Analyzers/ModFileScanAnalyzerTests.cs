using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.Services;
using Xunit;

namespace Scanner111.Test.Analysis.Analyzers;

[Trait("Category", "Unit")]
[Trait("Performance", "Fast")]
[Trait("Component", "Analyzer")]
public sealed class ModFileScanAnalyzerTests
{
    private readonly ILogger<ModFileScanAnalyzer> _logger;
    private readonly IModFileScanner _modFileScanner;
    private readonly ICrashGenChecker _crashGenChecker;
    private readonly IXsePluginChecker _xsePluginChecker;
    private readonly IAsyncYamlSettingsCore _yamlCore;
    private readonly ModFileScanAnalyzer _analyzer;

    public ModFileScanAnalyzerTests()
    {
        _logger = Substitute.For<ILogger<ModFileScanAnalyzer>>();
        _modFileScanner = Substitute.For<IModFileScanner>();
        _crashGenChecker = Substitute.For<ICrashGenChecker>();
        _xsePluginChecker = Substitute.For<IXsePluginChecker>();
        _yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        
        _analyzer = new ModFileScanAnalyzer(_logger, _modFileScanner, _crashGenChecker, _xsePluginChecker);
    }

    [Fact]
    public void Properties_HaveExpectedValues()
    {
        // Assert
        _analyzer.Name.Should().Be("ModFileScan");
        _analyzer.DisplayName.Should().Be("Mod File Scanning Analysis");
        _analyzer.Priority.Should().Be(15);
        _analyzer.Timeout.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    public async Task CanAnalyzeAsync_WithValidContext_ReturnsTrue()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);

        // Act
        var result = await _analyzer.CanAnalyzeAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanAnalyzeAsync_WithNullContext_ReturnsFalse()
    {
        // Act
        var result = await _analyzer.CanAnalyzeAsync(null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task PerformAnalysisAsync_WithAllServicesReturningEmptyResults_ReturnsSuccessWithInfoFragment()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _modFileScanner.ScanModsArchivedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _modFileScanner.CheckLogErrorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _crashGenChecker.CheckCrashGenSettingsAsync(Arg.Any<CancellationToken>())
            .Returns("");
        _xsePluginChecker.CheckXsePluginsAsync(Arg.Any<CancellationToken>())
            .Returns("");

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment.Title.Should().Contain("Mod File Scanning Analysis");
        result.Severity.Should().Be(AnalysisSeverity.Info);
        result.Metadata.Should().ContainKey("TotalIssues");
    }

    [Fact]
    public async Task PerformAnalysisAsync_WithUnpackedModIssues_ReturnsSuccessWithWarningFragment()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("❌ CAUTION : Bad texture dimensions found\n  - texture.dds (511x255)");
        _modFileScanner.ScanModsArchivedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _modFileScanner.CheckLogErrorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _crashGenChecker.CheckCrashGenSettingsAsync(Arg.Any<CancellationToken>())
            .Returns("");
        _xsePluginChecker.CheckXsePluginsAsync(Arg.Any<CancellationToken>())
            .Returns("");

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        result.Fragment.Children.Should().NotBeEmpty();
        result.Severity.Should().BeOneOf(AnalysisSeverity.Warning, AnalysisSeverity.Error);
    }

    [Fact]
    public async Task PerformAnalysisAsync_WithCrashGenIssues_IncludesCrashGenFragment()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _modFileScanner.ScanModsArchivedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _modFileScanner.CheckLogErrorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _crashGenChecker.CheckCrashGenSettingsAsync(Arg.Any<CancellationToken>())
            .Returns("❌ CAUTION : Achievements mod detected but setting is wrong");
        _xsePluginChecker.CheckXsePluginsAsync(Arg.Any<CancellationToken>())
            .Returns("");

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Children.Should().Contain(f => f.Title.Contains("Crash Generator"));
    }

    [Fact]
    public async Task PerformAnalysisAsync_WithXsePluginIssues_IncludesXseFragment()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _modFileScanner.ScanModsArchivedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _modFileScanner.CheckLogErrorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _crashGenChecker.CheckCrashGenSettingsAsync(Arg.Any<CancellationToken>())
            .Returns("");
        _xsePluginChecker.CheckXsePluginsAsync(Arg.Any<CancellationToken>())
            .Returns("❌ CAUTION: Wrong Address Library version installed");

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Children.Should().Contain(f => f.Title.Contains("Script Extender"));
    }

    [Fact]
    public async Task PerformAnalysisAsync_WithMultipleIssueTypes_CreatesMultipleFragments()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("⚠️ Texture issues found");
        _modFileScanner.ScanModsArchivedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("❌ BA2 format issues found");
        _modFileScanner.CheckLogErrorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("❓ Log errors detected");
        _crashGenChecker.CheckCrashGenSettingsAsync(Arg.Any<CancellationToken>())
            .Returns("✔️ Settings are correct");
        _xsePluginChecker.CheckXsePluginsAsync(Arg.Any<CancellationToken>())
            .Returns("✔️ Address Library is correct");

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Children.Should().HaveCountGreaterThan(2);
        result.Fragment.Children.Should().Contain(f => f.Title.Contains("Unpacked"));
        result.Fragment.Children.Should().Contain(f => f.Title.Contains("Archived"));
        result.Fragment.Children.Should().Contain(f => f.Title.Contains("Log"));
    }

    [Fact]
    public async Task PerformAnalysisAsync_StoresScanResultsInContext()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("Test report");
        _modFileScanner.ScanModsArchivedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _modFileScanner.CheckLogErrorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("");
        _crashGenChecker.CheckCrashGenSettingsAsync(Arg.Any<CancellationToken>())
            .Returns("");
        _xsePluginChecker.CheckXsePluginsAsync(Arg.Any<CancellationToken>())
            .Returns("");

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        context.TryGetSharedData<object>("ModFileScanResults", out var scanResults).Should().BeTrue();
        scanResults.Should().NotBeNull();
    }

    [Fact]
    public async Task PerformAnalysisAsync_WithServiceException_ReturnsFailureResult()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<string>(new InvalidOperationException("Service failed")));

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Fragment.Content.Should().Contain("failed");
    }

    [Fact]
    public async Task PerformAnalysisAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        // The analyzer checks cancellation early via cancellationToken.ThrowIfCancellationRequested()
        // so it should throw immediately without needing to mock the dependencies
        var act = () => _analyzer.AnalyzeAsync(context, cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PerformAnalysisAsync_SetsAppropriateMetadata()
    {
        // Arrange
        var context = new AnalysisContext("test.log", _yamlCore);
        
        _modFileScanner.ScanModsUnpackedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("No issues");
        _modFileScanner.ScanModsArchivedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("No issues");
        _modFileScanner.CheckLogErrorsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("No issues");
        _crashGenChecker.CheckCrashGenSettingsAsync(Arg.Any<CancellationToken>())
            .Returns("Settings OK");
        _xsePluginChecker.CheckXsePluginsAsync(Arg.Any<CancellationToken>())
            .Returns("Plugins OK");

        // Act
        var result = await _analyzer.AnalyzeAsync(context, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().ContainKey("UnpackedScanned");
        result.Metadata.Should().ContainKey("ArchivedScanned");
        result.Metadata.Should().ContainKey("LogsScanned");
        result.Metadata.Should().ContainKey("CrashGenChecked");
        result.Metadata.Should().ContainKey("XsePluginsChecked");
        result.Metadata.Should().ContainKey("TotalIssues");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModFileScanAnalyzer(null!, _modFileScanner, _crashGenChecker, _xsePluginChecker);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullModFileScanner_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModFileScanAnalyzer(_logger, null!, _crashGenChecker, _xsePluginChecker);
        act.Should().Throw<ArgumentNullException>().WithParameterName("modFileScanner");
    }

    [Fact]
    public void Constructor_WithNullCrashGenChecker_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModFileScanAnalyzer(_logger, _modFileScanner, null!, _xsePluginChecker);
        act.Should().Throw<ArgumentNullException>().WithParameterName("crashGenChecker");
    }

    [Fact]
    public void Constructor_WithNullXsePluginChecker_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new ModFileScanAnalyzer(_logger, _modFileScanner, _crashGenChecker, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("xsePluginChecker");
    }
}
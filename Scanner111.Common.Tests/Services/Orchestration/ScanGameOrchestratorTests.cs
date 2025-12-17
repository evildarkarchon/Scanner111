using FluentAssertions;
using Moq;
using Scanner111.Common.Models.GameIntegrity;
using Scanner111.Common.Models.GamePath;
using Scanner111.Common.Models.Reporting;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.FileIO;
using Scanner111.Common.Services.GameIntegrity;
using Scanner111.Common.Services.Orchestration;
using Scanner111.Common.Services.Reporting;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.Orchestration;

public class ScanGameOrchestratorTests
{
    private readonly Mock<IUnpackedModsScanner> _unpackedScanner;
    private readonly Mock<IBA2Scanner> _ba2Scanner;
    private readonly Mock<IIniValidator> _iniValidator;
    private readonly Mock<ITomlValidator> _tomlValidator;
    private readonly Mock<IXseChecker> _xseChecker;
    private readonly Mock<IGameIntegrityChecker> _integrityChecker;
    private readonly Mock<IScanGameReportBuilder> _reportBuilder;
    private readonly Mock<IFileIOService> _fileIO;
    private readonly ScanGameOrchestrator _orchestrator;

    public ScanGameOrchestratorTests()
    {
        _unpackedScanner = new Mock<IUnpackedModsScanner>();
        _ba2Scanner = new Mock<IBA2Scanner>();
        _iniValidator = new Mock<IIniValidator>();
        _tomlValidator = new Mock<ITomlValidator>();
        _xseChecker = new Mock<IXseChecker>();
        _integrityChecker = new Mock<IGameIntegrityChecker>();
        _reportBuilder = new Mock<IScanGameReportBuilder>();
        _fileIO = new Mock<IFileIOService>();

        _orchestrator = new ScanGameOrchestrator(
            _unpackedScanner.Object,
            _ba2Scanner.Object,
            _iniValidator.Object,
            _tomlValidator.Object,
            _xseChecker.Object,
            _integrityChecker.Object,
            _reportBuilder.Object,
            _fileIO.Object);
    }

    private ScanGameConfiguration CreateFullConfiguration()
    {
        return new ScanGameConfiguration
        {
            GameType = GameType.Fallout4,
            XseAcronym = "F4SE",
            GameDisplayName = "Fallout 4",
            GameRootPath = @"C:\Games\Fallout4",
            ModPath = @"C:\Games\Fallout4\Data",
            DocumentsGamePath = @"C:\Users\Test\Documents\My Games\Fallout4",
            XsePluginsPath = @"C:\Games\Fallout4\Data\F4SE\Plugins",
            CrashGenName = "Buffout4",
            ScanUnpacked = true,
            ScanArchives = true,
            ValidateIni = true,
            ValidateToml = true,
            CheckXse = true,
            CheckGameIntegrity = true,
            XseConfiguration = new XseConfiguration { Acronym = "F4SE" },
            GameIntegrityConfiguration = new GameIntegrityConfiguration { GameType = GameType.Fallout4 }
        };
    }

    private void SetupAllMocks()
    {
        _unpackedScanner.Setup(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnpackedScanResult());

        _ba2Scanner.Setup(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BA2ScanResult());

        _iniValidator.Setup(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IniScanResult());

        _tomlValidator.Setup(x => x.ValidateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TomlScanResult());

        _xseChecker.Setup(x => x.CheckIntegrityAsync(
            It.IsAny<XseConfiguration>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new XseScanResult());

        _integrityChecker.Setup(x => x.CheckIntegrityAsync(
            It.IsAny<GameIntegrityConfiguration>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GameIntegrityResult());

        _reportBuilder.Setup(x => x.BuildCombinedReport(It.IsAny<ScanGameReport>()))
            .Returns(new ReportFragment { Lines = new[] { "# Test Report" } });
    }

    [Fact]
    public async Task ScanAsync_WithAllScannersEnabled_RunsAllScanners()
    {
        // Arrange
        var config = CreateFullConfiguration();
        SetupAllMocks();

        // Act
        var result = await _orchestrator.ScanAsync(config);

        // Assert
        result.CompletedSuccessfully.Should().BeTrue();
        result.Errors.Should().BeEmpty();
        result.Report.Should().NotBeNull();

        _unpackedScanner.Verify(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _ba2Scanner.Verify(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _iniValidator.Verify(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _tomlValidator.Verify(x => x.ValidateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _xseChecker.Verify(x => x.CheckIntegrityAsync(
            It.IsAny<XseConfiguration>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _integrityChecker.Verify(x => x.CheckIntegrityAsync(
            It.IsAny<GameIntegrityConfiguration>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScanAsync_WithAllScannersEnabled_ReturnsCompleteReport()
    {
        // Arrange
        var config = CreateFullConfiguration();
        SetupAllMocks();

        // Act
        var result = await _orchestrator.ScanAsync(config);

        // Assert
        result.Report.XseAcronym.Should().Be("F4SE");
        result.Report.GameName.Should().Be("Fallout 4");
        result.Report.UnpackedResult.Should().NotBeNull();
        result.Report.ArchivedResult.Should().NotBeNull();
        result.Report.IniResult.Should().NotBeNull();
        result.Report.TomlResult.Should().NotBeNull();
        result.Report.XseResult.Should().NotBeNull();
        result.Report.IntegrityResult.Should().NotBeNull();
        result.GeneratedReport.Should().NotBeNull();
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task ScanAsync_WithOnlyUnpackedEnabled_SkipsOtherScanners()
    {
        // Arrange
        var config = new ScanGameConfiguration
        {
            GameType = GameType.Fallout4,
            XseAcronym = "F4SE",
            GameDisplayName = "Fallout 4",
            ModPath = @"C:\Games\Fallout4\Data",
            ScanUnpacked = true,
            ScanArchives = false,
            ValidateIni = false,
            ValidateToml = false,
            CheckXse = false,
            CheckGameIntegrity = false
        };
        SetupAllMocks();

        // Act
        var result = await _orchestrator.ScanAsync(config);

        // Assert
        result.CompletedSuccessfully.Should().BeTrue();
        result.Report.UnpackedResult.Should().NotBeNull();
        result.Report.ArchivedResult.Should().BeNull();
        result.Report.IniResult.Should().BeNull();
        result.Report.TomlResult.Should().BeNull();
        result.Report.XseResult.Should().BeNull();
        result.Report.IntegrityResult.Should().BeNull();

        _unpackedScanner.Verify(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _ba2Scanner.Verify(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanAsync_WithMissingPaths_SkipsAffectedScanners()
    {
        // Arrange
        var config = new ScanGameConfiguration
        {
            GameType = GameType.Fallout4,
            XseAcronym = "F4SE",
            GameDisplayName = "Fallout 4",
            // ModPath is null - should skip unpacked and BA2 scanners
            GameRootPath = null, // Should skip INI validator
            XsePluginsPath = null, // Should skip TOML validator
            ScanUnpacked = true,
            ScanArchives = true,
            ValidateIni = true,
            ValidateToml = true,
            CheckXse = false,
            CheckGameIntegrity = false
        };
        SetupAllMocks();

        // Act
        var result = await _orchestrator.ScanAsync(config);

        // Assert
        result.CompletedSuccessfully.Should().BeTrue();
        result.Report.UnpackedResult.Should().BeNull();
        result.Report.ArchivedResult.Should().BeNull();
        result.Report.IniResult.Should().BeNull();
        result.Report.TomlResult.Should().BeNull();

        _unpackedScanner.Verify(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _ba2Scanner.Verify(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _iniValidator.Verify(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);

        _tomlValidator.Verify(x => x.ValidateAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ScanAsync_WhenScannerThrows_CapturesErrorAndContinues()
    {
        // Arrange
        var config = CreateFullConfiguration();
        SetupAllMocks();

        // Make unpacked scanner throw
        _unpackedScanner.Setup(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error"));

        // Act
        var result = await _orchestrator.ScanAsync(config);

        // Assert
        result.CompletedSuccessfully.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ScannerName == "Unpacked");
        result.Errors[0].ErrorMessage.Should().Contain("Disk error");
        result.Report.UnpackedResult.Should().BeNull();
        // Other scanners should still complete
        result.Report.ArchivedResult.Should().NotBeNull();
        result.Report.IniResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ScanAsync_WhenMultipleScannersThrow_CapturesAllErrors()
    {
        // Arrange
        var config = CreateFullConfiguration();
        SetupAllMocks();

        // Make multiple scanners throw
        _unpackedScanner.Setup(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Disk error 1"));

        _ba2Scanner.Setup(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("BA2 error"));

        // Act
        var result = await _orchestrator.ScanAsync(config);

        // Assert
        result.CompletedSuccessfully.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(e => e.ScannerName == "Unpacked");
        result.Errors.Should().Contain(e => e.ScannerName == "Archives");
        // Other scanners should still complete
        result.Report.IniResult.Should().NotBeNull();
        result.Report.TomlResult.Should().NotBeNull();
    }

    [Fact]
    public async Task ScanAsync_WhenCancelled_ReturnsPartialResults()
    {
        // Arrange
        var config = CreateFullConfiguration();
        var cts = new CancellationTokenSource();
        SetupAllMocks();

        // Make unpacked scanner throw cancellation
        _unpackedScanner.Setup(x => x.ScanAsync(
            It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()))
            .Returns(async (string _, IReadOnlyDictionary<string, string>? _, bool _, CancellationToken ct) =>
            {
                await Task.Delay(100, ct);
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return new UnpackedScanResult();
            });

        // Act
        var result = await _orchestrator.ScanAsync(config, cancellationToken: cts.Token);

        // Assert
        // At least one error should be captured
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("cancelled"));
    }

    [Fact]
    public async Task ScanAsync_WithProgress_ReportsProgressCorrectly()
    {
        // Arrange
        var config = CreateFullConfiguration();
        SetupAllMocks();

        var progressReports = new List<ScanGameProgress>();
        var progress = new Progress<ScanGameProgress>(p => progressReports.Add(p));

        // Act
        var result = await _orchestrator.ScanAsync(config, progress);
        await Task.Delay(100); // Allow progress reports to complete

        // Assert
        progressReports.Should().NotBeEmpty();
        // Progress should increase over time and reach 100% at completion
        progressReports.Should().Contain(p => p.OverallPercentComplete == 100);
        // Should have progress for starting and completion at minimum
        progressReports.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ScanAsync_WithNoScannersEnabled_ReturnsEmptyReport()
    {
        // Arrange
        var config = new ScanGameConfiguration
        {
            GameType = GameType.Fallout4,
            XseAcronym = "F4SE",
            GameDisplayName = "Fallout 4",
            ScanUnpacked = false,
            ScanArchives = false,
            ValidateIni = false,
            ValidateToml = false,
            CheckXse = false,
            CheckGameIntegrity = false
        };
        SetupAllMocks();

        // Act
        var result = await _orchestrator.ScanAsync(config);

        // Assert
        result.CompletedSuccessfully.Should().BeTrue();
        result.Report.HasAnyResults.Should().BeFalse();
    }

    [Fact]
    public async Task ScanAndWriteReportAsync_WritesReportToFile()
    {
        // Arrange
        var config = CreateFullConfiguration();
        SetupAllMocks();
        var reportPath = @"C:\temp\report.md";

        // Act
        var result = await _orchestrator.ScanAndWriteReportAsync(config, reportPath);

        // Assert
        result.Should().NotBeNull();
        _fileIO.Verify(x => x.WriteFileAsync(
            reportPath,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ScanAndWriteReportAsync_WithEmptyReport_DoesNotWriteFile()
    {
        // Arrange
        var config = CreateFullConfiguration();
        SetupAllMocks();

        // Return empty report
        _reportBuilder.Setup(x => x.BuildCombinedReport(It.IsAny<ScanGameReport>()))
            .Returns(new ReportFragment()); // Empty, HasContent = false

        var reportPath = @"C:\temp\report.md";

        // Act
        var result = await _orchestrator.ScanAndWriteReportAsync(config, reportPath);

        // Assert
        result.Should().NotBeNull();
        _fileIO.Verify(x => x.WriteFileAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Constructor_WithNullDependency_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ScanGameOrchestrator(
            null!,
            _ba2Scanner.Object,
            _iniValidator.Object,
            _tomlValidator.Object,
            _xseChecker.Object,
            _integrityChecker.Object,
            _reportBuilder.Object,
            _fileIO.Object));

        Assert.Throws<ArgumentNullException>(() => new ScanGameOrchestrator(
            _unpackedScanner.Object,
            null!,
            _iniValidator.Object,
            _tomlValidator.Object,
            _xseChecker.Object,
            _integrityChecker.Object,
            _reportBuilder.Object,
            _fileIO.Object));
    }

    [Fact]
    public async Task ScanAsync_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _orchestrator.ScanAsync(null!));
    }
}

public class ScanGameConfigurationTests
{
    [Fact]
    public void CreateForGame_Fallout4_SetsCorrectDefaults()
    {
        // Arrange & Act
        var config = ScanGameConfiguration.CreateForGame(
            GameType.Fallout4,
            @"C:\Games\Fallout4",
            @"C:\Users\Test\Documents\My Games\Fallout4");

        // Assert
        config.GameType.Should().Be(GameType.Fallout4);
        config.XseAcronym.Should().Be("F4SE");
        config.GameDisplayName.Should().Be("Fallout 4");
        config.ModPath.Should().Be(@"C:\Games\Fallout4\Data");
        config.XsePluginsPath.Should().Be(@"C:\Games\Fallout4\Data\F4SE\Plugins");
        config.CrashGenName.Should().Be("Buffout4");
        config.XseScriptFiles.Should().ContainKey("f4se.dll");
        config.XseScriptFolders.Should().ContainKey("f4se");
    }

    [Fact]
    public void CreateForGame_SkyrimSE_SetsCorrectDefaults()
    {
        // Arrange & Act
        var config = ScanGameConfiguration.CreateForGame(
            GameType.SkyrimSE,
            @"C:\Games\SkyrimSE",
            @"C:\Users\Test\Documents\My Games\Skyrim Special Edition");

        // Assert
        config.GameType.Should().Be(GameType.SkyrimSE);
        config.XseAcronym.Should().Be("SKSE64");
        config.GameDisplayName.Should().Be("Skyrim Special Edition");
        config.CrashGenName.Should().Be("CrashLogger");
        config.XseScriptFiles.Should().ContainKey("skse.dll");
        config.XseScriptFolders.Should().ContainKey("skse");
    }

    [Fact]
    public void CreateForGame_WithCustomModPath_UsesCustomPath()
    {
        // Arrange & Act
        var config = ScanGameConfiguration.CreateForGame(
            GameType.Fallout4,
            @"C:\Games\Fallout4",
            @"C:\Users\Test\Documents\My Games\Fallout4",
            @"D:\Mods\Fallout4");

        // Assert
        config.ModPath.Should().Be(@"D:\Mods\Fallout4");
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new ScanGameConfiguration();

        // Assert
        config.ScanUnpacked.Should().BeTrue();
        config.ScanArchives.Should().BeTrue();
        config.ValidateIni.Should().BeTrue();
        config.ValidateToml.Should().BeTrue();
        config.CheckXse.Should().BeTrue();
        config.CheckGameIntegrity.Should().BeTrue();
        config.AnalyzeDdsTextures.Should().BeTrue();
    }
}

public class ScanGameProgressTests
{
    [Fact]
    public void Starting_ReturnsCorrectProgress()
    {
        // Act
        var progress = ScanGameProgress.Starting(6);

        // Assert
        progress.OverallPercentComplete.Should().Be(0);
        progress.ScannersCompleted.Should().Be(0);
        progress.TotalScanners.Should().Be(6);
        progress.CurrentOperation.Should().Be("Starting scan...");
    }

    [Fact]
    public void Completed_ReturnsCorrectProgress()
    {
        // Act
        var progress = ScanGameProgress.Completed(6);

        // Assert
        progress.OverallPercentComplete.Should().Be(100);
        progress.ScannersCompleted.Should().Be(6);
        progress.TotalScanners.Should().Be(6);
        progress.CurrentOperation.Should().Be("Scan complete");
    }
}

public class ScanGameResultTests
{
    [Fact]
    public void CompletedSuccessfully_WithNoErrors_ReturnsTrue()
    {
        // Arrange
        var result = new ScanGameResult
        {
            Errors = Array.Empty<ScannerError>()
        };

        // Assert
        result.CompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void CompletedSuccessfully_WithErrors_ReturnsFalse()
    {
        // Arrange
        var result = new ScanGameResult
        {
            Errors = new[] { new ScannerError("Test", "Error") }
        };

        // Assert
        result.CompletedSuccessfully.Should().BeFalse();
    }

    [Fact]
    public void HasAnyIssues_DelegatesToReport()
    {
        // Arrange
        var result = new ScanGameResult
        {
            Report = new ScanGameReport
            {
                UnpackedResult = new UnpackedScanResult
                {
                    CleanupIssues = new[] { new CleanupIssue("path", "rel", CleanupItemType.ReadmeFile) }
                }
            }
        };

        // Assert
        result.HasAnyIssues.Should().BeTrue();
    }
}

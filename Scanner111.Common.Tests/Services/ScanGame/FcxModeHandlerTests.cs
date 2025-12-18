using FluentAssertions;
using Moq;
using Scanner111.Common.Models.ScanGame;
using Scanner111.Common.Services.ScanGame;

namespace Scanner111.Common.Tests.Services.ScanGame;

/// <summary>
/// Tests for FcxModeHandler.
/// </summary>
public class FcxModeHandlerTests
{
    private readonly Mock<IIniValidator> _mockIniValidator;
    private readonly FcxModeHandler _handler;

    public FcxModeHandlerTests()
    {
        _mockIniValidator = new Mock<IIniValidator>();
        _handler = new FcxModeHandler(_mockIniValidator.Object);
    }

    [Fact]
    public async Task CheckAsync_WhenDisabled_ReturnsDisabledResult()
    {
        // Act
        var result = await _handler.CheckAsync(
            gameRootPath: null,
            gameName: "Fallout4",
            fcxEnabled: false);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.HasIssues.Should().BeFalse();
        result.ReportFragment.Should().NotBeNull();
        result.ReportFragment!.HasContent.Should().BeTrue();
    }

    [Fact]
    public async Task CheckAsync_WhenDisabled_DoesNotCallIniValidator()
    {
        // Act
        await _handler.CheckAsync(
            gameRootPath: @"C:\Games\Fallout4",
            gameName: "Fallout4",
            fcxEnabled: false);

        // Assert
        _mockIniValidator.Verify(
            x => x.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CheckAsync_WhenEnabledWithNullPath_ReturnsEnabledResultWithoutScan()
    {
        // Act
        var result = await _handler.CheckAsync(
            gameRootPath: null,
            gameName: "Fallout4",
            fcxEnabled: true);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.HasIssues.Should().BeFalse();
        _mockIniValidator.Verify(
            x => x.ScanAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void FromScanResults_WhenDisabled_ReturnsDisabledResult()
    {
        // Arrange
        var iniResult = new IniScanResult
        {
            ConfigIssues = new[]
            {
                new ConfigIssue("path", "file.ini", "Section", "Key", "bad", "good", "desc")
            }
        };

        // Act
        var result = _handler.FromScanResults(iniResult, null, fcxEnabled: false);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.HasIssues.Should().BeFalse();
        result.IniConfigIssues.Should().BeEmpty();
    }

    [Fact]
    public void FromScanResults_WithIniIssues_IncludesThemInResult()
    {
        // Arrange
        var iniResult = new IniScanResult
        {
            ConfigIssues = new[]
            {
                new ConfigIssue("path1", "file1.ini", "Section1", "Key1", "value1", "recommended1", "desc1"),
                new ConfigIssue("path2", "file2.ini", "Section2", "Key2", "value2", "recommended2", "desc2")
            }
        };

        // Act
        var result = _handler.FromScanResults(iniResult, null, fcxEnabled: true);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.HasIssues.Should().BeTrue();
        result.IniConfigIssues.Should().HaveCount(2);
        result.TotalIssueCount.Should().Be(2);
    }

    [Fact]
    public void FromScanResults_WithConsoleCommandIssues_IncludesThemInResult()
    {
        // Arrange
        var iniResult = new IniScanResult
        {
            ConsoleCommandIssues = new[]
            {
                new ConsoleCommandIssue("path", "fallout4.ini", "bat autoexec.txt")
            }
        };

        // Act
        var result = _handler.FromScanResults(iniResult, null, fcxEnabled: true);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.ConsoleCommandIssues.Should().HaveCount(1);
        result.ConsoleCommandIssues[0].CommandValue.Should().Be("bat autoexec.txt");
    }

    [Fact]
    public void FromScanResults_WithVSyncIssues_IncludesThemInResult()
    {
        // Arrange
        var iniResult = new IniScanResult
        {
            VSyncIssues = new[]
            {
                new VSyncIssue("path", "enblocal.ini", "ENGINE", "ForceVSync", true)
            }
        };

        // Act
        var result = _handler.FromScanResults(iniResult, null, fcxEnabled: true);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.VSyncIssues.Should().HaveCount(1);
        result.VSyncIssues[0].IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void FromScanResults_WithTomlIssues_IncludesThemInResult()
    {
        // Arrange
        var tomlResult = new TomlScanResult
        {
            ConfigIssues = new[]
            {
                new ConfigIssue("path", "Buffout4.toml", "Patches", "Achievements", "true", "false", "desc")
            }
        };

        // Act
        var result = _handler.FromScanResults(null, tomlResult, fcxEnabled: true);

        // Assert
        result.HasIssues.Should().BeTrue();
        result.TomlConfigIssues.Should().HaveCount(1);
    }

    [Fact]
    public void FromScanResults_WithMixedIssues_CountsAllIssues()
    {
        // Arrange
        var iniResult = new IniScanResult
        {
            ConfigIssues = new[]
            {
                new ConfigIssue("path", "file.ini", "Section", "Key", "bad", "good", "desc")
            },
            ConsoleCommandIssues = new[]
            {
                new ConsoleCommandIssue("path", "fallout4.ini", "command")
            },
            VSyncIssues = new[]
            {
                new VSyncIssue("path", "enblocal.ini", "ENGINE", "ForceVSync", true)
            }
        };

        var tomlResult = new TomlScanResult
        {
            ConfigIssues = new[]
            {
                new ConfigIssue("path", "Buffout4.toml", "Patches", "Setting", "val", "rec", "desc")
            }
        };

        // Act
        var result = _handler.FromScanResults(iniResult, tomlResult, fcxEnabled: true);

        // Assert
        result.TotalIssueCount.Should().Be(4);
        result.IniConfigIssues.Should().HaveCount(1);
        result.ConsoleCommandIssues.Should().HaveCount(1);
        result.VSyncIssues.Should().HaveCount(1);
        result.TomlConfigIssues.Should().HaveCount(1);
    }

    [Fact]
    public void FromScanResults_WithNoIssues_ReturnsEnabledWithNoIssues()
    {
        // Arrange
        var iniResult = new IniScanResult();

        // Act
        var result = _handler.FromScanResults(iniResult, null, fcxEnabled: true);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.HasIssues.Should().BeFalse();
        result.TotalIssueCount.Should().Be(0);
    }

    [Fact]
    public void CreateReportFragment_WhenDisabled_ContainsDisabledNotice()
    {
        // Arrange
        var result = FcxModeResult.Disabled;

        // Act
        var fragment = _handler.CreateReportFragment(result);

        // Assert
        fragment.HasContent.Should().BeTrue();
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("DISABLED");
        content.Should().Contain("Enable FCX Mode");
    }

    [Fact]
    public void CreateReportFragment_WhenEnabledWithNoIssues_ContainsNoIssuesMessage()
    {
        // Arrange
        var result = new FcxModeResult { IsEnabled = true };

        // Act
        var fragment = _handler.CreateReportFragment(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("FCX MODE IS ENABLED");
        content.Should().Contain("No configuration issues detected");
    }

    [Fact]
    public void CreateReportFragment_WithConfigIssues_FormatsCurrentVsRecommended()
    {
        // Arrange
        var result = new FcxModeResult
        {
            IsEnabled = true,
            IniConfigIssues = new[]
            {
                new ConfigIssue(
                    "C:\\path\\epo.ini",
                    "epo.ini",
                    "Particles",
                    "iMaxDesired",
                    "10000",
                    "5000",
                    "High particle count causes crashes",
                    ConfigIssueSeverity.Warning)
            }
        };

        // Act
        var fragment = _handler.CreateReportFragment(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("epo.ini");
        content.Should().Contain("iMaxDesired");
        content.Should().Contain("Current: `10000`");
        content.Should().Contain("Recommended: `5000`");
        content.Should().Contain("[!]"); // Warning severity icon
    }

    [Fact]
    public void CreateReportFragment_WithDifferentSeverities_UsesDifferentIcons()
    {
        // Arrange
        var result = new FcxModeResult
        {
            IsEnabled = true,
            IniConfigIssues = new[]
            {
                new ConfigIssue("p", "f1.ini", "S", "K1", "v", "r", "info", ConfigIssueSeverity.Info),
                new ConfigIssue("p", "f2.ini", "S", "K2", "v", "r", "warning", ConfigIssueSeverity.Warning),
                new ConfigIssue("p", "f3.ini", "S", "K3", "v", "r", "error", ConfigIssueSeverity.Error)
            }
        };

        // Act
        var fragment = _handler.CreateReportFragment(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("[i]"); // Info
        content.Should().Contain("[!]"); // Warning
        content.Should().Contain("[X]"); // Error
    }

    [Fact]
    public void CreateReportFragment_WithConsoleCommandIssues_IncludesWarningSection()
    {
        // Arrange
        var result = new FcxModeResult
        {
            IsEnabled = true,
            ConsoleCommandIssues = new[]
            {
                new ConsoleCommandIssue("path", "fallout4.ini", "bat autoexec.txt")
            }
        };

        // Act
        var fragment = _handler.CreateReportFragment(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("Console Command Warnings");
        content.Should().Contain("bat autoexec.txt");
        content.Should().Contain("slow down game startup");
    }

    [Fact]
    public void CreateReportFragment_WithVSyncIssues_IncludesVSyncSection()
    {
        // Arrange
        var result = new FcxModeResult
        {
            IsEnabled = true,
            VSyncIssues = new[]
            {
                new VSyncIssue("path", "enblocal.ini", "ENGINE", "ForceVSync", true)
            }
        };

        // Act
        var fragment = _handler.CreateReportFragment(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("VSync Settings Detected");
        content.Should().Contain("enblocal.ini");
        content.Should().Contain("ForceVSync");
        content.Should().Contain("Enabled");
    }

    [Fact]
    public void CreateReportFragment_WithTomlIssues_IncludesTomlSection()
    {
        // Arrange
        var result = new FcxModeResult
        {
            IsEnabled = true,
            TomlConfigIssues = new[]
            {
                new ConfigIssue("path", "Buffout4.toml", "Patches", "Achievements", "true", "false", "desc")
            }
        };

        // Act
        var fragment = _handler.CreateReportFragment(result);

        // Assert
        var content = string.Join("\n", fragment.Lines);
        content.Should().Contain("TOML Configuration Issues");
        content.Should().Contain("Buffout4.toml");
    }
}

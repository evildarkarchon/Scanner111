using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;

namespace Scanner111.Tests.Services;

/// <summary>
///     Focused unit tests for the <see cref="UpdateService" /> class that test behavior without HTTP interactions.
/// </summary>
[Collection("Network Tests")]
public class UpdateServiceFocusedTests
{
    private readonly Mock<ILogger<UpdateService>> _loggerMock;
    private readonly Mock<IMessageHandler> _messageHandlerMock;
    private readonly UpdateService _service;
    private readonly Mock<IApplicationSettingsService> _settingsServiceMock;

    public UpdateServiceFocusedTests()
    {
        _loggerMock = new Mock<ILogger<UpdateService>>();
        _settingsServiceMock = new Mock<IApplicationSettingsService>();
        _messageHandlerMock = new Mock<IMessageHandler>();
        _service = new UpdateService(_loggerMock.Object, _settingsServiceMock.Object, _messageHandlerMock.Object);
    }

    #region Version Comparison Logic (via Reflection)

    [Theory]
    [InlineData("1.0.0", "1.0.1", null, true)] // GitHub newer
    [InlineData("1.0.0", null, "1.0.1", true)] // Nexus newer
    [InlineData("1.0.0", "1.0.1", "1.0.2", true)] // Both newer
    [InlineData("1.0.1", "1.0.0", "1.0.0", false)] // Current is latest
    [InlineData("1.0.0", "1.0.0", "1.0.0", false)] // All same
    [InlineData(null, "1.0.0", null, true)] // Current version unknown
    [InlineData("1.0.0", null, null, false)] // No remote versions
    public void IsUpdateAvailable_VariousScenarios_ReturnsExpectedResult(
        string currentVer, string gitHubVer, string nexusVer, bool expected)
    {
        // Arrange
        var method = typeof(UpdateService).GetMethod(
            "IsUpdateAvailable",
            BindingFlags.NonPublic | BindingFlags.Static);

        var current = currentVer != null ? Version.Parse(currentVer) : null;
        var gitHub = gitHubVer != null ? Version.Parse(gitHubVer) : null;
        var nexus = nexusVer != null ? Version.Parse(nexusVer) : null;

        // Act
        var result = (bool)(method?.Invoke(null, new object?[] { current, gitHub, nexus }) ?? false);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_InitializesSuccessfully_WithValidDependencies()
    {
        // Act
        var service = new UpdateService(_loggerMock.Object, _settingsServiceMock.Object, _messageHandlerMock.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IUpdateService>();
    }

    [Fact]
    public void Constructor_DoesNotThrow_WithNullDependencies()
    {
        // Act & Assert - Constructor doesn't validate parameters
        var action1 = () => new UpdateService(null!, _settingsServiceMock.Object, _messageHandlerMock.Object);
        var action2 = () => new UpdateService(_loggerMock.Object, null!, _messageHandlerMock.Object);
        var action3 = () => new UpdateService(_loggerMock.Object, _settingsServiceMock.Object, null!);

        action1.Should().NotThrow();
        action2.Should().NotThrow();
        action3.Should().NotThrow();
    }

    #endregion

    #region IsLatestVersionAsync - Settings and Basic Behavior

    [Fact]
    public async Task IsLatestVersionAsync_WhenUpdateCheckDisabled_ReturnsFalseAndShowsError()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { EnableUpdateCheck = false });

        // Act
        var result = await _service.IsLatestVersionAsync();

        // Assert
        result.Should().BeFalse();
        _messageHandlerMock.Verify(x => x.ShowError(
                It.Is<string>(msg => msg.Contains("Update check failed") && msg.Contains("disabled")),
                It.IsAny<MessageTarget>()),
            Times.Once);
    }

    [Fact]
    public async Task IsLatestVersionAsync_InQuietMode_DoesNotShowMessages()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { EnableUpdateCheck = false });

        // Act
        var result = await _service.IsLatestVersionAsync(true);

        // Assert
        result.Should().BeFalse();
        _messageHandlerMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task IsLatestVersionAsync_PropagatesCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { EnableUpdateCheck = true, UpdateSource = "Both" });

        // Act - The method should handle cancellation gracefully
        var task = _service.IsLatestVersionAsync(true, cts.Token);
        cts.Cancel();

        // Assert - Should complete without throwing
        var result = await task;
        result.Should().BeFalse();
    }

    #endregion

    #region GetUpdateInfoAsync - Settings Validation

    [Fact]
    public async Task GetUpdateInfoAsync_WhenUpdateCheckDisabled_ReturnsErrorResult()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { EnableUpdateCheck = false });

        // Act
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        result.Should().NotBeNull();
        result.CheckSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Update checking is disabled in settings");
    }

    [Theory]
    [InlineData("GitHub")]
    [InlineData("Nexus")]
    [InlineData("Both")]
    public async Task GetUpdateInfoAsync_SetsUpdateSourceFromSettings(string updateSource)
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings
            {
                EnableUpdateCheck = true,
                UpdateSource = updateSource
            });

        // Act
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        result.Should().NotBeNull();
        result.UpdateSource.Should().Be(updateSource);
    }

    [Fact]
    public async Task GetUpdateInfoAsync_LogsDebugMessageWhenStarting()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings
            {
                EnableUpdateCheck = true,
                UpdateSource = "Both"
            });

        // Act
        await _service.GetUpdateInfoAsync();

        // Assert
        _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting update check")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Once);
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WithNullSettings_ThrowsNullReferenceException()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync((ApplicationSettings)null!);

        // Act
        var action = async () => await _service.GetUpdateInfoAsync();

        // Assert
        await action.Should().ThrowAsync<NullReferenceException>();
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WhenSettingsThrows_PropagatesException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Settings error");
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ThrowsAsync(expectedException);

        // Act
        var action = async () => await _service.GetUpdateInfoAsync();

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Settings error");
    }

    #endregion

    #region UpdateCheckResult Model Tests

    [Fact]
    public void UpdateCheckResult_DefaultValues_AreCorrect()
    {
        // Act
        var result = new UpdateCheckResult();

        // Assert
        result.CurrentVersion.Should().BeNull();
        result.LatestGitHubVersion.Should().BeNull();
        result.LatestNexusVersion.Should().BeNull();
        result.IsUpdateAvailable.Should().BeFalse();
        result.CheckSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().BeNull();
        result.UpdateSource.Should().Be("Both");
    }

    [Fact]
    public void UpdateCheckResult_AllPropertiesCanBeSet()
    {
        // Arrange
        var currentVersion = new Version(1, 0, 0);
        var gitHubVersion = new Version(1, 1, 0);
        var nexusVersion = new Version(1, 2, 0);

        // Act
        var result = new UpdateCheckResult
        {
            CurrentVersion = currentVersion,
            LatestGitHubVersion = gitHubVersion,
            LatestNexusVersion = nexusVersion,
            IsUpdateAvailable = true,
            CheckSuccessful = true,
            ErrorMessage = "Test error",
            UpdateSource = "GitHub"
        };

        // Assert
        result.CurrentVersion.Should().Be(currentVersion);
        result.LatestGitHubVersion.Should().Be(gitHubVersion);
        result.LatestNexusVersion.Should().Be(nexusVersion);
        result.IsUpdateAvailable.Should().BeTrue();
        result.CheckSuccessful.Should().BeTrue();
        result.ErrorMessage.Should().Be("Test error");
        result.UpdateSource.Should().Be("GitHub");
    }

    #endregion

    #region UpdateCheckException Tests

    [Fact]
    public void UpdateCheckException_WithMessage_StoresMessage()
    {
        // Act
        var exception = new UpdateCheckException("Test message");

        // Assert
        exception.Message.Should().Be("Test message");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void UpdateCheckException_WithMessageAndInnerException_StoresBoth()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new UpdateCheckException("Outer message", innerException);

        // Assert
        exception.Message.Should().Be("Outer message");
        exception.InnerException.Should().Be(innerException);
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task IsLatestVersionAsync_HandlesMultipleConcurrentCalls()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { EnableUpdateCheck = false });

        var tasks = new Task<bool>[10];

        // Act
        for (var i = 0; i < tasks.Length; i++) tasks[i] = _service.IsLatestVersionAsync(true);

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllBeEquivalentTo(false);
        _settingsServiceMock.Verify(x => x.LoadSettingsAsync(), Times.AtLeast(tasks.Length));
    }

    [Fact]
    public async Task GetUpdateInfoAsync_HandlesMultipleConcurrentCalls()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings
            {
                EnableUpdateCheck = false
            });

        var tasks = new Task<UpdateCheckResult>[5];

        // Act
        for (var i = 0; i < tasks.Length; i++) tasks[i] = _service.GetUpdateInfoAsync();

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r =>
        {
            r.Should().NotBeNull();
            r.CheckSuccessful.Should().BeFalse();
            r.ErrorMessage.Should().Be("Update checking is disabled in settings");
        });
    }

    #endregion

    #region Version Parsing Edge Cases (via Reflection)

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("Scanner111 v2.0.0", "2.0.0")]
    [InlineData("Release 3.1.4", "3.1.4")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    [InlineData("v1.2.3.4", "1.2.3.4")]
    [InlineData("Version 10.5.2", "10.5.2")]
    [InlineData("V1.2.3", "1.2.3")] // Case insensitive
    [InlineData("    v1.2.3    ", "1.2.3")] // With whitespace
    public void TryParseVersion_ValidFormats_ParsesCorrectly(string input, string expectedVersion)
    {
        // Arrange
        var method = typeof(UpdateService).GetMethod(
            "TryParseVersion",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (Version?)method?.Invoke(null, new object?[] { input });

        // Assert
        result.Should().NotBeNull();
        result.ToString().Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid")]
    [InlineData("v")]
    [InlineData("vv1.2.3")]
    [InlineData("not-a-version")]
    [InlineData("1.2.a")]
    public void TryParseVersion_InvalidFormats_ReturnsNull(string input)
    {
        // Arrange
        var method = typeof(UpdateService).GetMethod(
            "TryParseVersion",
            BindingFlags.NonPublic | BindingFlags.Static);

        // Act
        var result = (Version?)method?.Invoke(null, new object?[] { input });

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Integration-Style Tests (without actual HTTP)

    [Fact]
    public async Task FullUpdateCheckFlow_WhenDisabled_ReturnsExpectedMessages()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings { EnableUpdateCheck = false });

        // Act
        var checkResult = await _service.GetUpdateInfoAsync();
        var isLatestResult = await _service.IsLatestVersionAsync();

        // Assert
        checkResult.Should().NotBeNull();
        checkResult.CheckSuccessful.Should().BeFalse();
        checkResult.ErrorMessage.Should().Contain("disabled");

        isLatestResult.Should().BeFalse();

        _messageHandlerMock.Verify(x => x.ShowError(
                It.IsAny<string>(),
                It.IsAny<MessageTarget>()),
            Times.Once);
    }

    [Theory]
    [InlineData("GitHub", 1)]
    [InlineData("Nexus", 1)]
    [InlineData("Both", 1)]
    public async Task FullUpdateCheckFlow_WithDifferentSources_LogsAppropriately(string source, int expectedLogCalls)
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings
            {
                EnableUpdateCheck = true,
                UpdateSource = source
            });

        // Act
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        result.Should().NotBeNull();
        result.UpdateSource.Should().Be(source);

        _loggerMock.Verify(x => x.Log(
                LogLevel.Debug,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting update check")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Exactly(expectedLogCalls));
    }

    #endregion
}
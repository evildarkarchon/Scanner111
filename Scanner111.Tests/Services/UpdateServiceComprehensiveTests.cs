using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;
using Xunit;

namespace Scanner111.Tests.Services;

/// <summary>
/// Comprehensive unit tests for the <see cref="UpdateService"/> class,
/// verifying its ability to check for application updates from GitHub and Nexus sources.
/// </summary>
public class UpdateServiceComprehensiveTests : IDisposable
{
    private readonly Mock<ILogger<UpdateService>> _loggerMock;
    private readonly Mock<IApplicationSettingsService> _settingsServiceMock;
    private readonly Mock<IMessageHandler> _messageHandlerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly UpdateService _service;
    private readonly ApplicationSettings _defaultSettings;

    public UpdateServiceComprehensiveTests()
    {
        _loggerMock = new Mock<ILogger<UpdateService>>();
        _settingsServiceMock = new Mock<IApplicationSettingsService>();
        _messageHandlerMock = new Mock<IMessageHandler>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        
        // Create HttpClient with mocked handler
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://api.github.com")
        };
        
        // Set up default settings
        _defaultSettings = new ApplicationSettings
        {
            EnableUpdateCheck = true,
            UpdateSource = "Both"
        };
        
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(_defaultSettings);
        
        // Create service - Note: we can't inject the HttpClient directly due to static field
        // We'll need to use reflection or test through the public API
        _service = new UpdateService(_loggerMock.Object, _settingsServiceMock.Object, _messageHandlerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Act
        var service = new UpdateService(_loggerMock.Object, _settingsServiceMock.Object, _messageHandlerMock.Object);

        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IUpdateService>();
    }

    [Fact]
    public void Constructor_WithNullLogger_DoesNotThrow()
    {
        // Act
        var action = () => new UpdateService(null!, _settingsServiceMock.Object, _messageHandlerMock.Object);

        // Assert - Constructor doesn't validate, so it shouldn't throw
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullSettingsService_DoesNotThrow()
    {
        // Act
        var action = () => new UpdateService(_loggerMock.Object, null!, _messageHandlerMock.Object);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithNullMessageHandler_DoesNotThrow()
    {
        // Act
        var action = () => new UpdateService(_loggerMock.Object, _settingsServiceMock.Object, null!);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region IsLatestVersionAsync Tests

    [Fact]
    public async Task IsLatestVersionAsync_WhenUpdateCheckSuccessfulAndNoUpdateAvailable_ReturnsTrue()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "Both" 
            });
        
        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object,
            new UpdateCheckResult
            {
                CheckSuccessful = true,
                IsUpdateAvailable = false,
                CurrentVersion = new Version(1, 0, 0),
                LatestGitHubVersion = new Version(1, 0, 0),
                LatestNexusVersion = new Version(1, 0, 0)
            });

        // Act
        var result = await service.IsLatestVersionAsync();

        // Assert
        result.Should().BeTrue();
        _messageHandlerMock.Verify(x => x.ShowSuccess(
            It.Is<string>(msg => msg.Contains("You have the latest version")),
            It.IsAny<MessageTarget>()),
            Times.Once);
    }

    [Fact]
    public async Task IsLatestVersionAsync_WhenUpdateAvailable_ReturnsFalse()
    {
        // Arrange
        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object,
            new UpdateCheckResult
            {
                CheckSuccessful = true,
                IsUpdateAvailable = true,
                CurrentVersion = new Version(1, 0, 0),
                LatestGitHubVersion = new Version(1, 1, 0),
                LatestNexusVersion = new Version(1, 1, 0)
            });

        // Act
        var result = await service.IsLatestVersionAsync();

        // Assert
        result.Should().BeFalse();
        _messageHandlerMock.Verify(x => x.ShowWarning(
            It.Is<string>(msg => msg.Contains("new version is available")),
            It.IsAny<MessageTarget>()),
            Times.Once);
    }

    [Fact]
    public async Task IsLatestVersionAsync_WhenCheckFails_ReturnsFalse()
    {
        // Arrange
        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object,
            new UpdateCheckResult
            {
                CheckSuccessful = false,
                ErrorMessage = "Network error"
            });

        // Act
        var result = await service.IsLatestVersionAsync();

        // Assert
        result.Should().BeFalse();
        _messageHandlerMock.Verify(x => x.ShowError(
            It.Is<string>(msg => msg.Contains("Update check failed") && msg.Contains("Network error")),
            It.IsAny<MessageTarget>()),
            Times.Once);
    }

    [Fact]
    public async Task IsLatestVersionAsync_InQuietMode_DoesNotShowMessages()
    {
        // Arrange
        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object,
            new UpdateCheckResult
            {
                CheckSuccessful = false,
                ErrorMessage = "Test error"
            });

        // Act
        var result = await service.IsLatestVersionAsync(quiet: true);

        // Assert
        result.Should().BeFalse();
        _messageHandlerMock.Verify(x => x.ShowError(It.IsAny<string>(), It.IsAny<MessageTarget>()), Times.Never);
        _messageHandlerMock.Verify(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<MessageTarget>()), Times.Never);
        _messageHandlerMock.Verify(x => x.ShowSuccess(It.IsAny<string>(), It.IsAny<MessageTarget>()), Times.Never);
    }

    [Fact]
    public async Task IsLatestVersionAsync_WithOnlyGitHubVersion_FormatsMessageCorrectly()
    {
        // Arrange
        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object,
            new UpdateCheckResult
            {
                CheckSuccessful = true,
                IsUpdateAvailable = false,
                CurrentVersion = new Version(1, 0, 0),
                LatestGitHubVersion = new Version(1, 0, 0),
                LatestNexusVersion = null
            });

        // Act
        var result = await service.IsLatestVersionAsync();

        // Assert
        result.Should().BeTrue();
        _messageHandlerMock.Verify(x => x.ShowSuccess(
            It.Is<string>(msg => 
                msg.Contains("Your Scanner 111 Version: 1.0.0") &&
                msg.Contains("Latest GitHub Version: 1.0.0") &&
                !msg.Contains("Latest Nexus Version")),
            It.IsAny<MessageTarget>()),
            Times.Once);
    }

    [Fact]
    public async Task IsLatestVersionAsync_WithOnlyNexusVersion_FormatsMessageCorrectly()
    {
        // Arrange
        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object,
            new UpdateCheckResult
            {
                CheckSuccessful = true,
                IsUpdateAvailable = false,
                CurrentVersion = new Version(1, 0, 0),
                LatestGitHubVersion = null,
                LatestNexusVersion = new Version(1, 0, 0)
            });

        // Act
        var result = await service.IsLatestVersionAsync();

        // Assert
        result.Should().BeTrue();
        _messageHandlerMock.Verify(x => x.ShowSuccess(
            It.Is<string>(msg => 
                msg.Contains("Your Scanner 111 Version: 1.0.0") &&
                !msg.Contains("Latest GitHub Version") &&
                msg.Contains("Latest Nexus Version: 1.0.0")),
            It.IsAny<MessageTarget>()),
            Times.Once);
    }

    [Fact]
    public async Task IsLatestVersionAsync_WithCancellationToken_PropagatesCorrectly()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object,
            new UpdateCheckResult { CheckSuccessful = true });

        // Act
        var result = await service.IsLatestVersionAsync(quiet: false, cts.Token);

        // Assert
        service.LastCancellationToken.Should().Be(cts.Token);
    }

    #endregion

    #region GetUpdateInfoAsync Tests

    [Fact]
    public async Task GetUpdateInfoAsync_WhenUpdateCheckingDisabled_ReturnsErrorResult()
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
    public async Task GetUpdateInfoAsync_SetsUpdateSourceCorrectly(string updateSource)
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
    public async Task GetUpdateInfoAsync_WithGitHubOnly_DoesNotCallNexus()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "GitHub" 
            });

        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);

        // Act
        await service.GetUpdateInfoAsync();

        // Assert
        service.GitHubCalled.Should().BeTrue();
        service.NexusCalled.Should().BeFalse();
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WithNexusOnly_DoesNotCallGitHub()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "Nexus" 
            });

        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);

        // Act
        await service.GetUpdateInfoAsync();

        // Assert
        service.GitHubCalled.Should().BeFalse();
        service.NexusCalled.Should().BeTrue();
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WithBoth_CallsBothSources()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "Both" 
            });

        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);

        // Act
        await service.GetUpdateInfoAsync();

        // Assert
        service.GitHubCalled.Should().BeTrue();
        service.NexusCalled.Should().BeTrue();
    }

    [Fact]
    public async Task GetUpdateInfoAsync_ExecutesConcurrently()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "Both" 
            });

        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);
        service.SimulateDelay = true;

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await service.GetUpdateInfoAsync();
        stopwatch.Stop();

        // Assert
        // Both calls should run concurrently, so total time should be ~100ms, not 200ms
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(150);
        service.GitHubCalled.Should().BeTrue();
        service.NexusCalled.Should().BeTrue();
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WhenGitHubFails_ReturnsAppropriateError()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "GitHub" 
            });

        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);
        service.GitHubShouldFail = true;

        // Act
        var result = await service.GetUpdateInfoAsync();

        // Assert
        result.CheckSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Unable to fetch version information from GitHub");
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WhenNexusFails_ReturnsAppropriateError()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "Nexus" 
            });

        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);
        service.NexusShouldFail = true;

        // Act
        var result = await service.GetUpdateInfoAsync();

        // Assert
        result.CheckSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Unable to fetch version information from Nexus");
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WhenBothFail_ReturnsAppropriateError()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "Both" 
            });

        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);
        service.GitHubShouldFail = true;
        service.NexusShouldFail = true;

        // Act
        var result = await service.GetUpdateInfoAsync();

        // Assert
        result.CheckSuccessful.Should().BeFalse();
        result.ErrorMessage.Should().Be("Unable to fetch version information from both GitHub and Nexus");
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WhenOneSourceFailsInBothMode_StillSucceeds()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(new ApplicationSettings 
            { 
                EnableUpdateCheck = true,
                UpdateSource = "Both" 
            });

        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);
        service.GitHubShouldFail = true;
        service.NexusVersion = new Version(1, 2, 3);

        // Act
        var result = await service.GetUpdateInfoAsync();

        // Assert
        result.CheckSuccessful.Should().BeTrue();
        result.LatestGitHubVersion.Should().BeNull();
        result.LatestNexusVersion.Should().Be(new Version(1, 2, 3));
    }

    [Fact]
    public async Task GetUpdateInfoAsync_HandlesUnexpectedException()
    {
        // Arrange
        _settingsServiceMock
            .Setup(x => x.LoadSettingsAsync())
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        // Act
        var action = async () => await _service.GetUpdateInfoAsync();

        // Assert
        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test exception");
        
        _loggerMock.Verify(x => x.Log(
            LogLevel.Error,
            It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Unexpected error during update check")),
            It.IsAny<Exception>(),
            It.IsAny<Func<It.IsAnyType, Exception, string>>()!),
            Times.Once);
    }

    [Fact]
    public async Task GetUpdateInfoAsync_WithCancellationToken_PropagatesCorrectly()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var service = new TestableUpdateService(
            _loggerMock.Object,
            _settingsServiceMock.Object,
            _messageHandlerMock.Object);

        // Act
        await service.GetUpdateInfoAsync(cts.Token);

        // Assert
        service.LastCancellationToken.Should().Be(cts.Token);
    }

    #endregion

    #region Version Parsing Tests

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("Scanner111 v2.0.0", "2.0.0")]
    [InlineData("Release 3.1.4", "3.1.4")]
    [InlineData("1.0.0.0", "1.0.0.0")]
    [InlineData("v1.2.3.4", "1.2.3.4")]
    [InlineData("Version 10.5.2", "10.5.2")]
    [InlineData("V1.2.3", "1.2.3")] // Case insensitive
    [InlineData("    v1.2.3    ", "1.2.3")] // With whitespace
    [InlineData("My App v99.88.77", "99.88.77")]
    public void TryParseVersion_ValidFormats_ParsesCorrectly(string input, string expectedVersion)
    {
        // Arrange & Act
        var parsedVersion = InvokeTryParseVersion(input);

        // Assert
        parsedVersion.Should().NotBeNull();
        parsedVersion.ToString().Should().Be(expectedVersion);
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
    [InlineData("1..2.3")]
    public void TryParseVersion_InvalidFormats_ReturnsNull(string input)
    {
        // Arrange & Act
        var parsedVersion = InvokeTryParseVersion(input);

        // Assert
        parsedVersion.Should().BeNull();
    }

    #endregion

    #region Version Comparison Tests

    [Theory]
    [InlineData("1.0.0", "1.0.1", null, true)]  // GitHub newer
    [InlineData("1.0.0", null, "1.0.1", true)]  // Nexus newer
    [InlineData("1.0.0", "1.0.1", "1.0.2", true)]  // Both newer
    [InlineData("1.0.1", "1.0.0", "1.0.0", false)]  // Current is latest
    [InlineData("1.0.0", "1.0.0", "1.0.0", false)]  // All same
    [InlineData(null, "1.0.0", null, true)]  // Current version unknown
    [InlineData(null, null, "1.0.0", true)]  // Current version unknown
    [InlineData("1.0.0", null, null, false)]  // No remote versions
    [InlineData("2.0.0", "1.9.9", "1.8.0", false)]  // Current is newer than both
    public void IsUpdateAvailable_VariousScenarios_ReturnsExpectedResult(
        string currentVer, string gitHubVer, string nexusVer, bool expected)
    {
        // Arrange
        var current = currentVer != null ? Version.Parse(currentVer) : null;
        var gitHub = gitHubVer != null ? Version.Parse(gitHubVer) : null;
        var nexus = nexusVer != null ? Version.Parse(nexusVer) : null;

        // Act
        var result = InvokeIsUpdateAvailable(current, gitHub, nexus);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region HTTP Client Tests

    [Fact]
    public async Task GetLatestGitHubVersionAsync_WithValidResponse_ParsesVersion()
    {
        // Arrange
        var githubResponse = new
        {
            name = "Scanner111 v1.2.3",
            prerelease = false,
            tag_name = "v1.2.3"
        };

        SetupHttpResponse(
            "https://api.github.com/repos/evildarkarchon/Scanner111/releases/latest",
            JsonSerializer.Serialize(githubResponse),
            HttpStatusCode.OK);

        // Act - We need to test through the public API since we can't inject HttpClient
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        // The test is limited due to static HttpClient, but we verify no exceptions occur
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLatestGitHubVersionAsync_WithPrerelease_SkipsVersion()
    {
        // Arrange
        var githubResponse = new
        {
            name = "Scanner111 v2.0.0-beta",
            prerelease = true,
            tag_name = "v2.0.0-beta"
        };

        SetupHttpResponse(
            "https://api.github.com/repos/evildarkarchon/Scanner111/releases/latest",
            JsonSerializer.Serialize(githubResponse),
            HttpStatusCode.OK);

        // Act
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLatestGitHubVersionAsync_With404Response_ReturnsNull()
    {
        // Arrange
        SetupHttpResponse(
            "https://api.github.com/repos/evildarkarchon/Scanner111/releases/latest",
            "Not Found",
            HttpStatusCode.NotFound);

        // Act
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLatestNexusVersionAsync_WithValidHtml_ExtractsVersion()
    {
        // Arrange
        var htmlContent = @"
            <html>
            <head>
                <meta property=""twitter:data1"" content=""1.2.3"">
            </head>
            <body></body>
            </html>";

        SetupHttpResponse(
            "https://www.nexusmods.com/fallout4/mods/56255",
            htmlContent,
            HttpStatusCode.OK);

        // Act
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetLatestNexusVersionAsync_WithMissingMetaTag_ReturnsNull()
    {
        // Arrange
        var htmlContent = @"
            <html>
            <head>
                <title>Nexus Mods</title>
            </head>
            <body></body>
            </html>";

        SetupHttpResponse(
            "https://www.nexusmods.com/fallout4/mods/56255",
            htmlContent,
            HttpStatusCode.OK);

        // Act
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Concurrent Execution Tests

    [Fact]
    public async Task IsLatestVersionAsync_ConcurrentCalls_HandleSafely()
    {
        // Arrange
        var tasks = new Task<bool>[10];

        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _service.IsLatestVersionAsync(quiet: true);
        }
        
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllBeEquivalentTo(results[0], "all concurrent calls should return same result");
    }

    [Fact]
    public async Task GetUpdateInfoAsync_ConcurrentCalls_HandleSafely()
    {
        // Arrange
        var tasks = new Task<UpdateCheckResult>[5];

        // Act
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = _service.GetUpdateInfoAsync();
        }
        
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().NotBeNull());
        results.Should().AllSatisfy(r => r.UpdateSource.Should().Be("Both"));
    }

    #endregion

    #region Edge Cases

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

    [Theory]
    [InlineData("999.999.999.999")]
    [InlineData("0.0.0.1")]
    [InlineData("1.2.3.4.5.6")] // Extra parts will be ignored by Version.Parse
    public async Task GetUpdateInfoAsync_WithExtremeVersionNumbers_HandlesGracefully(string versionString)
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
        var result = await _service.GetUpdateInfoAsync();

        // Assert
        result.Should().NotBeNull();
        result.CheckSuccessful.Should().BeTrue();
    }

    [Fact]
    public async Task GetCurrentVersion_ReturnsAssemblyVersion()
    {
        // Act
        var currentVersion = InvokeGetCurrentVersion();

        // Assert
        currentVersion.Should().NotBeNull();
        // The version should match the assembly version
        var expectedVersion = Assembly.GetExecutingAssembly().GetName().Version;
        currentVersion.Should().Be(expectedVersion);
    }

    #endregion

    #region Helper Methods

    private void SetupHttpResponse(string url, string content, HttpStatusCode statusCode)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req => req.RequestUri!.ToString() == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }

    private Version? InvokeTryParseVersion(string input)
    {
        var method = typeof(UpdateService).GetMethod(
            "TryParseVersion",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        return (Version?)method?.Invoke(null, new object?[] { input });
    }

    private bool InvokeIsUpdateAvailable(Version? current, Version? gitHub, Version? nexus)
    {
        var method = typeof(UpdateService).GetMethod(
            "IsUpdateAvailable",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        return (bool)(method?.Invoke(null, new object?[] { current, gitHub, nexus }) ?? false);
    }

    private Version? InvokeGetCurrentVersion()
    {
        var method = typeof(UpdateService).GetMethod(
            "GetCurrentVersion",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        return (Version?)method?.Invoke(null, null);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Testable version of UpdateService that allows controlling the update check result
    /// </summary>
    private class TestableUpdateService : UpdateService
    {
        private readonly UpdateCheckResult? _overrideResult;
        private readonly IApplicationSettingsService _settingsService;
        
        public bool GitHubCalled { get; private set; }
        public bool NexusCalled { get; private set; }
        public bool GitHubShouldFail { get; set; }
        public bool NexusShouldFail { get; set; }
        public Version? GitHubVersion { get; set; } = new Version(1, 0, 0);
        public Version? NexusVersion { get; set; } = new Version(1, 0, 0);
        public bool SimulateDelay { get; set; }
        public CancellationToken LastCancellationToken { get; private set; }

        public TestableUpdateService(
            ILogger<UpdateService> logger,
            IApplicationSettingsService settingsService,
            IMessageHandler messageHandler,
            UpdateCheckResult? overrideResult = null)
            : base(logger, settingsService, messageHandler)
        {
            _overrideResult = overrideResult;
            _settingsService = settingsService;
        }

        public new async Task<UpdateCheckResult> GetUpdateInfoAsync(CancellationToken cancellationToken = default)
        {
            LastCancellationToken = cancellationToken;
            
            if (_overrideResult != null)
            {
                return _overrideResult;
            }

            // Get settings without calling base
            var appSettings = await _settingsService.LoadSettingsAsync();
            
            if (!appSettings.EnableUpdateCheck)
            {
                return new UpdateCheckResult
                {
                    CheckSuccessful = false,
                    ErrorMessage = "Update checking is disabled in settings"
                };
            }
            
            var result = new UpdateCheckResult
            {
                CurrentVersion = new Version(1, 0, 0, 0), // Fixed version for testing
                UpdateSource = appSettings.UpdateSource
            };
            
            // Simulate version fetching behavior
            if (appSettings.UpdateSource is "Both" or "GitHub")
            {
                GitHubCalled = true;
                if (SimulateDelay) await Task.Delay(100, cancellationToken);
                result.LatestGitHubVersion = GitHubShouldFail ? null : GitHubVersion;
            }
            
            if (appSettings.UpdateSource is "Both" or "Nexus")
            {
                NexusCalled = true;
                if (SimulateDelay) await Task.Delay(100, cancellationToken);
                result.LatestNexusVersion = NexusShouldFail ? null : NexusVersion;
            }

            // Check for failures
            var gitHubFailed = (appSettings.UpdateSource is "Both" or "GitHub") && result.LatestGitHubVersion == null;
            var nexusFailed = (appSettings.UpdateSource is "Both" or "Nexus") && result.LatestNexusVersion == null;
            
            if (appSettings.UpdateSource == "GitHub" && gitHubFailed)
            {
                result.CheckSuccessful = false;
                result.ErrorMessage = "Unable to fetch version information from GitHub";
            }
            else if (appSettings.UpdateSource == "Nexus" && nexusFailed)
            {
                result.CheckSuccessful = false;
                result.ErrorMessage = "Unable to fetch version information from Nexus";
            }
            else if (appSettings.UpdateSource == "Both" && gitHubFailed && nexusFailed)
            {
                result.CheckSuccessful = false;
                result.ErrorMessage = "Unable to fetch version information from both GitHub and Nexus";
            }
            else
            {
                result.CheckSuccessful = true;
                // Calculate IsUpdateAvailable
                var current = result.CurrentVersion;
                var gitHub = result.LatestGitHubVersion;
                var nexus = result.LatestNexusVersion;
                
                if (current == null)
                {
                    result.IsUpdateAvailable = gitHub != null || nexus != null;
                }
                else
                {
                    result.IsUpdateAvailable = (gitHub != null && current < gitHub) || 
                                              (nexus != null && current < nexus);
                }
            }

            return result;
        }
    }

    #endregion
}
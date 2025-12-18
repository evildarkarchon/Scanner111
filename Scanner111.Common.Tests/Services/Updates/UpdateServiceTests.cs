using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Scanner111.Common.Services.Updates;

namespace Scanner111.Common.Tests.Services.Updates;

/// <summary>
/// Tests for the UpdateService.
/// </summary>
public class UpdateServiceTests
{
    private readonly Mock<HttpMessageHandler> _mockHandler;
    private readonly HttpClient _httpClient;
    private readonly UpdateService _service;

    public UpdateServiceTests()
    {
        _mockHandler = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_mockHandler.Object)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Scanner111/1.0");

        _service = new UpdateService(NullLogger<UpdateService>.Instance, _httpClient);
    }

    #region GetCurrentVersion Tests

    [Fact]
    public void GetCurrentVersion_ReturnsVersionString()
    {
        var version = _service.GetCurrentVersion();

        version.Should().NotBeNullOrEmpty();
        version.Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    #endregion

    #region CheckForUpdatesAsync Tests

    [Fact]
    public async Task CheckForUpdatesAsync_WithNewerVersion_ReturnsUpdateAvailable()
    {
        // Arrange
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v99.0.0",
            name = "Version 99.0.0",
            body = "Release notes here",
            html_url = "https://github.com/evildarkarchon/Scanner111/releases/v99.0.0",
            published_at = DateTimeOffset.UtcNow,
            prerelease = false
        });

        SetupMockResponse(HttpStatusCode.OK, releaseJson);

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.IsUpdateAvailable.Should().BeTrue();
        result.LatestRelease.Should().NotBeNull();
        result.LatestRelease!.Version.Should().Be("99.0.0");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithSameVersion_ReturnsUpToDate()
    {
        // Arrange
        var currentVersion = _service.GetCurrentVersion();
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = $"v{currentVersion}",
            name = $"Version {currentVersion}",
            body = "Release notes",
            html_url = "https://github.com/evildarkarchon/Scanner111/releases",
            published_at = DateTimeOffset.UtcNow,
            prerelease = false
        });

        SetupMockResponse(HttpStatusCode.OK, releaseJson);

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithOlderVersion_ReturnsUpToDate()
    {
        // Arrange
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v0.0.1",
            name = "Version 0.0.1",
            body = "Old release",
            html_url = "https://github.com/evildarkarchon/Scanner111/releases",
            published_at = DateTimeOffset.UtcNow.AddYears(-1),
            prerelease = false
        });

        SetupMockResponse(HttpStatusCode.OK, releaseJson);

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.Success.Should().BeTrue();
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithNotFound_ReturnsFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.NotFound, "");

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.IsUpdateAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithNetworkError_ReturnsFailure()
    {
        // Arrange
        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Network error");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithPrerelease_ReturnsLatestRelease()
    {
        // Arrange
        var releasesJson = JsonSerializer.Serialize(new[]
        {
            new
            {
                tag_name = "v99.0.0-beta",
                name = "Version 99.0.0 Beta",
                body = "Beta release notes",
                html_url = "https://github.com/evildarkarchon/Scanner111/releases/v99.0.0-beta",
                published_at = DateTimeOffset.UtcNow,
                prerelease = true
            },
            new
            {
                tag_name = "v98.0.0",
                name = "Version 98.0.0",
                body = "Stable release",
                html_url = "https://github.com/evildarkarchon/Scanner111/releases/v98.0.0",
                published_at = DateTimeOffset.UtcNow.AddDays(-7),
                prerelease = false
            }
        });

        SetupMockResponse(HttpStatusCode.OK, releasesJson);

        // Act
        var result = await _service.CheckForUpdatesAsync(includePrerelease: true);

        // Assert
        result.Success.Should().BeTrue();
        result.LatestRelease.Should().NotBeNull();
        result.LatestRelease!.Version.Should().Be("99.0.0-beta");
        result.LatestRelease.IsPrerelease.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithCancellation_ReturnsFailure()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Operation cancelled", null, cts.Token));

        // Act
        var result = await _service.CheckForUpdatesAsync(cancellationToken: cts.Token);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task CheckForUpdatesAsync_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        SetupMockResponse(HttpStatusCode.OK, "not valid json");

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("parsing");
    }

    #endregion

    #region Version Parsing Tests

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("Version v1.2.3", "1.2.3")]
    [InlineData("Release v1.2.3-beta", "1.2.3-beta")]
    [InlineData("v1.2.3.4", "1.2.3.4")]
    public async Task CheckForUpdatesAsync_ParsesVersionCorrectly(string tagName, string expectedVersion)
    {
        // Arrange
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = tagName,
            name = "Test Release",
            body = "",
            html_url = "https://github.com/test",
            published_at = DateTimeOffset.UtcNow,
            prerelease = false
        });

        SetupMockResponse(HttpStatusCode.OK, releaseJson);

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.LatestRelease?.Version.Should().Be(expectedVersion);
    }

    #endregion

    #region ReleaseInfo Mapping Tests

    [Fact]
    public async Task CheckForUpdatesAsync_MapsReleaseInfoCorrectly()
    {
        // Arrange
        var publishedAt = DateTimeOffset.UtcNow;
        var releaseJson = JsonSerializer.Serialize(new
        {
            tag_name = "v2.0.0",
            name = "Version 2.0.0 - Major Update",
            body = "## What's New\n- Feature A\n- Feature B",
            html_url = "https://github.com/evildarkarchon/Scanner111/releases/tag/v2.0.0",
            published_at = publishedAt,
            prerelease = false
        });

        SetupMockResponse(HttpStatusCode.OK, releaseJson);

        // Act
        var result = await _service.CheckForUpdatesAsync();

        // Assert
        result.LatestRelease.Should().NotBeNull();
        result.LatestRelease!.TagName.Should().Be("v2.0.0");
        result.LatestRelease.Name.Should().Be("Version 2.0.0 - Major Update");
        result.LatestRelease.ReleaseNotes.Should().Contain("What's New");
        result.LatestRelease.HtmlUrl.Should().Contain("github.com");
        result.LatestRelease.IsPrerelease.Should().BeFalse();
    }

    #endregion

    private void SetupMockResponse(HttpStatusCode statusCode, string content)
    {
        _mockHandler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
    }
}

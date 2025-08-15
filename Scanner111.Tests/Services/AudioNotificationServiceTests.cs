using FluentAssertions;
using Moq;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Core.Services;

namespace Scanner111.Tests.Services;

public class AudioNotificationServiceTests
{
    private readonly Mock<IApplicationSettingsService> _mockSettingsService;
    private readonly AudioNotificationService _service;
    private readonly ApplicationSettings _testSettings;

    public AudioNotificationServiceTests()
    {
        _mockSettingsService = new Mock<IApplicationSettingsService>();
        _testSettings = new ApplicationSettings
        {
            EnableAudioNotifications = true,
            AudioVolume = 0.5f,
            CustomNotificationSounds = new Dictionary<string, string>()
        };

        _mockSettingsService.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(_testSettings);

        _service = new AudioNotificationService(_mockSettingsService.Object);
    }

    [Fact]
    public void Constructor_LoadsSettingsFromService()
    {
        _service.IsEnabled.Should().BeTrue();
        _service.Volume.Should().Be(0.5f);
        _mockSettingsService.Verify(x => x.LoadSettingsAsync(), Times.Once);
    }

    [Fact]
    public void IsEnabled_WhenSet_UpdatesValue()
    {
        _service.IsEnabled = false;

        _service.IsEnabled.Should().BeFalse();

        // Note: Settings save happens asynchronously in background
        // The service is designed to not block on settings saves
    }

    [Theory]
    [InlineData(0.0f, 0.0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(1.5f, 1.0f)] // Should clamp to 1.0
    [InlineData(-0.5f, 0.0f)] // Should clamp to 0.0
    public void Volume_ClampsToValidRange(float input, float expected)
    {
        _service.Volume = input;

        _service.Volume.Should().Be(expected);
    }

    [Fact]
    public async Task PlayScanCompleteAsync_WhenDisabled_DoesNotPlay()
    {
        _service.IsEnabled = false;

        // Should complete without throwing
        await _service.PlayScanCompleteAsync();
    }

    [Fact]
    public async Task PlayErrorFoundAsync_WhenDisabled_DoesNotPlay()
    {
        _service.IsEnabled = false;

        // Should complete without throwing
        await _service.PlayErrorFoundAsync();
    }

    [Fact]
    public async Task PlayCriticalIssueAsync_WhenDisabled_DoesNotPlay()
    {
        _service.IsEnabled = false;

        // Should complete without throwing
        await _service.PlayCriticalIssueAsync();
    }

    [Fact]
    public async Task SetCustomSound_AddsToCustomSounds()
    {
        const string testPath = "C:\\test\\sound.wav";

        _service.SetCustomSound(NotificationType.ScanComplete, testPath);

        // Wait a bit for the async save to complete
        await Task.Delay(100);

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s =>
                    s.CustomNotificationSounds != null &&
                    s.CustomNotificationSounds.ContainsKey("ScanComplete") &&
                    s.CustomNotificationSounds["ScanComplete"] == testPath)),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task SetCustomSound_WithEmptyPath_RemovesCustomSound()
    {
        _service.SetCustomSound(NotificationType.ErrorFound, "");

        // Wait a bit for the async save to complete
        await Task.Delay(100);

        _mockSettingsService.Verify(x => x.SaveSettingsAsync(
                It.Is<ApplicationSettings>(s =>
                    s.CustomNotificationSounds != null &&
                    !s.CustomNotificationSounds.ContainsKey("ErrorFound"))),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task PlayCustomSoundAsync_WithInvalidPath_DoesNotThrow()
    {
        _service.IsEnabled = true;

        // Should handle gracefully
        await _service.PlayCustomSoundAsync("C:\\nonexistent\\file.wav");
    }

    [Fact]
    public void Dispose_DoesNotThrow()
    {
        var service = new AudioNotificationService(_mockSettingsService.Object);

        // Should not throw
        service.Dispose();
        service.Dispose(); // Double dispose should also not throw
    }

    [Fact]
    public void Constructor_WithNullSettingsService_HandlesGracefully()
    {
        var service = new AudioNotificationService();

        service.IsEnabled.Should().BeFalse();
        service.Volume.Should().Be(0.5f);
    }
}
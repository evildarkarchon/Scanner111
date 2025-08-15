using System.Reflection;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.CLI;

public class AboutCommandTests : IDisposable
{
    private readonly TestMessageCapture _messageCapture;

    public AboutCommandTests()
    {
        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);
    }

    public void Dispose()
    {
        MessageHandler.Initialize(new TestMessageHandler());
    }

    [Fact]
    public async Task ExecuteAsync_DisplaysVersionAndAboutInformation()
    {
        // Arrange
        var command = new AboutCommand(_messageCapture);
        var options = new AboutOptions();

        var assembly = Assembly.GetAssembly(typeof(AboutCommand));
        var expectedVersion = assembly?.GetName().Version ?? new Version(1, 0, 0);

        // Act
        var result = await command.ExecuteAsync(options);

        // Assert
        result.Should().Be(0, "because the command should succeed");
        _messageCapture.InfoMessages.Should().Contain("Scanner111 - CLASSIC Crash Log Analyzer",
            "because the application name should be displayed");
        _messageCapture.InfoMessages.Should().Contain($"Version: {expectedVersion}",
            "because the version should be displayed");
        _messageCapture.InfoMessages.Should().Contain("Compatible with Bethesda games crash logs",
            "because compatibility information should be shown");
        _messageCapture.InfoMessages.Should().Contain("Based on CLASSIC Python implementation",
            "because the origin should be mentioned");
    }
}
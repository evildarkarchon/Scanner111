using System.Reflection;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;
using Scanner111.Tests.TestHelpers;
using Xunit;

namespace Scanner111.Tests.CLI;

public class AboutCommandTests : IDisposable
{
    private readonly TestMessageCapture _messageCapture;

    public AboutCommandTests()
    {
        _messageCapture = new TestMessageCapture();
        MessageHandler.Initialize(_messageCapture);
    }

    [Fact]
    public async Task ExecuteAsync_DisplaysVersionAndAboutInformation()
    {
        // Arrange
        var command = new AboutCommand();
        var options = new AboutOptions();
        
        var assembly = Assembly.GetAssembly(typeof(AboutCommand));
        var expectedVersion = assembly?.GetName().Version ?? new Version(1, 0, 0);

        // Act
        var result = await command.ExecuteAsync(options);

        // Assert
        Assert.Equal(0, result);
        Assert.Contains("Scanner111 - CLASSIC Crash Log Analyzer", _messageCapture.InfoMessages);
        Assert.Contains($"Version: {expectedVersion}", _messageCapture.InfoMessages);
        Assert.Contains("Compatible with Bethesda games crash logs", _messageCapture.InfoMessages);
        Assert.Contains("Based on CLASSIC Python implementation", _messageCapture.InfoMessages);
    }

    public void Dispose()
    {
        MessageHandler.Initialize(new TestMessageHandler());
    }
}
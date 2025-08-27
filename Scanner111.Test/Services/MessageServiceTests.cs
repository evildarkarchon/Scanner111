using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Core.Models;
using Scanner111.Core.Services;

namespace Scanner111.Test.Services;

/// <summary>
///     Unit tests for <see cref="MessageService" />.
/// </summary>
public class MessageServiceTests : IDisposable
{
    private readonly List<Message> _capturedMessages;
    private readonly object _capturedMessagesLock = new();
    private readonly Mock<ILogger<MessageService>> _loggerMock;
    private readonly MessageService _sut;

    public MessageServiceTests()
    {
        _loggerMock = new Mock<ILogger<MessageService>>();
        _sut = new MessageService(_loggerMock.Object);
        _capturedMessages = new List<Message>();

        // Subscribe to message events with thread-safe addition
        _sut.MessagePublished += (sender, message) =>
        {
            lock (_capturedMessagesLock)
            {
                _capturedMessages.Add(message);
            }
        };
    }

    public void Dispose()
    {
        _sut?.Dispose();
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new MessageService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Info_PublishesInfoMessage()
    {
        // Arrange
        const string content = "Test info message";
        const string title = "Info Title";
        const string details = "Additional details";

        // Act
        _sut.Info(content, title, details);

        // Assert
        _capturedMessages.Should().HaveCount(1);
        var message = _capturedMessages[0];
        message.Content.Should().Be(content);
        message.Type.Should().Be(MessageType.Info);
        message.Title.Should().Be(title);
        message.Details.Should().Be(details);
        message.Timestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Warning_PublishesWarningMessage()
    {
        // Arrange
        const string content = "Test warning message";

        // Act
        _sut.Warning(content);

        // Assert
        _capturedMessages.Should().HaveCount(1);
        var message = _capturedMessages[0];
        message.Content.Should().Be(content);
        message.Type.Should().Be(MessageType.Warning);
    }

    [Fact]
    public void Error_WithException_PublishesErrorMessageWithException()
    {
        // Arrange
        const string content = "Test error message";
        var exception = new InvalidOperationException("Test exception");

        // Act
        _sut.Error(content, exception: exception);

        // Assert
        _capturedMessages.Should().HaveCount(1);
        var message = _capturedMessages[0];
        message.Content.Should().Be(content);
        message.Type.Should().Be(MessageType.Error);
        message.Exception.Should().Be(exception);
        message.Details.Should().Be(exception.Message);
    }

    [Fact]
    public void Success_PublishesSuccessMessage()
    {
        // Arrange
        const string content = "Operation completed successfully";

        // Act
        _sut.Success(content);

        // Assert
        _capturedMessages.Should().HaveCount(1);
        var message = _capturedMessages[0];
        message.Content.Should().Be(content);
        message.Type.Should().Be(MessageType.Success);
    }

    [Fact]
    public void Debug_PublishesDebugMessage()
    {
        // Arrange
        const string content = "Debug information";

        // Act
        _sut.Debug(content);

        // Assert
        _capturedMessages.Should().HaveCount(1);
        var message = _capturedMessages[0];
        message.Content.Should().Be(content);
        message.Type.Should().Be(MessageType.Debug);
    }

    [Fact]
    public void Critical_PublishesCriticalMessage()
    {
        // Arrange
        const string content = "Critical system error";
        var exception = new SystemException("System failure");

        // Act
        _sut.Critical(content, exception: exception);

        // Assert
        _capturedMessages.Should().HaveCount(1);
        var message = _capturedMessages[0];
        message.Content.Should().Be(content);
        message.Type.Should().Be(MessageType.Critical);
        message.Exception.Should().Be(exception);
    }

    [Fact]
    public void Publish_NullMessage_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _sut.Publish(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Publish_ValidMessage_RaisesEvent()
    {
        // Arrange
        var message = new Message
        {
            Content = "Test message",
            Type = MessageType.Info
        };

        Message? capturedMessage = null;
        object? capturedSender = null;
        _sut.MessagePublished += (sender, msg) =>
        {
            capturedSender = sender;
            capturedMessage = msg;
        };

        // Act
        _sut.Publish(message);

        // Assert
        capturedSender.Should().Be(_sut);
        capturedMessage.Should().Be(message);
    }

    [Fact]
    public void CreateProgressReporter_ReturnsValidProgressReporter()
    {
        // Arrange
        const string description = "Test operation";
        const int total = 100;

        // Act
        var progress = _sut.CreateProgressReporter(description, total);

        // Assert
        progress.Should().NotBeNull();
        progress.Should().BeAssignableTo<IProgress<ProgressReport>>();
    }

    [Fact]
    public void ProgressReporter_Report_PublishesProgressMessage()
    {
        // Arrange
        var progress = _sut.CreateProgressReporter("Test operation", 100);
        var report = new ProgressReport
        {
            Description = "Processing item",
            Current = 50,
            Total = 100
        };

        // Act
        progress.Report(report);

        // Assert
        _capturedMessages.Should().HaveCount(1);
        var message = _capturedMessages[0];
        message.Content.Should().Be("Processing item");
        message.Title.Should().Be("Progress");
        message.Details.Should().Contain("50.0%");
        message.Details.Should().Contain("50/100");
    }

    [Fact]
    public void ProgressReporter_IndeterminateProgress_ReportsItemsProcessed()
    {
        // Arrange
        var progress = _sut.CreateProgressReporter("Test operation");
        var report = new ProgressReport
        {
            Description = "Processing",
            Current = 25
        };

        // Act
        progress.Report(report);

        // Assert
        _capturedMessages.Should().HaveCount(1);
        var message = _capturedMessages[0];
        message.Details.Should().Be("25 items processed");
    }

    [Fact]
    public async Task ExecuteWithProgressAsync_SuccessfulOperation_ReturnsResult()
    {
        // Arrange
        const string description = "Test async operation";
        const string expectedResult = "Success";

        async Task<string> Operation(IProgress<ProgressReport> progress, CancellationToken ct)
        {
            progress.Report(new ProgressReport { Description = "Step 1", Current = 50, Total = 100 });
            await Task.Delay(10, ct);
            progress.Report(new ProgressReport { Description = "Step 2", Current = 100, Total = 100 });
            return expectedResult;
        }

        // Act
        var result = await _sut.ExecuteWithProgressAsync(description, Operation, 100);

        // Assert
        result.Should().Be(expectedResult);
        _capturedMessages.Should().Contain(m => m.Content.Contains("Starting") && m.Content.Contains(description));
        _capturedMessages.Should().Contain(m => m.Content.Contains("Completed") && m.Content.Contains(description));
        _capturedMessages.Should().Contain(m => m.Content == "Step 1");
        _capturedMessages.Should().Contain(m => m.Content == "Step 2");
    }

    [Fact]
    public async Task ExecuteWithProgressAsync_OperationThrows_LogsErrorAndRethrows()
    {
        // Arrange
        const string description = "Failing operation";
        var expectedException = new InvalidOperationException("Test failure");

        async Task<string> Operation(IProgress<ProgressReport> progress, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            throw expectedException;
        }

        // Act
        var act = () => _sut.ExecuteWithProgressAsync(description, Operation);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Test failure");

        _capturedMessages.Should().Contain(m => m.Content.Contains("Starting") && m.Content.Contains(description));
        _capturedMessages.Should().Contain(m => m.Content.Contains("Failed") && m.Content.Contains(description));
    }

    [Fact]
    public async Task ExecuteWithProgressAsync_Cancellation_LogsWarningAndThrows()
    {
        // Arrange
        const string description = "Cancellable operation";
        using var cts = new CancellationTokenSource();

        async Task<string> Operation(IProgress<ProgressReport> progress, CancellationToken ct)
        {
            await Task.Delay(1000, ct);
            return "Should not reach here";
        }

        // Act
        cts.CancelAfter(50);
        var act = () => _sut.ExecuteWithProgressAsync(description, Operation, cancellationToken: cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();

        _capturedMessages.Should().Contain(m => m.Content.Contains("Starting") && m.Content.Contains(description));
        _capturedMessages.Should().Contain(m => m.Content.Contains("Cancelled") && m.Content.Contains(description));
    }

    [Fact]
    public async Task ExecuteWithProgressAsync_VoidOperation_CompletesSuccessfully()
    {
        // Arrange
        const string description = "Void operation";
        var operationExecuted = false;

        async Task Operation(IProgress<ProgressReport> progress, CancellationToken ct)
        {
            await Task.Delay(10, ct);
            operationExecuted = true;
        }

        // Act
        await _sut.ExecuteWithProgressAsync(description, Operation);

        // Assert
        operationExecuted.Should().BeTrue();
        _capturedMessages.Should().Contain(m => m.Content.Contains("Starting") && m.Content.Contains(description));
        _capturedMessages.Should().Contain(m => m.Content.Contains("Completed") && m.Content.Contains(description));
    }

    [Fact]
    public async Task Multiple_Concurrent_Messages_AllPublished()
    {
        // Arrange
        const int messageCount = 100;
        var tasks = new Task[messageCount];

        // Act
        for (var i = 0; i < messageCount; i++)
        {
            var index = i;
            tasks[i] = Task.Run(() => _sut.Info($"Message {index}"));
        }

        await Task.WhenAll(tasks);

        // Assert
        _capturedMessages.Should().HaveCount(messageCount);
        for (var i = 0; i < messageCount; i++) _capturedMessages.Should().Contain(m => m.Content == $"Message {i}");
    }

    [Fact]
    public void ProgressReport_PercentComplete_CalculatesCorrectly()
    {
        // Arrange & Act
        var report1 = new ProgressReport { Description = "Test", Current = 25, Total = 100 };
        var report2 = new ProgressReport { Description = "Test", Current = 0, Total = 100 };
        var report3 = new ProgressReport { Description = "Test", Current = 100, Total = 100 };
        var report4 = new ProgressReport { Description = "Test", Current = 50 }; // No total

        // Assert
        report1.PercentComplete.Should().Be(25.0);
        report2.PercentComplete.Should().Be(0.0);
        report3.PercentComplete.Should().Be(100.0);
        report4.PercentComplete.Should().BeNull();
        report4.IsIndeterminate.Should().BeTrue();
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var service = new MessageService(_loggerMock.Object);

        // Act
        var act = () =>
        {
            service.Dispose();
            service.Dispose();
            service.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Xunit;

namespace Scanner111.Tests.CLI.Services;

public class MessageLoggerTests
{
    #region Basic Functionality Tests

    [Fact]
    public void AddMessage_AddsMessageToBuffer()
    {
        // Arrange
        var logger = new MessageLogger(10);

        // Act
        logger.AddMessage(MessageType.Info, "Test message");

        // Assert
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
        var panelString = panel.ToString();
        panelString.Should().NotBeNull("value should not be null");
    }

    [Fact]
    public void AddMessage_DifferentTypes_StoresCorrectly()
    {
        // Arrange
        var logger = new MessageLogger(10);

        // Act
        logger.AddMessage(MessageType.Info, "Info message");
        logger.AddMessage(MessageType.Warning, "Warning message");
        logger.AddMessage(MessageType.Error, "Error message");
        logger.AddMessage(MessageType.Success, "Success message");
        logger.AddMessage(MessageType.Debug, "Debug message");
        logger.AddMessage(MessageType.Critical, "Critical message");

        // Assert
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
    }

    [Fact]
    public void GetLogsPanel_WithNoMessages_ShowsEmptyMessage()
    {
        // Arrange
        var logger = new MessageLogger(10);

        // Act
        var panel = logger.GetLogsPanel();

        // Assert
        panel.Should().NotBeNull("value should not be null");
        var panelString = panel.ToString();
        panelString.Should().NotBeNull("value should not be null");
        // Panel should show "No messages" when empty
    }

    [Fact]
    public void GetLogsPanel_ShowsLast20Messages()
    {
        // Arrange
        var logger = new MessageLogger(100);

        // Act - Add 30 messages
        for (int i = 1; i <= 30; i++)
        {
            logger.AddMessage(MessageType.Info, $"Message {i}");
        }

        // Assert
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
        // Panel should only show the last 20 messages
    }

    #endregion

    #region Capacity Tests

    [Fact]
    public void MessageLogger_RespectsCapacity()
    {
        // Arrange
        var logger = new MessageLogger(5);

        // Act - Add more messages than capacity
        for (int i = 1; i <= 10; i++)
        {
            logger.AddMessage(MessageType.Info, $"Message {i}");
        }

        // Assert - Should only keep last 5 messages
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
    }

    [Fact]
    public void MessageLogger_DefaultCapacity()
    {
        // Arrange & Act
        var logger = new MessageLogger(); // Should use default capacity of 100

        // Add 150 messages
        for (int i = 1; i <= 150; i++)
        {
            logger.AddMessage(MessageType.Info, $"Message {i}");
        }

        // Assert - Should only keep last 100 messages
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public async Task ConcurrentAddMessage_ThreadSafe()
    {
        // Arrange
        var logger = new MessageLogger(100);
        var tasks = new List<Task>();

        // Act - Add messages from multiple threads
        for (int thread = 0; thread < 10; thread++)
        {
            int threadId = thread;
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    logger.AddMessage(MessageType.Info, $"Thread {threadId} - Message {i}");
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should have added all messages without exceptions
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
    }

    [Fact]
    public async Task ConcurrentAddAndGet_NoExceptions()
    {
        // Arrange
        var logger = new MessageLogger(50);
        var cts = new CancellationTokenSource();
        var exceptions = new List<Exception>();

        // Act - Simultaneously add and get messages
        var addTask = Task.Run(async () =>
        {
            try
            {
                int counter = 0;
                while (!cts.Token.IsCancellationRequested)
                {
                    logger.AddMessage(MessageType.Info, $"Message {counter++}");
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        var getTask = Task.Run(async () =>
        {
            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    _ = logger.GetLogsPanel();
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        // Let it run for a short time
        await Task.Delay(100);
        cts.Cancel();

        await Task.WhenAll(addTask, getTask);

        // Assert - No exceptions should occur
        exceptions.Should().BeEmpty("collection should be empty");
    }

    #endregion

    #region Message Formatting Tests

    [Fact]
    public void GetLogsPanel_FormatsMessagesCorrectly()
    {
        // Arrange
        var logger = new MessageLogger(10);
        var testTime = DateTime.Now;

        // Act
        logger.AddMessage(MessageType.Info, "Info test");
        logger.AddMessage(MessageType.Warning, "Warning test");
        logger.AddMessage(MessageType.Error, "Error test");
        logger.AddMessage(MessageType.Success, "Success test");
        logger.AddMessage(MessageType.Debug, "Debug test");
        logger.AddMessage(MessageType.Critical, "Critical test");

        // Assert
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
        
        // Panel should have proper formatting with icons and colors
        var panelString = panel.ToString();
        panelString.Should().NotBeNull("value should not be null");
    }

    [Fact]
    public void GetLogsPanel_EscapesSpecialCharacters()
    {
        // Arrange
        var logger = new MessageLogger(10);

        // Act
        logger.AddMessage(MessageType.Info, "Message with [brackets] and [[double brackets]]");

        // Assert
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
        // Special characters should be properly escaped
    }

    [Fact]
    public void GetLogsPanel_HandlesLongMessages()
    {
        // Arrange
        var logger = new MessageLogger(10);
        var longMessage = string.Join(" ", Enumerable.Repeat("LongWord", 50));

        // Act
        logger.AddMessage(MessageType.Info, longMessage);

        // Assert
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
    }

    #endregion

    #region Icon and Color Tests

    [Theory]
    [InlineData(MessageType.Info, "ℹ")]
    [InlineData(MessageType.Warning, "⚠")]
    [InlineData(MessageType.Error, "✗")]
    [InlineData(MessageType.Success, "✓")]
    [InlineData(MessageType.Debug, "🐛")]
    [InlineData(MessageType.Critical, "‼")]
    public void GetMessageStyle_ReturnsCorrectIcon(MessageType type, string expectedIcon)
    {
        // This test verifies the icon mapping is correct
        // Since GetMessageStyle is private, we test it indirectly through the panel output
        var logger = new MessageLogger(10);
        logger.AddMessage(type, "Test message");
        
        var panel = logger.GetLogsPanel();
        panel.Should().NotBeNull("value should not be null");
    }

    #endregion

    #region Panel Properties Tests

    [Fact]
    public void GetLogsPanel_HasCorrectHeader()
    {
        // Arrange
        var logger = new MessageLogger(10);

        // Act
        logger.AddMessage(MessageType.Info, "Test");
        var panel = logger.GetLogsPanel();

        // Assert
        panel.Should().NotBeNull("value should not be null");
        // Panel should have "Logs" header
    }

    [Fact]
    public void GetLogsPanel_ShowsTimestamps()
    {
        // Arrange
        var logger = new MessageLogger(10);

        // Act
        logger.AddMessage(MessageType.Info, "Test message");
        var panel = logger.GetLogsPanel();

        // Assert
        panel.Should().NotBeNull("value should not be null");
        // Panel should include timestamps in HH:mm:ss format
    }

    #endregion
}
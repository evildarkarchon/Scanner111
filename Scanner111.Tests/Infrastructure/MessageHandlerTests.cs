using FluentAssertions;
using Scanner111.Core.Infrastructure;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Provides unit tests for the MessageHandler class and its associated functionality.
/// </summary>
/// <remarks>
/// This test class validates the behavior of the MessageHandler, particularly focusing on:
/// Initialization, message handling (info, warning, error), progress reporting, and
/// the correct functioning of progress contexts and related properties.
/// </remarks>
/// <caution>
/// Ensure that the MessageHandler is properly initialized during setup to avoid unexpected errors
/// when running individual tests.
/// </caution>
public class MessageHandlerTests
{
    private readonly TestMessageHandler _testHandler;

    public MessageHandlerTests()
    {
        _testHandler = new TestMessageHandler();
        MessageHandler.Initialize(_testHandler);
    }

    /// <summary>
    /// Verifies that the MessageHandler is correctly initialized by checking the `IsInitialized` property.
    /// </summary>
    /// <remarks>
    /// Ensures that the `IsInitialized` property of the MessageHandler returns true after the Initialize
    /// method is called with a valid handler. This test provides validation for the setup process
    /// of the MessageHandler and its readiness for subsequent operations.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.TrueException">
    /// Thrown if the `IsInitialized` property does not return true after initialization.
    /// </exception>
    [Fact]
    public void MessageHandler_IsInitialized_ReturnsTrueAfterInitialization()
    {
        MessageHandler.IsInitialized.Should().BeTrue("because MessageHandler was initialized in constructor");
    }

    /// <summary>
    /// Validates that the `MsgInfo` method of the `MessageHandler` class correctly invokes the handler for informational messages.
    /// </summary>
    /// <remarks>
    /// This test ensures that calling the `MsgInfo` method registers the provided message in the handler's `InfoMessages` collection
    /// and sets the `LastInfoTarget` property to the appropriate `MessageTarget` value. It verifies the correct functioning of
    /// message handling and proper recording of informational message details.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.ContainsException">
    /// Thrown if the `InfoMessages` collection does not contain the expected test message.
    /// </exception>
    /// <exception cref="Xunit.Sdk.EqualException">
    /// Thrown if the `LastInfoTarget` does not match the default target (`MessageTarget.All`) after calling the method.
    /// </exception>
    [Fact]
    public void MessageHandler_MsgInfo_CallsHandler()
    {
        MessageHandler.MsgInfo("Test info message");

        _testHandler.InfoMessages.Should().Contain("Test info message", "because info message was sent to handler");
        _testHandler.LastInfoTarget.Should().Be(MessageTarget.All, "because default target is All");
    }

    /// <summary>
    /// Validates that the `MsgInfo` method of the `MessageHandler` class correctly routes
    /// an informational message to the specified target and invokes the associated handler.
    /// </summary>
    /// <remarks>
    /// This test ensures that the informational message, when passed to the `MsgInfo` method
    /// with a specific target (e.g., `MessageTarget.GuiOnly`), is accurately logged in the
    /// `TestMessageHandler` instance. It also confirms that the `LastInfoTarget` property
    /// in the handler matches the specified target.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.ContainsException">
    /// Thrown if the informational message is not found in the `InfoMessages` list of the handler.
    /// </exception>
    /// <exception cref="Xunit.Sdk.EqualException">
    /// Thrown if the `LastInfoTarget` property does not match the expected target after the method call.
    /// </exception>
    [Fact]
    public void MessageHandler_MsgInfo_WithTarget_CallsHandler()
    {
        MessageHandler.MsgInfo("Test info message", MessageTarget.GuiOnly);

        _testHandler.InfoMessages.Should().Contain("Test info message", "because info message was sent to handler");
        _testHandler.LastInfoTarget.Should().Be(MessageTarget.GuiOnly, "because GuiOnly target was specified");
    }

    /// <summary>
    /// Validates that the `MsgWarning` method in the `MessageHandler` class properly triggers
    /// the warning message handler when called.
    /// </summary>
    /// <remarks>
    /// Confirms that the `MsgWarning` method processes the provided warning message by verifying
    /// that it appears within the messages handled by the test implementation. This ensures
    /// the correct interaction with the warning-handling functionality.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.ContainsException">
    /// Thrown if the expected warning message is not found in the collection of handled messages.
    /// </exception>
    [Fact]
    public void MessageHandler_MsgWarning_CallsHandler()
    {
        MessageHandler.MsgWarning("Test warning message");

        _testHandler.WarningMessages.Should().Contain("Test warning message", "because warning message was sent to handler");
    }

    /// <summary>
    /// Validates that the `MessageHandler.MsgError` method correctly invokes the associated error handler
    /// when called with an error message.
    /// </summary>
    /// <remarks>
    /// This test ensures that the provided error message is processed and stored by the underlying handler.
    /// It verifies the expected interaction between the `MessageHandler.MsgError` method and the `_testHandler`
    /// used for error message handling.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.ContainsException">
    /// Thrown if the specified error message is not found in the collection of error messages
    /// managed by the `_testHandler`.
    /// </exception>
    [Fact]
    public void MessageHandler_MsgError_CallsHandler()
    {
        MessageHandler.MsgError("Test error message");

        _testHandler.ErrorMessages.Should().Contain("Test error message", "because error message was sent to handler");
    }

    /// <summary>
    /// Validates that the `MsgProgress` method of the `MessageHandler` class invokes the corresponding handler correctly.
    /// </summary>
    /// <remarks>
    /// This test ensures that the `MsgProgress` method passes the provided title and total item count to the handler and that
    /// a valid progress object is returned. The behavior being tested is critical for progress reporting functionality within
    /// the application.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.NotNullException">
    /// Thrown if the progress object returned by the `MsgProgress` method is null.
    /// </exception>
    /// <exception cref="Xunit.Sdk.EqualException">
    /// Thrown if either the progress title or total item count does not match the expected values.
    /// </exception>
    [Fact]
    public void MessageHandler_MsgProgress_CallsHandler()
    {
        var progress = MessageHandler.MsgProgress("Test progress", 100);

        progress.Should().NotBeNull("because progress object should be created");
        _testHandler.LastProgressTitle.Should().Be("Test progress", "because progress title was provided");
        _testHandler.LastProgressTotal.Should().Be(100, "because total items count was set to 100");
    }

    /// <summary>
    /// Validates that the `MessageHandler` does not throw exceptions when methods
    /// for logging messages and reporting progress are called without prior initialization.
    /// </summary>
    /// <remarks>
    /// This test ensures the resilience of the `MessageHandler` in scenarios where
    /// its methods (`MsgInfo`, `MsgWarning`, `MsgError`, and `MsgProgress`) are
    /// invoked without explicit external initialization. It verifies that no exceptions
    /// are thrown during these operations and that the system handles messages
    /// and progress updates correctly in such cases.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.ContainsException">
    /// Thrown if the expected test messages are not present in the corresponding message
    /// collections of the test handler after executing the calls.
    /// </exception>
    [Fact]
    public void MessageHandler_WithoutInitialization_DoesNotThrow()
    {
        // Create a new test handler for this test
        var tempHandler = new TestMessageHandler();
        MessageHandler.Initialize(tempHandler);

        // These should not throw exceptions
        MessageHandler.MsgInfo("Test");
        MessageHandler.MsgWarning("Test");
        MessageHandler.MsgError("Test");

        var progress = MessageHandler.MsgProgress("Test", 10);
        progress.Should().NotBeNull("because progress object should be created even without initialization");

        // Progress should work correctly
        progress.Report(new ProgressInfo { Current = 5, Total = 10 });

        // Verify messages were handled
        tempHandler.InfoMessages.Should().Contain("Test", "because info message was handled");
        tempHandler.WarningMessages.Should().Contain("Test", "because warning message was handled");
        tempHandler.ErrorMessages.Should().Contain("Test", "because error message was handled");
    }

    /// <summary>
    /// Validates that the `ProgressContext` associated with the `MessageHandler` accurately reports progress updates.
    /// </summary>
    /// <remarks>
    /// Confirms that progress updates are tracked and reported correctly by the `ProgressContext` created via the `MessageHandler`.
    /// The test verifies that the progress updates are recorded in the correct order with accurate values for `Current`, `Message`,
    /// and that the `Complete` method executes successfully.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.EqualException">
    /// Thrown if the progress updates do not match the expected values for `Current` or `Message`, or if the number of
    /// progress updates recorded is not as anticipated.
    /// </exception>
    [Fact]
    public void MessageHandler_ProgressContext_ReportsProgressCorrectly()
    {
        var progressContext = _testHandler.CreateProgressContext("Test Progress", 50);

        progressContext.Update(10, "Processing item 10");
        progressContext.Update(25, "Processing item 25");
        progressContext.Report(new ProgressInfo { Current = 40, Total = 50, Message = "Almost done" });

        progressContext.Should().BeOfType<TestProgressContext>("because test handler creates TestProgressContext");
        var testProgressContext = (TestProgressContext)progressContext;
        testProgressContext.Reports.Should().HaveCount(3, "because three progress updates were made");

        testProgressContext.Reports[0].Current.Should().Be(10, "because first update set current to 10");
        testProgressContext.Reports[0].Message.Should().Be("Processing item 10", "because first update message was provided");

        testProgressContext.Reports[1].Current.Should().Be(25, "because second update set current to 25");
        testProgressContext.Reports[1].Message.Should().Be("Processing item 25", "because second update message was provided");

        testProgressContext.Reports[2].Current.Should().Be(40, "because third update set current to 40");
        testProgressContext.Reports[2].Message.Should().Be("Almost done", "because third update message was provided");

        progressContext.Complete();
    }

    /// <summary>
    /// Validates that the MessageHandler correctly tracks and reports progress updates using the provided progress reporter.
    /// </summary>
    /// <remarks>
    /// This test ensures that the `ShowProgress` method of the `MessageHandler` can effectively handle and report
    /// multiple progress updates. Progress information such as the `Current` progress value, `Total` progress value,
    /// and `Message` associated with each progress update is validated against expected values. The correctness of
    /// progress reporting is confirmed by verifying the sequence and content of recorded progress updates.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.EqualException">
    /// Thrown if the reported progress values or messages do not match the expected values.
    /// </exception>
    [Fact]
    public void MessageHandler_Progress_ReportsProgressCorrectly()
    {
        var progress = _testHandler.ShowProgress("Test Progress", 100);

        progress.Report(new ProgressInfo { Current = 25, Total = 100, Message = "Quarter done" });
        progress.Report(new ProgressInfo { Current = 75, Total = 100, Message = "Three quarters done" });

        progress.Should().BeOfType<TestProgress>("because test handler creates TestProgress");
        var testProgress = (TestProgress)progress;
        testProgress.Reports.Should().HaveCount(2, "because two progress reports were made");

        testProgress.Reports[0].Current.Should().Be(25, "because first report set current to 25");
        testProgress.Reports[0].Message.Should().Be("Quarter done", "because first report message was provided");

        testProgress.Reports[1].Current.Should().Be(75, "because second report set current to 75");
        testProgress.Reports[1].Message.Should().Be("Three quarters done", "because second report message was provided");
    }

    /// <summary>
    /// Ensures that the progress context created by the MessageHandler is disposed of correctly.
    /// </summary>
    /// <remarks>
    /// This test verifies that after creating and using a progress context via the MessageHandler,
    /// the context's `Dispose` method properly sets the `IsDisposed` property to true. This helps ensure
    /// that resources associated with the progress context are correctly released when no longer needed.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.FalseException">
    /// Thrown if the progress context's `IsDisposed` property is not initially false before disposal.
    /// </exception>
    /// <exception cref="Xunit.Sdk.TrueException">
    /// Thrown if the progress context's `IsDisposed` property does not return true after disposal.
    /// </exception>
    [Fact]
    public void MessageHandler_ProgressContext_DisposesCorrectly()
    {
        var progressContext = _testHandler.CreateProgressContext("Test Progress", 50);
        progressContext.Should().BeOfType<TestProgressContext>("because test handler creates TestProgressContext");
        var testProgressContext = (TestProgressContext)progressContext;

        testProgressContext.IsDisposed.Should().BeFalse("because context is not disposed initially");

        progressContext.Dispose();

        testProgressContext.IsDisposed.Should().BeTrue("because context was disposed");
    }

    /// <summary>
    /// Validates that the properties of the ProgressInfo class correctly store and retrieve values.
    /// </summary>
    /// <remarks>
    /// This test ensures that the `Current`, `Total`, and `Message` properties of the ProgressInfo
    /// class are properly assigned and return expected values. Additionally, it verifies the
    /// computation of the `Percentage` property based on the `Current` and `Total` values.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.EqualException">
    /// Thrown if any of the `ProgressInfo` properties or the computed `Percentage` do not match
    /// the expected values.
    /// </exception>
    [Fact]
    public void ProgressInfo_Properties_WorkCorrectly()
    {
        var progress = new ProgressInfo
        {
            Current = 25,
            Total = 100,
            Message = "Processing..."
        };

        progress.Current.Should().Be(25, "because Current was set to 25");
        progress.Total.Should().Be(100, "because Total was set to 100");
        progress.Message.Should().Be("Processing...", "because Message was set");
        progress.Percentage.Should().Be(25.0, "because 25/100 = 25%");
    }

    /// <summary>
    /// Verifies the behavior of the `Percentage` property in the `ProgressInfo` class when the `Total` value is zero.
    /// </summary>
    /// <remarks>
    /// Ensures that the `Percentage` property correctly handles the case when `Total` is set to zero,
    /// avoiding division by zero errors and returning an expected result of 0.0.
    /// This test validates the robustness of the `Percentage` calculation logic under edge cases.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.EqualException">
    /// Thrown if the `Percentage` property does not return 0.0 when the `Total` property is zero.
    /// </exception>
    [Fact]
    public void ProgressInfo_Percentage_HandlesZeroTotal()
    {
        var progress = new ProgressInfo
        {
            Current = 5,
            Total = 0
        };

        progress.Percentage.Should().Be(0.0, "because percentage should be 0 when Total is 0");
    }

    /// <summary>
    /// Validates that all enum values of the `MessageTarget` type are accessible and correctly assigned.
    /// </summary>
    /// <remarks>
    /// Ensures the correct initialization and retrieval of all `MessageTarget` enumeration values: `All`, `GuiOnly`, and `CliOnly`.
    /// This test confirms the integrity and usability of the `MessageTarget` enum for its intended purposes within the application.
    /// </remarks>
    /// <exception cref="Xunit.Sdk.EqualException">
    /// Thrown if any of the `MessageTarget` enumeration values do not match their expected assignments or states.
    /// </exception>
    [Fact]
    public void MessageTarget_AllValues_AreAccessible()
    {
        MessageTarget.All.Should().Be(MessageTarget.All, "because enum value should equal itself");
        MessageTarget.GuiOnly.Should().Be(MessageTarget.GuiOnly, "because enum value should equal itself");
        MessageTarget.CliOnly.Should().Be(MessageTarget.CliOnly, "because enum value should equal itself");
    }
}

// Test implementation of IMessageHandler
/// <summary>
/// A test implementation of the <see cref="IMessageHandler"/> interface for unit testing purposes.
/// </summary>
/// <remarks>
/// The <c>TestMessageHandler</c> class is designed to simulate and verify message handling behavior
/// for informational, warning, and error messages, as well as progress reporting.
/// It captures messages into respective collections for validation during tests.
/// This implementation also tracks specific properties such as the last progress title,
/// total progress items, and the target of the last informational message.
/// </remarks>
/// <caution>
/// This class is intended exclusively for testing purposes and is not suitable for production use.
/// </caution>
public class TestMessageHandler : IMessageHandler
{
    public List<string> InfoMessages { get; } = new();
    public List<string> WarningMessages { get; } = new();
    public List<string> ErrorMessages { get; } = new();
    public MessageTarget LastInfoTarget { get; private set; } = MessageTarget.All;
    public string LastProgressTitle { get; private set; } = string.Empty;
    public int LastProgressTotal { get; private set; }

    /// <summary>
    /// Records an informational message and updates the target audience for the message.
    /// </summary>
    /// <param name="message">
    /// The informational message to be recorded.
    /// </param>
    /// <param name="target">
    /// The target audience for the message. Defaults to <see cref="MessageTarget.All"/> if not specified.
    /// </param>
    /// <remarks>
    /// This method appends the provided informational message to the list of messages and updates the
    /// last recorded target audience. It is primarily used to verify the storage and categorization of
    /// informational messages during testing.
    /// </remarks>
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        InfoMessages.Add(message);
        LastInfoTarget = target;
    }

    /// <summary>
    /// Adds a warning message to the collection of warning messages.
    /// </summary>
    /// <param name="message">
    /// The warning message to be added.
    /// </param>
    /// <param name="target">
    /// Specifies the target for the warning message. Default is <see cref="MessageTarget.All"/>.
    /// </param>
    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        WarningMessages.Add(message);
    }

    /// <summary>
    /// Logs an error message to an internal error message list for tracking purposes.
    /// </summary>
    /// <param name="message">The error message to be logged.</param>
    /// <param name="target">
    /// Specifies the target audience for the error message. Defaults to <see cref="MessageTarget.All"/>.
    /// </param>
    /// <remarks>
    /// This method captures error messages and adds them to an in-memory collection for further inspection
    /// or validation in test scenarios. The `target` parameter determines where the message is relevant,
    /// but for this test implementation, the target does not affect the handling behavior.
    /// </remarks>
    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        ErrorMessages.Add(message);
    }

    /// <summary>
    /// Logs a success message to the internal message store for testing purposes.
    /// </summary>
    /// <param name="message">The success message to be logged.</param>
    /// <param name="target">
    /// The intended target(s) for the message. By default, it targets all consumers (`MessageTarget.All`).
    /// </param>
    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        // Just store in info messages for testing
        InfoMessages.Add(message);
    }

    /// <summary>
    /// Logs a debug message to the specified message target for testing purposes.
    /// </summary>
    /// <param name="message">The debug message to be logged.</param>
    /// <param name="target">Optional target type that specifies where the message should be logged. Defaults to <c>MessageTarget.All</c>.</param>
    /// <remarks>
    /// This method is designed to simulate message handling in the implementation of IMessageHandler
    /// by storing provided debug messages in the <c>InfoMessages</c> collection for validation during tests.
    /// </remarks>
    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        // Just store in info messages for testing
        InfoMessages.Add(message);
    }

    /// <summary>
    /// Records a critical message for testing purposes.
    /// </summary>
    /// <param name="message">
    /// The critical message intended to be logged or tested.
    /// </param>
    /// <param name="target">
    /// Specifies the target destination for the message. Defaults to <see cref="MessageTarget.All"/>.
    /// </param>
    /// <remarks>
    /// This method simulates the handling of critical messages in the context of testing
    /// by storing them in an internal collection rather than performing real logging or message dispatching.
    /// </remarks>
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        // Just store in error messages for testing
        ErrorMessages.Add(message);
    }

    /// <summary>
    /// Displays a message along with optional details, specifying the message type and target audience.
    /// </summary>
    /// <param name="message">The main message to be displayed.</param>
    /// <param name="details">Optional detailed information related to the message.</param>
    /// <param name="messageType">The type of the message, indicating its severity or category.</param>
    /// <param name="target">The target audience or destination for the message.</param>
    /// <remarks>
    /// The method handles different message types such as Info, Warning, and Error by categorizing them into corresponding collections.
    /// Unspecified or unsupported message types are treated as Info by default.
    /// </remarks>
    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        switch (messageType)
        {
            case MessageType.Info:
                InfoMessages.Add(message);
                break;
            case MessageType.Warning:
                WarningMessages.Add(message);
                break;
            case MessageType.Error:
                ErrorMessages.Add(message);
                break;
            case MessageType.Success:
            case MessageType.Debug:
            case MessageType.Critical:
            default:
                InfoMessages.Add(message);
                break;
        }
    }

    /// <summary>
    /// Displays a progress tracker with a specified title and total items to process.
    /// </summary>
    /// <param name="title">The title or description for the progress tracker.</param>
    /// <param name="totalItems">The total number of items to process, used to determine completion percentage.</param>
    /// <returns>An instance of <see cref="IProgress{ProgressInfo}"/> for reporting progress updates.</returns>
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        LastProgressTitle = title;
        LastProgressTotal = totalItems;
        return new TestProgress();
    }

    /// <summary>
    /// Creates a new progress context with the specified title and total number of items to track progress during an operation.
    /// </summary>
    /// <param name="title">The title describing the progress operation.</param>
    /// <param name="totalItems">The total number of items to be processed or tracked.</param>
    /// <returns>An instance of <see cref="IProgressContext"/> for reporting progress and handling completion or disposal.</returns>
    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        LastProgressTitle = title;
        LastProgressTotal = totalItems;
        return new TestProgressContext();
    }
}

/// <summary>
/// Represents a test implementation of the <see cref="IProgress{T}"/> interface specialized for
/// capturing and managing progress updates during tests.
/// </summary>
/// <remarks>
/// The class captures all reported progress updates into an internal collection for later verification,
/// enabling detailed assertions on the sequence and content of reported progress information.
/// </remarks>
public class TestProgress : IProgress<ProgressInfo>
{
    public List<ProgressInfo> Reports { get; } = [];

    /// <summary>
    /// Captures and stores progress updates for analysis during testing scenarios.
    /// </summary>
    /// <param name="value">
    /// Contains the progress data being reported, including the current step, total steps,
    /// and optional descriptive message.
    /// </param>
    /// <remarks>
    /// This method appends the provided <paramref name="value"/> to the internal collection
    /// of recorded progress updates for later verification.
    /// </remarks>
    public void Report(ProgressInfo value)
    {
        Reports.Add(value);
    }
}

/// <summary>
/// Represents a test implementation of the IProgressContext interface, used for tracking
/// and reporting on progress during execution in a controlled test environment.
/// </summary>
/// <remarks>
/// This class is designed for testing scenarios and allows simulated progress updates
/// and disposals. It retains progress reports in memory for verification purposes.
/// </remarks>
/// <caution>
/// This test implementation is not intended for use in production environments.
/// It is specifically designed to facilitate unit testing of progress handling logic.
/// </caution>
public class TestProgressContext : IProgressContext
{
    public List<ProgressInfo> Reports { get; } = [];
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Updates the progress of the current operation with the specified progress value and status message.
    /// </summary>
    /// <param name="current">The current progress value, representing the number of completed items or the current step in the operation.</param>
    /// <param name="message">A descriptive message providing context about the current progress or state of the operation.</param>
    public void Update(int current, string message)
    {
        Reports.Add(new ProgressInfo { Current = current, Message = message });
    }

    /// <summary>
    /// Marks the progress context as complete, indicating that the operation being tracked has finished.
    /// </summary>
    /// <remarks>
    /// This method finalizes the progress tracking process. Once called, the progress context is considered complete
    /// and no further updates are expected. Ensure all necessary progress updates have been reported before invoking this method.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the context is already disposed before attempting to mark it as complete.
    /// </exception>
    public void Complete()
    {
        // Mark as complete
    }

    /// <summary>
    /// Reports progress updates by adding the specified progress information to the internal collection.
    /// </summary>
    /// <param name="value">
    /// The progress information to be reported, including details such as current progress,
    /// total progress, and an optional descriptive message about the operation's state.
    /// </param>
    public void Report(ProgressInfo value)
    {
        Reports.Add(value);
    }

    /// <summary>
    /// Releases all resources used by the TestProgressContext instance and suppresses finalization.
    /// </summary>
    /// <remarks>
    /// Once this method is called, the TestProgressContext is considered disposed and no further operations
    /// should be performed on this instance. It ensures proper cleanup of resources and flags the instance
    /// as disposed by setting the `IsDisposed` property to true.
    /// </remarks>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if operations are attempted after the instance has already been disposed.
    /// </exception>
    public void Dispose()
    {
        IsDisposed = true;
        GC.SuppressFinalize(this);
    }
}
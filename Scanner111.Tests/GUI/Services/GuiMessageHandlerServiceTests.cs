using System;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;
using Scanner111.GUI.Services;
using Scanner111.GUI.ViewModels;
using Xunit;

namespace Scanner111.Tests.GUI.Services;

/// <summary>
/// Tests for GuiMessageHandlerService implementation.
/// These tests verify that the service correctly forwards messages to the MainWindowViewModel
/// and properly handles progress reporting.
/// </summary>
public class GuiMessageHandlerServiceTests
{
    private readonly GuiMessageHandlerService _service;
    private readonly Mock<MainWindowViewModel> _mockViewModel;

    public GuiMessageHandlerServiceTests()
    {
        _service = new GuiMessageHandlerService();
        
        // Create a mock ViewModel with virtual properties
        // Note: MainWindowViewModel properties need to be virtual for mocking
        // For now, we'll use a test double instead
        _mockViewModel = null!; // Will use TestMainWindowViewModel instead
    }

    [Fact]
    public void SetViewModel_StoresViewModelReference()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        
        // Act
        _service.SetViewModel(viewModel);
        
        // Assert - We can verify by calling a method and checking if it affects the view model
        _service.ShowInfo("Test message");
        viewModel.StatusText.Should().Be("Test message", "because the view model was set");
    }

    [Fact]
    public void ShowInfo_UpdatesViewModelStatusText()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        _service.ShowInfo("Information message");
        
        // Assert
        viewModel.StatusText.Should().Be("Information message", "because info messages update status directly");
    }

    [Fact]
    public void ShowWarning_PrependsWarningToStatusText()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        _service.ShowWarning("Warning message");
        
        // Assert
        viewModel.StatusText.Should().Be("Warning: Warning message", "because warning messages are prefixed");
    }

    [Fact]
    public void ShowError_PrependsErrorToStatusText()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        _service.ShowError("Error message");
        
        // Assert
        viewModel.StatusText.Should().Be("Error: Error message", "because error messages are prefixed");
    }

    [Fact]
    public void ShowSuccess_UpdatesViewModelStatusText()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        _service.ShowSuccess("Success message");
        
        // Assert
        viewModel.StatusText.Should().Be("Success message", "because success messages update status directly");
    }

    [Fact]
    public void ShowDebug_PrependsDebugToStatusText()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        _service.ShowDebug("Debug message");
        
        // Assert
        viewModel.StatusText.Should().Be("Debug: Debug message", "because debug messages are prefixed");
    }

    [Fact]
    public void ShowCritical_PrependsCriticalToStatusText()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        _service.ShowCritical("Critical message");
        
        // Assert
        viewModel.StatusText.Should().Be("CRITICAL: Critical message", "because critical messages are prefixed with CRITICAL");
    }

    [Theory]
    [InlineData(MessageType.Info, "", "Test message")]
    [InlineData(MessageType.Warning, "Warning: ", "Test message")]
    [InlineData(MessageType.Error, "Error: ", "Test message")]
    [InlineData(MessageType.Success, "", "Test message")]
    [InlineData(MessageType.Debug, "Debug: ", "Test message")]
    [InlineData(MessageType.Critical, "CRITICAL: ", "Test message")]
    public void ShowMessage_AppliesCorrectPrefixByType(MessageType messageType, string expectedPrefix, string message)
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        _service.ShowMessage(message, null, messageType);
        
        // Assert
        viewModel.StatusText.Should().Be($"{expectedPrefix}{message}", 
            "because message type determines the prefix");
    }

    [Fact]
    public void ShowMessage_IncludesDetailsWhenProvided()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        _service.ShowMessage("Main message", "Additional details", MessageType.Info);
        
        // Assert
        viewModel.StatusText.Should().Be("Main message - Additional details", 
            "because details are appended with a separator");
    }

    [Fact]
    public void ShowMessage_WithoutViewModel_DoesNotThrow()
    {
        // Arrange - No view model set
        
        // Act
        var action = () => _service.ShowMessage("Test message");
        
        // Assert
        action.Should().NotThrow("because the service should handle null view model gracefully");
    }

    [Fact]
    public void ShowProgress_ReturnsValidProgressInstance()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        var progress = _service.ShowProgress("Test Progress", 100);
        
        // Assert
        progress.Should().NotBeNull("because a progress instance should be created");
        progress.Should().BeAssignableTo<IProgress<ProgressInfo>>("because it should implement IProgress");
    }

    [Fact]
    public void ShowProgress_UpdatesViewModelWhenReported()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        var progress = _service.ShowProgress("Test Progress", 100);
        
        // Act
        progress.Report(new ProgressInfo 
        { 
            Current = 50, 
            Total = 100, 
            Message = "Processing..." 
        });
        
        // Assert
        viewModel.ProgressText.Should().Be("Test Progress: Processing...", 
            "because progress updates should include title and message");
        viewModel.ProgressValue.Should().Be(50.0, "because progress percentage should be calculated");
        viewModel.ProgressVisible.Should().BeTrue("because progress should be visible when active");
    }

    [Fact]
    public void CreateProgressContext_ReturnsDisposableContext()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        var context = _service.CreateProgressContext("Test Context", 100);
        
        // Assert
        context.Should().NotBeNull("because a context should be created");
        context.Should().BeAssignableTo<IProgressContext>("because it should implement IProgressContext");
        context.Should().BeAssignableTo<IDisposable>("because progress context should be disposable");
    }

    [Fact]
    public void ProgressContext_UpdatesViewModel()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act
        using (var context = _service.CreateProgressContext("Test Context", 100))
        {
            context.Update(25, "Step 1");
            
            // Assert - During progress
            viewModel.ProgressText.Should().Be("Test Context: Step 1", 
                "because context updates should update view model");
            viewModel.ProgressValue.Should().Be(25.0, "because progress should reflect current/total ratio");
            viewModel.ProgressVisible.Should().BeTrue("because progress should be visible during operation");
        }
        
        // Assert - After disposal
        viewModel.ProgressVisible.Should().BeFalse("because progress should be hidden after disposal");
    }

    [Fact]
    public void ProgressContext_Complete_SetsFullProgress()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        var context = _service.CreateProgressContext("Test Context", 100);
        
        // Act
        context.Complete();
        
        // Assert
        viewModel.ProgressText.Should().Be("Test Context: Completed", 
            "because complete should show completion message");
        viewModel.ProgressValue.Should().Be(100.0, "because complete should set progress to 100%");
    }

    [Fact]
    public void MessageTarget_ParameterIsIgnored()
    {
        // Arrange
        var viewModel = new TestMainWindowViewModel();
        _service.SetViewModel(viewModel);
        
        // Act - Test that different MessageTarget values produce the same result
        _service.ShowInfo("Message 1", MessageTarget.All);
        var result1 = viewModel.StatusText;
        
        _service.ShowInfo("Message 2", MessageTarget.GuiOnly);
        var result2 = viewModel.StatusText;
        
        _service.ShowInfo("Message 3", MessageTarget.CliOnly);
        var result3 = viewModel.StatusText;
        
        // Assert
        result1.Should().Be("Message 1", "because target is ignored in GUI handler");
        result2.Should().Be("Message 2", "because target is ignored in GUI handler");
        result3.Should().Be("Message 3", "because target is ignored in GUI handler");
    }

    /// <summary>
    /// Test implementation of MainWindowViewModel for service testing.
    /// This avoids the need for complex mocking of non-virtual properties.
    /// </summary>
    private class TestMainWindowViewModel : MainWindowViewModel
    {
        public TestMainWindowViewModel() : base(
            new Mock<ISettingsService>().Object,
            new Mock<GuiMessageHandlerService>().Object,
            new Mock<IUpdateService>().Object,
            new Mock<ICacheManager>().Object,
            new Mock<IUnsolvedLogsMover>().Object)
        {
            // Override async initialization to prevent side effects in tests
        }
        
        // Properties are already settable in base class, no override needed
    }
}
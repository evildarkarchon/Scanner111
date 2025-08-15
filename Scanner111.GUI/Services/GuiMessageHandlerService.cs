using System;
using Scanner111.Core.Infrastructure;
using Scanner111.GUI.ViewModels;

namespace Scanner111.GUI.Services;

/// <summary>
///     GUI-specific implementation of IMessageHandler that forwards messages to the MainWindowViewModel
///     for display in the user interface.
/// </summary>
public class GuiMessageHandlerService : IMessageHandler
{
    private MainWindowViewModel? _viewModel;

    /// <summary>
    ///     Shows an informational message.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="target">The target for the message (currently not used in GUI).</param>
    public void ShowInfo(string message, MessageTarget target = MessageTarget.All)
    {
        if (_viewModel != null)
            _viewModel.StatusText = message;
    }

    /// <summary>
    ///     Shows a warning message.
    /// </summary>
    /// <param name="message">The warning message to display.</param>
    /// <param name="target">The target for the message (currently not used in GUI).</param>
    public void ShowWarning(string message, MessageTarget target = MessageTarget.All)
    {
        if (_viewModel != null)
            _viewModel.StatusText = $"Warning: {message}";
    }

    /// <summary>
    ///     Shows an error message.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    /// <param name="target">The target for the message (currently not used in GUI).</param>
    public void ShowError(string message, MessageTarget target = MessageTarget.All)
    {
        if (_viewModel != null)
            _viewModel.StatusText = $"Error: {message}";
    }

    /// <summary>
    ///     Shows a success message.
    /// </summary>
    /// <param name="message">The success message to display.</param>
    /// <param name="target">The target for the message (currently not used in GUI).</param>
    public void ShowSuccess(string message, MessageTarget target = MessageTarget.All)
    {
        if (_viewModel != null)
            _viewModel.StatusText = message;
    }

    /// <summary>
    ///     Shows a debug message.
    /// </summary>
    /// <param name="message">The debug message to display.</param>
    /// <param name="target">The target for the message (currently not used in GUI).</param>
    public void ShowDebug(string message, MessageTarget target = MessageTarget.All)
    {
        // For GUI, we might want to ignore debug messages or log them separately
        // For now, just update status if in debug mode
        if (_viewModel != null)
            _viewModel.StatusText = $"Debug: {message}";
    }

    /// <summary>
    ///     Shows a critical error message.
    /// </summary>
    /// <param name="message">The critical error message to display.</param>
    /// <param name="target">The target for the message (currently not used in GUI).</param>
    public void ShowCritical(string message, MessageTarget target = MessageTarget.All)
    {
        if (_viewModel != null)
            _viewModel.StatusText = $"CRITICAL: {message}";
    }

    /// <summary>
    ///     Shows a message with optional details.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="details">Optional details for the message.</param>
    /// <param name="messageType">The type of message.</param>
    /// <param name="target">The target for the message (currently not used in GUI).</param>
    public void ShowMessage(string message, string? details = null, MessageType messageType = MessageType.Info,
        MessageTarget target = MessageTarget.All)
    {
        if (_viewModel == null) return;

        var prefix = messageType switch
        {
            MessageType.Warning => "Warning: ",
            MessageType.Error => "Error: ",
            MessageType.Success => "",
            MessageType.Debug => "Debug: ",
            MessageType.Critical => "CRITICAL: ",
            _ => ""
        };

        var fullMessage = details != null ? $"{prefix}{message} - {details}" : $"{prefix}{message}";
        _viewModel.StatusText = fullMessage;
    }

    /// <summary>
    ///     Shows a progress indicator.
    /// </summary>
    /// <param name="title">The title for the progress indicator.</param>
    /// <param name="totalItems">The total number of items to process.</param>
    /// <returns>An IProgress instance for reporting progress.</returns>
    public IProgress<ProgressInfo> ShowProgress(string title, int totalItems)
    {
        return new GuiProgress(_viewModel, title, totalItems);
    }

    /// <summary>
    ///     Creates a progress context for use with 'using' statements.
    /// </summary>
    /// <param name="title">The title for the progress context.</param>
    /// <param name="totalItems">The total number of items to process.</param>
    /// <returns>An IProgressContext instance.</returns>
    public IProgressContext CreateProgressContext(string title, int totalItems)
    {
        return new GuiProgressContext(_viewModel, title, totalItems);
    }

    /// <summary>
    ///     Sets the view model to receive message notifications.
    /// </summary>
    /// <param name="viewModel">The MainWindowViewModel to receive messages.</param>
    public void SetViewModel(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
    }
}

/// <summary>
///     GUI-specific progress implementation that updates the MainWindowViewModel.
/// </summary>
internal class GuiProgress : IProgress<ProgressInfo>
{
    private readonly string _title;
    private readonly int _totalItems;
    private readonly MainWindowViewModel? _viewModel;

    public GuiProgress(MainWindowViewModel? viewModel, string title, int totalItems)
    {
        _viewModel = viewModel;
        _title = title;
        _totalItems = totalItems;
    }

    public void Report(ProgressInfo value)
    {
        if (_viewModel == null) return;

        _viewModel.ProgressText = $"{_title}: {value.Message}";
        _viewModel.ProgressValue = value.Percentage;
        _viewModel.ProgressVisible = true;
    }
}

/// <summary>
///     GUI-specific progress context implementation.
/// </summary>
internal class GuiProgressContext : IProgressContext
{
    private readonly GuiProgress _progress;
    private readonly int _totalItems;
    private readonly MainWindowViewModel? _viewModel;
    private bool _disposed;

    public GuiProgressContext(MainWindowViewModel? viewModel, string title, int totalItems)
    {
        _viewModel = viewModel;
        _totalItems = totalItems;
        _progress = new GuiProgress(viewModel, title, totalItems);
    }

    public void Report(ProgressInfo value)
    {
        if (!_disposed)
            _progress.Report(value);
    }

    public void Update(int current, string message)
    {
        if (!_disposed)
        {
            var progressInfo = new ProgressInfo
            {
                Current = current,
                Total = _totalItems,
                Message = message
            };
            _progress.Report(progressInfo);
        }
    }

    public void Complete()
    {
        if (!_disposed)
        {
            var progressInfo = new ProgressInfo
            {
                Current = _totalItems,
                Total = _totalItems,
                Message = "Completed"
            };
            _progress.Report(progressInfo);
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            // Hide progress when done
            if (_viewModel != null)
                _viewModel.ProgressVisible = false;
        }
    }
}
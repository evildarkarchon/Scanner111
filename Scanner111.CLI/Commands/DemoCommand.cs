using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Commands;

public class DemoCommand : ICommand<DemoOptions>
{
    private readonly IMessageHandler _messageHandler;

    public DemoCommand(IMessageHandler messageHandler)
    {
        _messageHandler = Guard.NotNull(messageHandler, nameof(messageHandler));
    }

    /// <summary>
    ///     Executes the command logic asynchronously using the provided options.
    /// </summary>
    /// <param name="options">The options required to execute the command.</param>
    /// <returns>A task representing the asynchronous operation, returning an integer status code.</returns>
    public Task<int> ExecuteAsync(DemoOptions options)
    {
        _messageHandler.ShowInfo("This is an info message");
        _messageHandler.ShowWarning("This is a warning message");
        _messageHandler.ShowError("This is an error message");
        _messageHandler.ShowSuccess("This is a success message");
        _messageHandler.ShowDebug("This is a debug message");
        _messageHandler.ShowCritical("This is a critical message");

        // Demo progress
        using var progress = _messageHandler.CreateProgressContext("Demo Progress", 5);
        for (var i = 1; i <= 5; i++)
        {
            progress.Update(i, $"Step {i} of 5");
            Thread.Sleep(500); // Simulate work
        }

        progress.Complete();

        _messageHandler.ShowSuccess("Demo complete!");
        return Task.FromResult(0);
    }
}
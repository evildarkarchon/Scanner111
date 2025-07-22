using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Commands;

public class DemoCommand : ICommand<DemoOptions>
{
    /// <summary>
    /// Executes the command logic asynchronously using the provided options.
    /// </summary>
    /// <param name="options">The options required to execute the command.</param>
    /// <returns>A task representing the asynchronous operation, returning an integer status code.</returns>
    public Task<int> ExecuteAsync(DemoOptions options)
    {
        // Initialize CLI message handler
        var messageHandler = new CliMessageHandler();
        MessageHandler.Initialize(messageHandler);

        MessageHandler.MsgInfo("This is an info message");
        MessageHandler.MsgWarning("This is a warning message");
        MessageHandler.MsgError("This is an error message");
        MessageHandler.MsgSuccess("This is a success message");
        MessageHandler.MsgDebug("This is a debug message");
        MessageHandler.MsgCritical("This is a critical message");

        // Demo progress
        using var progress = MessageHandler.CreateProgressContext("Demo Progress", 5);
        for (var i = 1; i <= 5; i++)
        {
            progress.Update(i, $"Step {i} of 5");
            Thread.Sleep(500); // Simulate work
        }

        progress.Complete();

        MessageHandler.MsgSuccess("Demo complete!");
        return Task.FromResult(0);
    }
}
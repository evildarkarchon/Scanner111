using System.Reflection;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Commands;

public class AboutCommand : ICommand<AboutOptions>
{
    /// <summary>
    /// Executes the "about" command, displaying version and about information for the application.
    /// </summary>
    /// <param name="options">The options provided for the about command.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the exit code of the command.</returns>
    public Task<int> ExecuteAsync(AboutOptions options)
    {
        var messageHandler = new CliMessageHandler();
        MessageHandler.Initialize(messageHandler);

        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version ?? new Version(1, 0, 0);

        MessageHandler.MsgInfo("Scanner111 - CLASSIC Crash Log Analyzer");
        MessageHandler.MsgInfo($"Version: {version}");
        MessageHandler.MsgInfo("Compatible with Bethesda games crash logs");
        MessageHandler.MsgInfo("Based on CLASSIC Python implementation");

        return Task.FromResult(0);
    }
}
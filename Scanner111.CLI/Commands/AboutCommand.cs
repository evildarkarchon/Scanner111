using System.Reflection;
using Scanner111.CLI.Models;
using Scanner111.Core.Infrastructure;

namespace Scanner111.CLI.Commands;

public class AboutCommand : ICommand<AboutOptions>
{
    private readonly IMessageHandler _messageHandler;

    public AboutCommand(IMessageHandler messageHandler)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
    }

    /// <summary>
    /// Executes the "about" command, displaying version and about information for the application.
    /// </summary>
    /// <param name="options">The options provided for the about command.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the exit code of the command.</returns>
    public Task<int> ExecuteAsync(AboutOptions options)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version ?? new Version(1, 0, 0);

        _messageHandler.ShowInfo("Scanner111 - CLASSIC Crash Log Analyzer");
        _messageHandler.ShowInfo($"Version: {version}");
        _messageHandler.ShowInfo("Compatible with Bethesda games crash logs");
        _messageHandler.ShowInfo("Based on CLASSIC Python implementation");

        return Task.FromResult(0);
    }
}
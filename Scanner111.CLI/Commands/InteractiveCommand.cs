using Scanner111.CLI.Models;
using Scanner111.CLI.Services;

namespace Scanner111.CLI.Commands;

public class InteractiveCommand : ICommand<InteractiveOptions>
{
    private readonly ITerminalUIService _uiService;

    public InteractiveCommand(ITerminalUIService uiService)
    {
        _uiService = uiService;
    }

    public async Task<int> ExecuteAsync(InteractiveOptions options)
    {
        // Apply theme settings if needed
        if (options.Theme != "default")
        {
            // Theme configuration can be added later
        }

        // Run the interactive mode
        return await _uiService.RunInteractiveMode();
    }
}
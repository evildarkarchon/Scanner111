using CommandLine;

namespace Scanner111.CLI.Commands;

[Verb("interactive", HelpText = "Launch interactive TUI mode")]
public class InteractiveCommand
{
    [Option('t', "theme", Default = "Default", HelpText = "Color theme (Default, Dark, Light, HighContrast)")]
    public string Theme { get; set; } = "Default";
    
    [Option('d', "debug", Default = false, HelpText = "Enable debug mode")]
    public bool Debug { get; set; }
}
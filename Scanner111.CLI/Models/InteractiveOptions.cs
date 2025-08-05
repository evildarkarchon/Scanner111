using CommandLine;

namespace Scanner111.CLI.Models;

[Verb("interactive", HelpText = "Launch interactive Terminal UI mode")]
public class InteractiveOptions
{
    [Option("theme", HelpText = "Color theme for the UI (default, dark, light)", Default = "default")]
    public string Theme { get; set; } = "default";

    [Option("no-animations", HelpText = "Disable UI animations", Default = false)]
    public bool NoAnimations { get; set; }
}
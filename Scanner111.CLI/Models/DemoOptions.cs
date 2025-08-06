using CommandLine;

namespace Scanner111.CLI.Models;

[Verb("demo", HelpText = "Demonstrate message handler features")]
public class DemoOptions
{
    [Option("legacy-progress", HelpText = "Use legacy progress display instead of enhanced multi-progress view")]
    public bool UseLegacyProgress { get; set; }
}
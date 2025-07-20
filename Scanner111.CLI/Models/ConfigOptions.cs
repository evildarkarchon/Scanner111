using CommandLine;

namespace Scanner111.CLI.Models;

[Verb("config", HelpText = "Manage Scanner111 configuration")]
public class ConfigOptions
{
    [Option('l', "list", HelpText = "List current configuration")]
    public bool List { get; set; }
    
    [Option('s', "set", HelpText = "Set configuration value (format: key=value)")]
    public string? Set { get; set; }
    
    [Option('r', "reset", HelpText = "Reset configuration to defaults")]
    public bool Reset { get; set; }
    
    [Option("show-path", HelpText = "Show configuration file path")]
    public bool ShowPath { get; set; }
}
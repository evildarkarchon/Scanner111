using CommandLine;

namespace Scanner111.CLI.Commands;

[Verb("config", HelpText = "Manage configuration settings")]
public class ConfigCommand
{
    [Option('l', "list", Default = false, HelpText = "List all configuration settings")]
    public bool List { get; set; }
    
    [Option('g', "get", HelpText = "Get a specific configuration value")]
    public string? Get { get; set; }
    
    [Option('s', "set", HelpText = "Set a configuration value (use with --value)")]
    public string? Set { get; set; }
    
    [Option('v', "value", HelpText = "Value to set for the configuration key")]
    public string? Value { get; set; }
    
    [Option('r', "reset", Default = false, HelpText = "Reset all settings to defaults")]
    public bool Reset { get; set; }
}
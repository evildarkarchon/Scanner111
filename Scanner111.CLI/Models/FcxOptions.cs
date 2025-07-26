using CommandLine;

namespace Scanner111.CLI.Models;

[Verb("fcx", HelpText = "Run FCX file integrity checks")]
public class FcxOptions
{
    [Option('g', "game", Default = "Fallout4", HelpText = "Target game (currently only Fallout4 supported)")]
    public string Game { get; set; } = "Fallout4";
    
    [Option("check-only", HelpText = "Only check files, don't fix or backup")]
    public bool CheckOnly { get; set; }
    
    [Option("validate-hashes", HelpText = "Validate file hashes against known good versions")]
    public bool ValidateHashes { get; set; }
    
    [Option("check-mods", HelpText = "Check mod file integrity")]
    public bool CheckMods { get; set; }
    
    [Option("check-ini", HelpText = "Check INI file syntax and settings")]
    public bool CheckIni { get; set; }
    
    [Option("backup", HelpText = "Create backup of game files before making changes")]
    public bool Backup { get; set; }
    
    [Option("restore", HelpText = "Restore from backup")]
    public string? RestorePath { get; set; }
    
    [Option("game-path", HelpText = "Path to game installation directory")]
    public string? GamePath { get; set; }
    
    [Option("mods-folder", HelpText = "Path to mods folder")]
    public string? ModsFolder { get; set; }
    
    [Option("ini-folder", HelpText = "Path to INI files folder")]
    public string? IniFolder { get; set; }
    
    [Option('v', "verbose", HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }
    
    [Option("disable-colors", HelpText = "Disable colored output")]
    public bool DisableColors { get; set; }
    
    [Option("disable-progress", HelpText = "Disable progress bars")]
    public bool DisableProgress { get; set; }
    
    [Option('o', "output", HelpText = "Output file for results")]
    public string? OutputFile { get; set; }
}
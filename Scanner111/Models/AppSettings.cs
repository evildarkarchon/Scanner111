using System;
using System.Collections.Generic;
using System.IO;

namespace Scanner111.Models
{
    /// <summary>
    /// Application settings that can be loaded from configuration files and updated at runtime
    /// </summary>
    public class AppSettings
    {
        // Properties that were previously in GlobalRegistry.Keys can be defined here
        // Example: public string GamePath { get; set; }
        // For now, let's add a few based on the Python GlobalRegistry

        public string? GamePath { get; set; }
        public string? DocsPath { get; set; }
        public bool IsGuiMode { get; set; } = true; // Default to true as it's a GUI app
        public string VrGameVars { get; set; } = "";
        public string GameName { get; set; } = "Fallout4";
        public string LocalDir { get; set; } = Directory.GetCurrentDirectory();
        public bool IsPrerelease { get; set; }

        // Properties for crash analysis
        public List<string> ClassicRecordsList { get; set; } = new List<string>();
        public List<string> GameIgnoreRecords { get; set; } = new List<string>();
        public List<string> GameIgnorePlugins { get; set; } = new List<string>();
        public string CrashGeneratorName { get; set; } = "Buffout 4";
        public string? CrashXseAcronym { get; set; } = "F4SE"; // F4SE for Fallout 4, SKSE for Skyrim, etc.
        public string? GameRootName { get; set; } = "Fallout 4"; // Used for version detection in logs

        // FormID database settings
        public bool ShowFormIdValues { get; set; } = false;
        public bool FormIdDbExists { get; set; } = false;
        public string? FormIdDatabasePath { get; set; }
        public List<string> FormIdDbPaths { get; set; } = new List<string>();

        // FCX Mode settings (optional)
        public bool FcxMode { get; set; } = false;
        public bool MoveUnsolvedLogs { get; set; } = false;

        // Crash log formatting settings
        public bool SimplifyLogs { get; set; } = true;

        public List<string> SimplifyRemoveStrings { get; set; } = new List<string>
            { "Steam.dll", "nvwgf2umx.dll", "KERNELBASE.dll", "ntdll.dll" };

        public List<string> LastProcessingErrors { get; set; } = new List<string>();
        public bool PreserveOriginalFiles { get; set; } = true; // Create backup of original files
        public bool AutoDetectCrashLogs { get; set; } = true; // Attempt to validate if files are crash logs

        // Game-specific YAML database info
        public string YamlGameDatabasePath { get; set; } = "CLASSIC Fallout4.yaml";
        public YAML ActiveGameYaml { get; set; } = YAML.Game;

        // Paths for YAML stores
        public string YamlMainPath { get; set; } = Path.Combine("CLASSIC Data", "databases", "CLASSIC Main.yaml");
        public string YamlSettingsPath { get; set; } = "CLASSIC Settings.yaml";
        public string YamlIgnorePath { get; set; } = "CLASSIC Ignore.yaml";
        public string YamlGamePath { get; set; } = Path.Combine("CLASSIC Data", "databases", "CLASSIC Fallout4.yaml");
        public string YamlGameLocalPath { get; set; } = Path.Combine("CLASSIC Data", "CLASSIC Fallout4 Local.yaml");
        public string YamlTestPath { get; set; } = Path.Combine("tests", "test_settings.yaml");

        // Script Extender information
        public string XseAcronym { get; set; } = "F4SE";
        public string XseFullName { get; set; } = "Fallout 4 Script Extender";
        public string XseLatestVersion { get; set; } = "0.6.23";
        public string XseLatestVersionNg { get; set; } = "0.7.2";
        public int XseFileCount { get; set; } = 29;

        // Game version information
        public string GameVersion { get; set; } = "1.10.163";
        public string GameVersionNew { get; set; } = "1.10.984";
        public string GameSteamId { get; set; } = "377160";

        /// <summary>
        /// Updates settings based on the specified game
        /// </summary>
        public void UpdateForGame(string gameName)
        {
            GameName = gameName;

            // Set appropriate defaults based on the game
            switch (gameName.ToLowerInvariant())
            {
                case "fallout4":
                    GameRootName = "Fallout 4";
                    CrashXseAcronym = "F4SE";
                    CrashGeneratorName = "Buffout 4";
                    YamlGameDatabasePath = "CLASSIC Fallout4.yaml";
                    ActiveGameYaml = YAML.Game;
                    XseAcronym = "F4SE";
                    XseFullName = "Fallout 4 Script Extender";
                    XseLatestVersion = "0.6.23";
                    XseLatestVersionNg = "0.7.2";
                    GameVersion = "1.10.163";
                    GameVersionNew = "1.10.984";
                    GameSteamId = "377160";
                    break;

                case "fallout4vr":
                    GameRootName = "Fallout 4 VR";
                    CrashXseAcronym = "F4SE";
                    CrashGeneratorName = "Buffout 4";
                    YamlGameDatabasePath = "CLASSIC Fallout4VR.yaml";
                    ActiveGameYaml = YAML.Game;
                    XseAcronym = "F4SE";
                    XseFullName = "Fallout 4 Script Extender VR";
                    XseLatestVersion = "0.6.20";
                    GameVersion = "1.2.72";
                    GameSteamId = "611660";
                    break;

                case "skyrimse":
                    GameRootName = "Skyrim Special Edition";
                    CrashXseAcronym = "SKSE";
                    CrashGeneratorName = "Crash Logger";
                    YamlGameDatabasePath = "CLASSIC SkyrimSE.yaml";
                    ActiveGameYaml = YAML.Game;
                    XseAcronym = "SKSE";
                    XseFullName = "Skyrim Script Extender";
                    XseLatestVersion = "2.2.3";
                    GameVersion = "1.6.640";
                    GameSteamId = "489830";
                    break;

                default:
                    // Keep current settings if the game isn't recognized
                    break;
            }

            // Update any dependent settings

            // Set reasonable paths for game files based on typical installations
            if (GameRootName != null)
            {
                DocsPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    GameRootName.Replace(" ", ""));
            }
            else
            {
                DocsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            // Add default ignored plugins for this game
            GameIgnorePlugins = new List<string>
            {
                $"{GameRootName?.Replace(" ", "")}.esm",
                "DLCCoast.esm",
                "DLCNukaWorld.esm",
                "DLCRobot.esm",
                "DLCworkshop01.esm",
                "DLCworkshop02.esm",
                "DLCworkshop03.esm",
                "Unofficial Fallout 4 Patch.esp"
            };
        }
    }
}

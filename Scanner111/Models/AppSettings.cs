using System.Collections.Generic;
using System.IO;

namespace Scanner111.Models
{
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
        public string? GameRootName { get; set; } = "Fallout 4"; // Used for version detection in logs        // FormID database settings
        public bool ShowFormIdValues { get; set; } = false;
        public bool FormIdDbExists { get; set; } = false;
        public string? FormIdDatabasePath { get; set; }
        public List<string> FormIdDbPaths { get; set; } = new List<string>();

        // FCX Mode settings (optional)
        public bool FcxMode { get; set; } = false;
        public bool MoveUnsolvedLogs { get; set; } = false;

        // Other settings from "CLASSIC Settings.yaml" would go here or in a separate class
        // public string YamlCachePath { get; set; } // Example

        // The open_file_with_encoding functionality will be handled by a dedicated service.
    }
}

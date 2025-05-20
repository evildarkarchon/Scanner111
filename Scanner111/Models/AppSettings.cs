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

        // Other settings from "CLASSIC Settings.yaml" would go here or in a separate class
        // public string YamlCachePath { get; set; } // Example

        // The open_file_with_encoding functionality will be handled by a dedicated service.
    }
}

using System;
using System.IO;
using System.Threading.Tasks;

namespace Scanner111.Core.ModManagers.MO2
{
    public class MO2ProfileReader
    {
        public async Task<string?> GetActiveProfileFromIniAsync(string iniPath)
        {
            if (!File.Exists(iniPath))
                return null;

            try
            {
                var lines = await File.ReadAllLinesAsync(iniPath);
                bool inGeneralSection = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine == "[General]")
                    {
                        inGeneralSection = true;
                        continue;
                    }
                    
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        inGeneralSection = false;
                        continue;
                    }
                    
                    if (inGeneralSection && trimmedLine.StartsWith("selected_profile="))
                    {
                        var profileName = trimmedLine.Substring("selected_profile=".Length).Trim();
                        return string.IsNullOrEmpty(profileName) ? null : profileName;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }

        public async Task<ProfileSettings?> ReadProfileSettingsAsync(string profilePath)
        {
            var settingsFile = Path.Combine(profilePath, "settings.ini");
            if (!File.Exists(settingsFile))
                return null;

            var settings = new ProfileSettings();

            try
            {
                var lines = await File.ReadAllLinesAsync(settingsFile);
                string? currentSection = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    
                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        continue;
                    }
                    
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("#"))
                        continue;

                    var equalsIndex = trimmedLine.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        var key = trimmedLine.Substring(0, equalsIndex).Trim();
                        var value = trimmedLine.Substring(equalsIndex + 1).Trim();
                        
                        switch (currentSection)
                        {
                            case "General":
                                if (key == "LocalSavegames")
                                    settings.LocalSavegames = value == "true";
                                else if (key == "AutomaticArchiveInvalidation")
                                    settings.AutomaticArchiveInvalidation = value == "true";
                                break;
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }

            return settings;
        }
    }

    public class ProfileSettings
    {
        public bool LocalSavegames { get; set; }
        public bool AutomaticArchiveInvalidation { get; set; }
    }
}
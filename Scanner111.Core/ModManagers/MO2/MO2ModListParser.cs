namespace Scanner111.Core.ModManagers.MO2;

public class MO2ModListParser
{
    public async Task<IEnumerable<ModInfo>> ParseModListAsync(string modListPath, string mo2InstallPath)
    {
        if (!File.Exists(modListPath))
            return Enumerable.Empty<ModInfo>();

        var mods = new List<ModInfo>();
        var modsFolder = Path.Combine(mo2InstallPath, "mods");

        try
        {
            var lines = await File.ReadAllLinesAsync(modListPath);
            var loadOrder = 0;

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var modEntry = ParseModLine(line);
                if (modEntry == null)
                    continue;

                var modInfo = new ModInfo
                {
                    Id = modEntry.Name,
                    Name = modEntry.Name,
                    IsEnabled = modEntry.IsEnabled,
                    LoadOrder = loadOrder++,
                    FolderPath = Path.Combine(modsFolder, modEntry.Name)
                };

                // Read meta.ini if it exists
                await EnrichModInfoFromMetaAsync(modInfo);

                // Get list of files in mod folder
                if (Directory.Exists(modInfo.FolderPath)) modInfo.Files = GetModFiles(modInfo.FolderPath);

                mods.Add(modInfo);
            }
        }
        catch (Exception)
        {
            return mods;
        }

        return mods;
    }

    private ModListEntry? ParseModLine(string line)
    {
        var trimmedLine = line.Trim();

        if (string.IsNullOrEmpty(trimmedLine))
            return null;

        var isEnabled = true;
        var modName = trimmedLine;

        // Check if mod is disabled (starts with -)
        if (trimmedLine.StartsWith("-"))
        {
            isEnabled = false;
            modName = trimmedLine.Substring(1);
        }
        // Check if mod is enabled (starts with +)
        else if (trimmedLine.StartsWith("+"))
        {
            isEnabled = true;
            modName = trimmedLine.Substring(1);
        }
        // Check if mod is marked (starts with *)
        else if (trimmedLine.StartsWith("*"))
        {
            isEnabled = true;
            modName = trimmedLine.Substring(1);
        }

        return new ModListEntry
        {
            Name = modName.Trim(),
            IsEnabled = isEnabled
        };
    }

    private async Task EnrichModInfoFromMetaAsync(ModInfo modInfo)
    {
        var metaPath = Path.Combine(modInfo.FolderPath, "meta.ini");
        if (!File.Exists(metaPath))
            return;

        try
        {
            var lines = await File.ReadAllLinesAsync(metaPath);
            var inGeneralSection = false;

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

                if (!inGeneralSection)
                    continue;

                var equalsIndex = trimmedLine.IndexOf('=');
                if (equalsIndex > 0)
                {
                    var key = trimmedLine.Substring(0, equalsIndex).Trim();
                    var value = trimmedLine.Substring(equalsIndex + 1).Trim();

                    switch (key)
                    {
                        case "modName":
                            if (!string.IsNullOrEmpty(value))
                                modInfo.Name = value;
                            break;
                        case "version":
                            modInfo.Version = value;
                            break;
                        case "author":
                            modInfo.Author = value;
                            break;
                        case "description":
                            modInfo.Description = value;
                            break;
                        case "modid":
                            modInfo.Metadata["NexusModId"] = value;
                            break;
                        case "installationFile":
                            modInfo.Metadata["InstallationFile"] = value;
                            break;
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors reading meta.ini
        }
    }

    private List<string> GetModFiles(string modPath)
    {
        var files = new List<string>();

        try
        {
            var allFiles = Directory.GetFiles(modPath, "*.*", SearchOption.AllDirectories);
            foreach (var file in allFiles)
            {
                var relativePath = Path.GetRelativePath(modPath, file);
                files.Add(relativePath.Replace('\\', '/'));
            }
        }
        catch (Exception)
        {
            // Ignore errors reading files
        }

        return files;
    }

    private class ModListEntry
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}
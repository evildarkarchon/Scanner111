using System.Runtime.InteropServices;

namespace Scanner111.Core.ModManagers.MO2;

public class ModOrganizer2Manager : IModManager
{
    private readonly MO2ModListParser _modListParser;
    private readonly MO2ProfileReader _profileReader;
    private string? _installPath;

    public ModOrganizer2Manager()
    {
        _profileReader = new MO2ProfileReader();
        _modListParser = new MO2ModListParser();
    }

    public string Name => "Mod Organizer 2";
    public ModManagerType Type => ModManagerType.ModOrganizer2;

    public bool IsInstalled()
    {
        _installPath = FindMO2InstallPath();
        return !string.IsNullOrEmpty(_installPath) && Directory.Exists(_installPath);
    }

    public Task<string?> GetInstallPathAsync()
    {
        if (string.IsNullOrEmpty(_installPath))
            _installPath = FindMO2InstallPath();
        return Task.FromResult(_installPath);
    }

    public async Task<string?> GetStagingFolderAsync()
    {
        var installPath = await GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath))
            return null;

        var modsFolder = Path.Combine(installPath, "mods");
        return Directory.Exists(modsFolder) ? modsFolder : null;
    }

    public async Task<IEnumerable<ModInfo>> GetInstalledModsAsync(string? profileName = null)
    {
        var installPath = await GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath))
            return Enumerable.Empty<ModInfo>();

        profileName ??= await GetActiveProfileAsync();
        if (string.IsNullOrEmpty(profileName))
            return Enumerable.Empty<ModInfo>();

        var profilePath = Path.Combine(installPath, "profiles", profileName);
        var modListFile = Path.Combine(profilePath, "modlist.txt");

        if (!File.Exists(modListFile))
            return Enumerable.Empty<ModInfo>();

        return await _modListParser.ParseModListAsync(modListFile, installPath);
    }

    public async Task<IEnumerable<string>> GetProfilesAsync()
    {
        var installPath = await GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath))
            return Enumerable.Empty<string>();

        var profilesPath = Path.Combine(installPath, "profiles");
        if (!Directory.Exists(profilesPath))
            return Enumerable.Empty<string>();

        return Directory.GetDirectories(profilesPath)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .Cast<string>();
    }

    public async Task<string?> GetActiveProfileAsync()
    {
        var installPath = await GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath))
            return null;

        var iniPath = Path.Combine(installPath, "ModOrganizer.ini");
        if (!File.Exists(iniPath))
            return null;

        return await _profileReader.GetActiveProfileFromIniAsync(iniPath);
    }

    public async Task<Dictionary<string, string>> GetLoadOrderAsync(string? profileName = null)
    {
        var installPath = await GetInstallPathAsync();
        if (string.IsNullOrEmpty(installPath))
            return new Dictionary<string, string>();

        profileName ??= await GetActiveProfileAsync();
        if (string.IsNullOrEmpty(profileName))
            return new Dictionary<string, string>();

        var profilePath = Path.Combine(installPath, "profiles", profileName);
        var loadOrderFile = Path.Combine(profilePath, "loadorder.txt");

        if (!File.Exists(loadOrderFile))
            return new Dictionary<string, string>();

        var loadOrder = new Dictionary<string, string>();
        var lines = await File.ReadAllLinesAsync(loadOrderFile);

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (!string.IsNullOrEmpty(line) && !line.StartsWith("#")) loadOrder[line] = i.ToString("D3");
        }

        return loadOrder;
    }

    private string? FindMO2InstallPath()
    {
        // Check registry first
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var registryPaths = new[]
            {
                @"SOFTWARE\Mod Organizer Team\Mod Organizer",
                @"SOFTWARE\WOW6432Node\Mod Organizer Team\Mod Organizer"
            };

            foreach (var regPath in registryPaths)
            {
                var path = ModManagerDetector.GetRegistryValue(regPath, "InstallPath");
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }
        }

        // Check common installation paths
        var commonPaths = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "ModOrganizer2"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "ModOrganizer2"),
            @"C:\Modding\MO2",
            @"C:\Tools\ModOrganizer2"
        };

        foreach (var path in commonPaths)
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "ModOrganizer.exe")))
                return path;

        // Check for portable installations in current directory tree
        var currentDir = Directory.GetCurrentDirectory();
        var mo2Exe = Directory.GetFiles(currentDir, "ModOrganizer.exe", SearchOption.AllDirectories).FirstOrDefault();
        if (!string.IsNullOrEmpty(mo2Exe))
            return Path.GetDirectoryName(mo2Exe);

        return null;
    }
}
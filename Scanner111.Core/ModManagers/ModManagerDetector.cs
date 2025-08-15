using System.Runtime.InteropServices;
using Microsoft.Win32;
using Scanner111.Core.ModManagers.MO2;
using Scanner111.Core.ModManagers.Vortex;

namespace Scanner111.Core.ModManagers;

public class ModManagerDetector : IModManagerDetector
{
    private readonly List<IModManager> _detectedManagers = new();
    private bool _hasScanned;

    public async Task<IEnumerable<IModManager>> DetectInstalledManagersAsync()
    {
        if (_hasScanned)
            return _detectedManagers;

        _detectedManagers.Clear();

        // Detect Mod Organizer 2
        var mo2 = new ModOrganizer2Manager();
        if (mo2.IsInstalled()) _detectedManagers.Add(mo2);

        // Detect Vortex
        var vortex = new VortexManager();
        if (vortex.IsInstalled()) _detectedManagers.Add(vortex);

        _hasScanned = true;
        return await Task.FromResult(_detectedManagers);
    }

    public async Task<IModManager?> GetManagerByTypeAsync(ModManagerType type)
    {
        var managers = await DetectInstalledManagersAsync();
        return managers.FirstOrDefault(m => m.Type == type);
    }

    public static string? GetRegistryValue(string keyPath, string valueName)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(keyPath) ??
                            Registry.CurrentUser.OpenSubKey(keyPath);
            return key?.GetValue(valueName)?.ToString();
        }
        catch
        {
            return null;
        }
    }

    public static bool CheckPathExists(params string[] possiblePaths)
    {
        return possiblePaths.Any(path => !string.IsNullOrEmpty(path) && Directory.Exists(path));
    }
}

public interface IModManagerDetector
{
    Task<IEnumerable<IModManager>> DetectInstalledManagersAsync();
    Task<IModManager?> GetManagerByTypeAsync(ModManagerType type);
}
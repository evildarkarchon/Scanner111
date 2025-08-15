namespace Scanner111.Core.ModManagers;

public interface IModManager
{
    string Name { get; }
    ModManagerType Type { get; }

    bool IsInstalled();

    Task<string?> GetInstallPathAsync();

    Task<string?> GetStagingFolderAsync();

    Task<IEnumerable<ModInfo>> GetInstalledModsAsync(string? profileName = null);

    Task<IEnumerable<string>> GetProfilesAsync();

    Task<string?> GetActiveProfileAsync();

    Task<Dictionary<string, string>> GetLoadOrderAsync(string? profileName = null);
}
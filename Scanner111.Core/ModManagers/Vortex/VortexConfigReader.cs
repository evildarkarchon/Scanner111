using System.Text.Json;

namespace Scanner111.Core.ModManagers.Vortex;

public class VortexConfigReader
{
    public async Task<string?> GetActiveGameFromSettingsAsync(string settingsPath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(settingsPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("profiles", out var profiles) &&
                profiles.TryGetProperty("activeProfileId", out var activeProfileId))
            {
                var profileId = activeProfileId.GetString();
                if (!string.IsNullOrEmpty(profileId))
                {
                    // Extract game ID from profile ID (format: gameId#profileName)
                    var hashIndex = profileId.IndexOf('#');
                    if (hashIndex > 0)
                        return profileId.Substring(0, hashIndex);
                }
            }

            // Fallback to last active game
            if (doc.RootElement.TryGetProperty("gameMode", out var gameMode) &&
                gameMode.TryGetProperty("activeGameId", out var activeGameId))
                return activeGameId.GetString();
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public async Task<string?> GetStagingFolderFromSettingsAsync(string settingsPath)
    {
        try
        {
            var json = await File.ReadAllTextAsync(settingsPath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("mods", out var mods) &&
                mods.TryGetProperty("stagingFolder", out var stagingFolder))
                return stagingFolder.GetString();
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public async Task<IEnumerable<ModInfo>> GetModsFromStateAsync(string statePath, string gameId, string profileName)
    {
        var modsList = new List<ModInfo>();

        try
        {
            var json = await File.ReadAllTextAsync(statePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("persistent", out var persistent) &&
                persistent.TryGetProperty("mods", out var mods) &&
                mods.TryGetProperty(gameId, out var gameMods))
            {
                var loadOrder = 0;
                foreach (var modProp in gameMods.EnumerateObject())
                {
                    var modId = modProp.Name;
                    var modData = modProp.Value;

                    var modInfo = new ModInfo
                    {
                        Id = modId,
                        Name = GetJsonString(modData, "name") ?? modId,
                        Version = GetJsonString(modData, "version"),
                        Author = GetJsonString(modData, "author"),
                        IsEnabled = GetJsonBool(modData, "enabled"),
                        LoadOrder = loadOrder++,
                        FolderPath = GetJsonString(modData, "installPath") ?? string.Empty
                    };

                    // Get installation date
                    if (modData.TryGetProperty("installTime", out var installTime))
                        if (DateTime.TryParse(installTime.GetString(), out var date))
                            modInfo.InstallDate = date;

                    // Get mod attributes
                    if (modData.TryGetProperty("attributes", out var attributes))
                    {
                        if (attributes.TryGetProperty("modId", out var nexusId))
                            modInfo.Metadata["NexusModId"] = nexusId.GetInt32().ToString();

                        if (attributes.TryGetProperty("downloadGame", out var downloadGame))
                            modInfo.Metadata["DownloadGame"] = downloadGame.GetString() ?? string.Empty;

                        if (attributes.TryGetProperty("fileId", out var fileId))
                            modInfo.Metadata["FileId"] = fileId.GetInt32().ToString();
                    }

                    modsList.Add(modInfo);
                }
            }
        }
        catch (Exception)
        {
            return modsList;
        }

        return modsList;
    }

    public async Task<IEnumerable<string>> GetProfilesForGameAsync(string statePath, string gameId)
    {
        var profiles = new List<string>();

        try
        {
            var json = await File.ReadAllTextAsync(statePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("persistent", out var persistent) &&
                persistent.TryGetProperty("profiles", out var profilesData))
                foreach (var profileProp in profilesData.EnumerateObject())
                {
                    var profileId = profileProp.Name;
                    var profileData = profileProp.Value;

                    if (profileData.TryGetProperty("gameId", out var profGameId) &&
                        profGameId.GetString() == gameId)
                        if (profileData.TryGetProperty("name", out var name))
                        {
                            var profileName = name.GetString();
                            if (!string.IsNullOrEmpty(profileName))
                                profiles.Add(profileName);
                        }
                }
        }
        catch (Exception)
        {
            return profiles;
        }

        return profiles;
    }

    public async Task<string?> GetActiveProfileForGameAsync(string statePath, string gameId)
    {
        try
        {
            var json = await File.ReadAllTextAsync(statePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("settings", out var settings) &&
                settings.TryGetProperty("profiles", out var profiles) &&
                profiles.TryGetProperty("activeProfileId", out var activeProfileId))
            {
                var profileId = activeProfileId.GetString();
                if (!string.IsNullOrEmpty(profileId) && profileId.StartsWith(gameId + "#"))
                    return profileId.Substring(gameId.Length + 1);
            }
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    public async Task<Dictionary<string, string>> GetLoadOrderAsync(string statePath, string gameId, string profileName)
    {
        var loadOrder = new Dictionary<string, string>();

        try
        {
            var json = await File.ReadAllTextAsync(statePath);
            using var doc = JsonDocument.Parse(json);

            var profileId = $"{gameId}#{profileName}";

            if (doc.RootElement.TryGetProperty("persistent", out var persistent) &&
                persistent.TryGetProperty("loadOrder", out var loadOrderData) &&
                loadOrderData.TryGetProperty(profileId, out var profileLoadOrder))
                if (profileLoadOrder.ValueKind == JsonValueKind.Array)
                {
                    var index = 0;
                    foreach (var item in profileLoadOrder.EnumerateArray())
                    {
                        var modId = item.GetString();
                        if (!string.IsNullOrEmpty(modId))
                        {
                            loadOrder[modId] = index.ToString("D3");
                            index++;
                        }
                    }
                }
        }
        catch (Exception)
        {
            return loadOrder;
        }

        return loadOrder;
    }

    private string? GetJsonString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop))
            return prop.GetString();
        return null;
    }

    private bool GetJsonBool(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.True)
            return true;
        return false;
    }
}
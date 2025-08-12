using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Scanner111.Core.ModManagers.Vortex
{
    public class VortexManager : IModManager
    {
        private string? _vortexDataPath;
        private readonly VortexConfigReader _configReader;

        public VortexManager()
        {
            _configReader = new VortexConfigReader();
        }

        public string Name => "Vortex";
        public ModManagerType Type => ModManagerType.Vortex;

        public bool IsInstalled()
        {
            _vortexDataPath = FindVortexDataPath();
            return !string.IsNullOrEmpty(_vortexDataPath) && Directory.Exists(_vortexDataPath);
        }

        public Task<string?> GetInstallPathAsync()
        {
            if (string.IsNullOrEmpty(_vortexDataPath))
                _vortexDataPath = FindVortexDataPath();
            return Task.FromResult(_vortexDataPath);
        }

        public async Task<string?> GetStagingFolderAsync()
        {
            var dataPath = await GetInstallPathAsync();
            if (string.IsNullOrEmpty(dataPath))
                return null;

            var settingsPath = Path.Combine(dataPath, "settings.json");
            if (!File.Exists(settingsPath))
                return null;

            return await _configReader.GetStagingFolderFromSettingsAsync(settingsPath);
        }

        public async Task<IEnumerable<ModInfo>> GetInstalledModsAsync(string? profileName = null)
        {
            var dataPath = await GetInstallPathAsync();
            if (string.IsNullOrEmpty(dataPath))
                return Enumerable.Empty<ModInfo>();

            var gameId = await GetActiveGameIdAsync();
            if (string.IsNullOrEmpty(gameId))
                return Enumerable.Empty<ModInfo>();

            profileName ??= await GetActiveProfileAsync();
            if (string.IsNullOrEmpty(profileName))
                return Enumerable.Empty<ModInfo>();

            var statePath = Path.Combine(dataPath, "state.json");
            if (!File.Exists(statePath))
                return Enumerable.Empty<ModInfo>();

            return await _configReader.GetModsFromStateAsync(statePath, gameId, profileName);
        }

        public async Task<IEnumerable<string>> GetProfilesAsync()
        {
            var dataPath = await GetInstallPathAsync();
            if (string.IsNullOrEmpty(dataPath))
                return Enumerable.Empty<string>();

            var gameId = await GetActiveGameIdAsync();
            if (string.IsNullOrEmpty(gameId))
                return Enumerable.Empty<string>();

            var statePath = Path.Combine(dataPath, "state.json");
            if (!File.Exists(statePath))
                return Enumerable.Empty<string>();

            return await _configReader.GetProfilesForGameAsync(statePath, gameId);
        }

        public async Task<string?> GetActiveProfileAsync()
        {
            var dataPath = await GetInstallPathAsync();
            if (string.IsNullOrEmpty(dataPath))
                return null;

            var gameId = await GetActiveGameIdAsync();
            if (string.IsNullOrEmpty(gameId))
                return null;

            var statePath = Path.Combine(dataPath, "state.json");
            if (!File.Exists(statePath))
                return null;

            return await _configReader.GetActiveProfileForGameAsync(statePath, gameId);
        }

        public async Task<Dictionary<string, string>> GetLoadOrderAsync(string? profileName = null)
        {
            var dataPath = await GetInstallPathAsync();
            if (string.IsNullOrEmpty(dataPath))
                return new Dictionary<string, string>();

            var gameId = await GetActiveGameIdAsync();
            if (string.IsNullOrEmpty(gameId))
                return new Dictionary<string, string>();

            profileName ??= await GetActiveProfileAsync();
            if (string.IsNullOrEmpty(profileName))
                return new Dictionary<string, string>();

            var statePath = Path.Combine(dataPath, "state.json");
            if (!File.Exists(statePath))
                return new Dictionary<string, string>();

            return await _configReader.GetLoadOrderAsync(statePath, gameId, profileName);
        }

        private async Task<string?> GetActiveGameIdAsync()
        {
            var dataPath = await GetInstallPathAsync();
            if (string.IsNullOrEmpty(dataPath))
                return null;

            var settingsPath = Path.Combine(dataPath, "settings.json");
            if (!File.Exists(settingsPath))
                return null;

            return await _configReader.GetActiveGameFromSettingsAsync(settingsPath);
        }

        private string? FindVortexDataPath()
        {
            // Check registry first
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var registryPaths = new[]
                {
                    @"SOFTWARE\Black Tree Gaming Ltd\Vortex",
                    @"SOFTWARE\WOW6432Node\Black Tree Gaming Ltd\Vortex"
                };

                foreach (var regPath in registryPaths)
                {
                    var path = ModManagerDetector.GetRegistryValue(regPath, "InstallPath");
                    if (!string.IsNullOrEmpty(path))
                    {
                        // Vortex stores data in AppData, not install path
                        var appDataPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Vortex"
                        );
                        if (Directory.Exists(appDataPath))
                            return appDataPath;
                    }
                }
            }

            // Check default AppData location
            var defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Vortex"
            );
            
            if (Directory.Exists(defaultPath))
                return defaultPath;

            // Check for portable installation
            var localPath = Path.Combine(Directory.GetCurrentDirectory(), "Vortex");
            if (Directory.Exists(localPath) && File.Exists(Path.Combine(localPath, "settings.json")))
                return localPath;

            return null;
        }
    }
}
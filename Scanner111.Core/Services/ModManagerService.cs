using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Models;

namespace Scanner111.Core.Services
{
    public interface IModManagerService
    {
        Task<IEnumerable<IModManager>> GetAvailableManagersAsync();
        Task<IModManager?> GetActiveManagerAsync();
        Task<IEnumerable<ModInfo>> GetAllModsAsync();
        Task<Dictionary<string, string>> GetConsolidatedLoadOrderAsync();
        Task<string?> GetModStagingFolderAsync();
        void SetPreferredManager(ModManagerType type);
    }

    public class ModManagerService : IModManagerService
    {
        private readonly IModManagerDetector _detector;
        private readonly IMessageHandler _messageHandler;
        private readonly IApplicationSettingsService _settingsService;
        private ModManagerType? _preferredManager;
        private IEnumerable<IModManager>? _cachedManagers;
        private DateTime _cacheTime;
        private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

        public ModManagerService(
            IModManagerDetector detector,
            IMessageHandler messageHandler,
            IApplicationSettingsService settingsService)
        {
            _detector = detector ?? throw new ArgumentNullException(nameof(detector));
            _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public async Task<IEnumerable<IModManager>> GetAvailableManagersAsync()
        {
            // Check if mod managers are disabled in settings
            var settings = await _settingsService.LoadSettingsAsync();
            if (settings.ModManagerSettings?.SkipModManagerIntegration == true || 
                settings.AutoDetectModManagers == false)
            {
                _messageHandler.ShowInfo("Mod manager integration is disabled in settings");
                return Enumerable.Empty<IModManager>();
            }

            if (_cachedManagers != null && DateTime.Now - _cacheTime < _cacheExpiration)
                return _cachedManagers;

            _messageHandler.ShowInfo("Detecting installed mod managers...");
            
            _cachedManagers = await _detector.DetectInstalledManagersAsync();
            _cacheTime = DateTime.Now;

            foreach (var manager in _cachedManagers)
            {
                _messageHandler.ShowSuccess($"Found {manager.Name} at: {await manager.GetInstallPathAsync()}");
            }

            if (!_cachedManagers.Any())
            {
                _messageHandler.ShowInfo("No mod managers detected. FCX will analyze game files directly.");
            }

            return _cachedManagers;
        }

        public async Task<IModManager?> GetActiveManagerAsync()
        {
            // Check if mod managers are disabled first
            var settings = await _settingsService.LoadSettingsAsync();
            if (settings.ModManagerSettings?.SkipModManagerIntegration == true || 
                settings.AutoDetectModManagers == false)
            {
                return null;
            }

            var managers = await GetAvailableManagersAsync();
            
            if (!managers.Any())
                return null;

            // If preferred manager is set and available, use it
            if (_preferredManager.HasValue)
            {
                var preferred = managers.FirstOrDefault(m => m.Type == _preferredManager.Value);
                if (preferred != null)
                    return preferred;
            }

            // Check settings for default manager (already loaded above)
            if (!string.IsNullOrEmpty(settings.ModManagerSettings?.DefaultManager))
            {
                if (Enum.TryParse<ModManagerType>(settings.ModManagerSettings.DefaultManager, out var defaultType))
                {
                    var defaultManager = managers.FirstOrDefault(m => m.Type == defaultType);
                    if (defaultManager != null)
                        return defaultManager;
                }
            }

            // Return first available manager
            return managers.FirstOrDefault();
        }

        public async Task<IEnumerable<ModInfo>> GetAllModsAsync()
        {
            // Check if mod managers are disabled
            var settings = await _settingsService.LoadSettingsAsync();
            if (settings.ModManagerSettings?.SkipModManagerIntegration == true || 
                settings.AutoDetectModManagers == false)
            {
                return Enumerable.Empty<ModInfo>();
            }

            var manager = await GetActiveManagerAsync();
            if (manager == null)
                return Enumerable.Empty<ModInfo>();

            try
            {
                _messageHandler.ShowInfo($"Loading mods from {manager.Name}...");

                var mods = await manager.GetInstalledModsAsync();
                
                _messageHandler.ShowSuccess($"Loaded {mods.Count()} mods from {manager.Name}");

                return mods;
            }
            catch (Exception ex)
            {
                _messageHandler.ShowError($"Failed to load mods from {manager.Name}: {ex.Message}");
                return Enumerable.Empty<ModInfo>();
            }
        }

        public async Task<Dictionary<string, string>> GetConsolidatedLoadOrderAsync()
        {
            // Check if mod managers are disabled
            var settings = await _settingsService.LoadSettingsAsync();
            if (settings.ModManagerSettings?.SkipModManagerIntegration == true || 
                settings.AutoDetectModManagers == false)
            {
                return new Dictionary<string, string>();
            }

            var manager = await GetActiveManagerAsync();
            if (manager == null)
                return new Dictionary<string, string>();

            try
            {
                return await manager.GetLoadOrderAsync();
            }
            catch (Exception ex)
            {
                _messageHandler.ShowWarning($"Failed to load order from {manager.Name}: {ex.Message}");
                return new Dictionary<string, string>();
            }
        }

        public async Task<string?> GetModStagingFolderAsync()
        {
            // Check if mod managers are disabled
            var settings = await _settingsService.LoadSettingsAsync();
            if (settings.ModManagerSettings?.SkipModManagerIntegration == true || 
                settings.AutoDetectModManagers == false)
            {
                return null;
            }

            var manager = await GetActiveManagerAsync();
            if (manager == null)
                return null;

            try
            {
                return await manager.GetStagingFolderAsync();
            }
            catch (Exception ex)
            {
                _messageHandler.ShowWarning($"Failed to get staging folder from {manager.Name}: {ex.Message}");
                return null;
            }
        }

        public void SetPreferredManager(ModManagerType type)
        {
            _preferredManager = type;
        }
    }
}
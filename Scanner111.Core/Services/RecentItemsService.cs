using Scanner111.Core.Infrastructure;

namespace Scanner111.Core.Services;

public class RecentItemsService : IRecentItemsService
{
    private readonly Dictionary<string, DateTime> _accessTimes = new();
    private readonly IApplicationSettingsService? _settingsService;

    public RecentItemsService(IApplicationSettingsService? settingsService = null)
    {
        _settingsService = settingsService;
    }

    public event EventHandler<RecentItemsChangedEventArgs>? RecentItemsChanged;

    public IReadOnlyList<RecentItem> GetRecentLogFiles()
    {
        if (_settingsService == null) return new List<RecentItem>();
        var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        return ConvertToRecentItems(settings.RecentLogFiles, RecentItemType.LogFile);
    }

    public IReadOnlyList<RecentItem> GetRecentGamePaths()
    {
        if (_settingsService == null) return new List<RecentItem>();
        var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        return ConvertToRecentItems(settings.RecentGamePaths, RecentItemType.GamePath);
    }

    public IReadOnlyList<RecentItem> GetRecentScanDirectories()
    {
        if (_settingsService == null) return new List<RecentItem>();
        var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
        return ConvertToRecentItems(settings.RecentScanDirectories, RecentItemType.ScanDirectory);
    }

    public void AddRecentLogFile(string path)
    {
        AddRecentItem(path, RecentItemType.LogFile);
    }

    public void AddRecentGamePath(string path)
    {
        AddRecentItem(path, RecentItemType.GamePath);
    }

    public void AddRecentScanDirectory(string path)
    {
        AddRecentItem(path, RecentItemType.ScanDirectory);
    }

    public void ClearRecentLogFiles()
    {
        ClearRecentItems(RecentItemType.LogFile);
    }

    public void ClearRecentGamePaths()
    {
        ClearRecentItems(RecentItemType.GamePath);
    }

    public void ClearRecentScanDirectories()
    {
        ClearRecentItems(RecentItemType.ScanDirectory);
    }

    public void ClearAllRecentItems()
    {
        if (_settingsService != null)
            Task.Run(async () =>
            {
                var settings = await _settingsService.LoadSettingsAsync();
                settings.RecentLogFiles.Clear();
                settings.RecentGamePaths.Clear();
                settings.RecentScanDirectories.Clear();
                await _settingsService.SaveSettingsAsync(settings);
            });

        _accessTimes.Clear();

        // Notify for each type
        RecentItemsChanged?.Invoke(this, new RecentItemsChangedEventArgs { ItemType = RecentItemType.LogFile });
        RecentItemsChanged?.Invoke(this, new RecentItemsChangedEventArgs { ItemType = RecentItemType.GamePath });
        RecentItemsChanged?.Invoke(this, new RecentItemsChangedEventArgs { ItemType = RecentItemType.ScanDirectory });
    }

    public bool RemoveRecentItem(RecentItemType type, string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        var removed = false;

        if (_settingsService != null)
        {
            var settings = _settingsService.LoadSettingsAsync().GetAwaiter().GetResult();
            switch (type)
            {
                case RecentItemType.LogFile:
                    removed = settings.RecentLogFiles.Remove(path);
                    break;
                case RecentItemType.GamePath:
                    removed = settings.RecentGamePaths.Remove(path);
                    break;
                case RecentItemType.ScanDirectory:
                    removed = settings.RecentScanDirectories.Remove(path);
                    break;
            }

            if (removed) _settingsService.SaveSettingsAsync(settings).GetAwaiter().GetResult();
        }

        if (removed)
        {
            _accessTimes.Remove(path);

            RecentItemsChanged?.Invoke(this, new RecentItemsChangedEventArgs
            {
                ItemType = type,
                RemovedPath = path
            });
        }

        return removed;
    }

    public async Task<bool> IsFileAccessibleAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        return await Task.Run(() =>
        {
            try
            {
                if (File.Exists(path))
                {
                    // Try to open the file to check if it's accessible
                    using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    return true;
                }

                if (Directory.Exists(path))
                {
                    // For directories, check if we can enumerate files
                    _ = Directory.GetFiles(path, "*", SearchOption.TopDirectoryOnly).Take(1).ToList();
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }).ConfigureAwait(false);
    }

    private void AddRecentItem(string path, RecentItemType type)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        _accessTimes[path] = DateTime.Now;
        if (_settingsService != null)
            Task.Run(async () =>
            {
                var settings = await _settingsService.LoadSettingsAsync();
                switch (type)
                {
                    case RecentItemType.LogFile:
                        settings.AddRecentLogFile(path);
                        break;
                    case RecentItemType.GamePath:
                        settings.AddRecentGamePath(path);
                        break;
                    case RecentItemType.ScanDirectory:
                        settings.AddRecentScanDirectory(path);
                        break;
                }

                await _settingsService.SaveSettingsAsync(settings);
            });

        RecentItemsChanged?.Invoke(this, new RecentItemsChangedEventArgs
        {
            ItemType = type,
            AddedPath = path
        });
    }

    private void ClearRecentItems(RecentItemType type)
    {
        if (_settingsService != null)
            Task.Run(async () =>
            {
                var settings = await _settingsService.LoadSettingsAsync();
                switch (type)
                {
                    case RecentItemType.LogFile:
                        settings.RecentLogFiles.Clear();
                        break;
                    case RecentItemType.GamePath:
                        settings.RecentGamePaths.Clear();
                        break;
                    case RecentItemType.ScanDirectory:
                        settings.RecentScanDirectories.Clear();
                        break;
                }

                await _settingsService.SaveSettingsAsync(settings);
            });

        RecentItemsChanged?.Invoke(this, new RecentItemsChangedEventArgs
        {
            ItemType = type
        });
    }

    private IReadOnlyList<RecentItem> ConvertToRecentItems(List<string> paths, RecentItemType type)
    {
        var items = new List<RecentItem>();

        foreach (var path in paths)
        {
            var item = new RecentItem
            {
                Path = path,
                Type = type,
                DisplayName = GetDisplayName(path),
                Exists = CheckExists(path, type),
                LastAccessed = _accessTimes.ContainsKey(path) ? _accessTimes[path] : DateTime.MinValue
            };

            items.Add(item);
        }

        return items;
    }

    private static string GetDisplayName(string path)
    {
        try
        {
            if (File.Exists(path)) return Path.GetFileName(path);

            if (Directory.Exists(path))
            {
                var dirName = Path.GetFileName(path);
                return string.IsNullOrEmpty(dirName) ? path : dirName;
            }

            // For non-existent paths, still try to get a reasonable name
            var name = Path.GetFileName(path);
            return string.IsNullOrEmpty(name) ? path : name;
        }
        catch
        {
            return path;
        }
    }

    private static bool CheckExists(string path, RecentItemType type)
    {
        try
        {
            return type == RecentItemType.LogFile ? File.Exists(path) : Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }
}
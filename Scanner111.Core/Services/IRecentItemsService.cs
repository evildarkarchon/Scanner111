namespace Scanner111.Core.Services;

public interface IRecentItemsService
{
    IReadOnlyList<RecentItem> GetRecentLogFiles();
    IReadOnlyList<RecentItem> GetRecentGamePaths();
    IReadOnlyList<RecentItem> GetRecentScanDirectories();

    void AddRecentLogFile(string path);
    void AddRecentGamePath(string path);
    void AddRecentScanDirectory(string path);

    void ClearRecentLogFiles();
    void ClearRecentGamePaths();
    void ClearRecentScanDirectories();
    void ClearAllRecentItems();

    bool RemoveRecentItem(RecentItemType type, string path);
    Task<bool> IsFileAccessibleAsync(string path);

    event EventHandler<RecentItemsChangedEventArgs>? RecentItemsChanged;
}

public class RecentItem
{
    public string Path { get; set; } = "";
    public DateTime LastAccessed { get; set; }
    public bool Exists { get; set; }
    public string DisplayName { get; set; } = "";
    public RecentItemType Type { get; set; }
}

public enum RecentItemType
{
    LogFile,
    GamePath,
    ScanDirectory
}

public class RecentItemsChangedEventArgs : EventArgs
{
    public RecentItemType ItemType { get; set; }
    public string? AddedPath { get; set; }
    public string? RemovedPath { get; set; }
}
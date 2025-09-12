using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Scanner111.CLI.Services;

/// <summary>
/// Service for monitoring file changes with advanced features.
/// </summary>
public class FileWatcher : IFileWatcher
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastChangeTime = new();
    private readonly ConcurrentDictionary<string, FileInfo> _fileInfoCache = new();
    private readonly ILogger<FileWatcher> _logger;
    private readonly object _lock = new();
    private readonly Timer _debounceTimer;
    private readonly Queue<FileChangedEventArgs> _pendingEvents = new();
    
    private bool _disposed = false;
    private int _debounceDelayMs = 500; // Default 500ms debounce
    
    /// <summary>
    /// Event raised when a monitored file changes.
    /// </summary>
    public event EventHandler<FileChangedEventArgs>? FileChanged;
    
    /// <summary>
    /// Event triggered when an error occurs during file watching.
    /// </summary>
    public event EventHandler<FileWatcherErrorEventArgs>? Error;
    
    /// <summary>
    /// Gets whether the watcher is currently active.
    /// </summary>
    public bool IsWatching => _watchers.Any(kvp => kvp.Value.EnableRaisingEvents);
    
    /// <summary>
    /// Gets the list of currently watched files.
    /// </summary>
    public IReadOnlyList<string> WatchedFiles => _watchers.Keys.ToList();
    
    /// <summary>
    /// Gets the path being watched (for backward compatibility).
    /// </summary>
    public string? WatchedPath => _watchers.Keys.FirstOrDefault();
    
    /// <summary>
    /// Initializes a new instance of the <see cref="FileWatcher"/> class.
    /// </summary>
    public FileWatcher(ILogger<FileWatcher> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _debounceTimer = new Timer(ProcessPendingEvents, null, Timeout.Infinite, Timeout.Infinite);
    }
    
    /// <summary>
    /// Starts monitoring a file for changes.
    /// </summary>
    /// <param name="filePath">The file path to monitor.</param>
    /// <param name="filter">Optional filter pattern (e.g., "*.log").</param>
    public void StartWatching(string filePath, string filter = "*.*")
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath));
        
        try
        {
            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath)!;
            var fileName = Path.GetFileName(fullPath);
            
            if (!File.Exists(fullPath))
            {
                _logger.LogWarning("File does not exist: {FilePath}", filePath);
                OnError(filePath, new FileNotFoundException($"File not found: {filePath}"), "File not found");
                return;
            }
            
            // Stop watching this file if already being watched
            StopWatching(fullPath);
            
            var watcher = new FileSystemWatcher(directory)
            {
                Filter = string.IsNullOrEmpty(filter) || filter == "*.*" ? fileName : filter,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                EnableRaisingEvents = false,
                IncludeSubdirectories = false
            };
            
            watcher.Changed += OnFileSystemChanged;
            watcher.Created += OnFileSystemChanged;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnFileSystemError;
            
            _watchers[fullPath] = watcher;
            _fileInfoCache[fullPath] = new FileInfo(fullPath);
            
            watcher.EnableRaisingEvents = true;
            
            _logger.LogInformation("Started watching file: {FilePath} with filter: {Filter}", filePath, filter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start watching file: {FilePath}", filePath);
            OnError(filePath, ex, $"Failed to start watching file: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Starts watching multiple files.
    /// </summary>
    /// <param name="filePaths">The files to watch.</param>
    /// <param name="filter">Optional filter pattern (e.g., "*.log").</param>
    public void StartWatchingMultiple(IEnumerable<string> filePaths, string filter = "*.*")
    {
        if (filePaths == null)
            throw new ArgumentNullException(nameof(filePaths));
        
        foreach (var filePath in filePaths)
        {
            StartWatching(filePath, filter);
        }
    }
    
    /// <summary>
    /// Stops monitoring all files.
    /// </summary>
    public void StopWatching()
    {
        lock (_lock)
        {
            var watchedFiles = _watchers.Keys.ToList();
            
            foreach (var filePath in watchedFiles)
            {
                StopWatching(filePath);
            }
            
            _pendingEvents.Clear();
            _debounceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            _logger.LogInformation("Stopped watching all files");
        }
    }
    
    /// <summary>
    /// Stops watching a specific file.
    /// </summary>
    /// <param name="filePath">The file to stop watching.</param>
    public void StopWatching(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;
        
        var fullPath = Path.GetFullPath(filePath);
        
        if (_watchers.TryRemove(fullPath, out var watcher))
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Changed -= OnFileSystemChanged;
                watcher.Created -= OnFileSystemChanged;
                watcher.Renamed -= OnFileRenamed;
                watcher.Error -= OnFileSystemError;
                watcher.Dispose();
                
                _lastChangeTime.TryRemove(fullPath, out _);
                _fileInfoCache.TryRemove(fullPath, out _);
                
                _logger.LogInformation("Stopped watching file: {FilePath}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error stopping file watcher for: {FilePath}", filePath);
            }
        }
    }
    
    /// <summary>
    /// Sets the debounce delay for file change events.
    /// </summary>
    /// <param name="delay">The delay in milliseconds.</param>
    public void SetDebounceDelay(int delay)
    {
        if (delay < 0) throw new ArgumentOutOfRangeException(nameof(delay), "Delay must be non-negative");
        
        _debounceDelayMs = delay;
        _logger.LogDebug("Set debounce delay to {Delay}ms", delay);
    }
    
    /// <summary>
    /// Forces a manual check of all watched files.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ForceCheckAsync()
    {
        var tasks = new List<Task>();
        
        foreach (var kvp in _watchers.ToList())
        {
            tasks.Add(Task.Run(() => CheckFileChanged(kvp.Key)));
        }
        
        if (tasks.Any())
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
            _logger.LogDebug("Completed force check of {Count} files", tasks.Count);
        }
    }
    
    /// <summary>
    /// Releases all resources used by the FileWatcher.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            StopWatching();
            _debounceTimer?.Dispose();
            _disposed = true;
        }
    }
    
    private void OnFileSystemChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            var filePath = e.FullPath;
            var now = DateTime.UtcNow;
            
            // Update last change time
            _lastChangeTime[filePath] = now;
            
            // Add to pending events for debouncing
            lock (_lock)
            {
                _pendingEvents.Enqueue(new FileChangedEventArgs(filePath, e.ChangeType));
            }
            
            // Start or restart the debounce timer
            _debounceTimer.Change(_debounceDelayMs, Timeout.Infinite);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file system change event for: {FilePath}", e.FullPath);
            OnError(e.FullPath, ex, "Error processing file change event");
        }
    }
    
    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            _logger.LogDebug("File renamed: {OldPath} -> {FullPath}", e.OldFullPath, e.FullPath);
            
            // If the new name matches our filter, treat it as a change
            if (Path.GetFileName(e.FullPath) == Path.GetFileName(WatchedPath))
            {
                FileChanged?.Invoke(this, new FileChangedEventArgs(e.FullPath, e.ChangeType));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling file rename event");
        }
    }
    
    private void OnFileSystemError(object sender, ErrorEventArgs e)
    {
        var watcher = sender as FileSystemWatcher;
        var filePath = watcher?.Path ?? "Unknown";
        
        _logger.LogError(e.GetException(), "File system watcher error for path: {Path}", filePath);
        OnError(filePath, e.GetException(), "File system watcher error");
        
        // Try to recover by restarting the watcher
        if (watcher != null)
        {
            _ = Task.Run(() => RecoverWatcher(watcher));
        }
    }
    
    private async Task RecoverWatcher(FileSystemWatcher failedWatcher)
    {
        try
        {
            await Task.Delay(1000).ConfigureAwait(false); // Wait before recovery attempt
            
            var watcherEntry = _watchers.FirstOrDefault(kvp => kvp.Value == failedWatcher);
            if (!string.IsNullOrEmpty(watcherEntry.Key))
            {
                _logger.LogInformation("Attempting to recover file watcher for: {FilePath}", watcherEntry.Key);
                
                // Restart watching the file
                var filePath = watcherEntry.Key;
                StopWatching(filePath);
                
                // Wait a moment then restart
                await Task.Delay(500).ConfigureAwait(false);
                StartWatching(filePath);
                
                _logger.LogInformation("Successfully recovered file watcher for: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover file watcher");
        }
    }
    
    private void ProcessPendingEvents(object? state)
    {
        try
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var eventsToProcess = new List<FileChangedEventArgs>();
                
                // Group events by file path and take the most recent one for each file
                var eventGroups = new Dictionary<string, FileChangedEventArgs>();
                
                while (_pendingEvents.Count > 0)
                {
                    var eventArgs = _pendingEvents.Dequeue();
                    
                    // Only process if the debounce period has elapsed
                    if (_lastChangeTime.TryGetValue(eventArgs.FullPath, out var lastChange) &&
                        (now - lastChange).TotalMilliseconds >= _debounceDelayMs)
                    {
                        eventGroups[eventArgs.FullPath] = eventArgs;
                    }
                    else
                    {
                        // Put it back for later processing
                        _pendingEvents.Enqueue(eventArgs);
                    }
                }
                
                eventsToProcess.AddRange(eventGroups.Values);
                
                // Process the debounced events
                foreach (var eventArgs in eventsToProcess)
                {
                    ProcessFileChange(eventArgs);
                }
                
                // If there are still pending events, restart the timer
                if (_pendingEvents.Count > 0)
                {
                    _debounceTimer.Change(_debounceDelayMs, Timeout.Infinite);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing pending file change events");
        }
    }
    
    private void ProcessFileChange(FileChangedEventArgs eventArgs)
    {
        try
        {
            var filePath = eventArgs.FullPath;
            
            // Verify the file actually changed by comparing with cached info
            if (HasFileReallyChanged(filePath))
            {
                _logger.LogDebug("File change detected: {FilePath} ({ChangeType})", filePath, eventArgs.ChangeType);
                FileChanged?.Invoke(this, eventArgs);
            }
            else
            {
                _logger.LogTrace("File change event ignored (no actual change): {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file change: {FilePath}", eventArgs.FullPath);
            OnError(eventArgs.FullPath, ex, "Error processing file change");
        }
    }
    
    private bool HasFileReallyChanged(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                // File was deleted, that's a real change
                _fileInfoCache.TryRemove(filePath, out _);
                return true;
            }
            
            var currentInfo = new FileInfo(filePath);
            
            if (!_fileInfoCache.TryGetValue(filePath, out var cachedInfo))
            {
                // No cached info, assume it's a real change
                _fileInfoCache[filePath] = currentInfo;
                return true;
            }
            
            // Compare file properties
            var hasChanged = currentInfo.Length != cachedInfo.Length ||
                           currentInfo.LastWriteTimeUtc != cachedInfo.LastWriteTimeUtc;
            
            if (hasChanged)
            {
                _fileInfoCache[filePath] = currentInfo;
            }
            
            return hasChanged;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking if file really changed: {FilePath}", filePath);
            return true; // Assume it changed if we can't determine
        }
    }
    
    private void CheckFileChanged(string filePath)
    {
        try
        {
            if (HasFileReallyChanged(filePath))
            {
                var eventArgs = new FileChangedEventArgs(filePath, WatcherChangeTypes.Changed);
                FileChanged?.Invoke(this, eventArgs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual file check: {FilePath}", filePath);
            OnError(filePath, ex, "Error during manual file check");
        }
    }
    
    private void OnError(string filePath, Exception exception, string message)
    {
        var errorArgs = new FileWatcherErrorEventArgs
        {
            FilePath = filePath,
            Exception = exception,
            Message = message
        };
        
        Error?.Invoke(this, errorArgs);
    }
}
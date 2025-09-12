using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace Scanner111.CLI.UI;

/// <summary>
/// Delegate for handling keyboard shortcuts.
/// </summary>
/// <param name="context">The context when the shortcut was triggered.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>True if the shortcut was handled; otherwise, false.</returns>
public delegate Task<bool> KeyboardShortcutHandler(KeyboardContext context, CancellationToken cancellationToken = default);

/// <summary>
/// Context information for keyboard shortcuts.
/// </summary>
/// <param name="Key">The key that was pressed.</param>
/// <param name="Modifiers">The modifier keys that were pressed.</param>
/// <param name="ScreenContext">Optional context about the current screen.</param>
public record KeyboardContext(
    ConsoleKey Key,
    ConsoleModifiers Modifiers,
    string? ScreenContext = null);

/// <summary>
/// Represents a keyboard shortcut configuration.
/// </summary>
/// <param name="Key">The primary key.</param>
/// <param name="Modifiers">Required modifier keys.</param>
/// <param name="Description">Description of what the shortcut does.</param>
/// <param name="Context">Optional context where this shortcut applies.</param>
/// <param name="Handler">The handler function.</param>
public record KeyboardShortcut(
    ConsoleKey Key,
    ConsoleModifiers Modifiers,
    string Description,
    string? Context,
    KeyboardShortcutHandler Handler);

/// <summary>
/// Service for handling global keyboard shortcuts and customization.
/// </summary>
public class KeyboardHandler : IDisposable
{
    private readonly ILogger<KeyboardHandler> _logger;
    private readonly ConcurrentDictionary<string, KeyboardShortcut> _shortcuts = new();
    private readonly ConcurrentDictionary<string, KeyboardShortcut> _contextShortcuts = new();
    private readonly Dictionary<string, string> _customizations = new();
    private readonly object _lock = new();
    private bool _disposed = false;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="KeyboardHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public KeyboardHandler(ILogger<KeyboardHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RegisterDefaultShortcuts();
        LoadCustomizations();
    }
    
    /// <summary>
    /// Registers a global keyboard shortcut.
    /// </summary>
    /// <param name="key">The key to register.</param>
    /// <param name="modifiers">Required modifier keys.</param>
    /// <param name="description">Description of the shortcut.</param>
    /// <param name="handler">The handler function.</param>
    /// <param name="context">Optional context where this shortcut applies.</param>
    public void RegisterShortcut(
        ConsoleKey key,
        ConsoleModifiers modifiers,
        string description,
        KeyboardShortcutHandler handler,
        string? context = null)
    {
        var shortcut = new KeyboardShortcut(key, modifiers, description, context, handler);
        var shortcutKey = GetShortcutKey(key, modifiers);
        
        lock (_lock)
        {
            if (string.IsNullOrEmpty(context))
            {
                _shortcuts[shortcutKey] = shortcut;
                _logger.LogDebug("Registered global shortcut: {Key}+{Modifiers} - {Description}", key, modifiers, description);
            }
            else
            {
                var contextKey = $"{context}:{shortcutKey}";
                _contextShortcuts[contextKey] = shortcut;
                _logger.LogDebug("Registered context shortcut: {Context}:{Key}+{Modifiers} - {Description}", context, key, modifiers, description);
            }
        }
    }
    
    /// <summary>
    /// Processes a key press and executes any matching shortcuts.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">The modifier keys that were pressed.</param>
    /// <param name="screenContext">Optional screen context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if a shortcut was handled; otherwise, false.</returns>
    public async Task<bool> ProcessKeyAsync(
        ConsoleKey key,
        ConsoleModifiers modifiers,
        string? screenContext = null,
        CancellationToken cancellationToken = default)
    {
        if (_disposed) return false;
        
        var context = new KeyboardContext(key, modifiers, screenContext);
        var shortcutKey = GetShortcutKey(key, modifiers);
        
        try
        {
            // Try context-specific shortcuts first
            if (!string.IsNullOrEmpty(screenContext))
            {
                var contextKey = $"{screenContext}:{shortcutKey}";
                if (_contextShortcuts.TryGetValue(contextKey, out var contextShortcut))
                {
                    _logger.LogDebug("Executing context shortcut: {Context}:{Key}+{Modifiers}", screenContext, key, modifiers);
                    return await contextShortcut.Handler(context, cancellationToken);
                }
            }
            
            // Try global shortcuts
            if (_shortcuts.TryGetValue(shortcutKey, out var globalShortcut))
            {
                _logger.LogDebug("Executing global shortcut: {Key}+{Modifiers}", key, modifiers);
                return await globalShortcut.Handler(context, cancellationToken);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing keyboard shortcut: {Key}+{Modifiers}", key, modifiers);
            return false;
        }
    }
    
    /// <summary>
    /// Gets all registered shortcuts, optionally filtered by context.
    /// </summary>
    /// <param name="context">Optional context to filter by.</param>
    /// <returns>Collection of shortcuts.</returns>
    public IEnumerable<KeyboardShortcut> GetShortcuts(string? context = null)
    {
        lock (_lock)
        {
            var shortcuts = new List<KeyboardShortcut>();
            
            // Add global shortcuts
            shortcuts.AddRange(_shortcuts.Values);
            
            // Add context-specific shortcuts
            if (!string.IsNullOrEmpty(context))
            {
                var contextPrefix = $"{context}:";
                shortcuts.AddRange(_contextShortcuts
                    .Where(kvp => kvp.Key.StartsWith(contextPrefix))
                    .Select(kvp => kvp.Value));
            }
            
            return shortcuts.OrderBy(s => s.Key).ThenBy(s => s.Modifiers);
        }
    }
    
    /// <summary>
    /// Creates a help panel showing available shortcuts.
    /// </summary>
    /// <param name="context">Optional context to show shortcuts for.</param>
    /// <returns>A panel containing shortcut information.</returns>
    public Panel CreateHelpPanel(string? context = null)
    {
        var shortcuts = GetShortcuts(context);
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("[bold]Shortcut[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Description[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Context[/]").LeftAligned());
        
        foreach (var shortcut in shortcuts)
        {
            var keyText = FormatShortcut(shortcut.Key, shortcut.Modifiers);
            var contextText = string.IsNullOrEmpty(shortcut.Context) ? "Global" : shortcut.Context;
            
            table.AddRow(
                $"[yellow]{keyText}[/]",
                shortcut.Description,
                $"[dim]{contextText}[/]"
            );
        }
        
        if (!shortcuts.Any())
        {
            table.AddRow("[dim]No shortcuts available[/]", "", "");
        }
        
        return new Panel(table)
        {
            Header = new PanelHeader($"[cyan]Keyboard Shortcuts{(string.IsNullOrEmpty(context) ? "" : $" - {context}")}[/]"),
            Border = BoxBorder.Rounded
        };
    }
    
    /// <summary>
    /// Customizes a shortcut key binding.
    /// </summary>
    /// <param name="originalKey">The original key.</param>
    /// <param name="originalModifiers">The original modifiers.</param>
    /// <param name="newKey">The new key.</param>
    /// <param name="newModifiers">The new modifiers.</param>
    /// <param name="context">Optional context.</param>
    public void CustomizeShortcut(
        ConsoleKey originalKey,
        ConsoleModifiers originalModifiers,
        ConsoleKey newKey,
        ConsoleModifiers newModifiers,
        string? context = null)
    {
        lock (_lock)
        {
            var originalShortcutKey = GetShortcutKey(originalKey, originalModifiers);
            var newShortcutKey = GetShortcutKey(newKey, newModifiers);
            var customizationKey = string.IsNullOrEmpty(context) ? originalShortcutKey : $"{context}:{originalShortcutKey}";
            
            _customizations[customizationKey] = newShortcutKey;
            _logger.LogInformation("Customized shortcut: {Original} -> {New} (Context: {Context})", 
                originalShortcutKey, newShortcutKey, context ?? "Global");
        }
        
        SaveCustomizations();
    }
    
    /// <summary>
    /// Resets all customizations to defaults.
    /// </summary>
    public void ResetCustomizations()
    {
        lock (_lock)
        {
            _customizations.Clear();
        }
        
        SaveCustomizations();
        _logger.LogInformation("Reset all keyboard customizations to defaults");
    }
    
    private void RegisterDefaultShortcuts()
    {
        // Global shortcuts
        RegisterShortcut(ConsoleKey.F1, ConsoleModifiers.None, "Show help", ShowHelpAsync);
        RegisterShortcut(ConsoleKey.F5, ConsoleModifiers.None, "Refresh current view", RefreshAsync);
        RegisterShortcut(ConsoleKey.Escape, ConsoleModifiers.None, "Go back or exit", GoBackAsync);
        RegisterShortcut(ConsoleKey.Q, ConsoleModifiers.None, "Quit application", QuitAsync);
        RegisterShortcut(ConsoleKey.S, ConsoleModifiers.Control, "Save current state", SaveAsync);
        RegisterShortcut(ConsoleKey.O, ConsoleModifiers.Control, "Open file", OpenAsync);
        
        // Navigation shortcuts
        RegisterShortcut(ConsoleKey.Tab, ConsoleModifiers.None, "Next item", NextItemAsync);
        RegisterShortcut(ConsoleKey.Tab, ConsoleModifiers.Shift, "Previous item", PreviousItemAsync);
        RegisterShortcut(ConsoleKey.Enter, ConsoleModifiers.None, "Select/Confirm", SelectAsync);
        
        // Analysis shortcuts
        RegisterShortcut(ConsoleKey.R, ConsoleModifiers.Control, "Run analysis", RunAnalysisAsync);
        RegisterShortcut(ConsoleKey.M, ConsoleModifiers.Control, "Monitor log file", MonitorLogAsync);
        RegisterShortcut(ConsoleKey.H, ConsoleModifiers.Control, "View session history", ViewHistoryAsync);
        
        _logger.LogDebug("Registered {Count} default keyboard shortcuts", _shortcuts.Count);
    }
    
    private void LoadCustomizations()
    {
        try
        {
            var configPath = GetCustomizationPath();
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var customizations = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                if (customizations != null)
                {
                    lock (_lock)
                    {
                        foreach (var kvp in customizations)
                        {
                            _customizations[kvp.Key] = kvp.Value;
                        }
                    }
                    
                    _logger.LogDebug("Loaded {Count} keyboard customizations", customizations.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load keyboard customizations");
        }
    }
    
    private void SaveCustomizations()
    {
        try
        {
            var configPath = GetCustomizationPath();
            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            
            var json = System.Text.Json.JsonSerializer.Serialize(_customizations, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            
            File.WriteAllText(configPath, json);
            _logger.LogDebug("Saved keyboard customizations to {Path}", configPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save keyboard customizations");
        }
    }
    
    private static string GetCustomizationPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scanner111",
            "keyboard-shortcuts.json");
    }
    
    private static string GetShortcutKey(ConsoleKey key, ConsoleModifiers modifiers)
    {
        return $"{modifiers}+{key}";
    }
    
    private static string FormatShortcut(ConsoleKey key, ConsoleModifiers modifiers)
    {
        var parts = new List<string>();
        
        if (modifiers.HasFlag(ConsoleModifiers.Control))
            parts.Add("Ctrl");
        if (modifiers.HasFlag(ConsoleModifiers.Alt))
            parts.Add("Alt");
        if (modifiers.HasFlag(ConsoleModifiers.Shift))
            parts.Add("Shift");
        
        parts.Add(key.ToString());
        
        return string.Join("+", parts);
    }
    
    // Default shortcut handlers - these can be overridden by registering new handlers
    private async Task<bool> ShowHelpAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        // This would typically be handled by the current screen
        _logger.LogDebug("Help shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> RefreshAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Refresh shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> GoBackAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Go back shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> QuitAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Quit shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the application handle it
    }
    
    private async Task<bool> SaveAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Save shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> OpenAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Open shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> NextItemAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Next item shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> PreviousItemAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Previous item shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> SelectAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Select shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> RunAnalysisAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Run analysis shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> MonitorLogAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Monitor log shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    private async Task<bool> ViewHistoryAsync(KeyboardContext context, CancellationToken cancellationToken)
    {
        _logger.LogDebug("View history shortcut triggered");
        await Task.CompletedTask;
        return false; // Let the screen handle it
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            SaveCustomizations();
            _shortcuts.Clear();
            _contextShortcuts.Clear();
            _customizations.Clear();
            _disposed = true;
        }
    }
}
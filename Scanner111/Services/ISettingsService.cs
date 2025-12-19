using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace Scanner111.Services;

/// <summary>
/// Service for managing application settings that can be shared between ViewModels.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets or sets the scan path for crash logs.
    /// </summary>
    string ScanPath { get; set; }

    /// <summary>
    /// Gets or sets the mods folder path.
    /// </summary>
    string ModsFolderPath { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent scans.
    /// </summary>
    int MaxConcurrent { get; set; }

    /// <summary>
    /// Gets or sets whether FCX mode is enabled.
    /// </summary>
    bool FcxMode { get; set; }

    /// <summary>
    /// Gets or sets whether FormID values should be shown.
    /// </summary>
    bool ShowFormIdValues { get; set; }

    /// <summary>
    /// Gets or sets whether to check for updates on application startup.
    /// </summary>
    bool CheckForUpdatesOnStartup { get; set; }

    /// <summary>
    /// Gets or sets whether to include prerelease versions in update checks.
    /// </summary>
    bool IncludePrereleases { get; set; }

    /// <summary>
    /// Gets or sets whether VR mode is enabled.
    /// Affects path detection and game executable names.
    /// </summary>
    bool VrMode { get; set; }

    /// <summary>
    /// Gets or sets whether to simplify crash log output.
    /// Warning: May remove important troubleshooting information.
    /// </summary>
    bool SimplifyLogs { get; set; }

    /// <summary>
    /// Gets or sets whether to move unsolved logs to a subfolder.
    /// </summary>
    bool MoveUnsolvedLogs { get; set; }

    /// <summary>
    /// Gets or sets the custom INI folder path.
    /// </summary>
    string IniFolderPath { get; set; }

    /// <summary>
    /// Loads settings from persistent storage.
    /// </summary>
    void Load();

    /// <summary>
    /// Saves settings to persistent storage.
    /// </summary>
    void Save();

    /// <summary>
    /// Resets settings to default values.
    /// </summary>
    void ResetToDefaults();

    /// <summary>
    /// Gets or sets the preferred window dimensions for a specific page key.
    /// </summary>
    /// <param name="pageKey">The unique key for the page (e.g., ViewModel type name).</param>
    /// <param name="width">The width to save.</param>
    /// <param name="height">The height to save.</param>
    void SaveWindowSize(string pageKey, double width, double height);

    /// <summary>
    /// Gets the preferred window dimensions for a specific page key.
    /// Returns a default size if not found.
    /// </summary>
    (double Width, double Height) GetWindowSize(string pageKey);
}

/// <summary>
/// Implementation of ISettingsService with JSON persistence.
/// </summary>
public class SettingsService : ReactiveObject, ISettingsService
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "CLASSIC");

    private static readonly string SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");

    [Reactive] public string ScanPath { get; set; } = string.Empty;
    [Reactive] public string ModsFolderPath { get; set; } = string.Empty;
    [Reactive] public int MaxConcurrent { get; set; } = 50;
    [Reactive] public bool FcxMode { get; set; }
    [Reactive] public bool ShowFormIdValues { get; set; }
    [Reactive] public bool CheckForUpdatesOnStartup { get; set; } = true;
    [Reactive] public bool IncludePrereleases { get; set; }
    [Reactive] public bool VrMode { get; set; }
    [Reactive] public bool SimplifyLogs { get; set; }
    [Reactive] public bool MoveUnsolvedLogs { get; set; }
    [Reactive] public string IniFolderPath { get; set; } = string.Empty;

    // Dictionary to store saved window sizes for each page
    private Dictionary<string, WindowSizeData> _windowSizes = new();

    public SettingsService()
    {
        Load();
    }

    public void Load()
    {
        if (!File.Exists(SettingsFilePath)) return;

        try
        {
            var json = File.ReadAllText(SettingsFilePath);
            var data = JsonSerializer.Deserialize<SettingsData>(json);
            if (data != null)
            {
                ScanPath = data.ScanPath ?? string.Empty;
                ModsFolderPath = data.ModsFolderPath ?? string.Empty;
                MaxConcurrent = data.MaxConcurrent;
                FcxMode = data.FcxMode;
                ShowFormIdValues = data.ShowFormIdValues;
                CheckForUpdatesOnStartup = data.CheckForUpdatesOnStartup;
                IncludePrereleases = data.IncludePrereleases;
                VrMode = data.VrMode;
                SimplifyLogs = data.SimplifyLogs;
                MoveUnsolvedLogs = data.MoveUnsolvedLogs;
                IniFolderPath = data.IniFolderPath ?? string.Empty;

                if (data.WindowSizes != null)
                {
                    _windowSizes = data.WindowSizes;
                }
            }
        }
        catch
        {
            // If loading fails, use defaults
        }
    }

    public void Save()
    {
        try
        {
            if (!Directory.Exists(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
            }

            var data = new SettingsData
            {
                ScanPath = ScanPath,
                ModsFolderPath = ModsFolderPath,
                MaxConcurrent = MaxConcurrent,
                FcxMode = FcxMode,
                ShowFormIdValues = ShowFormIdValues,
                CheckForUpdatesOnStartup = CheckForUpdatesOnStartup,
                IncludePrereleases = IncludePrereleases,
                VrMode = VrMode,
                SimplifyLogs = SimplifyLogs,
                MoveUnsolvedLogs = MoveUnsolvedLogs,
                IniFolderPath = IniFolderPath,
                WindowSizes = _windowSizes
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(data, options);
            File.WriteAllText(SettingsFilePath, json);
        }
        catch
        {
            // Silently fail if saving fails
        }
    }

    public void ResetToDefaults()
    {
        ScanPath = string.Empty;
        ModsFolderPath = string.Empty;
        MaxConcurrent = 50;
        FcxMode = false;
        ShowFormIdValues = false;
        CheckForUpdatesOnStartup = true;
        IncludePrereleases = false;
        VrMode = false;
        SimplifyLogs = false;
        MoveUnsolvedLogs = false;
        IniFolderPath = string.Empty;
        _windowSizes.Clear();
    }

    public void SaveWindowSize(string pageKey, double width, double height)
    {
        if (string.IsNullOrEmpty(pageKey)) return;

        if (_windowSizes.TryGetValue(pageKey, out var existing))
        {
            existing.Width = width;
            existing.Height = height;
        }
        else
        {
            _windowSizes[pageKey] = new WindowSizeData { Width = width, Height = height };
        }
    }

    public (double Width, double Height) GetWindowSize(string pageKey)
    {
        if (!string.IsNullOrEmpty(pageKey) && _windowSizes.TryGetValue(pageKey, out var size))
        {
            return (size.Width, size.Height);
        }

        // Custom defaults per page
        if (pageKey == "AboutViewModel")
        {
            return (1100, 800);
        }

        // Return default values if not found
        return (1000, 600);
    }

    /// <summary>
    /// Internal data class for JSON serialization.
    /// </summary>
    private class SettingsData
    {
        public string? ScanPath { get; set; }
        public string? ModsFolderPath { get; set; }
        public int MaxConcurrent { get; set; } = 50;
        public bool FcxMode { get; set; }
        public bool ShowFormIdValues { get; set; }
        public bool CheckForUpdatesOnStartup { get; set; } = true;
        public bool IncludePrereleases { get; set; }
        public bool VrMode { get; set; }
        public bool SimplifyLogs { get; set; }
        public bool MoveUnsolvedLogs { get; set; }
        public string? IniFolderPath { get; set; }
        public Dictionary<string, WindowSizeData>? WindowSizes { get; set; }
    }

    /// <summary>
    /// Simple data class for window dimensions.
    /// </summary>
    public class WindowSizeData
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }
}

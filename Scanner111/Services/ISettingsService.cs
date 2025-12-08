using System;
using System.IO;
using System.Text.Json;
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
                ShowFormIdValues = ShowFormIdValues
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
    }
}


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
}

/// <summary>
/// Implementation of ISettingsService using ReactiveObject for change notifications.
/// </summary>
public class SettingsService : ReactiveObject, ISettingsService
{
    [Reactive] public string ScanPath { get; set; } = string.Empty;
    [Reactive] public string ModsFolderPath { get; set; } = string.Empty;
    [Reactive] public int MaxConcurrent { get; set; } = 50;
    [Reactive] public bool FcxMode { get; set; }
    [Reactive] public bool ShowFormIdValues { get; set; }
}

using System.Collections.Generic;

namespace Scanner111.ClassicLib;

/// <summary>
/// Contains constant values used throughout the application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Collection of settings keys that should not produce warnings when their values are null.
    /// </summary>
    public static readonly HashSet<string> SettingsIgnoreNone = new()
    {
        "SCAN Custom Path",
        "MODS Folder Path",
        "Root_Folder_Game",
        "Root_Folder_Docs"
    };
}
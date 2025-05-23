namespace Scanner111.ClassicLib
{
    /// <summary>
    /// Provides global registry information similar to the Python GlobalRegistry
    /// </summary>
    public static class GlobalRegistry
    {
        /// <summary>
        /// Gets the current game name
        /// </summary>
        /// <returns>The name of the current game</returns>
        public static string GetGame()
        {
            // This would typically be set elsewhere in the application
            // For now, return "Fallout4" as default
            return "Fallout4";
        }
    }
}


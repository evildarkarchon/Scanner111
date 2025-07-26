using System;

class TestBuffoutVersion
{
    static void Main()
    {
        // Test version parsing
        var currentVersion = ParseBuffoutVersion("Buffout 4 v1.28.6");
        var latestOriginal = ParseBuffoutVersion("Buffout 4 v1.28.6");
        var latestNg = ParseBuffoutVersion("Buffout 4 v1.37.0 Mar 12 2025 22:11:48");
        
        Console.WriteLine($"Current: {currentVersion}");
        Console.WriteLine($"Latest Original: {latestOriginal}");
        Console.WriteLine($"Latest NG: {latestNg}");
        
        if (currentVersion != null && latestOriginal != null)
        {
            Console.WriteLine($"Current >= Original: {currentVersion >= latestOriginal}");
        }
        
        if (currentVersion != null && latestNg != null)
        {
            Console.WriteLine($"Current >= NG: {currentVersion >= latestNg}");
        }
    }
    
    static Version? ParseBuffoutVersion(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
            return null;

        try
        {
            // Handle formats like "Buffout 4 v1.28.6" or "Buffout 4 v1.37.0 Mar 12 2025 22:11:48"
            var parts = versionString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            // Find the version part (starts with 'v')
            string? versionPart = null;
            foreach (var part in parts)
            {
                if (part.StartsWith("v", StringComparison.OrdinalIgnoreCase) && part.Length > 1)
                {
                    versionPart = part.Substring(1); // Remove the 'v'
                    break;
                }
            }

            if (versionPart == null)
                return null;

            // Version.Parse requires at least Major.Minor
            var versionParts = versionPart.Split('.');
            if (versionParts.Length == 2)
            {
                versionPart += ".0"; // Add revision if missing
            }
            else if (versionParts.Length == 1)
            {
                versionPart += ".0.0"; // Add minor and revision if missing
            }

            return Version.Parse(versionPart);
        }
        catch
        {
            return null;
        }
    }
}
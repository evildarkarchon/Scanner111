using System.Text.RegularExpressions;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
/// Parser for Bethesda game crash logs (Buffout 4/Crash Logger format)
/// </summary>
public static class CrashLogParser
{
    /// <summary>
    /// Parse a crash log from file content
    /// </summary>
    public static async Task<CrashLog?> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Read file with UTF-8 encoding and ignore errors (matching Python implementation)
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken);
            
            if (lines.Length < 20) // Too short to be a valid crash log
                return null;
                
            var crashLog = new CrashLog
            {
                FilePath = filePath,
                OriginalLines = lines.ToList()
            };
            
            // Parse header information
            ParseHeader(lines, crashLog);
            
            // Extract segments
            var segments = ExtractSegments(lines);
            
            // Process segments - handle missing segments gracefully
            // segments[0] = Compatibility/Crashgen Settings
            // segments[1] = System Specs
            // segments[2] = Call Stack
            // segments[3] = Modules
            // segments[4] = XSE Plugins
            // segments[5] = Plugins
            
            if (segments.Count > 0)
                ParseCrashgenSettings(segments[0], crashLog);
                
            if (segments.Count > 2)
                crashLog.CallStack = segments[2];
                
            if (segments.Count > 4)
                ParseXseModules(segments[4], crashLog);
                
            if (segments.Count > 5)
                ParsePluginsSection(segments[5], crashLog);
            
            return crashLog;
        }
        catch (OperationCanceledException)
        {
            // Let cancellation exceptions propagate
            throw;
        }
        catch
        {
            return null;
        }
    }
    
    private static void ParseHeader(string[] lines, CrashLog crashLog)
    {
        foreach (var line in lines)
        {
            // Game version (e.g., "Fallout 4 v1.10.163")
            if (line.StartsWith("Fallout") || line.StartsWith("Skyrim"))
            {
                crashLog.GameVersion = line.Trim();
            }
            
            // Crash generator version (e.g., "Buffout 4 v1.26.2")
            if (line.StartsWith("Buffout") || line.StartsWith("Crash Logger"))
            {
                crashLog.CrashGenVersion = line.Trim();
            }
            
            // Main error
            if (line.StartsWith("Unhandled exception"))
            {
                // Replace only the first occurrence of | with newline
                var index = line.IndexOf('|');
                crashLog.MainError = index >= 0 
                    ? line.Substring(0, index) + "\n" + line.Substring(index + 1) 
                    : line;
            }
        }
    }
    
    private static List<List<string>> ExtractSegments(string[] crashData)
    {
        var segments = new List<List<string>>();
        var segmentBoundaries = new List<(string start, string end)>
        {
            ("\t[Compatibility]", "SYSTEM SPECS:"),
            ("SYSTEM SPECS:", "PROBABLE CALL STACK:"),
            ("PROBABLE CALL STACK:", "MODULES:"),
            ("MODULES:", "F4SE PLUGINS:"), // Will be adjusted based on game
            ("F4SE PLUGINS:", "PLUGINS:"),
            ("PLUGINS:", "EOF")
        };
        
        // Determine XSE type based on game and adjust boundaries
        foreach (var line in crashData)
        {
            if (line.Contains("SKSE"))
            {
                segmentBoundaries[3] = ("MODULES:", "SKSE PLUGINS:");
                segmentBoundaries[4] = ("SKSE PLUGINS:", "PLUGINS:");
                break;
            }
        }
        
        var totalLines = crashData.Length;
        var currentIndex = 0;
        var segmentIndex = 0;
        var collecting = false;
        var segmentStartIndex = 0;
        var currentBoundary = segmentBoundaries[0].start;
        
        while (currentIndex < totalLines && segmentIndex < segmentBoundaries.Count)
        {
            var line = crashData[currentIndex];
            
            if (line.StartsWith(currentBoundary))
            {
                if (collecting)
                {
                    // End of current segment
                    var segmentEndIndex = currentIndex > 0 ? currentIndex : 0;
                    var segment = new List<string>();
                    for (int i = segmentStartIndex; i < segmentEndIndex; i++)
                    {
                        var trimmedLine = crashData[i].Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                            segment.Add(trimmedLine);
                    }
                    segments.Add(segment);
                    segmentIndex++;
                    
                    if (segmentIndex >= segmentBoundaries.Count)
                        break;
                }
                else
                {
                    // Start of new segment
                    segmentStartIndex = currentIndex + 1;
                }
                
                collecting = !collecting;
                currentBoundary = collecting ? segmentBoundaries[segmentIndex].end : segmentBoundaries[segmentIndex].start;
                
                // Handle EOF marker
                if (collecting && currentBoundary == "EOF")
                {
                    // Add all remaining lines
                    var segment = new List<string>();
                    for (int i = segmentStartIndex; i < totalLines; i++)
                    {
                        var trimmedLine = crashData[i].Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                            segment.Add(trimmedLine);
                    }
                    segments.Add(segment);
                    break;
                }
                
                if (!collecting)
                {
                    // Don't increment in case current line is also next boundary
                    currentIndex--;
                }
            }
            
            // Handle end of file while collecting
            if (collecting && currentIndex == totalLines - 1)
            {
                var segment = new List<string>();
                for (int i = segmentStartIndex; i <= currentIndex; i++)
                {
                    var trimmedLine = crashData[i].Trim();
                    if (!string.IsNullOrEmpty(trimmedLine))
                        segment.Add(trimmedLine);
                }
                segments.Add(segment);
            }
            
            currentIndex++;
        }
        
        // Ensure we have all expected segments (add empty if missing)
        while (segments.Count < segmentBoundaries.Count)
        {
            segments.Add(new List<string>());
        }
        
        return segments;
    }
    
    private static void ParsePluginsSection(List<string> pluginLines, CrashLog crashLog)
    {
        // Plugin format: [FE:001]   LoadOrder.esp
        var pluginRegex = new Regex(@"\[([A-F0-9]{2}):([A-F0-9]{3})\]\s+(.+)");
        
        foreach (var line in pluginLines)
        {
            var match = pluginRegex.Match(line);
            if (match.Success)
            {
                var loadOrder = $"{match.Groups[1].Value}:{match.Groups[2].Value}";
                var pluginName = match.Groups[3].Value.Trim();
                crashLog.Plugins[pluginName] = loadOrder;
            }
        }
        
        crashLog.IsIncomplete = crashLog.Plugins.Count == 0;
    }
    
    private static void ParseCrashgenSettings(List<string> settingsLines, CrashLog crashLog)
    {
        // Parse crashgen configuration settings like [Compatibility], [Patches], etc.
        foreach (var line in settingsLines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('['))
                continue;
                
            // Format: "Key: value"
            if (trimmedLine.Contains(':'))
            {
                var parts = trimmedLine.Split(':', 2);
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var valueStr = parts[1].Trim();
                    
                    // Parse value as bool, int, or string
                    object value = valueStr.ToLower() switch
                    {
                        "true" => true,
                        "false" => false,
                        _ when int.TryParse(valueStr, out var intVal) => intVal,
                        _ => valueStr
                    };
                    
                    crashLog.CrashgenSettings[key] = value;
                }
            }
        }
    }
    
    private static void ParseXseModules(List<string> xseLines, CrashLog crashLog)
    {
        // Extract module names from XSE plugins section
        // Format: "ModuleName.dll v1.2.3" or just "ModuleName.dll"
        var modulePattern = new Regex(@"^(.+?\.dll)\s*(?:v.*)?$", RegexOptions.IgnoreCase);
        
        foreach (var line in xseLines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;
                
            var match = modulePattern.Match(trimmedLine);
            if (match.Success)
            {
                var moduleName = match.Groups[1].Value.ToLower();
                crashLog.XseModules.Add(moduleName);
            }
            else
            {
                // Fallback: add the line as-is but lowercase
                crashLog.XseModules.Add(trimmedLine.ToLower());
            }
        }
    }
}
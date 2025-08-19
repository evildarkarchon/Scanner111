using Scanner111.Core.Abstractions;
using Scanner111.Core.Models;

namespace Scanner111.Core.Infrastructure;

/// <summary>
///     Utility class for parsing Bethesda game crash logs, specifically in Buffout 4/Crash Logger format.
/// </summary>
public partial class CrashLogParser : ICrashLogParser
{
    private readonly IFileSystem _fileSystem;
    
    public CrashLogParser(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }
    
    /// <summary>
    ///     Parses a crash log from the specified file path.
    /// </summary>
    /// <param name="filePath">
    ///     The path to the crash log file to parse.
    /// </param>
    /// <param name="cancellationToken">
    ///     A token to monitor for cancellation requests.
    /// </param>
    /// <returns>
    ///     A <see cref="CrashLog" /> instance containing the parsed crash log data, or null if the file is not a valid crash
    ///     log.
    /// </returns>
    public async Task<CrashLog?> ParseAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            // Read file with UTF-8 encoding and ignore errors (matching Python implementation)
            var content = await _fileSystem.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var lines = content.Split('\n', StringSplitOptions.None);

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

    /// <summary>
    ///     Parses the header section of the crash log to extract the game version, crash generator version, and main error
    ///     details.
    /// </summary>
    /// <param name="lines">
    ///     The lines of the crash log being parsed.
    /// </param>
    /// <param name="crashLog">
    ///     The <see cref="CrashLog" /> instance to populate with extracted header information.
    /// </param>
    private void ParseHeader(string[] lines, CrashLog crashLog)
    {
        foreach (var line in lines)
        {
            // Game version (e.g., "Fallout 4 v1.10.163")
            if (line.StartsWith("Fallout") || line.StartsWith("Skyrim")) crashLog.GameVersion = line.Trim();

            // Crash generator version (e.g., "Buffout 4 v1.26.2")
            if (line.StartsWith("Buffout") || line.StartsWith("Crash Logger")) crashLog.CrashGenVersion = line.Trim();

            // Main error
            if (!line.StartsWith("Unhandled exception")) continue;
            // Replace only the first occurrence of | with newline
            var index = line.IndexOf('|');
            crashLog.MainError = index >= 0
                ? string.Concat(line.AsSpan(0, index), "\n", line.AsSpan(index + 1))
                : line;
        }
    }

    /// <summary>
    ///     Extracts segments from the provided crash log data based on predefined boundaries.
    /// </summary>
    /// <param name="crashData">
    ///     An array of strings containing the lines of the crash log file.
    /// </param>
    /// <returns>
    ///     A list of segments, where each segment is represented as a list of strings.
    ///     Returns an empty list if no segments were found or the log data is invalid.
    /// </returns>
    private List<List<string>> ExtractSegments(string[] crashData)
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
        if (crashData.Any(line => line.Contains("SKSE")))
        {
            segmentBoundaries[3] = ("MODULES:", "SKSE PLUGINS:");
            segmentBoundaries[4] = ("SKSE PLUGINS:", "PLUGINS:");
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
                    for (var i = segmentStartIndex; i < segmentEndIndex; i++)
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
                currentBoundary =
                    collecting ? segmentBoundaries[segmentIndex].end : segmentBoundaries[segmentIndex].start;

                // Handle EOF marker
                if (collecting && currentBoundary == "EOF")
                {
                    // Add all remaining lines
                    var segment = new List<string>();
                    for (var i = segmentStartIndex; i < totalLines; i++)
                    {
                        var trimmedLine = crashData[i].Trim();
                        if (!string.IsNullOrEmpty(trimmedLine))
                            segment.Add(trimmedLine);
                    }

                    segments.Add(segment);
                    break;
                }

                if (!collecting)
                    // Don't increment in case current line is also next boundary
                    currentIndex--;
            }

            // Handle end of file while collecting
            if (collecting && currentIndex == totalLines - 1)
            {
                var segment = new List<string>();
                for (var i = segmentStartIndex; i <= currentIndex; i++)
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
        while (segments.Count < segmentBoundaries.Count) segments.Add(new List<string>());

        return segments;
    }

    /// <summary>
    ///     Parses the plugins section of the crash log and extracts plugin data.
    /// </summary>
    /// <param name="pluginLines">
    ///     The lines of text representing the plugins section from the crash log.
    /// </param>
    /// <param name="crashLog">
    ///     The <see cref="CrashLog" /> instance where the parsed plugin data will be stored.
    /// </param>
    private void ParsePluginsSection(List<string> pluginLines, CrashLog crashLog)
    {
        // Plugin format: [FE:001]   LoadOrder.esp
        var pluginRegex = PluginRegex();

        foreach (var line in pluginLines)
        {
            var match = pluginRegex.Match(line);
            if (!match.Success) continue;
            var loadOrder = $"{match.Groups[1].Value}:{match.Groups[2].Value}";
            var pluginName = match.Groups[3].Value.Trim();
            crashLog.Plugins[pluginName] = loadOrder;
        }

        crashLog.IsIncomplete = crashLog.Plugins.Count == 0;
    }

    /// <summary>
    ///     Parses the crashgen settings from the specified list of lines and populates the related data in the provided crash
    ///     log instance.
    /// </summary>
    /// <param name="settingsLines">
    ///     A list of strings representing the crashgen settings section from the crash log.
    /// </param>
    /// <param name="crashLog">
    ///     The <see cref="CrashLog" /> instance to populate with the parsed crashgen settings data.
    /// </param>
    private void ParseCrashgenSettings(List<string> settingsLines, CrashLog crashLog)
    {
        // Parse crashgen configuration settings like [Compatibility], [Patches], etc.
        foreach (var line in settingsLines)
        {
            var trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith('['))
                continue;

            // Format: "Key: value"
            if (!trimmedLine.Contains(':')) continue;
            var parts = trimmedLine.Split(':', 2);
            if (parts.Length != 2) continue;
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

    /// <summary>
    ///     Parses the XSE (eXtended Script Extender) modules section from the provided lines and updates the crash log with
    ///     the extracted modules.
    /// </summary>
    /// <param name="xseLines">
    ///     The lines of text corresponding to the XSE plugins section in the crash log.
    /// </param>
    /// <param name="crashLog">
    ///     The crash log instance to update with the parsed XSE modules data.
    /// </param>
    private void ParseXseModules(List<string> xseLines, CrashLog crashLog)
    {
        // Extract module names from XSE plugins section
        // Format: "ModuleName.dll v1.2.3" or just "ModuleName.dll"
        var modulePattern = ModulePattern();

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

    [GeneratedRegex(@"\[([A-F0-9]{2}):([A-F0-9]{3})\]\s+(.+)")]
    private partial Regex PluginRegex();

    [GeneratedRegex(@"^(.+?\.dll)\s*(?:v.*)?$", RegexOptions.IgnoreCase, "en-US")]
    private partial Regex ModulePattern();
}
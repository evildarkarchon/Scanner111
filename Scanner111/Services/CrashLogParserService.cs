using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Scanner111.Models;

namespace Scanner111.Services;

/// <summary>
///     Service responsible for parsing crash log files
/// </summary>
public partial class CrashLogParserService
{
    // Expected section headers in the crash log
    private const string SystemSpecsHeader = "SYSTEM SPECS:";
    private const string ProbableCallStackHeader = "PROBABLE CALL STACK:";
    private const string RegistersHeader = "REGISTERS:";
    private const string StackHeader = "STACK:"; // Detailed stack trace with RSP+ addresses
    private const string ModulesHeader = "MODULES:";
    private const string F4SePluginsHeader = "F4SE PLUGINS:";

    private const string PluginsHeader = "PLUGINS:"; // Plugin load order

    // Regex for parsing initial version lines
    private static readonly Regex FalloutVersionRegex = new(@"^Fallout 4 v(\d+\.\d+\.\d+.*)", RegexOptions.IgnoreCase);
    private static readonly Regex BuffoutVersionRegex = new(@"^Buffout 4 v(\d+\.\d+\.\d+.*)", RegexOptions.IgnoreCase);

    private static readonly Regex GenericCrashGenRegex =
        new(@"^([a-zA-Z0-9\s]+?) v(\d+\.\d+\.\d+.*)", RegexOptions.IgnoreCase); // Broader match for other crash gens

    // Regex for parsing plugin lines
    // Captures: Optional Index (e.g., "00", "FE 001"), Plugin File (e.g., "Fallout4.esm"), Optional Version
    private static readonly Regex PluginEntryRegex = CreatePluginEntryRegex();

    /// <summary>
    ///     Parses the raw content of a crash log file into a structured ParsedCrashLog object.
    ///     This is where the logic similar to Python's find_segments would go.
    /// </summary>
    public async Task<ParsedCrashLog> ParseCrashLogContentAsync(string logFilePath)
    {
        if (!File.Exists(logFilePath)) return new ParsedCrashLog(logFilePath, []); // Return empty if file not found

        var lines = (await File.ReadAllLinesAsync(logFilePath)).ToList();
        var parsedLog = new ParsedCrashLog(logFilePath, lines);

        var lineIndex = 0;

        // 1. Parse Game Version and Crash Generator Name from the beginning of the log
        if (lines.Count > lineIndex)
        {
            var gameVersionMatch = FalloutVersionRegex.Match(lines[lineIndex].Trim());
            if (gameVersionMatch.Success)
            {
                parsedLog.GameVersion = gameVersionMatch.Groups[1].Value;
                lineIndex++;
            }
        }

        if (lines.Count > lineIndex)
        {
            var lineContent = lines[lineIndex].Trim();
            var buffoutMatch = BuffoutVersionRegex.Match(lineContent);
            if (buffoutMatch.Success)
            {
                parsedLog.CrashGeneratorName = $"Buffout 4 v{buffoutMatch.Groups[1].Value}";
                lineIndex++;
            }
            else
            {
                var genericMatch = GenericCrashGenRegex.Match(lineContent);
                if (genericMatch.Success)
                {
                    parsedLog.CrashGeneratorName = genericMatch.Groups[0].Value; // Full match
                    lineIndex++;
                }
            }
        }

        // 2. Parse segments
        string? currentSegmentKey = null; // Stores the header key for OtherSegments

        for (; lineIndex < lines.Count; lineIndex++)
        {
            var currentLine = lines[lineIndex]; // Keep original line for content
            var trimmedLine = currentLine.Trim();

            // Skip fully empty lines when checking for headers, but preserve them in content
            if (string.IsNullOrWhiteSpace(trimmedLine))
            {
                // If inside a segment, add the empty line to it.
                if (currentSegmentKey != null)
                    switch (currentSegmentKey)
                    {
                        case ProbableCallStackHeader:
                            parsedLog.CallStack.Add(currentLine);
                            break;
                        case PluginsHeader: /* PLUGINS section usually doesn't have empty lines as valid entries */
                            break;
                        default:
                        {
                            if (parsedLog.OtherSegments.TryGetValue(currentSegmentKey, out var segmentList))
                                segmentList.Add(currentLine);
                            break;
                        }
                    }
                else // Before any major segment, empty lines can be part of MainErrorSegment
                    parsedLog.MainErrorSegment.Add(currentLine);

                continue;
            }

            // Check for new segment headers
            if (trimmedLine.StartsWith(SystemSpecsHeader, StringComparison.OrdinalIgnoreCase))
            {
                currentSegmentKey = SystemSpecsHeader;
            }
            else if (trimmedLine.StartsWith(ProbableCallStackHeader, StringComparison.OrdinalIgnoreCase))
            {
                currentSegmentKey = ProbableCallStackHeader;
            }
            else if (trimmedLine.StartsWith(RegistersHeader, StringComparison.OrdinalIgnoreCase))
            {
                currentSegmentKey = RegistersHeader;
            }
            else if (trimmedLine.StartsWith(StackHeader, StringComparison.OrdinalIgnoreCase))
            {
                currentSegmentKey = StackHeader; // This is the detailed one
            }
            else if (trimmedLine.StartsWith(ModulesHeader, StringComparison.OrdinalIgnoreCase))
            {
                currentSegmentKey = ModulesHeader;
            }
            else if (trimmedLine.StartsWith(F4SePluginsHeader, StringComparison.OrdinalIgnoreCase))
            {
                currentSegmentKey = F4SePluginsHeader;
            }
            else if (trimmedLine.StartsWith(PluginsHeader, StringComparison.OrdinalIgnoreCase))
            {
                currentSegmentKey = PluginsHeader;
            }
            else // Not a new header, so it's content for the current (or main error) segment
            {
                if (currentSegmentKey == null) // Still in the MainErrorSegment (before first recognized header)
                    parsedLog.MainErrorSegment.Add(currentLine);
                else // Belongs to an active segment
                    switch (currentSegmentKey)
                    {
                        case ProbableCallStackHeader:
                            parsedLog.CallStack.Add(currentLine);
                            break;
                        case PluginsHeader:
                            var pluginMatch = PluginEntryRegex.Match(trimmedLine);
                            if (pluginMatch.Success)
                            {
                                var pluginName = pluginMatch.Groups[2].Value;
                                // Identifier can be index or version, or just "N/A" if neither
                                var identifier = pluginMatch.Groups[1].Success ? pluginMatch.Groups[1].Value.Trim() :
                                    pluginMatch.Groups[3].Success ? pluginMatch.Groups[3].Value.Trim() : "N/A";
                                parsedLog.LoadedPlugins[pluginName] = identifier;
                            }

                            // Optionally, add non-matching, non-empty lines to a "comments" or "raw" list for this segment if needed.
                            break;
                        default: // For SystemSpecs, Registers, Stack, Modules, F4SEPlugins
                            if (!parsedLog.OtherSegments.ContainsKey(currentSegmentKey))
                                parsedLog.OtherSegments[currentSegmentKey] = [];
                            parsedLog.OtherSegments[currentSegmentKey].Add(currentLine);
                            break;
                    }

                continue; // Content line processed, continue to next line
            }

            // If we've reached here, it means a new segment header was identified.
            // The header line itself is not added as content.
            // Initialize list for new "OtherSegment" if it's the first time we see this header
            if (currentSegmentKey != ProbableCallStackHeader && currentSegmentKey != PluginsHeader &&
                !parsedLog.OtherSegments.ContainsKey(currentSegmentKey))
                parsedLog.OtherSegments[currentSegmentKey] = [];
        }

        return parsedLog;
    }

    [GeneratedRegex(
        @"^\s*(?:\[\s*([0-9A-Fa-f]+(?:\s+[0-9A-Fa-f]+)?)\s*\]\s+)?([a-zA-Z0-9_.\-]+?\.(?:es[mlp]|esp|esm))(?:\s+\((.+?)\))?\s*$",
        RegexOptions.IgnoreCase, "en-US")]
    private static partial Regex CreatePluginEntryRegex();
}
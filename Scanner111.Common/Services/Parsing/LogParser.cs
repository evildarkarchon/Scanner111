using System.Text.RegularExpressions;
using Scanner111.Common.Models.Analysis;

namespace Scanner111.Common.Services.Parsing;

/// <summary>
/// Parses crash logs and extracts segments and header information.
/// </summary>
public partial class LogParser : ILogParser
{
    private readonly CrashHeaderParser _headerParser;

    /// <summary>
    /// Regex to match segment headers in crash logs.
    /// Matches patterns like [Compatibility], SYSTEM SPECS:, PROBABLE CALL STACK:, etc.
    /// </summary>
    [GeneratedRegex(@"^\s*\[(.*?)\]|^SYSTEM SPECS:|^PROBABLE CALL STACK:|^MODULES:|^PLUGINS:|^XSE PLUGINS:", RegexOptions.Multiline)]
    private static partial Regex SegmentHeaderRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="LogParser"/> class.
    /// </summary>
    public LogParser()
    {
        _headerParser = new CrashHeaderParser();
    }

    /// <inheritdoc/>
    public async Task<LogParseResult> ParseAsync(string logContent, CancellationToken cancellationToken = default)
    {
        // Allow async operation to be cancelled
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var header = ParseHeader(logContent);
            if (header == null)
            {
                return new LogParseResult
                {
                    IsValid = false,
                    ErrorMessage = "Failed to parse crash header"
                };
            }

            var segments = ExtractSegments(logContent);

            // Check if log is complete (has PLUGINS section)
            var isComplete = segments.Any(s =>
                s.Name.Equals("PLUGINS", StringComparison.OrdinalIgnoreCase));

            return new LogParseResult
            {
                Header = header,
                Segments = segments,
                IsValid = true,
                ErrorMessage = isComplete ? null : "Log appears incomplete (missing PLUGINS section)"
            };
        }
        catch (Exception ex)
        {
            return new LogParseResult
            {
                IsValid = false,
                ErrorMessage = $"Parsing failed: {ex.Message}"
            };
        }
    }

    /// <inheritdoc/>
    public CrashHeader? ParseHeader(string logContent)
    {
        return _headerParser.Parse(logContent);
    }

    /// <inheritdoc/>
    public IReadOnlyList<LogSegment> ExtractSegments(string logContent)
    {
        var segments = new List<LogSegment>();
        var matches = SegmentHeaderRegex().Matches(logContent);

        if (matches.Count == 0)
        {
            return segments;
        }

        for (int i = 0; i < matches.Count; i++)
        {
            var startMatch = matches[i];
            var startIndex = startMatch.Index;
            var endIndex = i < matches.Count - 1
                ? matches[i + 1].Index
                : logContent.Length;

            var sectionContent = logContent[startIndex..endIndex];
            var lines = sectionContent.Split('\n', StringSplitOptions.None)
                .Select(line => line.TrimEnd('\r'))
                .ToList();

            // Extract segment name from the match
            string segmentName;
            if (startMatch.Groups[1].Success && !string.IsNullOrWhiteSpace(startMatch.Groups[1].Value))
            {
                // Bracketed format: [Compatibility]
                segmentName = startMatch.Groups[1].Value.Trim();
            }
            else
            {
                // Colon format: SYSTEM SPECS:, MODULES:, etc.
                segmentName = startMatch.Value.TrimEnd(':').Trim();
            }

            segments.Add(new LogSegment
            {
                Name = segmentName,
                Lines = lines,
                StartIndex = startIndex,
                EndIndex = endIndex
            });
        }

        return segments;
    }
}

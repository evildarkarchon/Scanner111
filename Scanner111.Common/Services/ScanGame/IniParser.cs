using System.Text;

namespace Scanner111.Common.Services.ScanGame;

/// <summary>
/// Lightweight INI file parser that preserves section structure.
/// </summary>
/// <remarks>
/// <para>
/// Supports standard INI format with sections [SectionName], key=value pairs,
/// and comments starting with ; or #. Handles various encodings with UTF-8 fallback.
/// </para>
/// <para>
/// Section and key lookups are case-insensitive, but original casing is preserved
/// in the returned data structure.
/// </para>
/// </remarks>
public sealed class IniParser
{
    private static readonly Encoding Utf8WithErrorHandling =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false);

    /// <summary>
    /// Parses INI content from a string.
    /// </summary>
    /// <param name="content">The INI file content.</param>
    /// <returns>A dictionary of sections, each containing key-value pairs.</returns>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Parse(string content)
    {
        var sections = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        var currentSection = string.Empty;

        // Ensure we have a default section for settings before any [Section] header
        sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(content);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            ProcessLine(line, sections, ref currentSection);
        }

        // Convert to read-only dictionaries
        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (sectionName, values) in sections)
        {
            if (values.Count > 0 || sectionName != string.Empty)
            {
                result[sectionName] = values;
            }
        }

        return result;
    }

    /// <summary>
    /// Parses INI content from a file asynchronously.
    /// </summary>
    /// <param name="filePath">The path to the INI file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of sections with their key-value pairs.</returns>
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> ParseFileAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var content = await ReadFileWithEncodingDetectionAsync(filePath, cancellationToken).ConfigureAwait(false);
        return Parse(content);
    }

    /// <summary>
    /// Gets a value from parsed INI data.
    /// </summary>
    /// <param name="sections">The parsed sections.</param>
    /// <param name="section">The section name (use empty string for global section).</param>
    /// <param name="key">The key name.</param>
    /// <returns>The value, or null if not found.</returns>
    public static string? GetValue(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> sections,
        string section,
        string key)
    {
        if (sections.TryGetValue(section, out var sectionValues))
        {
            if (sectionValues.TryGetValue(key, out var value))
            {
                return value;
            }
        }
        return null;
    }

    /// <summary>
    /// Checks if a setting exists in parsed INI data.
    /// </summary>
    /// <param name="sections">The parsed sections.</param>
    /// <param name="section">The section name.</param>
    /// <param name="key">The key name.</param>
    /// <returns>True if the setting exists; otherwise, false.</returns>
    public static bool HasSetting(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> sections,
        string section,
        string key)
    {
        return sections.TryGetValue(section, out var sectionValues) &&
               sectionValues.ContainsKey(key);
    }

    private void ProcessLine(
        string line,
        Dictionary<string, Dictionary<string, string>> sections,
        ref string currentSection)
    {
        var trimmed = line.Trim();

        // Skip empty lines
        if (string.IsNullOrEmpty(trimmed))
        {
            return;
        }

        // Skip comment lines (;, #, or //)
        if (trimmed.StartsWith(';') || trimmed.StartsWith('#') || trimmed.StartsWith("//"))
        {
            return;
        }

        // Check for section header [SectionName]
        if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
        {
            currentSection = trimmed[1..^1].Trim();
            if (!sections.ContainsKey(currentSection))
            {
                sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            return;
        }

        // Parse key=value (or key:value for some formats)
        var separatorIndex = trimmed.IndexOf('=');
        if (separatorIndex < 0)
        {
            separatorIndex = trimmed.IndexOf(':');
        }

        if (separatorIndex > 0)
        {
            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            // Remove inline comments (but be careful with URLs containing ://)
            var commentIndex = FindInlineCommentIndex(value);
            if (commentIndex > 0)
            {
                value = value[..commentIndex].TrimEnd();
            }

            // Remove surrounding quotes from value
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }

            // Ensure section exists
            if (!sections.ContainsKey(currentSection))
            {
                sections[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            sections[currentSection][key] = value;
        }
    }

    private static int FindInlineCommentIndex(string value)
    {
        // Look for ; or # that's not part of a quoted string or URL
        var inQuote = false;
        var quoteChar = '\0';

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            if (!inQuote && (c == '"' || c == '\''))
            {
                inQuote = true;
                quoteChar = c;
            }
            else if (inQuote && c == quoteChar)
            {
                inQuote = false;
            }
            else if (!inQuote && (c == ';' || c == '#'))
            {
                // Make sure it's not part of a URL scheme (://)
                if (c == '#' && i > 0 && value[i - 1] == ':')
                {
                    continue; // Part of a URL like "color:#ffffff"
                }
                return i;
            }
        }

        return -1;
    }

    private static async Task<string> ReadFileWithEncodingDetectionAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        // Read file bytes first
        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);

        // Check for BOM to detect encoding and get byte offset
        var (encoding, bomLength) = DetectEncodingWithBom(fileBytes);

        // Decode content, skipping the BOM if present
        return encoding.GetString(fileBytes, bomLength, fileBytes.Length - bomLength);
    }

    private static (Encoding Encoding, int BomLength) DetectEncodingWithBom(byte[] bytes)
    {
        if (bytes.Length >= 3)
        {
            // UTF-8 BOM (EF BB BF)
            if (bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return (Encoding.UTF8, 3);
            }
        }

        if (bytes.Length >= 2)
        {
            // UTF-16 LE BOM (FF FE)
            if (bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return (Encoding.Unicode, 2);
            }

            // UTF-16 BE BOM (FE FF)
            if (bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return (Encoding.BigEndianUnicode, 2);
            }
        }

        // Default to UTF-8 with error handling for robustness, no BOM
        return (Utf8WithErrorHandling, 0);
    }
}

using System.Collections.Generic;
using System.Text;

namespace Scanner111.Models;

/// <summary>
///     Builder pattern implementation for crash reports
/// </summary>
public class CrashReportBuilder
{
    private readonly List<string> _sectionOrder = [];
    private readonly Dictionary<string, List<string>> _sections = new();

    public string LogFileName { get; set; } = string.Empty;
    public bool ScanFailed { get; set; }
    public Dictionary<string, int> Statistics { get; } = new();

    /// <summary>
    ///     Adds a new section to the report
    /// </summary>
    public CrashReportBuilder AddSection(string sectionName, params string[] lines)
    {
        if (!_sections.TryGetValue(sectionName, out var value))
        {
            value = [];
            _sections[sectionName] = value;
            _sectionOrder.Add(sectionName);
        }

        value.AddRange(lines);
        return this;
    }

    /// <summary>
    ///     Adds content to an existing section
    /// </summary>
    public void AddToSection(string sectionName, params string[] lines)
    {
        if (!_sections.TryGetValue(sectionName, out var section))
        {
            AddSection(sectionName, lines);
            return;
        }

        section.AddRange(lines);
    }

    /// <summary>
    ///     Updates a statistic value
    /// </summary>
    public CrashReportBuilder AddStatistic(string key, int value)
    {
        Statistics[key] = value;
        return this;
    }

    /// <summary>
    ///     Builds the final report as a list of strings
    /// </summary>
    public List<string> Build()
    {
        var result = new List<string>();

        foreach (var section in _sectionOrder)
        {
            result.AddRange(_sections[section]);
            result.Add(string.Empty); // Add separator between sections
        }

        return result;
    }

    /// <summary>
    ///     Builds the report as a CrashLogProcessResult
    /// </summary>
    public CrashLogProcessResult BuildResult()
    {
        return new CrashLogProcessResult
        {
            LogFileName = LogFileName,
            ScanFailed = ScanFailed,
            Statistics = new Dictionary<string, int>(Statistics),
            Report = Build()
        };
    }

    /// <summary>
    ///     Converts the builder to a formatted string
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();

        foreach (var section in _sectionOrder)
        {
            foreach (var line in _sections[section]) sb.AppendLine(line);

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
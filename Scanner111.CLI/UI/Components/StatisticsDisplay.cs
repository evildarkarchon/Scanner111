using System.Diagnostics;
using Scanner111.Core.Analysis;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Scanner111.CLI.UI.Components;

/// <summary>
/// Component for displaying analysis statistics and metrics.
/// </summary>
public class StatisticsDisplay
{
    private readonly Dictionary<string, TimeSpan> _analyzerTimings = new();
    private readonly Dictionary<string, int> _analyzerSuccessCount = new();
    private readonly Dictionary<string, int> _analyzerFailureCount = new();
    private readonly Dictionary<string, long> _memoryUsage = new();
    
    /// <summary>
    /// Updates timing information for an analyzer.
    /// </summary>
    /// <param name="analyzerName">The name of the analyzer.</param>
    /// <param name="elapsed">The elapsed time.</param>
    public void UpdateTiming(string analyzerName, TimeSpan elapsed)
    {
        _analyzerTimings[analyzerName] = elapsed;
    }
    
    /// <summary>
    /// Records a successful analysis.
    /// </summary>
    /// <param name="analyzerName">The name of the analyzer.</param>
    public void RecordSuccess(string analyzerName)
    {
        _analyzerSuccessCount[analyzerName] = _analyzerSuccessCount.GetValueOrDefault(analyzerName, 0) + 1;
    }
    
    /// <summary>
    /// Records a failed analysis.
    /// </summary>
    /// <param name="analyzerName">The name of the analyzer.</param>
    public void RecordFailure(string analyzerName)
    {
        _analyzerFailureCount[analyzerName] = _analyzerFailureCount.GetValueOrDefault(analyzerName, 0) + 1;
    }
    
    /// <summary>
    /// Updates memory usage information.
    /// </summary>
    /// <param name="category">The memory category.</param>
    /// <param name="bytes">The memory usage in bytes.</param>
    public void UpdateMemoryUsage(string category, long bytes)
    {
        _memoryUsage[category] = bytes;
    }
    
    /// <summary>
    /// Creates a severity distribution bar chart.
    /// </summary>
    /// <param name="results">The analysis results.</param>
    /// <returns>A bar chart showing severity distribution.</returns>
    public IRenderable CreateSeverityChart(IEnumerable<AnalysisResult> results)
    {
        var severityGroups = results
            .GroupBy(r => r.Severity.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
        
        var chart = new BarChart()
            .Width(60)
            .Label("[bold underline]Severity Distribution[/]")
            .CenterLabel();
        
        // Define severity order and colors
        var severityOrder = new[] { "Critical", "High", "Medium", "Low", "Info", "Unknown" };
        var severityColors = new Dictionary<string, Color>
        {
            { "Critical", Color.Red },
            { "High", Color.Orange3 },
            { "Medium", Color.Yellow },
            { "Low", Color.Blue },
            { "Info", Color.Green },
            { "Unknown", Color.Grey }
        };
        
        foreach (var severity in severityOrder)
        {
            if (severityGroups.TryGetValue(severity, out var count) && count > 0)
            {
                chart.AddItem(
                    severity,
                    count,
                    severityColors.GetValueOrDefault(severity, Color.White));
            }
        }
        
        return new Panel(chart)
        {
            Header = new PanelHeader("[cyan]Analysis Results by Severity[/]"),
            Border = BoxBorder.Rounded
        };
    }
    
    /// <summary>
    /// Creates a performance metrics table.
    /// </summary>
    /// <returns>A table showing performance metrics.</returns>
    public IRenderable CreatePerformanceTable()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Yellow)
            .AddColumn(new TableColumn("[bold]Analyzer[/]").Centered())
            .AddColumn(new TableColumn("[bold]Duration[/]").Centered())
            .AddColumn(new TableColumn("[bold]Success Rate[/]").Centered())
            .AddColumn(new TableColumn("[bold]Status[/]").Centered());
        
        foreach (var analyzer in _analyzerTimings.Keys.OrderBy(x => x))
        {
            var duration = _analyzerTimings.GetValueOrDefault(analyzer, TimeSpan.Zero);
            var successCount = _analyzerSuccessCount.GetValueOrDefault(analyzer, 0);
            var failureCount = _analyzerFailureCount.GetValueOrDefault(analyzer, 0);
            var totalCount = successCount + failureCount;
            
            var successRate = totalCount > 0 ? (double)successCount / totalCount * 100 : 0;
            var statusColor = successRate switch
            {
                >= 90 => "green",
                >= 70 => "yellow",
                _ => "red"
            };
            
            var durationText = duration.TotalSeconds < 1 
                ? $"{duration.TotalMilliseconds:F0}ms"
                : $"{duration.TotalSeconds:F2}s";
            
            table.AddRow(
                analyzer,
                durationText,
                $"[{statusColor}]{successRate:F1}%[/] ({successCount}/{totalCount})",
                totalCount > 0 ? $"[{statusColor}]{"●"}[/]" : "[grey]○[/]"
            );
        }
        
        if (!_analyzerTimings.Any())
        {
            table.AddRow("[dim]No analyzer data available[/]", "", "", "");
        }
        
        return new Panel(table)
        {
            Header = new PanelHeader("[cyan]Analyzer Performance[/]"),
            Border = BoxBorder.Rounded
        };
    }
    
    /// <summary>
    /// Creates a memory usage table.
    /// </summary>
    /// <returns>A table showing memory usage statistics.</returns>
    public IRenderable CreateMemoryTable()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Cyan1)
            .AddColumn(new TableColumn("[bold]Category[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Usage[/]").RightAligned())
            .AddColumn(new TableColumn("[bold]Visual[/]").LeftAligned());
        
        // Add system memory info
        var process = Process.GetCurrentProcess();
        var workingSet = process.WorkingSet64;
        var privateMemory = process.PrivateMemorySize64;
        
        table.AddRow(
            "Working Set",
            FormatBytes(workingSet),
            CreateMemoryBar(workingSet, 1024 * 1024 * 1024) // 1GB reference
        );
        
        table.AddRow(
            "Private Memory",
            FormatBytes(privateMemory),
            CreateMemoryBar(privateMemory, 1024 * 1024 * 1024)
        );
        
        // Add custom memory tracking
        foreach (var kvp in _memoryUsage.OrderByDescending(x => x.Value))
        {
            table.AddRow(
                kvp.Key,
                FormatBytes(kvp.Value),
                CreateMemoryBar(kvp.Value, _memoryUsage.Values.DefaultIfEmpty(1).Max())
            );
        }
        
        // Add GC information
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var totalMemory = GC.GetTotalMemory(false);
        
        table.AddEmptyRow();
        table.AddRow("[dim]GC Gen 0[/]", gen0.ToString(), "");
        table.AddRow("[dim]GC Gen 1[/]", gen1.ToString(), "");
        table.AddRow("[dim]GC Gen 2[/]", gen2.ToString(), "");
        table.AddRow("[dim]GC Total Memory[/]", FormatBytes(totalMemory), "");
        
        return new Panel(table)
        {
            Header = new PanelHeader("[cyan]Memory Usage[/]"),
            Border = BoxBorder.Rounded
        };
    }
    
    /// <summary>
    /// Creates a file metrics table.
    /// </summary>
    /// <param name="logFile">The log file being analyzed.</param>
    /// <returns>A table showing file metrics.</returns>
    public IRenderable CreateFileMetricsTable(string? logFile = null)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Green)
            .AddColumn(new TableColumn("[bold]Metric[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Value[/]").RightAligned());
        
        if (!string.IsNullOrEmpty(logFile) && File.Exists(logFile))
        {
            var fileInfo = new FileInfo(logFile);
            var lineCount = CountLines(logFile);
            
            table.AddRow("File Size", FormatBytes(fileInfo.Length));
            table.AddRow("Line Count", lineCount.ToString("N0"));
            table.AddRow("Created", fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"));
            table.AddRow("Modified", fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            
            // Estimate complexity based on file size and line count
            var avgLineLength = lineCount > 0 ? (double)fileInfo.Length / lineCount : 0;
            var complexity = avgLineLength switch
            {
                < 50 => "Simple",
                < 100 => "Moderate",
                < 200 => "Complex",
                _ => "Very Complex"
            };
            
            var complexityColor = avgLineLength switch
            {
                < 50 => "green",
                < 100 => "yellow",
                < 200 => "orange3",
                _ => "red"
            };
            
            table.AddRow("Avg Line Length", $"{avgLineLength:F1} chars");
            table.AddRow("Complexity", $"[{complexityColor}]{complexity}[/]");
        }
        else
        {
            table.AddRow("[dim]No file information available[/]", "");
        }
        
        return new Panel(table)
        {
            Header = new PanelHeader("[cyan]File Metrics[/]"),
            Border = BoxBorder.Rounded
        };
    }
    
    /// <summary>
    /// Creates a comprehensive statistics layout.
    /// </summary>
    /// <param name="results">The analysis results.</param>
    /// <param name="logFile">The log file being analyzed.</param>
    /// <returns>A layout containing all statistics.</returns>
    public Layout CreateStatisticsLayout(IEnumerable<AnalysisResult> results, string? logFile = null)
    {
        var layout = new Layout("Statistics")
            .SplitRows(
                new Layout("Top")
                    .SplitColumns(
                        new Layout("Severity"),
                        new Layout("Performance")
                    ),
                new Layout("Bottom")
                    .SplitColumns(
                        new Layout("Memory"),
                        new Layout("File")
                    )
            );
        
        layout["Severity"].Update(CreateSeverityChart(results));
        layout["Performance"].Update(CreatePerformanceTable());
        layout["Memory"].Update(CreateMemoryTable());
        layout["File"].Update(CreateFileMetricsTable(logFile));
        
        return layout;
    }
    
    /// <summary>
    /// Clears all statistics.
    /// </summary>
    public void Clear()
    {
        _analyzerTimings.Clear();
        _analyzerSuccessCount.Clear();
        _analyzerFailureCount.Clear();
        _memoryUsage.Clear();
    }
    
    private static string FormatBytes(long bytes)
    {
        const long KB = 1024;
        const long MB = KB * 1024;
        const long GB = MB * 1024;
        
        return bytes switch
        {
            >= GB => $"{bytes / (double)GB:F2} GB",
            >= MB => $"{bytes / (double)MB:F2} MB",
            >= KB => $"{bytes / (double)KB:F2} KB",
            _ => $"{bytes} B"
        };
    }
    
    private static string CreateMemoryBar(long value, long maxValue)
    {
        if (maxValue == 0) return "";
        
        var percentage = (double)value / maxValue;
        var barLength = 20;
        var filledLength = (int)(percentage * barLength);
        
        var color = percentage switch
        {
            >= 0.9 => "red",
            >= 0.7 => "orange3",
            >= 0.5 => "yellow",
            _ => "green"
        };
        
        var bar = new string('█', filledLength) + new string('░', barLength - filledLength);
        return $"[{color}]{bar}[/] {percentage:P1}";
    }
    
    private static int CountLines(string filePath)
    {
        try
        {
            return File.ReadLines(filePath).Count();
        }
        catch
        {
            return 0;
        }
    }
}
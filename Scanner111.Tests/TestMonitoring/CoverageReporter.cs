using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Scanner111.Tests.TestMonitoring;

public class CoverageReporter
{
    private readonly string _coverageDirectory;
    private readonly string _outputDirectory;

    public CoverageReporter(
        string coverageDirectory = "TestResults", 
        string outputDirectory = "TestResults/Coverage")
    {
        _coverageDirectory = coverageDirectory;
        _outputDirectory = outputDirectory;
        Directory.CreateDirectory(_outputDirectory);
    }

    public async Task<CoverageReport> AnalyzeCoverageAsync()
    {
        var coberturaFiles = Directory.GetFiles(_coverageDirectory, "coverage.cobertura.xml", SearchOption.AllDirectories);
        
        if (!coberturaFiles.Any())
        {
            return new CoverageReport
            {
                GeneratedAt = DateTime.UtcNow,
                HasCoverageData = false,
                Message = "No coverage data found. Run tests with --collect:\"XPlat Code Coverage\" to generate coverage data."
            };
        }

        var latestCoverageFile = coberturaFiles
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .First();

        return await ParseCoberturaFileAsync(latestCoverageFile);
    }

    private async Task<CoverageReport> ParseCoberturaFileAsync(string filePath)
    {
        var content = await File.ReadAllTextAsync(filePath);
        var doc = XDocument.Parse(content);
        
        var report = new CoverageReport
        {
            GeneratedAt = DateTime.UtcNow,
            HasCoverageData = true
        };

        var coverage = doc.Root;
        if (coverage == null) return report;

        report.LineRate = double.Parse(coverage.Attribute("line-rate")?.Value ?? "0");
        report.BranchRate = double.Parse(coverage.Attribute("branch-rate")?.Value ?? "0");
        report.LinesCovered = int.Parse(coverage.Attribute("lines-covered")?.Value ?? "0");
        report.LinesValid = int.Parse(coverage.Attribute("lines-valid")?.Value ?? "0");
        report.BranchesCovered = int.Parse(coverage.Attribute("branches-covered")?.Value ?? "0");
        report.BranchesValid = int.Parse(coverage.Attribute("branches-valid")?.Value ?? "0");

        var packages = coverage.Element("packages")?.Elements("package") ?? Enumerable.Empty<XElement>();
        
        foreach (var package in packages)
        {
            var packageName = package.Attribute("name")?.Value ?? "Unknown";
            var packageCoverage = new PackageCoverage
            {
                Name = packageName,
                LineRate = double.Parse(package.Attribute("line-rate")?.Value ?? "0"),
                BranchRate = double.Parse(package.Attribute("branch-rate")?.Value ?? "0")
            };

            var classes = package.Element("classes")?.Elements("class") ?? Enumerable.Empty<XElement>();
            
            foreach (var classElement in classes)
            {
                var className = classElement.Attribute("name")?.Value ?? "Unknown";
                var filename = classElement.Attribute("filename")?.Value ?? "";
                
                var classCoverage = new ClassCoverage
                {
                    Name = className,
                    Filename = filename,
                    LineRate = double.Parse(classElement.Attribute("line-rate")?.Value ?? "0"),
                    BranchRate = double.Parse(classElement.Attribute("branch-rate")?.Value ?? "0")
                };

                var methods = classElement.Element("methods")?.Elements("method") ?? Enumerable.Empty<XElement>();
                
                foreach (var method in methods)
                {
                    var methodName = method.Attribute("name")?.Value ?? "Unknown";
                    var signature = method.Attribute("signature")?.Value ?? "";
                    
                    var methodCoverage = new MethodCoverage
                    {
                        Name = methodName,
                        Signature = signature,
                        LineRate = double.Parse(method.Attribute("line-rate")?.Value ?? "0"),
                        BranchRate = double.Parse(method.Attribute("branch-rate")?.Value ?? "0")
                    };
                    
                    classCoverage.Methods.Add(methodCoverage);
                }

                var lines = classElement.Element("lines")?.Elements("line") ?? Enumerable.Empty<XElement>();
                classCoverage.TotalLines = lines.Count();
                classCoverage.CoveredLines = lines.Count(l => l.Attribute("hits")?.Value != "0");
                
                packageCoverage.Classes.Add(classCoverage);
            }
            
            report.Packages.Add(packageCoverage);
        }

        IdentifyUncoveredAreas(report);
        GenerateCoverageRecommendations(report);
        
        return report;
    }

    private void IdentifyUncoveredAreas(CoverageReport report)
    {
        var threshold = 0.8;
        
        foreach (var package in report.Packages)
        {
            foreach (var classInfo in package.Classes.Where(c => c.LineRate < threshold))
            {
                report.UncoveredAreas.Add(new UncoveredArea
                {
                    Type = "Class",
                    Name = classInfo.Name,
                    CoverageRate = classInfo.LineRate,
                    Priority = DeterminePriority(classInfo.Name, classInfo.LineRate)
                });

                foreach (var method in classInfo.Methods.Where(m => m.LineRate < 0.5))
                {
                    report.UncoveredAreas.Add(new UncoveredArea
                    {
                        Type = "Method",
                        Name = $"{classInfo.Name}.{method.Name}",
                        CoverageRate = method.LineRate,
                        Priority = DeterminePriority($"{classInfo.Name}.{method.Name}", method.LineRate)
                    });
                }
            }
        }

        report.UncoveredAreas = report.UncoveredAreas
            .OrderBy(a => a.Priority)
            .ThenBy(a => a.CoverageRate)
            .Take(20)
            .ToList();
    }

    private CoveragePriority DeterminePriority(string name, double coverageRate)
    {
        var criticalComponents = new[] { "Pipeline", "Analyzer", "Parser", "Service", "Handler" };
        var isCritical = criticalComponents.Any(c => name.Contains(c, StringComparison.OrdinalIgnoreCase));
        
        if (isCritical && coverageRate < 0.5)
            return CoveragePriority.Critical;
        if (isCritical && coverageRate < 0.8)
            return CoveragePriority.High;
        if (coverageRate < 0.3)
            return CoveragePriority.High;
        if (coverageRate < 0.6)
            return CoveragePriority.Medium;
        
        return CoveragePriority.Low;
    }

    private void GenerateCoverageRecommendations(CoverageReport report)
    {
        if (report.LineRate < 0.8)
        {
            report.Recommendations.Add($"Overall line coverage is {report.LineRate:P0}. Target is 80% minimum.");
        }

        if (report.BranchRate < 0.7)
        {
            report.Recommendations.Add($"Branch coverage is low at {report.BranchRate:P0}. Add tests for edge cases and error paths.");
        }

        var criticalUncovered = report.UncoveredAreas
            .Where(a => a.Priority == CoveragePriority.Critical)
            .ToList();

        if (criticalUncovered.Any())
        {
            report.Recommendations.Add($"Critical components need immediate coverage: {string.Join(", ", criticalUncovered.Take(3).Select(a => a.Name))}");
        }

        var analyzersWithLowCoverage = report.Packages
            .SelectMany(p => p.Classes)
            .Where(c => c.Name.Contains("Analyzer") && c.LineRate < 0.8)
            .ToList();

        if (analyzersWithLowCoverage.Any())
        {
            report.Recommendations.Add($"Analyzers need better test coverage: {string.Join(", ", analyzersWithLowCoverage.Take(3).Select(c => c.Name))}");
        }
    }

    public async Task GenerateCoverageReportsAsync(CoverageReport report)
    {
        await GenerateHtmlCoverageReportAsync(report);
        await GenerateJsonCoverageReportAsync(report);
        await GenerateMarkdownCoverageReportAsync(report);
        await GenerateBadgeAsync(report);
    }

    private async Task GenerateHtmlCoverageReportAsync(CoverageReport report)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html><head>");
        html.AppendLine("<title>Scanner111 Test Coverage Report</title>");
        html.AppendLine(@"<style>
            body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 20px; background: #f5f5f5; }
            .container { max-width: 1200px; margin: 0 auto; background: white; padding: 20px; border-radius: 8px; }
            h1 { color: #333; border-bottom: 3px solid #007acc; padding-bottom: 10px; }
            .coverage-bar { display: inline-block; width: 200px; height: 25px; background: #e0e0e0; border-radius: 12px; overflow: hidden; position: relative; }
            .coverage-fill { height: 100%; transition: width 0.3s; }
            .coverage-text { position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); font-weight: bold; }
            .good { background: linear-gradient(90deg, #28a745, #20c997); }
            .warning { background: linear-gradient(90deg, #ffc107, #fd7e14); }
            .danger { background: linear-gradient(90deg, #dc3545, #e91e63); }
            .metric-card { display: inline-block; padding: 15px 25px; margin: 10px; background: white; border: 1px solid #ddd; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
            .metric-value { font-size: 32px; font-weight: bold; }
            .metric-label { color: #666; font-size: 14px; margin-top: 5px; }
            table { width: 100%; border-collapse: collapse; margin: 20px 0; }
            th { background: #f8f9fa; padding: 12px; text-align: left; border-bottom: 2px solid #dee2e6; }
            td { padding: 10px; border-bottom: 1px solid #dee2e6; }
            tr:hover { background: #f8f9fa; }
            .priority-critical { color: #dc3545; font-weight: bold; }
            .priority-high { color: #fd7e14; font-weight: bold; }
            .priority-medium { color: #ffc107; }
            .priority-low { color: #28a745; }
        </style>");
        html.AppendLine("</head><body>");
        html.AppendLine("<div class='container'>");
        
        html.AppendLine($"<h1>Test Coverage Report - {DateTime.Now:yyyy-MM-dd HH:mm}</h1>");

        if (!report.HasCoverageData)
        {
            html.AppendLine($"<p>{report.Message}</p>");
        }
        else
        {
            html.AppendLine("<div class='metrics'>");
            html.AppendLine($@"<div class='metric-card'>
                <div class='metric-value'>{report.LineRate:P0}</div>
                <div class='metric-label'>Line Coverage</div>
            </div>");
            html.AppendLine($@"<div class='metric-card'>
                <div class='metric-value'>{report.BranchRate:P0}</div>
                <div class='metric-label'>Branch Coverage</div>
            </div>");
            html.AppendLine($@"<div class='metric-card'>
                <div class='metric-value'>{report.LinesCovered}/{report.LinesValid}</div>
                <div class='metric-label'>Lines Covered</div>
            </div>");
            html.AppendLine("</div>");

            html.AppendLine("<h2>Package Coverage</h2>");
            html.AppendLine("<table>");
            html.AppendLine("<tr><th>Package</th><th>Line Coverage</th><th>Branch Coverage</th><th>Classes</th></tr>");
            
            foreach (var package in report.Packages.OrderBy(p => p.LineRate))
            {
                var barClass = package.LineRate >= 0.8 ? "good" : package.LineRate >= 0.6 ? "warning" : "danger";
                html.AppendLine($@"<tr>
                    <td>{package.Name}</td>
                    <td>
                        <div class='coverage-bar'>
                            <div class='coverage-fill {barClass}' style='width:{package.LineRate * 100}%'></div>
                            <span class='coverage-text'>{package.LineRate:P0}</span>
                        </div>
                    </td>
                    <td>{package.BranchRate:P0}</td>
                    <td>{package.Classes.Count}</td>
                </tr>");
            }
            
            html.AppendLine("</table>");

            if (report.UncoveredAreas.Any())
            {
                html.AppendLine("<h2>Priority Areas for Coverage Improvement</h2>");
                html.AppendLine("<table>");
                html.AppendLine("<tr><th>Component</th><th>Type</th><th>Current Coverage</th><th>Priority</th></tr>");
                
                foreach (var area in report.UncoveredAreas.Take(10))
                {
                    var priorityClass = $"priority-{area.Priority.ToString().ToLower()}";
                    html.AppendLine($@"<tr>
                        <td>{area.Name}</td>
                        <td>{area.Type}</td>
                        <td>{area.CoverageRate:P0}</td>
                        <td class='{priorityClass}'>{area.Priority}</td>
                    </tr>");
                }
                
                html.AppendLine("</table>");
            }

            if (report.Recommendations.Any())
            {
                html.AppendLine("<h2>Recommendations</h2>");
                html.AppendLine("<ul>");
                foreach (var rec in report.Recommendations)
                {
                    html.AppendLine($"<li>{rec}</li>");
                }
                html.AppendLine("</ul>");
            }
        }

        html.AppendLine("</div></body></html>");
        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "coverage-report.html"), html.ToString());
    }

    private async Task GenerateJsonCoverageReportAsync(CoverageReport report)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        
        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "coverage-report.json"), json);
    }

    private async Task GenerateMarkdownCoverageReportAsync(CoverageReport report)
    {
        var md = new StringBuilder();
        
        md.AppendLine("# Test Coverage Report");
        md.AppendLine($"*Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");
        md.AppendLine();

        if (!report.HasCoverageData)
        {
            md.AppendLine(report.Message);
        }
        else
        {
            md.AppendLine("## Summary");
            md.AppendLine($"- **Line Coverage**: {report.LineRate:P1} ({report.LinesCovered}/{report.LinesValid})");
            md.AppendLine($"- **Branch Coverage**: {report.BranchRate:P1} ({report.BranchesCovered}/{report.BranchesValid})");
            md.AppendLine();

            md.AppendLine("## Package Coverage");
            md.AppendLine("| Package | Line Coverage | Branch Coverage | Classes |");
            md.AppendLine("|---------|--------------|-----------------|---------|");
            
            foreach (var package in report.Packages.OrderBy(p => p.LineRate))
            {
                md.AppendLine($"| {package.Name} | {package.LineRate:P0} | {package.BranchRate:P0} | {package.Classes.Count} |");
            }
            md.AppendLine();

            if (report.UncoveredAreas.Any())
            {
                md.AppendLine("## Priority Areas for Improvement");
                md.AppendLine("| Component | Type | Coverage | Priority |");
                md.AppendLine("|-----------|------|----------|----------|");
                
                foreach (var area in report.UncoveredAreas.Take(10))
                {
                    md.AppendLine($"| {area.Name} | {area.Type} | {area.CoverageRate:P0} | {area.Priority} |");
                }
                md.AppendLine();
            }

            if (report.Recommendations.Any())
            {
                md.AppendLine("## Recommendations");
                foreach (var rec in report.Recommendations)
                {
                    md.AppendLine($"- {rec}");
                }
            }
        }

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "coverage-report.md"), md.ToString());
    }

    private async Task GenerateBadgeAsync(CoverageReport report)
    {
        var coverage = report.HasCoverageData ? (int)(report.LineRate * 100) : 0;
        var color = coverage >= 80 ? "brightgreen" : coverage >= 60 ? "yellow" : "red";
        
        var badge = $@"{{
  ""schemaVersion"": 1,
  ""label"": ""coverage"",
  ""message"": ""{coverage}%"",
  ""color"": ""{color}""
}}";

        await File.WriteAllTextAsync(Path.Combine(_outputDirectory, "coverage-badge.json"), badge);
    }
}

public class CoverageReport
{
    public DateTime GeneratedAt { get; set; }
    public bool HasCoverageData { get; set; }
    public string Message { get; set; } = string.Empty;
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
    public int LinesCovered { get; set; }
    public int LinesValid { get; set; }
    public int BranchesCovered { get; set; }
    public int BranchesValid { get; set; }
    public List<PackageCoverage> Packages { get; set; } = new();
    public List<UncoveredArea> UncoveredAreas { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class PackageCoverage
{
    public string Name { get; set; } = string.Empty;
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
    public List<ClassCoverage> Classes { get; set; } = new();
}

public class ClassCoverage
{
    public string Name { get; set; } = string.Empty;
    public string Filename { get; set; } = string.Empty;
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
    public int TotalLines { get; set; }
    public int CoveredLines { get; set; }
    public List<MethodCoverage> Methods { get; set; } = new();
}

public class MethodCoverage
{
    public string Name { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
    public double LineRate { get; set; }
    public double BranchRate { get; set; }
}

public class UncoveredArea
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double CoverageRate { get; set; }
    public CoveragePriority Priority { get; set; }
}

public enum CoveragePriority
{
    Critical,
    High,
    Medium,
    Low
}
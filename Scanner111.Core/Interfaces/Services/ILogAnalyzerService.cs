using Scanner111.Core.Models;

namespace Scanner111.Core.Interfaces.Services;

public interface ILogAnalyzerService
{
    Task<CrashLog> AnalyzeCrashLogAsync(string filePath);
    Task<IEnumerable<string>> ExtractPluginsFromLogAsync(string filePath);
    Task<IEnumerable<string>> ExtractErrorsFromLogAsync(string filePath);
    Task<IEnumerable<string>> ExtractCallStackFromLogAsync(string filePath);
    Task GenerateReportAsync(CrashLog crashLog, string outputPath);
}
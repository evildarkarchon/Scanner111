using BenchmarkDotNet.Attributes;
using Scanner111.Common.Services.Parsing;

namespace Scanner111.Benchmarks;

[MemoryDiagnoser]
public class LogParserBenchmarks
{
    private string _logContent = string.Empty;
    private LogParser _parser = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Find sample log
        var currentDir = AppDomain.CurrentDomain.BaseDirectory;
        var rootDir = Directory.GetParent(currentDir)?.Parent?.Parent?.Parent?.Parent?.FullName;
        var logPath = Path.Combine(rootDir ?? ".", "sample_logs", "FO4", "crash-12624.log");
        
        if (!File.Exists(logPath))
        {
            // Fallback for different execution contexts
             logPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "sample_logs", "FO4", "crash-12624.log"));
        }
        
        if (!File.Exists(logPath))
        {
            // Absolute fallback
            logPath = @"J:\Scanner111\sample_logs\FO4\crash-12624.log";
        }

        if (File.Exists(logPath))
        {
            _logContent = File.ReadAllText(logPath);
        }
        else
        {
            throw new FileNotFoundException($"Sample log not found at {logPath}");
        }

        _parser = new LogParser();
    }

    [Benchmark]
    public async Task ParseAsync()
    {
        await _parser.ParseAsync(_logContent);
    }

    [Benchmark]
    public void ExtractSegments()
    {
        _parser.ExtractSegments(_logContent);
    }
}

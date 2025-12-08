using BenchmarkDotNet.Running;

namespace Scanner111.Benchmarks;

class Program
{
    static void Main(string[] args)
    {
        var summary = BenchmarkRunner.Run<LogParserBenchmarks>();
    }
}
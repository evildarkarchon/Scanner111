using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Scanner111.CLI.Test.Infrastructure;

public abstract class CliTestBase : IDisposable
{
    protected readonly TestConsole Console;
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ILogger Logger;
    protected readonly CancellationTokenSource CancellationTokenSource;

    protected CliTestBase()
    {
        Console = new TestConsole();
        CancellationTokenSource = new CancellationTokenSource();
        
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        
        Logger = ServiceProvider.GetRequiredService<ILogger<CliTestBase>>();
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => builder.AddDebug());
        services.AddSingleton<IAnsiConsole>(Console);
        services.AddSingleton(Console);
    }

    protected AnalysisContext CreateAnalysisContext(string logFilePath = "test.log")
    {
        var yamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        var context = new AnalysisContext(logFilePath, yamlCore);
        return context;
    }

    protected ReportFragment CreateTestReportFragment(string title = "Test Fragment")
    {
        var builder = ReportFragmentBuilder.Create()
            .WithTitle(title)
            .WithType(FragmentType.Info)
            .Append("Test content");
        
        return builder.Build();
    }

    protected string GetTestDataPath(string fileName)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(baseDir, "TestData", fileName);
    }

    public virtual void Dispose()
    {
        CancellationTokenSource?.Cancel();
        CancellationTokenSource?.Dispose();
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
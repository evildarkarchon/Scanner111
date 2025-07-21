using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Pipeline;

public class AnalyzerFactoryTests
{
    private readonly AnalyzerFactory _factory;

    public AnalyzerFactoryTests()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add required services for analyzers
        services.AddTransient<IYamlSettingsProvider, TestYamlSettingsProvider>();
        services.AddTransient<IFormIdDatabaseService, TestFormIdDatabaseService>();
        services.AddTransient<IMessageHandler, TestMessageHandler>();
        services.AddTransient<ICacheManager, TestCacheManager>();

        // Add analyzers
        services.AddTransient<FormIdAnalyzer>();
        services.AddTransient<PluginAnalyzer>();
        services.AddTransient<SuspectScanner>();
        services.AddTransient<SettingsScanner>();
        services.AddTransient<RecordScanner>();

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        _factory = new AnalyzerFactory(serviceProvider);
    }

    [Fact]
    public void CreateAnalyzers_ShouldReturnAllRegisteredAnalyzers()
    {
        // Act
        var analyzers = _factory.CreateAnalyzers("Fallout4").ToList();

        // Assert
        Assert.Equal(5, analyzers.Count);
        Assert.Contains(analyzers, a => a is FormIdAnalyzer);
        Assert.Contains(analyzers, a => a is PluginAnalyzer);
        Assert.Contains(analyzers, a => a is SuspectScanner);
        Assert.Contains(analyzers, a => a is SettingsScanner);
        Assert.Contains(analyzers, a => a is RecordScanner);
    }

    [Fact]
    public void CreateAnalyzers_ShouldReturnAnalyzersOrderedByPriority()
    {
        // Act
        var analyzers = _factory.CreateAnalyzers("Fallout4").ToList();

        // Assert
        Assert.True(analyzers.Count > 1);
        for (var i = 1; i < analyzers.Count; i++)
            Assert.True(analyzers[i - 1].Priority <= analyzers[i].Priority,
                $"Analyzer at index {i - 1} has higher priority ({analyzers[i - 1].Priority}) than analyzer at index {i} ({analyzers[i].Priority})");
    }

    [Theory]
    [InlineData("FormId", typeof(FormIdAnalyzer))]
    [InlineData("Plugin", typeof(PluginAnalyzer))]
    [InlineData("Suspect", typeof(SuspectScanner))]
    [InlineData("Settings", typeof(SettingsScanner))]
    [InlineData("Record", typeof(RecordScanner))]
    public void CreateAnalyzer_WithValidName_ShouldReturnCorrectAnalyzerType(string name, Type expectedType)
    {
        // Act
        var analyzer = _factory.CreateAnalyzer(name);

        // Assert
        Assert.NotNull(analyzer);
        Assert.IsType(expectedType, analyzer);
    }

    [Theory]
    [InlineData("InvalidAnalyzer")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NonExistent")]
    public void CreateAnalyzer_WithInvalidName_ShouldReturnNull(string name)
    {
        // Act
        var analyzer = _factory.CreateAnalyzer(name);

        // Assert
        Assert.Null(analyzer);
    }

    [Fact]
    public void GetAvailableAnalyzers_ShouldReturnAllRegisteredAnalyzerNames()
    {
        // Act
        var availableAnalyzers = _factory.GetAvailableAnalyzers().ToList();

        // Assert
        Assert.Equal(5, availableAnalyzers.Count);
        Assert.Contains("FormId", availableAnalyzers);
        Assert.Contains("Plugin", availableAnalyzers);
        Assert.Contains("Suspect", availableAnalyzers);
        Assert.Contains("Settings", availableAnalyzers);
        Assert.Contains("Record", availableAnalyzers);
    }

    [Fact]
    public void CreateAnalyzers_WithDifferentGames_ShouldReturnSameAnalyzers()
    {
        // Act
        var analyzers1 = _factory.CreateAnalyzers("Fallout4").ToList();
        var analyzers2 = _factory.CreateAnalyzers("Skyrim").ToList();

        // Assert
        Assert.Equal(analyzers1.Count, analyzers2.Count);

        var types1 = analyzers1.Select(a => a.GetType()).OrderBy(t => t.Name).ToList();
        var types2 = analyzers2.Select(a => a.GetType()).OrderBy(t => t.Name).ToList();

        Assert.Equal(types1, types2);
    }

    [Fact]
    public void CreateAnalyzer_ShouldCreateNewInstanceEachTime()
    {
        // Act
        var analyzer1 = _factory.CreateAnalyzer("FormId");
        var analyzer2 = _factory.CreateAnalyzer("FormId");

        // Assert
        Assert.NotNull(analyzer1);
        Assert.NotNull(analyzer2);
        Assert.NotSame(analyzer1, analyzer2);
        Assert.IsType<FormIdAnalyzer>(analyzer1);
        Assert.IsType<FormIdAnalyzer>(analyzer2);
    }

    [Fact]
    public void CreateAnalyzers_ShouldCreateNewInstancesEachTime()
    {
        // Act
        var analyzers1 = _factory.CreateAnalyzers("Fallout4").ToList();
        var analyzers2 = _factory.CreateAnalyzers("Fallout4").ToList();

        // Assert
        Assert.Equal(analyzers1.Count, analyzers2.Count);

        for (var i = 0; i < analyzers1.Count; i++)
        {
            Assert.NotSame(analyzers1[i], analyzers2[i]);
            Assert.Equal(analyzers1[i].GetType(), analyzers2[i].GetType());
        }
    }

    [Fact]
    public void CreateAnalyzers_ShouldNotReturnNullAnalyzers()
    {
        // Act
        var analyzers = _factory.CreateAnalyzers("Fallout4").ToList();

        // Assert
        Assert.All(analyzers, Assert.NotNull);
    }

    [Fact]
    public void CreateAnalyzers_EachAnalyzer_ShouldHaveValidProperties()
    {
        // Act
        var analyzers = _factory.CreateAnalyzers("Fallout4").ToList();

        // Assert
        Assert.NotEmpty(analyzers);

        foreach (var analyzer in analyzers)
        {
            // Validate analyzer name
            Assert.NotNull(analyzer.Name);
            Assert.NotEmpty(analyzer.Name);
            Assert.False(string.IsNullOrWhiteSpace(analyzer.Name), "Analyzer name cannot be null or whitespace");

            // Validate analyzer priority
            var priority = analyzer.Priority;
            Assert.True(priority >= 0,
                $"Analyzer '{analyzer.Name}' has invalid priority: {priority}. Priority must be >= 0.");

            // Validate analyzer type
            Assert.NotNull(analyzer.GetType());
        }
    }
}
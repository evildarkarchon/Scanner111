using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Pipeline;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Pipeline;

/// <summary>
///     Contains unit tests for the <see cref="AnalyzerFactory" /> class.
/// </summary>
/// <remarks>
///     These tests ensure proper functionality of the analyzer factory
///     and validate the creation and behavior of analyzers in different scenarios.
/// </remarks>
/// <example>
///     This class verifies:<br />
///     - Proper registration of analyzers and components.<br />
///     - Correct creation of analyzers by name and priority.<br />
///     - Handling of invalid or unregistered analyzer names.<br />
///     - Consistency of returned analyzer instances.<br />
///     - Proper behavior of factory methods in different game types.
/// </example>
[Collection("Pipeline Tests")]
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
        services.AddTransient<IApplicationSettingsService, TestApplicationSettingsService>();
        services.AddTransient<IHashValidationService, TestHashValidationService>();

        // Add analyzers
        services.AddTransient<FormIdAnalyzer>();
        services.AddTransient<PluginAnalyzer>();
        services.AddTransient<SuspectScanner>();
        services.AddTransient<SettingsScanner>();
        services.AddTransient<RecordScanner>();
        services.AddTransient<FileIntegrityAnalyzer>();

        IServiceProvider serviceProvider = services.BuildServiceProvider();
        _factory = new AnalyzerFactory(serviceProvider);
    }

    /// Tests that the CreateAnalyzers method of the AnalyzerFactory returns all registered analyzers for a given game.
    /// Verifies that:
    /// <br />
    /// - The total count of returned analyzers matches the expected value.
    /// <br />
    /// - The list of returned analyzers includes instances of specific analyzer types: FormIdAnalyzer, PluginAnalyzer,
    /// SuspectScanner, SettingsScanner, and RecordScanner.
    /// <br />
    /// This test ensures that all analyzers registered within the factory are successfully created and retrieved for
    /// a specified game configuration.
    [Fact]
    public void CreateAnalyzers_ShouldReturnAllRegisteredAnalyzers()
    {
        // Act
        var analyzers = _factory.CreateAnalyzers("Fallout4").ToList();

        // Assert
        Assert.Equal(7, analyzers.Count);
        Assert.Contains(analyzers, a => a is FormIdAnalyzer);
        Assert.Contains(analyzers, a => a is PluginAnalyzer);
        Assert.Contains(analyzers, a => a is SuspectScanner);
        Assert.Contains(analyzers, a => a is SettingsScanner);
        Assert.Contains(analyzers, a => a is RecordScanner);
        Assert.Contains(analyzers, a => a is FileIntegrityAnalyzer);
        Assert.Contains(analyzers, a => a is BuffoutVersionAnalyzerV2);
    }

    /// Ensures that the CreateAnalyzers method of the AnalyzerFactory returns a list of analyzers ordered by their priority values.
    /// Verifies the following:
    /// <br />
    /// - The returned analyzers collection contains more than one analyzer.
    /// <br />
    /// - The analyzers are sorted such that each analyzer's priority value is less than or equal to the priority of the next one in the list.
    /// <br />
    /// This test guarantees proper priority-based ordering of analyzers created for a specified game.
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

    /// Tests that the CreateAnalyzer method of the AnalyzerFactory correctly returns an analyzer instance of the expected type
    /// when provided with a valid analyzer name.
    /// Verifies that:
    /// <br />
    /// - The returned analyzer is not null.
    /// <br />
    /// - The returned analyzer is of the specified type.
    /// <br />
    /// This test ensures proper behavior of the factory when creating specific analyzers based on their registered names.
    /// <param name="name">The name of the analyzer to create.</param>
    /// <param name="expectedType">The expected type of the analyzer instance to be returned.</param>
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

    /// Verifies that the CreateAnalyzer method of the AnalyzerFactory class returns null when provided
    /// with an invalid or unregistered analyzer name.
    /// Ensures that:
    /// <br />
    /// - Input names not registered in the factory result in a null return value.
    /// <br />
    /// - Edge cases such as empty strings, whitespace-only strings, and non-existent analyzer names
    /// are handled correctly.
    /// <param name="name">
    ///     The name of the analyzer to create. Can be an invalid or unregistered name, empty string, or whitespace.
    /// </param>
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

    /// Tests that the GetAvailableAnalyzers method of the AnalyzerFactory class returns the correct set of analyzer names.
    /// Verifies that:
    /// <br />
    /// - The total count of available analyzers matches the expected value.
    /// <br />
    /// - The list of analyzer names includes "FormId", "Plugin", "Suspect", "Settings", and "Record".
    /// <br />
    /// This test ensures that all registered analyzers are properly listed by the factory.
    [Fact]
    public void GetAvailableAnalyzers_ShouldReturnAllRegisteredAnalyzerNames()
    {
        // Act
        var availableAnalyzers = _factory.GetAvailableAnalyzers().ToList();

        // Assert
        Assert.Equal(7, availableAnalyzers.Count);
        Assert.Contains("FormId", availableAnalyzers);
        Assert.Contains("Plugin", availableAnalyzers);
        Assert.Contains("Suspect", availableAnalyzers);
        Assert.Contains("Settings", availableAnalyzers);
        Assert.Contains("Record", availableAnalyzers);
        Assert.Contains("FileIntegrity", availableAnalyzers);
        Assert.Contains("BuffoutVersion", availableAnalyzers);
    }

    /// Verifies that the CreateAnalyzers method of the AnalyzerFactory produces analyzers with consistent types regardless
    /// of the specific game provided as input.
    /// <br />
    /// Ensures that:
    /// <br />
    /// - The count of analyzers created for different games is the same.
    /// <br />
    /// - The analyzers' types returned for one game are identical to those returned for another game, regardless of order.
    /// <br />
    /// This test confirms the uniformity of the analyzer creation mechanism across different game configurations.
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

    /// Validates that the CreateAnalyzer method of the AnalyzerFactory creates a new, distinct instance of the specified analyzer each time it is called.
    /// Ensures the following:
    /// <br />
    /// - The returned analyzers are not null.
    /// <br />
    /// - The two instances of the requested analyzer (e.g., FormIdAnalyzer) are not the same object reference.
    /// <br />
    /// - The returned analyzer instances are of the expected type.
    /// <br />
    /// This test guarantees that the factory does not reuse or cache instances of analyzers but instead produces a new instance for each request.
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

    /// Validates that calling CreateAnalyzers on the AnalyzerFactory produces new instances of each analyzer every time.
    /// Ensures that:
    /// <br />
    /// - When CreateAnalyzers is invoked multiple times for the same game input, it returns distinct instances of analyzers.
    /// <br />
    /// - The total count and types of analyzers remain consistent across multiple calls.
    /// <br />
    /// Verifies that analyzers are uniquely instantiated and that the method avoids reusing objects from previous method calls.
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

    /// Ensures that the CreateAnalyzers method does not return null values for any analyzers created for a specified game.
    /// Verifies that:
    /// <br />
    /// - All analyzers returned from the factory are non-null instances.
    /// <br />
    /// - Null values are not inadvertently included in the list of analyzers.
    /// <br />
    /// This test ensures the integrity of the factory's output and eliminates the possibility of null analyzers being returned.
    [Fact]
    public void CreateAnalyzers_ShouldNotReturnNullAnalyzers()
    {
        // Act
        var analyzers = _factory.CreateAnalyzers("Fallout4").ToList();

        // Assert
        Assert.All(analyzers, Assert.NotNull);
    }

    /// Validates that each analyzer created by the CreateAnalyzers method of the AnalyzerFactory has properties with valid values.
    /// Ensures that:
    /// <br />
    /// - Each analyzer has a non-null, non-empty name that is not whitespace.
    /// <br />
    /// - Each analyzer has a priority value greater than or equal to 0.
    /// <br />
    /// - The type of each analyzer instance is correctly defined and not null.
    /// <br />
    /// This test guarantees that the analyzers produced by the factory meet the expected property constraints and structural integrity.
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
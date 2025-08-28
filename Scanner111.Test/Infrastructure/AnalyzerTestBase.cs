using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Analysis;
using Scanner111.Core.Configuration;
using Scanner111.Core.Reporting;

namespace Scanner111.Test.Infrastructure;

/// <summary>
///     Base class for analyzer tests providing common setup and utilities.
/// </summary>
public abstract class AnalyzerTestBase<TAnalyzer> : IDisposable 
    where TAnalyzer : IAnalyzer
{
    protected readonly ILogger<TAnalyzer> Logger;
    protected readonly IAsyncYamlSettingsCore MockYamlCore;
    protected readonly AnalysisContext TestContext;
    protected readonly CancellationTokenSource TestCancellation;
    protected TAnalyzer Sut = default!; // System Under Test

    protected AnalyzerTestBase()
    {
        Logger = Substitute.For<ILogger<TAnalyzer>>();
        MockYamlCore = Substitute.For<IAsyncYamlSettingsCore>();
        TestCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        
        // Setup default YAML responses
        SetupDefaultYamlSettings();
        
        // Create test context
        TestContext = new AnalysisContext(@"C:\test\crashlog.txt", MockYamlCore);
        
        // Initialize the analyzer (must be done by derived class)
        Sut = CreateAnalyzer();
    }

    /// <summary>
    ///     Creates the analyzer instance to test. Must be implemented by derived classes.
    /// </summary>
    protected abstract TAnalyzer CreateAnalyzer();

    /// <summary>
    ///     Sets up default YAML settings. Override to customize.
    /// </summary>
    protected virtual void SetupDefaultYamlSettings()
    {
        MockYamlCore.GetSettingAsync<List<string>>(
                Arg.Any<YamlStore>(), 
                Arg.Any<string>(), 
                Arg.Any<List<string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<List<string>?>(new List<string>()));
            
        MockYamlCore.GetSettingAsync<Dictionary<string, string>>(
                Arg.Any<YamlStore>(), 
                Arg.Any<string>(), 
                Arg.Any<Dictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Dictionary<string, string>?>(new Dictionary<string, string>()));
            
        MockYamlCore.GetSettingAsync<string>(
                Arg.Any<YamlStore>(), 
                Arg.Any<string>(), 
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));
    }

    /// <summary>
    ///     Adds shared data to the test context.
    /// </summary>
    protected void WithSharedData<T>(string key, T data)
    {
        TestContext.SetSharedData(key, data);
    }

    /// <summary>
    ///     Configures a YAML setting response.
    /// </summary>
    protected void WithYamlSetting<T>(YamlStore store, string key, T? value)
    {
        MockYamlCore.GetSettingAsync<T>(
                store, 
                key, 
                Arg.Any<T?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(value));
    }

    /// <summary>
    ///     Runs the analyzer and returns the result.
    /// </summary>
    protected async Task<AnalysisResult> RunAnalyzerAsync(
        CancellationToken? cancellationToken = null)
    {
        var token = cancellationToken ?? TestCancellation.Token;
        return await Sut.AnalyzeAsync(TestContext, token);
    }

    /// <summary>
    ///     Asserts that the analyzer can analyze the current context.
    /// </summary>
    protected async Task AssertCanAnalyzeAsync(bool expected = true)
    {
        var canAnalyze = await Sut.CanAnalyzeAsync(TestContext);
        canAnalyze.Should().Be(expected);
    }

    /// <summary>
    ///     Asserts basic analyzer properties.
    /// </summary>
    protected void AssertAnalyzerProperties(
        string expectedName, 
        string expectedDisplayName,
        int expectedPriority,
        TimeSpan? expectedTimeout = null)
    {
        Sut.Name.Should().Be(expectedName);
        Sut.DisplayName.Should().Be(expectedDisplayName);
        Sut.Priority.Should().Be(expectedPriority);
        
        if (expectedTimeout.HasValue)
        {
            Sut.Timeout.Should().Be(expectedTimeout.Value);
        }
    }

    /// <summary>
    ///     Creates a successful analysis result for testing.
    /// </summary>
    protected AnalysisResult CreateSuccessResult(
        string title = "Test Result",
        string content = "Test content",
        Dictionary<string, string>? metadata = null)
    {
        var fragment = ReportFragment.CreateInfo(title, content);
        return AnalysisResult.CreateSuccess(Sut.Name, fragment);
    }

    /// <summary>
    ///     Creates a failure analysis result for testing.
    /// </summary>
    protected AnalysisResult CreateFailureResult(params string[] errors)
    {
        var result = AnalysisResult.CreateFailure(Sut.Name, errors.FirstOrDefault() ?? "Test failure");
        foreach (var error in errors.Skip(1))
        {
            result.AddError(error);
        }
        return result;
    }

    /// <summary>
    ///     Asserts that an analysis result is successful with expected content.
    /// </summary>
    protected void AssertSuccessResult(
        AnalysisResult result,
        Action<ReportFragment>? fragmentAssertion = null,
        Action<Dictionary<string, string>>? metadataAssertion = null)
    {
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Fragment.Should().NotBeNull();
        
        fragmentAssertion?.Invoke(result.Fragment!);
        if (metadataAssertion != null && result.Metadata != null)
        {
            var metadata = result.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString() ?? string.Empty);
            metadataAssertion(metadata);
        }
    }

    /// <summary>
    ///     Asserts that an analysis result is a failure with expected errors.
    /// </summary>
    protected void AssertFailureResult(
        AnalysisResult result,
        params string[] expectedErrorPatterns)
    {
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        
        foreach (var pattern in expectedErrorPatterns)
        {
            result.Errors.Should().Contain(e => e.Contains(pattern));
        }
    }

    public virtual void Dispose()
    {
        TestCancellation?.Dispose();
    }
}
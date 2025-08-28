using FluentAssertions;
using NSubstitute;
using Scanner111.Core.Configuration;
using Scanner111.Core.Services;
using Scanner111.Test.Infrastructure;

namespace Scanner111.Test.Services;

/// <summary>
///     Unit tests for LogReformatter service.
/// </summary>
public sealed class LogReformatterTests : IntegrationTestBase
{
    private LogReformatter _logReformatter = null!;

    protected override void ConfigureServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
        base.ConfigureServices(services);
        
        // Configure mock settings to return specific values for tests
        var mockSettings = Substitute.For<IAsyncYamlSettingsCore>();
        mockSettings.GetSettingAsync<string>(YamlStore.Settings, "SimplifyLogs", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("false");
        
        services.AddSingleton(mockSettings);
    }

    protected override async Task OnInitializeAsync()
    {
        _logReformatter = GetService<LogReformatter>();
        await base.OnInitializeAsync();
    }

    protected override async Task OnDisposeAsync()
    {
        if (_logReformatter != null)
        {
            await _logReformatter.DisposeAsync();
        }
        await base.OnDisposeAsync();
    }

    [Fact]
    public async Task ReformatSingleLogAsync_WithPluginBrackets_ReplacesSpacesWithZeros()
    {
        // Arrange
        var logContent = """
            Scanner111 v1.0.0
            PLUGINS:
                [00]     Skyrim.esm
                [0A 12]  TestPlugin.esp
                [FF 00]  AnotherPlugin.esp
            End of plugins
            """;

        var logFile = await CreateTestFileAsync("test.log", logContent);

        // Act
        await _logReformatter.ReformatSingleLogAsync(logFile);

        // Assert
        var reformattedContent = await File.ReadAllTextAsync(logFile);
        reformattedContent.Should().Contain("[0A012]  TestPlugin.esp");
        reformattedContent.Should().Contain("[FF000]  AnotherPlugin.esp");
        reformattedContent.Should().Contain("[00]     Skyrim.esm");
    }

    [Fact]
    public async Task ReformatSingleLogAsync_WithoutPluginsSection_LeavesContentUnchanged()
    {
        // Arrange
        var logContent = """
            Scanner111 v1.0.0
            System Information:
            OS: Windows 11
            Call Stack:
              [0] SkyrimSE.exe+0x123456
            End of log
            """;

        var logFile = await CreateTestFileAsync("test.log", logContent);

        // Act
        await _logReformatter.ReformatSingleLogAsync(logFile);

        // Assert
        var reformattedContent = await File.ReadAllTextAsync(logFile);
        reformattedContent.Should().Be(logContent);
    }

    [Fact]
    public async Task ReformatSingleLogAsync_WithSimplifyEnabled_RemovesMatchingLines()
    {
        // Arrange
        var logContent = """
            Scanner111 v1.0.0
            Important information
            Verbose logging that should be removed
            More important data
            Another verbose entry
            End of log
            """;

        var logFile = await CreateTestFileAsync("test.log", logContent);
        var removePatterns = new[] { "verbose", "Verbose" };

        // Act
        await _logReformatter.ReformatSingleLogAsync(logFile, removePatterns, simplifyLogs: true);

        // Assert
        var reformattedContent = await File.ReadAllTextAsync(logFile);
        reformattedContent.Should().NotContain("Verbose logging that should be removed");
        reformattedContent.Should().NotContain("Another verbose entry");
        reformattedContent.Should().Contain("Important information");
        reformattedContent.Should().Contain("More important data");
    }

    [Fact]
    public async Task ReformatCrashLogsAsync_WithMultipleFiles_ProcessesAllFiles()
    {
        // Arrange
        var logFiles = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var content = $"""
                Scanner111 v1.0.0 - File {i}
                PLUGINS:
                    [0A 1{i}]  TestPlugin{i}.esp
                End of file {i}
                """;
            var fileName = $"crashlog_{i}.log";
            var filePath = await CreateTestFileAsync(fileName, content);
            logFiles.Add(filePath);
        }

        // Act
        await _logReformatter.ReformatCrashLogsAsync(logFiles);

        // Assert
        for (int i = 0; i < 5; i++)
        {
            var content = await File.ReadAllTextAsync(logFiles[i]);
            content.Should().Contain($"[0A01{i}]  TestPlugin{i}.esp");
        }
    }

    [Fact]
    public async Task BatchFileMoveAsync_WithValidOperations_MovesAllFiles()
    {
        // Arrange
        var operations = new List<(string Source, string Destination)>();

        for (int i = 0; i < 3; i++)
        {
            var sourceFile = await CreateTestFileAsync($"source_{i}.txt", $"Content {i}");
            var destinationPath = Path.Combine(TestDirectory, $"moved_{i}.txt");
            operations.Add((sourceFile, destinationPath));
        }

        // Act
        await _logReformatter.BatchFileMoveAsync(operations);

        // Assert
        foreach (var (source, destination) in operations)
        {
            File.Exists(source).Should().BeFalse();
            File.Exists(destination).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("    [00 01 02]  Plugin.esp", "    [00010002]  Plugin.esp")]
    [InlineData("    [FF AA BB CC]  Test.esp", "    [FFAABBCC]  Test.esp")]
    [InlineData("    [00]  NoSpaces.esp", "    [00]  NoSpaces.esp")]
    [InlineData("    [  A B  ]  Spaces.esp", "    [00A0B00]  Spaces.esp")]
    public async Task ReformatSingleLogAsync_WithVariousBracketFormats_FormatsCorrectly(
        string inputLine, 
        string expectedLine)
    {
        // Arrange
        var logContent = $"""
            Scanner111 v1.0.0
            PLUGINS:
            {inputLine}
            End
            """;

        var logFile = await CreateTestFileAsync("test.log", logContent);

        // Act
        await _logReformatter.ReformatSingleLogAsync(logFile);

        // Assert
        var reformattedContent = await File.ReadAllTextAsync(logFile);
        reformattedContent.Should().Contain(expectedLine);
    }
}
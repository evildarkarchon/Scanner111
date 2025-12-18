using System.CommandLine;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.Cli.Commands;

namespace Scanner111.Cli.Tests.Commands;

public class ScanCommandTests
{
    [Fact]
    public void Create_WithServiceProvider_ReturnsRootCommand()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var command = ScanCommand.Create(serviceProvider);

        // Assert
        command.Should().NotBeNull();
        command.Description.Should().Contain("Scanner111");
    }

    [Fact]
    public void Create_HasExpectedOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var command = ScanCommand.Create(serviceProvider);
        var optionNames = command.Options.Select(o => o.Name).ToList();

        // Assert - option names don't include the -- prefix
        optionNames.Should().Contain("scan-path");
        optionNames.Should().Contain("fcx-mode");
        optionNames.Should().Contain("show-fid-values");
        optionNames.Should().Contain("stat-logging");
        optionNames.Should().Contain("move-unsolved");
        optionNames.Should().Contain("simplify-logs");
        optionNames.Should().Contain("ini-path");
        optionNames.Should().Contain("mods-folder-path");
        optionNames.Should().Contain("concurrency");
        optionNames.Should().Contain("quiet");
    }

    [Fact]
    public void Create_QuietOption_HasAlias()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var command = ScanCommand.Create(serviceProvider);
        var quietOption = command.Options.First(o => o.Name == "quiet");

        // Assert
        quietOption.Aliases.Should().Contain("-q");
    }

    [Fact]
    public void Create_ConcurrencyOption_HasDefaultValue()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        // Act
        var command = ScanCommand.Create(serviceProvider);
        var concurrencyOption = command.Options.First(o => o.Name == "concurrency");

        // Assert
        concurrencyOption.Should().NotBeNull();
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public async Task InvokeAsync_WithHelpOption_ReturnsZero(string helpArg)
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var command = ScanCommand.Create(serviceProvider);

        // Act
        var result = await command.InvokeAsync(new[] { helpArg });

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public async Task InvokeAsync_WithVersion_ReturnsZero()
    {
        // Arrange
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();
        var command = ScanCommand.Create(serviceProvider);

        // Act
        var result = await command.InvokeAsync(new[] { "--version" });

        // Assert
        result.Should().Be(0);
    }
}

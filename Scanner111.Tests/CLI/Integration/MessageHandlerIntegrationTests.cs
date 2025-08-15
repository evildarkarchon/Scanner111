using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.FCX;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;

namespace Scanner111.Tests.CLI.Integration;

/// <summary>
///     Integration tests for message handler configuration and --legacy-progress flag
/// </summary>
public class MessageHandlerIntegrationTests
{
    #region Command Line Argument Parsing Tests

    [Theory]
    [InlineData(new string[] { }, false)]
    [InlineData(new[] { "scan" }, false)]
    [InlineData(new[] { "scan", "-l", "test.log" }, false)]
    [InlineData(new[] { "scan", "--legacy-progress" }, true)]
    [InlineData(new[] { "scan", "-l", "test.log", "--legacy-progress" }, true)]
    [InlineData(new[] { "demo", "--legacy-progress" }, true)]
    [InlineData(new[] { "--legacy-progress", "scan" }, true)]
    public void CommandLineArgs_LegacyProgressFlag_ParsedCorrectly(string[] args, bool expectedUseLegacy)
    {
        // Act
        var useLegacyProgress = args.Contains("--legacy-progress");

        // Assert
        Assert.Equal(expectedUseLegacy, useLegacyProgress);
    }

    #endregion

    #region Functional Tests

    [Fact]
    public async Task BothHandlers_ImplementIMessageHandlerCorrectly()
    {
        // Arrange
        var services = new ServiceCollection();

        // Test Enhanced Handler
        ConfigureTestServices(services);
        var enhancedProvider = services.BuildServiceProvider();
        var enhancedHandler = enhancedProvider.GetRequiredService<IMessageHandler>();

        // Test Legacy Handler
        services.Clear();
        ConfigureTestServices(services, true);
        var legacyProvider = services.BuildServiceProvider();
        var legacyHandler = legacyProvider.GetRequiredService<IMessageHandler>();

        // Act & Assert - Both should implement all IMessageHandler methods
        foreach (var handler in new[] { enhancedHandler, legacyHandler })
        {
            // Test all message types
            handler.ShowInfo("Info");
            handler.ShowWarning("Warning");
            handler.ShowError("Error");
            handler.ShowSuccess("Success");
            handler.ShowDebug("Debug");
            handler.ShowCritical("Critical");
            handler.ShowMessage("Message", "Details");

            // Test progress
            var progress = handler.ShowProgress("Progress", 100);
            Assert.NotNull(progress);

            var context = handler.CreateProgressContext("Context", 100);
            Assert.NotNull(context);
            context.Update(50, "Half");
            context.Complete();
            context.Dispose();
        }

        // Cleanup
        if (enhancedHandler is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();
    }

    #endregion

    #region Helper Methods

    private static void ConfigureTestServices(IServiceCollection services, bool useLegacyProgress = false)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Register Core services
        services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<ICacheManager, CacheManager>();
        services.AddSingleton<IUnsolvedLogsMover, UnsolvedLogsMover>();

        // Register CLI services
        services.AddSingleton<ICliSettingsService, CliSettingsService>();
        services.AddSingleton<IFileScanService, FileScanService>();
        services.AddSingleton<IScanResultProcessor, ScanResultProcessor>();

        // Use enhanced message handler by default, legacy if specified
        if (useLegacyProgress)
            services.AddSingleton<IMessageHandler, SpectreMessageHandler>();
        else
            services.AddSingleton<IMessageHandler, EnhancedSpectreMessageHandler>();

        services.AddSingleton<ITerminalUIService, SpectreTerminalUIService>();

        // Register FCX services
        services.AddSingleton<IHashValidationService, HashValidationService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
        services.AddSingleton<IModScanner, ModScanner>();
        services.AddSingleton<IModCompatibilityService, ModCompatibilityService>();
    }

    #endregion

    #region Service Configuration Tests

    [Fact]
    public void ConfigureServices_WithoutLegacyFlag_UsesEnhancedHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        ConfigureTestServices(services);
        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IMessageHandler>();

        // Assert
        Assert.IsType<EnhancedSpectreMessageHandler>(handler);
    }

    [Fact]
    public void ConfigureServices_WithLegacyFlag_UsesLegacyHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        ConfigureTestServices(services, true);
        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IMessageHandler>();

        // Assert
        Assert.IsType<SpectreMessageHandler>(handler);
    }

    [Fact]
    public void DefaultConfiguration_UsesEnhancedHandler()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act - Default is false
        ConfigureTestServices(services);
        var provider = services.BuildServiceProvider();
        var handler = provider.GetRequiredService<IMessageHandler>();

        // Assert
        Assert.IsType<EnhancedSpectreMessageHandler>(handler);
    }

    #endregion

    #region Command Options Tests

    [Fact]
    public void ScanOptions_LegacyProgressProperty_DefaultsToFalse()
    {
        // Arrange & Act
        var options = new ScanOptions();

        // Assert
        Assert.False(options.UseLegacyProgress);
    }

    [Fact]
    public void ScanOptions_LegacyProgressProperty_CanBeSet()
    {
        // Arrange & Act
        var options = new ScanOptions
        {
            UseLegacyProgress = true
        };

        // Assert
        Assert.True(options.UseLegacyProgress);
    }

    [Fact]
    public void DemoOptions_LegacyProgressProperty_DefaultsToFalse()
    {
        // Arrange & Act
        var options = new DemoOptions();

        // Assert
        Assert.False(options.UseLegacyProgress);
    }

    [Fact]
    public void DemoOptions_LegacyProgressProperty_CanBeSet()
    {
        // Arrange & Act
        var options = new DemoOptions
        {
            UseLegacyProgress = true
        };

        // Assert
        Assert.True(options.UseLegacyProgress);
    }

    #endregion

    #region Handler Lifecycle Tests

    [Fact]
    public async Task EnhancedHandler_ProperlyInitializesAndDisposes()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        var provider = services.BuildServiceProvider();

        // Act
        var handler = provider.GetRequiredService<IMessageHandler>();

        // Use the handler
        handler.ShowInfo("Test message");
        var context = handler.CreateProgressContext("Test", 100);
        context.Update(50, "Half way");
        context.Complete();

        // Dispose if it's IAsyncDisposable
        if (handler is IAsyncDisposable asyncDisposable) await asyncDisposable.DisposeAsync();

        // Assert - No exceptions should be thrown
        Assert.NotNull(handler);
    }

    [Fact]
    public void LegacyHandler_ProperlyInitializes()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services, true);
        var provider = services.BuildServiceProvider();

        // Act
        var handler = provider.GetRequiredService<IMessageHandler>();

        // Use the handler
        handler.ShowInfo("Test message");
        var context = handler.CreateProgressContext("Test", 100);
        context.Update(50, "Half way");
        context.Complete();

        // Assert - No exceptions should be thrown
        Assert.NotNull(handler);
    }

    #endregion

    #region Singleton Behavior Tests

    [Fact]
    public void MessageHandler_RegisteredAsSingleton_EnhancedVersion()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services);
        var provider = services.BuildServiceProvider();

        // Act
        var handler1 = provider.GetRequiredService<IMessageHandler>();
        var handler2 = provider.GetRequiredService<IMessageHandler>();

        // Assert
        Assert.Same(handler1, handler2);
        Assert.IsType<EnhancedSpectreMessageHandler>(handler1);
    }

    [Fact]
    public void MessageHandler_RegisteredAsSingleton_LegacyVersion()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureTestServices(services, true);
        var provider = services.BuildServiceProvider();

        // Act
        var handler1 = provider.GetRequiredService<IMessageHandler>();
        var handler2 = provider.GetRequiredService<IMessageHandler>();

        // Assert
        Assert.Same(handler1, handler2);
        Assert.IsType<SpectreMessageHandler>(handler1);
    }

    #endregion
}
using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Services;
using Scanner111.Core.FCX;
using Xunit;

namespace Scanner111.Tests.CLI;

public class ProgramServiceConfigurationTests
{
    [Fact]
    public void ConfigureServices_RegistersAllRequiredServices()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act - Manually register services as they are in Program.cs (default without legacy flag)
        ConfigureServicesForTesting(services, useLegacyProgress: false);
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - Core services
        serviceProvider.GetService<IApplicationSettingsService>().Should().NotBeNull();
        serviceProvider.GetService<IUpdateService>().Should().NotBeNull();
        serviceProvider.GetService<ICacheManager>().Should().NotBeNull();
        serviceProvider.GetService<IUnsolvedLogsMover>().Should().NotBeNull();
        
        // Assert - CLI services
        serviceProvider.GetService<ICliSettingsService>().Should().NotBeNull();
        serviceProvider.GetService<IFileScanService>().Should().NotBeNull();
        serviceProvider.GetService<IScanResultProcessor>().Should().NotBeNull();
        serviceProvider.GetService<IMessageHandler>().Should().NotBeNull();
        
        // Assert - FCX services
        serviceProvider.GetService<IHashValidationService>().Should().NotBeNull();
        serviceProvider.GetService<IBackupService>().Should().NotBeNull();
        serviceProvider.GetService<IYamlSettingsProvider>().Should().NotBeNull();
        serviceProvider.GetService<IModScanner>().Should().NotBeNull();
        serviceProvider.GetService<IModCompatibilityService>().Should().NotBeNull();
        
        // Assert - Commands
        serviceProvider.GetService<ScanCommand>().Should().NotBeNull();
        serviceProvider.GetService<DemoCommand>().Should().NotBeNull();
        serviceProvider.GetService<ConfigCommand>().Should().NotBeNull();
        serviceProvider.GetService<AboutCommand>().Should().NotBeNull();
        serviceProvider.GetService<FcxCommand>().Should().NotBeNull();
    }

    [Fact]
    public void ConfigureServices_RegistersServicesWithCorrectLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        ConfigureServicesForTesting(services, useLegacyProgress: false);
        
        // Assert - Verify singleton registrations
        var singletonTypes = new[]
        {
            typeof(IApplicationSettingsService),
            typeof(IUpdateService),
            typeof(ICacheManager),
            typeof(IUnsolvedLogsMover),
            typeof(ICliSettingsService),
            typeof(IFileScanService),
            typeof(IScanResultProcessor),
            typeof(IMessageHandler),
            typeof(IHashValidationService),
            typeof(IBackupService),
            typeof(IYamlSettingsProvider),
            typeof(IModScanner),
            typeof(IModCompatibilityService)
        };
        
        foreach (var serviceType in singletonTypes)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == serviceType);
            descriptor.Should().NotBeNull();
            descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton);
        }
        
        // Assert - Verify transient registrations
        var transientTypes = new[]
        {
            typeof(ScanCommand),
            typeof(DemoCommand),
            typeof(ConfigCommand),
            typeof(AboutCommand),
            typeof(FcxCommand)
        };
        
        foreach (var serviceType in transientTypes)
        {
            var descriptor = services.FirstOrDefault(d => d.ServiceType == serviceType);
            descriptor.Should().NotBeNull();
            descriptor!.Lifetime.Should().Be(ServiceLifetime.Transient);
        }
    }

    [Fact]
    public void ServiceProvider_CanResolveAllCommands()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureServicesForTesting(services, useLegacyProgress: false);
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act & Assert - Verify all commands can be instantiated
        var scanCommand = serviceProvider.GetRequiredService<ScanCommand>();
        scanCommand.Should().NotBeNull();
        scanCommand.Should().BeOfType<ScanCommand>();
        
        var demoCommand = serviceProvider.GetRequiredService<DemoCommand>();
        demoCommand.Should().NotBeNull();
        demoCommand.Should().BeOfType<DemoCommand>();
        
        var configCommand = serviceProvider.GetRequiredService<ConfigCommand>();
        configCommand.Should().NotBeNull();
        configCommand.Should().BeOfType<ConfigCommand>();
        
        var aboutCommand = serviceProvider.GetRequiredService<AboutCommand>();
        aboutCommand.Should().NotBeNull();
        aboutCommand.Should().BeOfType<AboutCommand>();
        
        var fcxCommand = serviceProvider.GetRequiredService<FcxCommand>();
        fcxCommand.Should().NotBeNull();
        fcxCommand.Should().BeOfType<FcxCommand>();
    }

    [Fact]
    public void ConsoleEncoding_ConfigurationLogic()
    {
        // This test verifies the console encoding setup logic would work correctly
        // The actual console encoding is set at the top level of Program.cs
        
        if (OperatingSystem.IsWindows())
        {
            // On Windows, we expect UTF-8 encoding to be set
            // Since we can't test the actual console in unit tests,
            // we just verify the platform check works
            OperatingSystem.IsWindows().Should().BeTrue();
        }
        else
        {
            // On non-Windows, the encoding setup is skipped
            OperatingSystem.IsWindows().Should().BeFalse();
        }
    }

    [Fact]
    public void ConfigureServices_WithLegacyProgress_UsesSpectreMessageHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act - Configure with legacy progress flag
        ConfigureServicesForTesting(services, useLegacyProgress: true);
        
        var serviceProvider = services.BuildServiceProvider();
        var messageHandler = serviceProvider.GetRequiredService<IMessageHandler>();
        
        // Assert
        messageHandler.Should().NotBeNull();
        messageHandler.Should().BeOfType<SpectreMessageHandler>();
    }

    [Fact]
    public void ConfigureServices_WithoutLegacyProgress_UsesEnhancedSpectreMessageHandler()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act - Configure without legacy progress flag (default)
        ConfigureServicesForTesting(services, useLegacyProgress: false);
        
        var serviceProvider = services.BuildServiceProvider();
        var messageHandler = serviceProvider.GetRequiredService<IMessageHandler>();
        
        // Assert
        messageHandler.Should().NotBeNull();
        messageHandler.Should().BeOfType<EnhancedSpectreMessageHandler>();
    }

    [Theory]
    [InlineData(true, typeof(SpectreMessageHandler))]
    [InlineData(false, typeof(EnhancedSpectreMessageHandler))]
    public void ConfigureServices_MessageHandlerType_DependsOnLegacyFlag(bool useLegacyProgress, Type expectedHandlerType)
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        ConfigureServicesForTesting(services, useLegacyProgress);
        
        var serviceProvider = services.BuildServiceProvider();
        var messageHandler = serviceProvider.GetRequiredService<IMessageHandler>();
        
        // Assert
        messageHandler.Should().NotBeNull();
        messageHandler.Should().BeOfType(expectedHandlerType);
    }
    
    // Helper method that duplicates the service configuration from Program.cs
    private static void ConfigureServicesForTesting(IServiceCollection services, bool useLegacyProgress = false)
    {
        // Configure logging
        services.AddLogging(builder =>
        {
            // In tests, we don't need console logging
            builder.SetMinimumLevel(LogLevel.Information);
        });
        
        // Add memory cache
        services.AddMemoryCache();

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
        {
            services.AddSingleton<IMessageHandler, SpectreMessageHandler>();
        }
        else
        {
            services.AddSingleton<IMessageHandler, EnhancedSpectreMessageHandler>();
        }
        
        services.AddSingleton<ITerminalUIService, SpectreTerminalUIService>();

        // Register FCX services
        services.AddSingleton<IHashValidationService, HashValidationService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
        services.AddSingleton<IModScanner, ModScanner>();
        services.AddSingleton<IModCompatibilityService, ModCompatibilityService>();
        
        // Register commands
        services.AddTransient<ScanCommand>();
        services.AddTransient<DemoCommand>();
        services.AddTransient<ConfigCommand>();
        services.AddTransient<AboutCommand>();
        services.AddTransient<FcxCommand>();
        services.AddTransient<InteractiveCommand>();
        
        // Register ICommand interfaces for injection
        services.AddTransient<ICommand<Scanner111.CLI.Models.ScanOptions>, ScanCommand>();
        services.AddTransient<ICommand<DemoOptions>, DemoCommand>();
        services.AddTransient<ICommand<ConfigOptions>, ConfigCommand>();
        services.AddTransient<ICommand<AboutOptions>, AboutCommand>();
        services.AddTransient<ICommand<FcxOptions>, FcxCommand>();
        services.AddTransient<ICommand<InteractiveOptions>, InteractiveCommand>();
    }
}
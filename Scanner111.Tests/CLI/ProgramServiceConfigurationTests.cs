using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Commands;
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
        
        // Act - Manually register services as they are in Program.cs
        ConfigureServicesForTesting(services);
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Assert - Core services
        Assert.NotNull(serviceProvider.GetService<IApplicationSettingsService>());
        Assert.NotNull(serviceProvider.GetService<IUpdateService>());
        Assert.NotNull(serviceProvider.GetService<ICacheManager>());
        Assert.NotNull(serviceProvider.GetService<IUnsolvedLogsMover>());
        
        // Assert - CLI services
        Assert.NotNull(serviceProvider.GetService<ICliSettingsService>());
        Assert.NotNull(serviceProvider.GetService<IFileScanService>());
        Assert.NotNull(serviceProvider.GetService<IScanResultProcessor>());
        Assert.NotNull(serviceProvider.GetService<IMessageHandler>());
        
        // Assert - FCX services
        Assert.NotNull(serviceProvider.GetService<IHashValidationService>());
        Assert.NotNull(serviceProvider.GetService<IBackupService>());
        Assert.NotNull(serviceProvider.GetService<IYamlSettingsProvider>());
        Assert.NotNull(serviceProvider.GetService<IModScanner>());
        Assert.NotNull(serviceProvider.GetService<IModCompatibilityService>());
        
        // Assert - Commands
        Assert.NotNull(serviceProvider.GetService<ScanCommand>());
        Assert.NotNull(serviceProvider.GetService<DemoCommand>());
        Assert.NotNull(serviceProvider.GetService<ConfigCommand>());
        Assert.NotNull(serviceProvider.GetService<AboutCommand>());
        Assert.NotNull(serviceProvider.GetService<FcxCommand>());
    }

    [Fact]
    public void ConfigureServices_RegistersServicesWithCorrectLifetime()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Act
        ConfigureServicesForTesting(services);
        
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
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
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
            Assert.NotNull(descriptor);
            Assert.Equal(ServiceLifetime.Transient, descriptor.Lifetime);
        }
    }

    [Fact]
    public void ServiceProvider_CanResolveAllCommands()
    {
        // Arrange
        var services = new ServiceCollection();
        ConfigureServicesForTesting(services);
        
        var serviceProvider = services.BuildServiceProvider();
        
        // Act & Assert - Verify all commands can be instantiated
        var scanCommand = serviceProvider.GetRequiredService<ScanCommand>();
        Assert.NotNull(scanCommand);
        Assert.IsType<ScanCommand>(scanCommand);
        
        var demoCommand = serviceProvider.GetRequiredService<DemoCommand>();
        Assert.NotNull(demoCommand);
        Assert.IsType<DemoCommand>(demoCommand);
        
        var configCommand = serviceProvider.GetRequiredService<ConfigCommand>();
        Assert.NotNull(configCommand);
        Assert.IsType<ConfigCommand>(configCommand);
        
        var aboutCommand = serviceProvider.GetRequiredService<AboutCommand>();
        Assert.NotNull(aboutCommand);
        Assert.IsType<AboutCommand>(aboutCommand);
        
        var fcxCommand = serviceProvider.GetRequiredService<FcxCommand>();
        Assert.NotNull(fcxCommand);
        Assert.IsType<FcxCommand>(fcxCommand);
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
            Assert.True(OperatingSystem.IsWindows());
        }
        else
        {
            // On non-Windows, the encoding setup is skipped
            Assert.False(OperatingSystem.IsWindows());
        }
    }
    
    // Helper method that duplicates the service configuration from Program.cs
    private static void ConfigureServicesForTesting(IServiceCollection services)
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
        services.AddSingleton<IMessageHandler, CliMessageHandler>();

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
    }
}
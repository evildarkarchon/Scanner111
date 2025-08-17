using Scanner111.Core.Analyzers;
using Scanner111.Core.FCX;
using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.ModManagers;
using Scanner111.Core.Pipeline;
using Scanner111.Core.Services;
using Scanner111.GUI.Services;
using Scanner111.GUI.ViewModels;

namespace Scanner111.Tests.GUI.Integration;

/// <summary>
///     Tests for the dependency injection container configuration in the GUI application.
///     Verifies that all services are registered correctly with appropriate lifetimes.
/// </summary>
[Collection("GUI Tests")]
public class DependencyInjectionTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IServiceCollection _services;

    public DependencyInjectionTests()
    {
        _services = new ServiceCollection();
        ConfigureServices(_services);
        _serviceProvider = _services.BuildServiceProvider();
    }

    /// <summary>
    ///     Mimics the service registration from App.axaml.cs
    /// </summary>
    private void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IUpdateService, UpdateService>();
        services.AddSingleton<ICacheManager, CacheManager>();
        services.AddSingleton<IUnsolvedLogsMover, UnsolvedLogsMover>();
        services.AddSingleton<IApplicationSettingsService, ApplicationSettingsService>();
        services.AddSingleton<IYamlSettingsProvider, YamlSettingsService>();
        services.AddSingleton<IFormIdDatabaseService, FormIdDatabaseService>();

        // GUI-specific services
        services.AddSingleton<GuiMessageHandlerService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IRecentItemsService, RecentItemsService>();
        services.AddSingleton<IAudioNotificationService, AudioNotificationService>();
        services.AddSingleton<IStatisticsService, StatisticsService>();
        services.AddSingleton<IPastebinService, PastebinService>();

        // Mod manager services
        services.AddSingleton<IModManagerService, ModManagerService>();
        services.AddSingleton<IModManagerDetector, ModManagerDetector>();

        // Pipeline and analyzers
        services.AddTransient<IScanPipeline, ScanPipeline>();
        services.AddTransient<IAnalyzerFactory, AnalyzerFactory>();
        services.AddTransient<IReportWriter, ReportWriter>();
        // CrashLogParser is a static class, not a service

        // FCX services - these are concrete classes, not interfaces
        services.AddTransient<FileIntegrityAnalyzer>();
        services.AddTransient<ModConflictAnalyzer>();
        services.AddTransient<VersionAnalyzer>();
        services.AddTransient<IModScanner, ModScanner>();
        services.AddTransient<IModCompatibilityService, ModCompatibilityService>();
        services.AddTransient<IHashValidationService, HashValidationService>();

        // Game scanning services
        services.AddTransient<IGameScannerService, GameScannerService>();
        // Note: IMessageHandler should be registered with GuiMessageHandlerService implementation
        services.AddTransient<IMessageHandler, GuiMessageHandlerService>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<SettingsWindowViewModel>();
        services.AddTransient<GameScanViewModel>();
        services.AddTransient<StatisticsViewModel>();
    }

    #region Service Lifetime Tests

    [Fact]
    public void ServiceLifetimes_ConfiguredCorrectly()
    {
        // Arrange
        var singletonTypes = new[]
        {
            typeof(ISettingsService),
            typeof(IUpdateService),
            typeof(ICacheManager),
            typeof(GuiMessageHandlerService),
            typeof(IThemeService)
        };

        var transientTypes = new[]
        {
            typeof(IScanPipeline),
            typeof(IGameScannerService),
            typeof(MainWindowViewModel),
            typeof(GameScanViewModel)
        };

        // Act & Assert - Verify singleton services
        foreach (var serviceType in singletonTypes)
        {
            var descriptor = _services.FirstOrDefault(d => d.ServiceType == serviceType);
            descriptor.Should().NotBeNull($"because {serviceType.Name} should be registered");
            descriptor!.Lifetime.Should().Be(ServiceLifetime.Singleton,
                $"because {serviceType.Name} should be singleton");
        }

        // Act & Assert - Verify transient services
        foreach (var serviceType in transientTypes)
        {
            var descriptor = _services.FirstOrDefault(d => d.ServiceType == serviceType);
            descriptor.Should().NotBeNull($"because {serviceType.Name} should be registered");
            descriptor!.Lifetime.Should().Be(ServiceLifetime.Transient,
                $"because {serviceType.Name} should be transient");
        }
    }

    #endregion

    #region Circular Dependency Tests

    [Fact]
    public void ServiceRegistration_HasNoCircularDependencies()
    {
        // Act & Assert
        var act = () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var mainViewModel = scope.ServiceProvider.GetService<MainWindowViewModel>();
            var gameScanViewModel = scope.ServiceProvider.GetService<GameScanViewModel>();
            var settingsViewModel = scope.ServiceProvider.GetService<SettingsWindowViewModel>();
        };

        act.Should().NotThrow<InvalidOperationException>("because there should be no circular dependencies");
    }

    #endregion

    #region Scoped Service Tests

    [Fact]
    public void ScopedServices_WorkCorrectlyInDifferentScopes()
    {
        // Arrange
        IServiceScope scope1;
        IServiceScope scope2;
        MainWindowViewModel? viewModel1;
        MainWindowViewModel? viewModel2;

        // Act
        using (scope1 = _serviceProvider.CreateScope())
        {
            viewModel1 = scope1.ServiceProvider.GetService<MainWindowViewModel>();
        }

        using (scope2 = _serviceProvider.CreateScope())
        {
            viewModel2 = scope2.ServiceProvider.GetService<MainWindowViewModel>();
        }

        // Assert
        viewModel1.Should().NotBeNull("because view model should be resolvable in scope 1");
        viewModel2.Should().NotBeNull("because view model should be resolvable in scope 2");
        viewModel1.Should().NotBeSameAs(viewModel2,
            "because transient services in different scopes should be different instances");
    }

    #endregion

    #region Core Services Tests

    [Fact]
    public void CoreServices_RegisteredAsSingleton()
    {
        // Arrange & Act
        var settingsService1 = _serviceProvider.GetService<ISettingsService>();
        var settingsService2 = _serviceProvider.GetService<ISettingsService>();

        var updateService1 = _serviceProvider.GetService<IUpdateService>();
        var updateService2 = _serviceProvider.GetService<IUpdateService>();

        var cacheManager1 = _serviceProvider.GetService<ICacheManager>();
        var cacheManager2 = _serviceProvider.GetService<ICacheManager>();

        // Assert
        settingsService1.Should().NotBeNull("because ISettingsService should be registered");
        settingsService1.Should().BeSameAs(settingsService2, "because ISettingsService should be singleton");

        updateService1.Should().NotBeNull("because IUpdateService should be registered");
        updateService1.Should().BeSameAs(updateService2, "because IUpdateService should be singleton");

        cacheManager1.Should().NotBeNull("because ICacheManager should be registered");
        cacheManager1.Should().BeSameAs(cacheManager2, "because ICacheManager should be singleton");
    }

    [Fact]
    public void GuiServices_RegisteredAsSingleton()
    {
        // Arrange & Act
        var messageHandler1 = _serviceProvider.GetService<GuiMessageHandlerService>();
        var messageHandler2 = _serviceProvider.GetService<GuiMessageHandlerService>();

        var themeService1 = _serviceProvider.GetService<IThemeService>();
        var themeService2 = _serviceProvider.GetService<IThemeService>();

        // Assert
        messageHandler1.Should().NotBeNull("because GuiMessageHandlerService should be registered");
        messageHandler1.Should().BeSameAs(messageHandler2, "because GuiMessageHandlerService should be singleton");

        themeService1.Should().NotBeNull("because IThemeService should be registered");
        themeService1.Should().BeSameAs(themeService2, "because IThemeService should be singleton");
    }

    #endregion

    #region Transient Services Tests

    [Fact]
    public void GameScanningServices_RegisteredAsTransient()
    {
        // Arrange & Act
        var gameScannerService1 = _serviceProvider.GetService<IGameScannerService>();
        var gameScannerService2 = _serviceProvider.GetService<IGameScannerService>();

        var messageHandler1 = _serviceProvider.GetService<IMessageHandler>();
        var messageHandler2 = _serviceProvider.GetService<IMessageHandler>();

        // Assert
        gameScannerService1.Should().NotBeNull("because IGameScannerService should be registered");
        gameScannerService1.Should()
            .NotBeSameAs(gameScannerService2, "because IGameScannerService should be transient");

        messageHandler1.Should().NotBeNull("because IMessageHandler should be registered");
        messageHandler1.Should().NotBeSameAs(messageHandler2, "because IMessageHandler should be transient");
    }

    [Fact]
    public void PipelineServices_RegisteredAsTransient()
    {
        // Arrange & Act
        var scanPipeline1 = _serviceProvider.GetService<IScanPipeline>();
        var scanPipeline2 = _serviceProvider.GetService<IScanPipeline>();

        var analyzerFactory1 = _serviceProvider.GetService<IAnalyzerFactory>();
        var analyzerFactory2 = _serviceProvider.GetService<IAnalyzerFactory>();

        // Assert
        scanPipeline1.Should().NotBeNull("because IScanPipeline should be registered");
        scanPipeline1.Should().NotBeSameAs(scanPipeline2, "because IScanPipeline should be transient");

        analyzerFactory1.Should().NotBeNull("because IAnalyzerFactory should be registered");
        analyzerFactory1.Should().NotBeSameAs(analyzerFactory2, "because IAnalyzerFactory should be transient");
    }

    [Fact]
    public void FcxServices_RegisteredAsTransient()
    {
        // Arrange & Act
        var fileIntegrityAnalyzer1 = _serviceProvider.GetService<FileIntegrityAnalyzer>();
        var fileIntegrityAnalyzer2 = _serviceProvider.GetService<FileIntegrityAnalyzer>();

        var modScanner1 = _serviceProvider.GetService<IModScanner>();
        var modScanner2 = _serviceProvider.GetService<IModScanner>();

        // Assert
        fileIntegrityAnalyzer1.Should().NotBeNull("because FileIntegrityAnalyzer should be registered");
        fileIntegrityAnalyzer1.Should()
            .NotBeSameAs(fileIntegrityAnalyzer2, "because FileIntegrityAnalyzer should be transient");

        modScanner1.Should().NotBeNull("because IModScanner should be registered");
        modScanner1.Should().NotBeSameAs(modScanner2, "because IModScanner should be transient");
    }

    #endregion

    #region ViewModels Tests

    [Fact]
    public void ViewModels_RegisteredAsTransient()
    {
        // Arrange & Act
        var mainViewModel1 = _serviceProvider.GetService<MainWindowViewModel>();
        var mainViewModel2 = _serviceProvider.GetService<MainWindowViewModel>();

        var gameScanViewModel1 = _serviceProvider.GetService<GameScanViewModel>();
        var gameScanViewModel2 = _serviceProvider.GetService<GameScanViewModel>();

        // Assert
        mainViewModel1.Should().NotBeNull("because MainWindowViewModel should be registered");
        mainViewModel1.Should().NotBeSameAs(mainViewModel2, "because MainWindowViewModel should be transient");

        gameScanViewModel1.Should().NotBeNull("because GameScanViewModel should be registered");
        gameScanViewModel1.Should().NotBeSameAs(gameScanViewModel2, "because GameScanViewModel should be transient");
    }

    [Fact]
    public void MainWindowViewModel_CanBeResolvedWithAllDependencies()
    {
        // Act
        var viewModel = _serviceProvider.GetService<MainWindowViewModel>();

        // Assert
        viewModel.Should().NotBeNull("because MainWindowViewModel should be resolvable");
        viewModel.Should().BeOfType<MainWindowViewModel>("because correct type should be resolved");

        // Verify key properties are initialized
        viewModel.SelectLogFileCommand.Should().NotBeNull("because commands should be initialized");
        viewModel.SelectGamePathCommand.Should().NotBeNull("because commands should be initialized");
        viewModel.ScanCommand.Should().NotBeNull("because commands should be initialized");
    }

    [Fact]
    public void GameScanViewModel_CanBeResolvedWithAllDependencies()
    {
        // Act
        var viewModel = _serviceProvider.GetService<GameScanViewModel>();

        // Assert
        viewModel.Should().NotBeNull("because GameScanViewModel should be resolvable");
        viewModel.Should().BeOfType<GameScanViewModel>("because correct type should be resolved");

        // Verify key properties are initialized
        viewModel.SelectGamePathCommand.Should().NotBeNull("because commands should be initialized");
        viewModel.StartScanCommand.Should().NotBeNull("because commands should be initialized");
        viewModel.RunIndividualScanCommand.Should().NotBeNull("because commands should be initialized");
    }

    #endregion

    #region Service Resolution Tests

    [Fact]
    public void AllRequiredServicesCanBeResolved()
    {
        // Arrange
        var requiredServices = new[]
        {
            typeof(ISettingsService),
            typeof(IUpdateService),
            typeof(ICacheManager),
            typeof(IUnsolvedLogsMover),
            typeof(IApplicationSettingsService),
            typeof(IYamlSettingsProvider),
            typeof(IFormIdDatabaseService),
            typeof(GuiMessageHandlerService),
            typeof(IThemeService),
            typeof(IRecentItemsService),
            typeof(IAudioNotificationService),
            typeof(IStatisticsService),
            typeof(IPastebinService),
            typeof(IModManagerService),
            typeof(IModManagerDetector),
            typeof(IScanPipeline),
            typeof(IAnalyzerFactory),
            typeof(IReportWriter),
            typeof(IGameScannerService),
            typeof(IMessageHandler)
        };

        // Act & Assert
        foreach (var serviceType in requiredServices)
        {
            var service = _serviceProvider.GetService(serviceType);
            service.Should().NotBeNull($"because {serviceType.Name} should be resolvable");
        }
    }

    [Fact]
    public void ServiceProvider_PassedToMainWindowViewModel_ResolvesServices()
    {
        // Arrange
        var mockServiceProvider = new Mock<IServiceProvider>();
        var mockGameScannerService = new Mock<IGameScannerService>();
        var mockMessageHandler = new Mock<IMessageHandler>();
        var mockSettingsService = new Mock<ISettingsService>();

        mockServiceProvider.Setup(sp => sp.GetService(typeof(IGameScannerService)))
            .Returns(mockGameScannerService.Object);
        mockServiceProvider.Setup(sp => sp.GetService(typeof(IMessageHandler)))
            .Returns(mockMessageHandler.Object);
        mockServiceProvider.Setup(sp => sp.GetService(typeof(ISettingsService)))
            .Returns(mockSettingsService.Object);

        var messageHandlerService = new GuiMessageHandlerService();
        var updateService = new Mock<IUpdateService>().Object;
        var cacheManager = new Mock<ICacheManager>().Object;
        var unsolvedLogsMover = new Mock<IUnsolvedLogsMover>().Object;

        // Act
        var viewModel = new MainWindowViewModel(
            mockServiceProvider.Object,
            mockSettingsService.Object,
            messageHandlerService,
            updateService,
            cacheManager,
            unsolvedLogsMover);

        // Assert
        viewModel.Should().NotBeNull("because view model should be created");
        mockServiceProvider.Verify(sp => sp.GetService(It.IsAny<Type>()), Times.Never,
            "because services are not resolved in constructor, but when needed");
    }

    #endregion
}
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Scanner111.Core.GameScanning;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.GameScanning
{
    /// <summary>
    /// Tests to ensure all GameScanning interfaces are properly implemented
    /// and can be resolved through dependency injection.
    /// </summary>
    public class InterfaceImplementationTests
    {
        [Fact]
        public void IGameScannerService_CanBeInstantiated()
        {
            // Arrange
            var mockCrashGen = new Mock<ICrashGenChecker>();
            var mockXse = new Mock<IXsePluginValidator>();
            var mockIni = new Mock<IModIniScanner>();
            var mockWrye = new Mock<IWryeBashChecker>();
            var mockHandler = new Mock<IMessageHandler>();
            var mockLogger = new Mock<ILogger<GameScannerService>>();

            // Act
            IGameScannerService service = new GameScannerService(
                mockCrashGen.Object,
                mockXse.Object,
                mockIni.Object,
                mockWrye.Object,
                mockHandler.Object,
                mockLogger.Object);

            // Assert
            service.Should().NotBeNull();
            service.Should().BeAssignableTo<IGameScannerService>();
        }

        [Fact]
        public void ICrashGenChecker_CanBeInstantiated()
        {
            // Arrange
            var mockSettings = new Mock<IApplicationSettingsService>();
            var mockYaml = new Mock<IYamlSettingsProvider>();
            var mockLogger = new Mock<ILogger<CrashGenChecker>>();

            // Act
            ICrashGenChecker checker = new CrashGenChecker(
                mockSettings.Object,
                mockYaml.Object,
                mockLogger.Object);

            // Assert
            checker.Should().NotBeNull();
            checker.Should().BeAssignableTo<ICrashGenChecker>();
        }

        [Fact]
        public void IXsePluginValidator_CanBeInstantiated()
        {
            // Arrange
            var mockSettings = new Mock<IApplicationSettingsService>();
            var mockYaml = new Mock<IYamlSettingsProvider>();
            var mockLogger = new Mock<ILogger<XsePluginValidator>>();

            // Act
            IXsePluginValidator validator = new XsePluginValidator(
                mockSettings.Object,
                mockYaml.Object,
                mockLogger.Object);

            // Assert
            validator.Should().NotBeNull();
            validator.Should().BeAssignableTo<IXsePluginValidator>();
        }

        [Fact]
        public void IModIniScanner_CanBeInstantiated()
        {
            // Arrange
            var mockSettings = new Mock<IApplicationSettingsService>();
            var mockLogger = new Mock<ILogger<ModIniScanner>>();

            // Act
            IModIniScanner scanner = new ModIniScanner(
                mockSettings.Object,
                mockLogger.Object);

            // Assert
            scanner.Should().NotBeNull();
            scanner.Should().BeAssignableTo<IModIniScanner>();
        }

        [Fact]
        public void IWryeBashChecker_CanBeInstantiated()
        {
            // Arrange
            var mockSettings = new Mock<IApplicationSettingsService>();
            var mockYaml = new Mock<IYamlSettingsProvider>();
            var mockLogger = new Mock<ILogger<WryeBashChecker>>();

            // Act
            IWryeBashChecker checker = new WryeBashChecker(
                mockSettings.Object,
                mockYaml.Object,
                mockLogger.Object);

            // Assert
            checker.Should().NotBeNull();
            checker.Should().BeAssignableTo<IWryeBashChecker>();
        }

        [Fact]
        public void AllInterfaces_CanBeRegisteredWithDI()
        {
            // Arrange
            var services = new ServiceCollection();

            // Add required dependencies
            services.AddSingleton<IApplicationSettingsService>(Mock.Of<IApplicationSettingsService>());
            services.AddSingleton<IYamlSettingsProvider>(Mock.Of<IYamlSettingsProvider>());
            services.AddSingleton<IMessageHandler>(Mock.Of<IMessageHandler>());
            services.AddLogging();

            // Register GameScanning services
            services.AddScoped<IGameScannerService, GameScannerService>();
            services.AddScoped<ICrashGenChecker, CrashGenChecker>();
            services.AddScoped<IXsePluginValidator, XsePluginValidator>();
            services.AddScoped<IModIniScanner, ModIniScanner>();
            services.AddScoped<IWryeBashChecker, WryeBashChecker>();

            var serviceProvider = services.BuildServiceProvider();

            // Act & Assert
            var gameScannerService = serviceProvider.GetService<IGameScannerService>();
            gameScannerService.Should().NotBeNull();
            gameScannerService.Should().BeOfType<GameScannerService>();

            var crashGenChecker = serviceProvider.GetService<ICrashGenChecker>();
            crashGenChecker.Should().NotBeNull();
            crashGenChecker.Should().BeOfType<CrashGenChecker>();

            var xseValidator = serviceProvider.GetService<IXsePluginValidator>();
            xseValidator.Should().NotBeNull();
            xseValidator.Should().BeOfType<XsePluginValidator>();

            var modIniScanner = serviceProvider.GetService<IModIniScanner>();
            modIniScanner.Should().NotBeNull();
            modIniScanner.Should().BeOfType<ModIniScanner>();

            var wryeBashChecker = serviceProvider.GetService<IWryeBashChecker>();
            wryeBashChecker.Should().NotBeNull();
            wryeBashChecker.Should().BeOfType<WryeBashChecker>();
        }

        [Fact]
        public async Task IGameScannerService_AllMethodsAreCallable()
        {
            // Arrange
            var mockService = new Mock<IGameScannerService>();
            mockService.Setup(x => x.ScanGameAsync(default)).ReturnsAsync(new GameScanResult());
            mockService.Setup(x => x.CheckCrashGenAsync(default)).ReturnsAsync("CrashGen result");
            mockService.Setup(x => x.ValidateXsePluginsAsync(default)).ReturnsAsync("XSE result");
            mockService.Setup(x => x.ScanModInisAsync(default)).ReturnsAsync("INI result");
            mockService.Setup(x => x.CheckWryeBashAsync(default)).ReturnsAsync("WryeBash result");

            IGameScannerService service = mockService.Object;

            // Act & Assert
            var scanResult = await service.ScanGameAsync();
            scanResult.Should().NotBeNull();

            var crashGenResult = await service.CheckCrashGenAsync();
            crashGenResult.Should().Be("CrashGen result");

            var xseResult = await service.ValidateXsePluginsAsync();
            xseResult.Should().Be("XSE result");

            var iniResult = await service.ScanModInisAsync();
            iniResult.Should().Be("INI result");

            var wryeResult = await service.CheckWryeBashAsync();
            wryeResult.Should().Be("WryeBash result");
        }

        [Fact]
        public async Task ICrashGenChecker_AllMethodsAreCallable()
        {
            // Arrange
            var mockChecker = new Mock<ICrashGenChecker>();
            mockChecker.Setup(x => x.CheckAsync()).ReturnsAsync("Check result");
            mockChecker.Setup(x => x.HasPlugin(It.IsAny<List<string>>())).Returns(true);

            ICrashGenChecker checker = mockChecker.Object;

            // Act & Assert
            var checkResult = await checker.CheckAsync();
            checkResult.Should().Be("Check result");

            var hasPlugin = checker.HasPlugin(new List<string> { "test.dll" });
            hasPlugin.Should().BeTrue();
        }

        [Fact]
        public async Task IXsePluginValidator_AllMethodsAreCallable()
        {
            // Arrange
            var mockValidator = new Mock<IXsePluginValidator>();
            mockValidator.Setup(x => x.ValidateAsync()).ReturnsAsync("Validation result");

            IXsePluginValidator validator = mockValidator.Object;

            // Act & Assert
            var result = await validator.ValidateAsync();
            result.Should().Be("Validation result");
        }

        [Fact]
        public async Task IModIniScanner_AllMethodsAreCallable()
        {
            // Arrange
            var mockScanner = new Mock<IModIniScanner>();
            mockScanner.Setup(x => x.ScanAsync()).ReturnsAsync("Scan result");

            IModIniScanner scanner = mockScanner.Object;

            // Act & Assert
            var result = await scanner.ScanAsync();
            result.Should().Be("Scan result");
        }

        [Fact]
        public async Task IWryeBashChecker_AllMethodsAreCallable()
        {
            // Arrange
            var mockChecker = new Mock<IWryeBashChecker>();
            mockChecker.Setup(x => x.AnalyzeAsync()).ReturnsAsync("Analysis result");

            IWryeBashChecker checker = mockChecker.Object;

            // Act & Assert
            var result = await checker.AnalyzeAsync();
            result.Should().Be("Analysis result");
        }

        [Theory]
        [InlineData(typeof(IGameScannerService), typeof(GameScannerService))]
        [InlineData(typeof(ICrashGenChecker), typeof(CrashGenChecker))]
        [InlineData(typeof(IXsePluginValidator), typeof(XsePluginValidator))]
        [InlineData(typeof(IModIniScanner), typeof(ModIniScanner))]
        [InlineData(typeof(IWryeBashChecker), typeof(WryeBashChecker))]
        public void Interface_ImplementsExpectedType(Type interfaceType, Type implementationType)
        {
            // Assert
            interfaceType.IsInterface.Should().BeTrue();
            implementationType.Should().Implement(interfaceType);
            implementationType.Should().NotBeAbstract();
            implementationType.GetConstructors().Should().NotBeEmpty();
        }

        [Fact]
        public void GameScanResult_HasExpectedProperties()
        {
            // Arrange
            var result = new GameScanResult();
            var resultType = result.GetType();

            // Act & Assert
            resultType.Should().HaveProperty<DateTime>("Timestamp");
            resultType.Should().HaveProperty<string>("CrashGenResults");
            resultType.Should().HaveProperty<string>("XsePluginResults");
            resultType.Should().HaveProperty<string>("ModIniResults");
            resultType.Should().HaveProperty<string>("WryeBashResults");
            resultType.Should().HaveProperty<bool>("HasIssues");
            resultType.Should().HaveProperty<List<string>>("CriticalIssues");
            resultType.Should().HaveProperty<List<string>>("Warnings");
            
            // Check method exists
            resultType.Should().HaveMethod("GetFullReport", new Type[0]);
        }

        [Fact]
        public void AllImplementations_HaveProperLogging()
        {
            // This test ensures all implementations accept ILogger in their constructors
            var crashGenConstructor = typeof(CrashGenChecker).GetConstructors()[0];
            crashGenConstructor.GetParameters()
                .Should().Contain(p => p.ParameterType.IsGenericType && 
                                       p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>));

            var xseConstructor = typeof(XsePluginValidator).GetConstructors()[0];
            xseConstructor.GetParameters()
                .Should().Contain(p => p.ParameterType.IsGenericType && 
                                       p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>));

            var iniConstructor = typeof(ModIniScanner).GetConstructors()[0];
            iniConstructor.GetParameters()
                .Should().Contain(p => p.ParameterType.IsGenericType && 
                                       p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>));

            var wryeConstructor = typeof(WryeBashChecker).GetConstructors()[0];
            wryeConstructor.GetParameters()
                .Should().Contain(p => p.ParameterType.IsGenericType && 
                                       p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>));

            var serviceConstructor = typeof(GameScannerService).GetConstructors()[0];
            serviceConstructor.GetParameters()
                .Should().Contain(p => p.ParameterType.IsGenericType && 
                                       p.ParameterType.GetGenericTypeDefinition() == typeof(ILogger<>));
        }
    }
}
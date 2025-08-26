# Scanner111 Development Guidelines

This document provides essential development information for the Scanner111 project - a multi-platform application built with .NET 9.0 and Avalonia UI for YAML configuration management and game data scanning.

## Project Architecture

Scanner111 is organized as a multi-project solution with clear separation of concerns:

- **Scanner111.Core**: Core business logic, YAML processing, file I/O, and dependency injection services
- **Scanner111**: Main Avalonia UI library with ViewModels, Views, and shared UI components
- **Scanner111.Desktop**: Windows desktop executable entry point using Avalonia.Desktop
- **Scanner111.CLI**: Command-line interface using CommandLineParser and Spectre.Console
- **Scanner111.Test**: Comprehensive test suite using xUnit, FluentAssertions, and Moq

## Build and Configuration

### Prerequisites
- .NET 9.0 SDK
- Visual Studio 2022 17.14+ or JetBrains Rider

### Key Build Configuration

The project uses modern .NET 9.0 features with strict quality enforcement:

- **Nullable Reference Types**: Enabled project-wide via `Directory.Build.props`
- **Implicit Usings**: Enabled for cleaner code
- **Avalonia Framework**: Version 11.3.4 with Fluent theme, ReactiveUI, and compiled bindings
- **Warning as Errors**: Async safety violations (CS1998) treated as errors
- **Language Version**: Latest C# features enabled

### Building the Solution

```powershell
# Build entire solution
dotnet build Scanner111.sln

# Build specific projects
dotnet build Scanner111.Core\Scanner111.Core.csproj
dotnet build Scanner111.Desktop\Scanner111.Desktop.csproj
dotnet build Scanner111.CLI\Scanner111.CLI.csproj

# Release build
dotnet build Scanner111.sln -c Release
```

### Running Applications

```powershell
# Desktop Application
dotnet run --project Scanner111.Desktop

# CLI Application  
dotnet run --project Scanner111.CLI
```

## Testing Framework

### Test Infrastructure

The project uses a professional testing setup:
- **xUnit**: Primary test framework with global usings enabled
- **FluentAssertions**: Readable and expressive assertions
- **Moq**: Mocking framework for dependencies
- **coverlet.collector**: Code coverage collection
- **IAsyncLifetime**: For async test setup/teardown

### Running Tests

```powershell
# Run all tests
dotnet test Scanner111.Test.csproj

# Run with verbosity
dotnet test Scanner111.Test.csproj -v normal

# Run specific test class
dotnet test --filter "FullyQualifiedName~AsyncYamlSettingsCoreTests"

# Run specific test method
dotnet test --filter "FullyQualifiedName~GetPathForStoreAsync_ShouldReturnCorrectPaths"

# Generate code coverage
dotnet test Scanner111.Test.csproj --collect:"XPlat Code Coverage"
```

### Writing Tests

Follow the established patterns demonstrated in `AsyncYamlSettingsCoreTests.cs`:

```csharp
public class YourTestClass : IAsyncLifetime
{
    private readonly Mock<IDependency> _mockDependency;
    private YourService _sut = null!; // System Under Test

    public YourTestClass()
    {
        _mockDependency = new Mock<IDependency>();
    }

    public async Task InitializeAsync()
    {
        // Setup test environment
        _sut = new YourService(_mockDependency.Object);
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        // Cleanup resources
        await _sut.DisposeAsync();
    }

    [Fact]
    public async Task Method_Scenario_ExpectedResult()
    {
        // Arrange
        _mockDependency.Setup(x => x.DoSomething()).ReturnsAsync("result");

        // Act
        var result = await _sut.MethodUnderTest();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Be("expected");
        _mockDependency.Verify(x => x.DoSomething(), Times.Once);
    }
}
```

### Test Organization

- Place tests in `Scanner111.Test` project
- Mirror the namespace structure of the code being tested
- Use descriptive test names: `Method_Scenario_ExpectedResult`
- Group related tests in the same class
- Use `Theory` with `InlineData` for parameterized tests

## Code Style and Development Standards

### Dependency Injection

The project follows Microsoft.Extensions.DependencyInjection patterns:

```csharp
// Service registration (see ServiceCollectionExtensions.cs)
services.AddYamlSettings(options =>
{
    options.CacheTtl = TimeSpan.FromMinutes(30);
    options.EnableMetrics = true;
});

// Custom implementations
services.AddYamlSettingsWithCustomIo(
    provider => new CustomFileIoCore(),
    options => options.DefaultGame = "Skyrim"
);
```

### YAML Configuration

The core functionality revolves around YAML processing with:
- **YamlDotNet 16.3.0**: Primary YAML serialization
- **Async/Await patterns**: All I/O operations are async
- **Caching strategy**: Intelligent caching for performance
- **Store-based organization**: Different YAML stores (Main, Settings, Game, etc.)

### Error Handling

- Use proper exception handling with meaningful error messages
- Leverage nullable reference types to prevent null reference exceptions  
- Follow async best practices to avoid deadlocks
- Static stores (like Main database) are read-only - modifications will throw `InvalidOperationException`

### Performance Considerations

- File I/O operations are cached with configurable TTL
- Static configuration files are cached permanently
- Metrics collection is available when enabled
- Concurrent loading of multiple stores is supported

## Development Workflow

### Adding New Features

1. **Core Logic**: Add business logic to `Scanner111.Core`
2. **UI Components**: Add views/viewmodels to main `Scanner111` project
3. **CLI Commands**: Extend `Scanner111.CLI` for command-line functionality
4. **Tests**: Add comprehensive tests to `Scanner111.Test`
5. **Dependency Registration**: Update `ServiceCollectionExtensions.cs` if needed

### Debugging

- Avalonia.Diagnostics is available in Debug builds only
- Use structured logging with Microsoft.Extensions.Logging
- Enable metrics collection for performance monitoring
- Test with both mocked and real file I/O implementations

### Key Dependencies

**Core Libraries:**
- YamlDotNet 16.3.0 - YAML processing
- Microsoft.Extensions.* 9.0.8 - DI, Logging, Options
- System.Text.Encoding.CodePages 9.0.8 - Text encoding support

**UI Libraries:**
- Avalonia 11.3.4 - Cross-platform UI framework
- Avalonia.ReactiveUI - MVVM support
- System.Data.SQLite 2.0.1 - Database support

**Testing Libraries:**
- xUnit 2.9.3 - Test framework
- FluentAssertions 8.6.0 - Assertion library
- Moq 4.20.72 - Mocking framework

## Project-Specific Notes

- The application appears designed for game mod/data management (references to Fallout 4, Skyrim)
- Configuration files follow "CLASSIC" naming convention (To be removed in the near future)
- COM interop is enabled for Windows-specific functionality
- The project supports both desktop GUI and command-line interfaces
- Test data suggests integration with game registry and crash log analysis

Generated: 2025-08-26 02:16
# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Scanner111 is a modern C# port of a legacy crash log analysis application (originally named "CLASSIC"). It analyzes game crash logs, identifies problematic plugins, and provides detailed reports. The project consists of a GUI application (Avalonia), a CLI application, and a shared business logic library.

## Project Structure

```
Scanner111/                          # Solution root
├── Code to Port/                    # READ-ONLY: Original Python source code
├── sample_logs/                     # READ-ONLY: Sample crash logs for testing
├── sample_output/                   # READ-ONLY: Expected output for validation
├── Scanner111/                      # Avalonia UI library (shared XAML/views)
├── Scanner111.Desktop/              # Desktop executable (entry point)
├── Scanner111.CLI/                  # CLI executable
├── Scanner111.Common/               # Shared business logic library
├── Scanner111.Tests/                # Tests for Scanner111 (UI)
├── Scanner111.Desktop.Tests/        # Tests for Scanner111.Desktop
├── Scanner111.CLI.Tests/            # Tests for Scanner111.CLI
└── Scanner111.Common.Tests/         # Tests for Scanner111.Common
```

### Project Responsibilities

- **Scanner111**: Avalonia UI components, views, view models, and shared UI logic
- **Scanner111.Desktop**: Desktop application entry point and platform-specific code
- **Scanner111.CLI**: Command-line interface for batch processing and automation
- **Scanner111.Common**: ALL business logic (analysis, parsing, reporting, data access)
- **Test Projects**: One xUnit test project per main project

## Technology Stack

- **.NET 9.0**: All projects target `net9.0`
- **Avalonia 11.3.7**: Cross-platform UI framework
- **xUnit**: Testing framework
- **Nullable Reference Types**: Enabled across all projects

## Common Development Commands

### Building and Running

```bash
# Build entire solution
dotnet build

# Run tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run CLI application
dotnet run --project Scanner111.CLI

# Run Desktop application
dotnet run --project Scanner111.Desktop

# Build specific project
dotnet build Scanner111.Common

# Clean solution
dotnet clean
```

### Testing

```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test Scanner111.Common.Tests

# Run specific test class
dotnet test --filter "FullyQualifiedName~NamespaceOrClassName"

# Run specific test method
dotnet test --filter "FullyQualifiedName~MethodName"

# Run tests with coverage (if configured)
dotnet test --collect:"XPlat Code Coverage"
```

## Development Guidelines

### Code Organization

1. **Business Logic Placement**
   - ALL business logic MUST go in `Scanner111.Common`
   - UI projects (Scanner111, Scanner111.Desktop) only contain UI-specific code
   - CLI project only contains command-line interface logic
   - No business logic duplication between projects

2. **Separation of Concerns**
   - Analysis logic: `Scanner111.Common`
   - Data access: `Scanner111.Common`
   - UI presentation: `Scanner111` (Avalonia views/viewmodels)
   - Entry points: `Scanner111.Desktop` and `Scanner111.CLI`

3. **File Naming Conventions**
   - One class per file
   - File name matches class name
   - Use PascalCase for all file names
   - Organize by feature/domain within each project

### Porting from Legacy Code

1. **Reference Materials** (READ-ONLY, never modify):
   - `Code to Port/`: Original Python implementation
   - `sample_logs/`: Test crash logs
   - `sample_output/`: Expected analysis results

2. **Naming Conventions**:
   - Replace "CLASSIC" with "Scanner111" in all code
   - Use modern C# naming conventions (PascalCase, camelCase)
   - Avoid Python-specific patterns (use C# idioms)

3. **Modernization Priorities**:
   - Use async/await for I/O operations
   - Leverage LINQ for data transformations
   - Use dependency injection where appropriate
   - Apply modern C# patterns (records, pattern matching, etc.)

### Code Quality Standards

1. **Null Safety**
   - Nullable reference types enabled
   - Use null-forgiving operator (`!`) sparingly
   - Prefer null-conditional operators (`?.`, `??`)

2. **Async Patterns**
   - Use `async`/`await` for all I/O operations
   - Suffix async methods with `Async`
   - Use `ConfigureAwait(false)` in library code
   - Pass `CancellationToken` for long-running operations

3. **Testing Requirements**
   - Write tests for all business logic in `Scanner111.Common`
   - Use xUnit for all test projects
   - Follow Arrange-Act-Assert pattern
   - Test edge cases and error conditions

4. **Documentation**
   - Use XML documentation comments for public APIs
   - Document complex algorithms and business rules
   - Keep comments up-to-date with code changes

### Solution Configuration

- **Directory.Build.props**: Shared properties (AvaloniaVersion, Nullable settings)
- **ImplicitUsings**: Enabled for cleaner code
- **LangVersion**: Latest C# features available

## Architecture Principles

### Layered Architecture

```
┌─────────────────────────────────────┐
│  Scanner111.Desktop / Scanner111.CLI │  ← Entry Points
├─────────────────────────────────────┤
│         Scanner111 (UI)              │  ← Presentation Layer
├─────────────────────────────────────┤
│      Scanner111.Common               │  ← Business Logic Layer
└─────────────────────────────────────┘
```

### Dependency Flow

- Desktop/CLI → Scanner111 (UI) → Scanner111.Common
- Test projects reference their corresponding main projects
- No circular dependencies
- Common library has no UI dependencies

### Key Patterns

1. **Dependency Injection**: Use constructor injection for dependencies
2. **Repository Pattern**: For data access in Scanner111.Common
3. **MVVM**: For Avalonia UI (ViewModels in Scanner111)
4. **Strategy Pattern**: For different analysis algorithms
5. **Factory Pattern**: For creating analyzers and processors

## Common Pitfalls to Avoid

1. **❌ Don't modify read-only directories**
   - Never change files in `Code to Port/`
   - Never modify `sample_logs/` or `sample_output/`

2. **❌ Don't put business logic in UI projects**
   - Keep Scanner111 and Scanner111.Desktop UI-focused
   - Move all analysis, parsing, and data access to Scanner111.Common

3. **❌ Don't replicate Python patterns**
   - Avoid direct Python-to-C# translation
   - Use C# language features and idioms

4. **❌ Don't ignore nullable warnings**
   - Address null reference warnings immediately
   - Don't disable nullable context

5. **❌ Don't create unnecessary abstractions**
   - Keep it simple until complexity is needed
   - Avoid over-engineering

## Testing Strategy

### Test Organization

- **Unit Tests**: Test individual classes in isolation
- **Integration Tests**: Test component interactions
- **UI Tests**: Use Avalonia.Headless for UI testing (if needed)

### Test Naming Convention

```csharp
[Fact]
public void MethodName_Scenario_ExpectedBehavior()
{
    // Arrange

    // Act

    // Assert
}
```

### Test Coverage Goals

- 80%+ coverage for Scanner111.Common
- Critical path coverage for UI and CLI
- All edge cases and error conditions

## File System Conventions

### Project-Specific Folders

```
Scanner111.Common/
├── Analysis/          # Crash analysis logic
├── Models/            # Data models and DTOs
├── Services/          # Business services
├── Data/              # Data access
└── Utilities/         # Helper classes
```

### Configuration Files

- **Directory.Build.props**: Solution-wide MSBuild properties
- **.editorconfig**: Code style rules (if present)
- **global.json**: SDK version pinning (if present)

## Version Control

### Current Branch

- Main development branch: `main`
- Feature branches: `feature/description`
- Bug fix branches: `fix/description`

### Commit Guidelines

- Use clear, descriptive commit messages
- Reference issue numbers if applicable
- Keep commits focused and atomic

## Resources and References

### Official Documentation

- **Avalonia Documentation**: https://docs.avaloniaui.net/
- **.NET 9 Documentation**: https://learn.microsoft.com/dotnet/
- **xUnit Documentation**: https://xunit.net/
- **Microsoft Learn**: https://learn.microsoft.com (searchable via MCP server)

### Using Microsoft Learn MCP Server

Claude Code has access to the `microsoft_learn` MCP server for searching official Microsoft documentation:

- **microsoft_docs_search**: Search Microsoft Learn for .NET, C#, Azure, and other Microsoft technologies
- **microsoft_code_sample_search**: Find official code examples and snippets
- **microsoft_docs_fetch**: Retrieve complete documentation pages

Use these tools to get up-to-date information about:
- .NET 9.0 features and APIs
- C# language features
- Best practices and architectural guidance

## Quick Reference

### Add New Analyzer

1. Create analyzer class in `Scanner111.Common/Analysis/`
2. Implement business logic with tests
3. Wire up in appropriate service/orchestrator
4. Add UI integration in Scanner111 if needed

### Add New Model

1. Create model in `Scanner111.Common/Models/`
2. Add validation if needed
3. Update related services
4. Add serialization tests

### Add New UI Feature

1. Design in `Scanner111/Views/`
2. Create ViewModel in `Scanner111/ViewModels/`
3. Implement business logic in `Scanner111.Common`
4. Wire up in Desktop/CLI as needed

---

**Remember**: Scanner111.Common is the heart of the application. Keep UI concerns separate, and always validate against the original sample data when porting features.

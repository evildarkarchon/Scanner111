# Scanner111 Development Guidelines

This document captures the key steps and tips for building, testing, and contributing to Scanner111. It is tailored for Windows/PowerShell and .NET 8.

## Prerequisites
- .NET SDK 8.0 (or newer compatible with solution)
- PowerShell (built-in on Windows)
- Optional: Rider/Visual Studio 2022 for IDE support

Verify your environment:
- dotnet --info

## Project Structure (high-level)
- Scanner111.sln — Solution file
- Scanner111.Core — Core libraries and services
- Scanner111.GUI — Avalonia UI (net8.0)
- Scanner111.CLI — Command-line interface
- Scanner111.Tests — xUnit-based test suite (references Core/GUI/CLI)
- Code to Port — Legacy Python and assets (not required for building .NET projects)

## Build and Configuration
Typical commands from the repository root (PowerShell):
- Restore packages
  - dotnet restore .\Scanner111.sln
- Build Debug
  - dotnet build .\Scanner111.sln -c Debug
- Build Release
  - dotnet build .\Scanner111.sln -c Release

Build a specific project (examples):
- CLI
  - dotnet build .\Scanner111.CLI\Scanner111.CLI.csproj -c Debug
- GUI (Avalonia)
  - dotnet build .\Scanner111.GUI\Scanner111.GUI.csproj -c Debug

Run the CLI locally:
- dotnet run --project .\Scanner111.CLI\Scanner111.CLI.csproj -- -h

Notes:
- All paths use Windows backslashes.
- If you change public APIs in Core/GUI/CLI, re-run dotnet restore if the project references or packages were updated.

## Testing
The solution uses xUnit with FluentAssertions and coverlet integration via Microsoft.NET.Test.Sdk and coverlet.collector.

General test commands from repository root:
- Run all tests
  - dotnet test .\Scanner111.Tests\Scanner111.Tests.csproj -c Debug
- Run tests with detailed logging
  - dotnet test .\Scanner111.Tests\Scanner111.Tests.csproj -c Debug -v normal
- Filter tests (by fully-qualified name, class, or trait)
  - dotnet test .\Scanner111.Tests\Scanner111.Tests.csproj --filter "FullyQualifiedName~Scanner111.Tests.GUI.Converters.BooleanToFindingsTextConverterTests"
  - dotnet test .\Scanner111.Tests\Scanner111.Tests.csproj --filter "ClassName=Scanner111.Tests.GUI.Converters.BooleanToFindingsTextConverterTests"
  - dotnet test .\Scanner111.Tests\Scanner111.Tests.csproj --filter "TestCategory=Fast"  (only if traits are present)

Collect coverage (via coverlet collector):
- dotnet test .\Scanner111.Tests\Scanner111.Tests.csproj -c Debug --collect:"XPlat Code Coverage"
  - Results will be stored under TestResults/ and may be viewable with coverage tools.

Watch mode (reruns on file changes):
- dotnet watch --project .\Scanner111.Tests\Scanner111.Tests.csproj test

Important: Some tests are known to be slow or hang (e.g., deep integration or IO-heavy tests). Prefer filtering down to a known-fast class or method when iterating locally.

### Quick, Verified Test Example
To quickly verify your setup, run a fast, stable test from the Converters suite:
- dotnet test .\Scanner111.Tests\Scanner111.Tests.csproj --filter "FullyQualifiedName~Scanner111.Tests.GUI.Converters.BooleanToFindingsTextConverterTests.Convert_ValidBoolean_ReturnsCorrectText"

This specific test has been executed successfully during guideline authoring to ensure the command works as shown.

### How to Add and Run a New Test (Step-by-step)
Below is a minimal example you can add temporarily to prove your test pipeline. You can add and then remove it after validation.

1) Create a new file under the test project, for example:
   - Path: .\Scanner111.Tests\Smoke\HelloWorldTests.cs

2) File contents:
   using FluentAssertions;
   using Xunit;
   
   namespace Scanner111.Tests.Smoke;
   
   public class HelloWorldTests
   {
       [Fact]
       public void True_is_true()
       {
           true.Should().BeTrue();
       }
   }

3) Run just this test:
- dotnet test .\Scanner111.Tests\Scanner111.Tests.csproj --filter "FullyQualifiedName~Scanner111.Tests.Smoke.HelloWorldTests.True_is_true"

4) Remove the temporary test file once done to keep the repository clean.

## Code Style and Conventions
- C# Language Version: from .NET 8 defaults + ImplicitUsings enabled (except in Scanner111.GUI), Nullable enabled in csproj
- Testing: xUnit with FluentAssertions
  - Arrange/Act/Assert structure is widely used.
  - Prefer expressive assertions (result.Should().Be(...))
- DI & Services: Microsoft.Extensions.DependencyInjection is used in tests; follow existing patterns in Core/CLI/GUI.
- Namespaces: Follow folder-based namespaces under Scanner111.*
- Async/Concurrency: Where applicable, prefer async/await and CancellationToken support; see existing services and tests for patterns (e.g., CancellationSupportTests).

## Troubleshooting
- Restores fail
  - Ensure internet connectivity and correct NuGet sources; re-run: dotnet nuget locals all --clear
- Binding errors or missing types
  - Clean and rebuild: dotnet clean .\Scanner111.sln; dotnet build .\Scanner111.sln -c Debug
- Slow/hanging tests
  - Use --filter to target only the fast unit tests (e.g., Converters tests).
  - Avoid full-solution runs when iterating quickly.
- Coverage report empty
  - Check that --collect:"XPlat Code Coverage" is passed and that TestResults directory is created.

## Notes for AI/Tooling
- Prefer Windows-style paths in commands.
- Use project-specific filters when running tests to avoid long integration suites.
- Do not commit temporary files created for testing demonstrations; remove them after verifying.


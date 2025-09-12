# Scanner111 Project Overview

## Purpose
Scanner111 is a modern C# port of a legacy crash log analysis application designed to analyze game crash logs, identify problematic plugins, and provide detailed reports. It's particularly focused on analyzing crashes from modded games.

## Tech Stack
- **Language**: C# 11+ with nullable reference types enabled
- **Framework**: .NET 8.0
- **UI Frameworks**: 
  - Avalonia 11.3.4 (Desktop application)
  - Spectre.Console (CLI application)
- **Testing**: xUnit, NSubstitute, FluentAssertions, Avalonia.Headless
- **Configuration**: YamlDotNet for YAML-based configuration
- **DI Container**: Microsoft.Extensions.DependencyInjection

## Project Structure
- **Scanner111.Core**: Core business logic, analyzers, services (all non-UI code)
- **Scanner111.CLI**: Command-line interface with interactive mode
- **Scanner111.Desktop**: Avalonia-based desktop application
- **Scanner111.Test**: Core unit and integration tests
- **Scanner111.CLI.Test**: CLI-specific tests
- **Scanner111**: Legacy/shared components

## Key Features
- Multi-analyzer orchestration with configurable execution strategies
- Thread-safe async operations throughout
- Comprehensive crash log analysis with multiple specialized analyzers
- YAML-based configuration system
- Hierarchical report generation
- FormID database lookups for plugin identification
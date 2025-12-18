## ADDED Requirements

### Requirement: Logging Infrastructure

The system SHALL provide structured logging using `Microsoft.Extensions.Logging` abstractions with Serilog as the provider.

#### Scenario: Logging configured at application startup
- **GIVEN** the application is starting
- **WHEN** the DI container is configured
- **THEN** logging services SHALL be registered
- **AND** file logging SHALL be configured with daily rotation
- **AND** log files SHALL be stored in `%LOCALAPPDATA%\Scanner111\logs\`

#### Scenario: Services receive logger instances
- **GIVEN** a service requires logging
- **WHEN** the service is resolved from DI
- **THEN** an `ILogger<T>` instance SHALL be injected

### Requirement: Orchestration Logging

Orchestration services SHALL log operation lifecycle events and errors.

#### Scenario: ScanGameOrchestrator logs scan lifecycle
- **GIVEN** a game scan operation
- **WHEN** the scan starts
- **THEN** the orchestrator SHALL log the start with scanner configuration
- **WHEN** each scanner completes
- **THEN** the orchestrator SHALL log the scanner name, result status, and duration
- **WHEN** a scanner fails
- **THEN** the orchestrator SHALL log the error with full exception details at Error level

#### Scenario: LogOrchestrator logs analysis pipeline
- **GIVEN** a crash log analysis
- **WHEN** the analysis starts
- **THEN** the orchestrator SHALL log the log file path
- **WHEN** parsing completes
- **THEN** the orchestrator SHALL log parse result (valid/invalid, segment count)
- **WHEN** analysis completes
- **THEN** the orchestrator SHALL log findings summary (plugin count, suspect count)

### Requirement: Error Logging

Services with exception handlers SHALL log caught exceptions before handling them.

#### Scenario: Silent exception handlers log errors
- **GIVEN** a service catches an exception silently
- **WHEN** the exception is caught
- **THEN** the service SHALL log the exception at Warning or Error level
- **AND** the log entry SHALL include exception type, message, and stack trace

#### Scenario: Database errors are logged
- **GIVEN** a SQLite operation in `FormIdAnalyzer`
- **WHEN** a `SqliteException` or `InvalidOperationException` occurs
- **THEN** the exception SHALL be logged at Warning level before returning default result

#### Scenario: File system errors are logged
- **GIVEN** a file operation in `XseChecker`, `GamePathDetector`, or `DocsPathDetector`
- **WHEN** `IOException` or `UnauthorizedAccessException` occurs
- **THEN** the exception SHALL be logged at Warning level with the path that failed

### Requirement: Log Export

Users SHALL be able to export application logs for bug reports.

#### Scenario: User exports debug log
- **GIVEN** the application has been running
- **WHEN** the user clicks "Export Debug Log" or equivalent
- **THEN** the system SHALL copy current log files to a user-selected location
- **OR** open the log folder in Windows Explorer

#### Scenario: Log file location is discoverable
- **GIVEN** a user needs to find log files
- **WHEN** the user views Settings or About
- **THEN** the log file path SHALL be displayed

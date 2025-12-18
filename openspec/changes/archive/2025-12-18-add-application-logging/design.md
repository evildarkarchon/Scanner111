# Design: Application Logging Infrastructure

## Context

Scanner111 is a desktop application that analyzes crash logs. Currently, there is no application logging - exceptions are silently caught or converted to user-facing errors. This makes debugging difficult and prevents users from providing useful diagnostic information.

**Stakeholders:**
- End users (need exportable logs for bug reports)
- Developers (need diagnostic information for debugging)

**Constraints:**
- Must not significantly impact scan performance
- Must work with existing DI infrastructure (`Microsoft.Extensions.DependencyInjection`)
- Log files must be accessible to users for bug reports
- Must follow existing async patterns (`ConfigureAwait(false)`)

## Goals / Non-Goals

**Goals:**
- Provide structured logging throughout the application
- Enable file-based logging for diagnostics
- Allow users to export logs for bug reports
- Instrument services with silent exception handlers

**Non-Goals:**
- Real-time log viewer in GUI (future enhancement)
- Log aggregation/remote logging
- Performance profiling (separate concern)
- Replacing existing error handling patterns

## Decisions

### Decision 1: Use `Microsoft.Extensions.Logging` as abstraction

**Rationale:** Already using `Microsoft.Extensions.DependencyInjection`, so the logging abstractions integrate seamlessly. The `ILogger<T>` pattern provides typed loggers that are easy to inject and test.

**Alternatives considered:**
- **Serilog directly**: More features but couples code to Serilog API
- **NLog**: Similar capability but less DI integration
- **Custom logger**: More work, no benefit

### Decision 2: Use Serilog as the logging provider

**Rationale:** Serilog provides excellent file sinks with automatic rotation, structured logging, and easy configuration. The `Serilog.Extensions.Logging` package bridges to `Microsoft.Extensions.Logging`.

**Configuration:**
```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        path: Path.Combine(AppDataPath, "logs", "scanner111-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();
```

### Decision 3: Log file location

Store logs in `%LOCALAPPDATA%\Scanner111\logs\`:
- Standard Windows application data location
- User-accessible for bug reports
- Separate from game data/settings

### Decision 4: Logging levels strategy

| Level | Use Case |
|-------|----------|
| `Error` | Exceptions, operation failures |
| `Warning` | Recoverable issues, degraded operation |
| `Information` | Major milestones (scan start/end, reports generated) |
| `Debug` | Detailed operation flow (for troubleshooting) |

Default level: `Information` in Release, `Debug` in Debug builds.

### Decision 5: Inject loggers via constructor

Use constructor injection with `ILogger<T>`:

```csharp
public class ScanGameOrchestrator : IScanGameOrchestrator
{
    private readonly ILogger<ScanGameOrchestrator> _logger;

    public ScanGameOrchestrator(
        ILogger<ScanGameOrchestrator> logger,
        // ... other dependencies
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

## Risks / Trade-offs

| Risk | Mitigation |
|------|------------|
| Performance impact from logging | Use appropriate log levels; benchmark critical paths |
| Log files consuming disk space | Automatic rotation with 7-day retention |
| Sensitive data in logs | Never log file contents, only paths and metadata |
| Breaking existing tests | Loggers are nullable/mockable in tests |

## Migration Plan

1. **Phase 1**: Add infrastructure (packages, DI configuration)
2. **Phase 2**: Instrument orchestration services (highest value)
3. **Phase 3**: Instrument remaining services with exception handlers
4. **Phase 4**: Add user-facing export feature

Rollback: Remove Serilog configuration; `ILogger<T>` injections become no-ops with `NullLogger<T>`.

## Open Questions

1. Should we add a minimum log level setting in the UI? (Deferred - start with fixed levels)
2. Should logs include structured properties for searching? (Yes, but simple format for now)

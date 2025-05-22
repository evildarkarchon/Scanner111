using Microsoft.Extensions.DependencyInjection;

namespace Scanner111.ClassicLib.ScanLog.Services;

/// <summary>
/// Extension methods for registering CrashLog scan services with the DI container.
/// </summary>
public static class CrashLogServiceExtensions
{
    /// <summary>
    /// Adds all crash log scanning services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The modified service collection.</returns>
    public static IServiceCollection AddCrashLogScanServices(this IServiceCollection services)
    {
        // Register services with appropriate lifetimes
        services.AddSingleton<ICrashLogFileService, CrashLogFileService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IModDetectionService, ModDetectionService>();
        services.AddSingleton<IReportWriterService, ReportWriterService>();

        // Register the main scan service with its dependencies
        services.AddSingleton<ICrashLogScanService, CrashLogScanService>();

        return services;
    }
}

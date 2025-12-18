using Microsoft.Extensions.DependencyInjection;
using Scanner111.Common.Services.Analysis;
using Scanner111.Common.Services.Configuration;
using Scanner111.Common.Services.Database;
using Scanner111.Common.Services.DocsPath;
using Scanner111.Common.Services.FileIO;
using Scanner111.Common.Services.GameIntegrity;
using Scanner111.Common.Services.Logging;
using Scanner111.Common.Services.Orchestration;
using Scanner111.Common.Services.Papyrus;
using Scanner111.Common.Services.Parsing;
using Scanner111.Common.Services.Pastebin;
using Scanner111.Common.Services.PathValidation;
using Scanner111.Common.Services.Reporting;
using Scanner111.Common.Services.ScanGame;
using Scanner111.Common.Services.Settings;
using System.Net.Http;

namespace Scanner111.Common.DependencyInjection;

/// <summary>
/// Extension methods for registering Scanner111.Common services with dependency injection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds all Scanner111.Common services required for crash log scanning.
    /// This method registers all business logic services that can be shared between
    /// GUI and CLI applications.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="logPathProvider">Optional custom log path provider. If null, a default one is created.</param>
    /// <param name="databasePath">Optional path to the FormID database. If null, database lookups are disabled.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddScanner111CommonServices(
        this IServiceCollection services,
        ILogPathProvider? logPathProvider = null,
        string? databasePath = null)
    {
        // Log path provider (used for application logs, not crash logs)
        if (logPathProvider is not null)
        {
            services.AddSingleton(logPathProvider);
        }
        else
        {
            services.AddSingleton<ILogPathProvider, LogPathProvider>();
        }

        // Core file I/O and parsing services
        services.AddSingleton<IFileIOService, FileIOService>();
        services.AddSingleton<ILogParser, LogParser>();

        // Analysis services
        services.AddSingleton<IPluginAnalyzer, PluginAnalyzer>();
        services.AddSingleton<ISuspectScanner, SuspectScanner>();
        services.AddSingleton<ISettingsScanner, SettingsScanner>();
        services.AddSingleton<IFormIdAnalyzer, FormIdAnalyzer>();

        // Configuration services
        services.AddSingleton<IYamlConfigLoader, YamlConfigLoader>();
        services.AddSingleton<IConfigurationCache, ConfigurationCache>();

        // Reporting services
        services.AddSingleton<IReportWriter, ReportWriter>();

        // Path validation and user settings services
        services.AddSingleton<IUserSettingsService, UserSettingsService>();
        services.AddSingleton<IPathValidator, PathValidator>();

        // Database (FormID lookups)
        services.AddSingleton<IDatabaseConnectionFactory>(provider =>
            new SqliteDatabaseConnectionFactory(databasePath ?? "path_to_your_db.sqlite"));

        // ScanGame services
        services.AddSingleton<IIniValidator, IniValidator>();
        services.AddSingleton<IDocsPathDetector, DocsPathDetector>();
        services.AddSingleton<IUnpackedModsScanner, UnpackedModsScanner>();
        services.AddSingleton<IBA2Scanner, BA2Scanner>();
        services.AddSingleton<IDDSAnalyzer, DDSAnalyzer>();
        services.AddSingleton<ITomlValidator, TomlValidator>();
        services.AddSingleton<IXseChecker, XseChecker>();
        services.AddSingleton<IScanGameReportBuilder, ScanGameReportBuilder>();

        // GameIntegrity services
        services.AddSingleton<IGameIntegrityChecker, GameIntegrityChecker>();

        // Papyrus monitoring services
        services.AddSingleton<IPapyrusLogReader, PapyrusLogReader>();
        services.AddTransient<IPapyrusMonitorService, PapyrusMonitorService>();

        // HTTP client for external services (Pastebin, etc.)
        services.AddSingleton<HttpClient>(_ =>
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.Add("User-Agent", "Scanner111/1.0");
            return client;
        });
        services.AddSingleton<IPastebinService, PastebinService>();

        // Orchestration services (transient - one instance per scan operation)
        services.AddTransient<ILogOrchestrator, LogOrchestrator>();
        services.AddTransient<IScanGameOrchestrator, ScanGameOrchestrator>();

        // Factory delegates for orchestrators (used by ScanExecutor)
        services.AddSingleton<Func<ILogOrchestrator>>(sp => () => sp.GetRequiredService<ILogOrchestrator>());
        services.AddSingleton<Func<IScanGameOrchestrator>>(sp => () => sp.GetRequiredService<IScanGameOrchestrator>());

        // Scan executor (main entry point for batch scanning)
        services.AddSingleton<IScanExecutor, ScanExecutor>();

        return services;
    }
}

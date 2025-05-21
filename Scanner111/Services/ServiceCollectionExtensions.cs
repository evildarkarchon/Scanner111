using Microsoft.Extensions.DependencyInjection;
using Scanner111.Models; // If you need to register models or options
using Scanner111.Services;
using Scanner111.ViewModels; // If you have service interfaces and implementations

namespace Scanner111.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {            // Register the warning database and settings first
            services.AddSingleton<WarningDatabase>();
            services.AddSingleton<AppSettings>();
            services.AddSingleton<IYamlSettingsCacheService, YamlSettingsCacheServiceAdapter>();

            // Register our scanner services
            services.AddSingleton<CrashLogParserService>();
            services.AddSingleton<PluginDetectionService>();
            services.AddSingleton<CrashStackAnalysis>();
            services.AddSingleton<FormIdDatabaseService>();
            services.AddSingleton<FormIdDatabaseImporter>();
            services.AddSingleton<CrashAnalysisService>();
            services.AddSingleton<CrashLogFormattingService>();
            services.AddSingleton<ModDetectionService>();
            services.AddSingleton<SpecializedSettingsCheckService>();
            services.AddSingleton<CrashReportGenerator>();
            services.AddSingleton<ScanLogService>();

            // Register Game and Mod scanning services
            services.AddSingleton<ILogErrorCheckService, LogErrorCheckService>();
            services.AddSingleton<IModScanningService, ModScanningService>(); services.AddSingleton<IGameFileManagementService, GameFileManagementService>();
            services.AddSingleton<IScanGameService, ScanGameService>();            // Register specialized check services
            services.AddSingleton<ICheckCrashgenSettingsService, CheckCrashgenSettingsService>();
            services.AddSingleton<ICheckXsePluginsService, CheckXsePluginsService>();
            services.AddSingleton<IScanModInisService, ScanModInisService>();
            services.AddSingleton<IScanWryeCheckService, ScanWryeCheckService>();            // Register update checking service
            services.AddSingleton<IUpdateCheckService, UpdateCheckService>();

            // Register Papyrus log monitoring service
            services.AddSingleton<IPapyrusLogMonitoringService, PapyrusLogMonitoringService>();

            return services;
        }

        //You could add other extension methods for different groups of services
        public static IServiceCollection AddViewModelServices(this IServiceCollection services)
        {
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<FormIdDatabaseViewModel>();
            services.AddTransient<PapyrusMonitoringViewModel>();
            return services;
        }
    }
}

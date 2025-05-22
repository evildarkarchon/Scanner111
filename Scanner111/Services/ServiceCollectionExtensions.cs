using Microsoft.Extensions.DependencyInjection;
using Scanner111.Models; // If you need to register models or options
using Scanner111.Services;
using Scanner111.ViewModels; // If you have service interfaces and implementations
using System;

namespace Scanner111.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            Console.WriteLine("Registering custom services...");

            try
            {
                // Register the warning database and settings first
                services.AddSingleton<WarningDatabase>();
                services.AddSingleton<AppSettings>();
                services.AddSingleton<YamlSettingsCacheService>();
                services.AddSingleton<IYamlSettingsCacheService, YamlSettingsCacheServiceAdapter>();
                Console.WriteLine("Core services registered.");

                // Register directory detection services
                services.AddSingleton<IGameDirectoryService, GameDirectoryService>();
                Console.WriteLine("Directory services registered.");

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
                Console.WriteLine("Scanner services registered.");

                // Register Game and Mod scanning services
                services.AddSingleton<ILogErrorCheckService, LogErrorCheckService>();
                services.AddSingleton<IModScanningService, ModScanningService>();
                services.AddSingleton<IGameFileManagementService, GameFileManagementService>();
                services.AddSingleton<IScanGameService, ScanGameService>();

                // Register specialized check services
                services.AddSingleton<ICheckCrashgenSettingsService, CheckCrashgenSettingsService>();
                services.AddSingleton<ICheckXsePluginsService, CheckXsePluginsService>();
                services.AddSingleton<IScanModInisService, ScanModInisService>();
                services.AddSingleton<IScanWryeCheckService, ScanWryeCheckService>();

                // Register update checking service
                services.AddSingleton<IUpdateCheckService, UpdateCheckService>();
                Console.WriteLine("Utility services registered.");

                // Register Papyrus log monitoring service
                services.AddSingleton<IPapyrusLogMonitoringService, PapyrusLogMonitoringService>();
                Console.WriteLine("Monitoring services registered.");

                // Register view models
                services.AddViewModelServices();
                Console.WriteLine("ViewModels registered.");

                Console.WriteLine("All services registered successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering services: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw;
            }

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


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
            services.AddSingleton<YamlSettingsCacheService>();            // Register our scanner services
            services.AddSingleton<CrashLogParserService>();
            services.AddSingleton<PluginDetectionService>();
            services.AddSingleton<CrashStackAnalysis>(); services.AddSingleton<FormIdDatabaseService>();
            services.AddSingleton<FormIdDatabaseImporter>();
            services.AddSingleton<CrashAnalysisService>();
            services.AddSingleton<CrashLogFormattingService>();
            services.AddSingleton<ScanLogService>();

            return services;
        }        //You could add other extension methods for different groups of services
        public static IServiceCollection AddViewModelServices(this IServiceCollection services)
        {
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<FormIdDatabaseViewModel>();
            return services;
        }
    }
}

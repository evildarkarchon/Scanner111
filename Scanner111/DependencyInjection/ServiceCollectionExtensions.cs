using Microsoft.Extensions.DependencyInjection;

namespace Scanner111.DependencyInjection
{
    /// <summary>
    /// Extension methods for configuring services in the application.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers all services required by the Scanner111 application.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddScanner111Services(this IServiceCollection services)
        {
            // Register core services
            RegisterCoreServices(services);
            
            // Register view models
            RegisterViewModels(services);
            
            // Register services
            RegisterApplicationServices(services);
            
            return services;
        }

        private static void RegisterCoreServices(IServiceCollection services)
        {
            // TODO: Add core service registrations
            // Example: services.AddSingleton<ILogger, FileLogger>();
        }

        private static void RegisterViewModels(IServiceCollection services)
        {
            // TODO: Register your view models
            // Example: services.AddTransient<MainWindowViewModel>();
        }

        private static void RegisterApplicationServices(IServiceCollection services)
        {
            // TODO: Register application-specific services
            // Example: services.AddSingleton<ISettingsManager, YamlSettingsManager>();
        }
    }
}

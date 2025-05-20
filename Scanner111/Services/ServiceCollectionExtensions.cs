using Microsoft.Extensions.DependencyInjection;
using Scanner111.Models; // If you need to register models or options
using Scanner111.Services;
using Scanner111.ViewModels; // If you have service interfaces and implementations

namespace Scanner111.Services
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddCustomServices(this IServiceCollection services)
        {
            // Example: Register a hypothetical FileService
            // services.AddSingleton<IFileService, FileService>();

            // Example: Register another service
            // services.AddTransient<IMyOtherService, MyOtherService>();

            // You can add more service registrations here

            return services;
        }

        //You could add other extension methods for different groups of services
        public static IServiceCollection AddViewModelServices(this IServiceCollection services)
        {
            services.AddTransient<MainWindowViewModel>();
            return services;
        }
    }
}

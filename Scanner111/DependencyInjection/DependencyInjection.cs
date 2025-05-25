using Microsoft.Extensions.DependencyInjection;
using System;

namespace Scanner111.DependencyInjection;

/// <summary>
/// Provides access to the application's dependency injection services.
/// </summary>
public static class DependencyInjection
{
    private static IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes the dependency injection container with the provided service provider.
    /// </summary>
    /// <param name="serviceProvider">The service provider to use.</param>
    public static void Initialize(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Gets the service provider.
    /// </summary>
    public static IServiceProvider ServiceProvider =>
        _serviceProvider ??
        throw new InvalidOperationException("Service provider is not initialized. Call Initialize first.");

    /// <summary>
    /// Gets a service of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the service to get.</typeparam>
    /// <returns>The service instance.</returns>
    public static T GetService<T>() where T : class
    {
        return ServiceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Gets a service of the specified type if available.
    /// </summary>
    /// <typeparam name="T">The type of the service to get.</typeparam>
    /// <returns>The service instance or null if not registered.</returns>
    public static T? GetServiceOrDefault<T>() where T : class
    {
        return ServiceProvider.GetService<T>();
    }
}
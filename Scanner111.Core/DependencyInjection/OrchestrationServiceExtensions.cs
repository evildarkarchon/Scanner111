using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;

namespace Scanner111.Core.DependencyInjection;

/// <summary>
///     Extension methods for registering orchestration services.
/// </summary>
public static class OrchestrationServiceExtensions
{
    /// <summary>
    ///     Adds the analyzer orchestration system to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAnalyzerOrchestration(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register core orchestration services
        services.TryAddSingleton<IAnalyzerOrchestrator, AnalyzerOrchestrator>();
        services.TryAddSingleton<IReportComposer, ReportComposer>();

        // Register all built-in analyzers as singletons
        services.AddBuiltInAnalyzers();

        return services;
    }

    /// <summary>
    ///     Adds the analyzer orchestration system with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Action to configure orchestration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAnalyzerOrchestration(
        this IServiceCollection services,
        Action<OrchestrationBuilder> configureOptions)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));

        // Add base orchestration
        services.AddAnalyzerOrchestration();

        // Configure with builder
        var builder = new OrchestrationBuilder(services);
        configureOptions(builder);

        return services;
    }

    /// <summary>
    ///     Registers all built-in analyzers.
    /// </summary>
    private static IServiceCollection AddBuiltInAnalyzers(this IServiceCollection services)
    {
        // Register analyzers as IAnalyzer so they can be discovered
        services.AddSingleton<IAnalyzer, PathValidationAnalyzer>();
        services.AddSingleton<IAnalyzer, GameIntegrityAnalyzer>();
        services.AddSingleton<IAnalyzer, DocumentsPathAnalyzer>();

        // Also register them by their concrete type for direct injection if needed
        services.TryAddSingleton<PathValidationAnalyzer>();
        services.TryAddSingleton<GameIntegrityAnalyzer>();
        services.TryAddSingleton<DocumentsPathAnalyzer>();

        return services;
    }

    /// <summary>
    ///     Adds a custom analyzer to the orchestration system.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="lifetime">The service lifetime (default is Singleton).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAnalyzer<TAnalyzer>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TAnalyzer : class, IAnalyzer
    {
        // Register as IAnalyzer for discovery
        services.Add(new ServiceDescriptor(typeof(IAnalyzer), typeof(TAnalyzer), lifetime));

        // Register concrete type for direct injection
        services.TryAdd(new ServiceDescriptor(typeof(TAnalyzer), typeof(TAnalyzer), lifetime));

        return services;
    }

    /// <summary>
    ///     Adds a custom analyzer with factory to the orchestration system.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="factory">Factory function to create the analyzer.</param>
    /// <param name="lifetime">The service lifetime (default is Singleton).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAnalyzer<TAnalyzer>(
        this IServiceCollection services,
        Func<IServiceProvider, TAnalyzer> factory,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TAnalyzer : class, IAnalyzer
    {
        // Register as IAnalyzer for discovery
        services.Add(new ServiceDescriptor(typeof(IAnalyzer), factory, lifetime));

        // Register concrete type for direct injection
        services.TryAdd(new ServiceDescriptor(typeof(TAnalyzer), factory, lifetime));

        return services;
    }
}

/// <summary>
///     Builder for configuring the orchestration system.
/// </summary>
public sealed class OrchestrationBuilder
{
    internal OrchestrationBuilder(IServiceCollection services)
    {
        Services = services;
    }

    /// <summary>
    ///     Gets the service collection being configured.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    ///     Adds a custom analyzer.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public OrchestrationBuilder AddAnalyzer<TAnalyzer>()
        where TAnalyzer : class, IAnalyzer
    {
        Services.AddAnalyzer<TAnalyzer>();
        return this;
    }

    /// <summary>
    ///     Adds a custom analyzer with factory.
    /// </summary>
    /// <typeparam name="TAnalyzer">The analyzer type.</typeparam>
    /// <param name="factory">Factory to create the analyzer.</param>
    /// <returns>The builder for chaining.</returns>
    public OrchestrationBuilder AddAnalyzer<TAnalyzer>(Func<IServiceProvider, TAnalyzer> factory)
        where TAnalyzer : class, IAnalyzer
    {
        Services.AddAnalyzer(factory);
        return this;
    }

    /// <summary>
    ///     Removes all built-in analyzers.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OrchestrationBuilder ClearBuiltInAnalyzers()
    {
        Services.RemoveAll<PathValidationAnalyzer>();
        Services.RemoveAll<GameIntegrityAnalyzer>();
        Services.RemoveAll<DocumentsPathAnalyzer>();

        // Also remove from IAnalyzer registrations
        var analyzerDescriptors = new List<ServiceDescriptor>();
        foreach (var descriptor in Services)
            if (descriptor.ServiceType == typeof(IAnalyzer) &&
                (descriptor.ImplementationType == typeof(PathValidationAnalyzer) ||
                 descriptor.ImplementationType == typeof(GameIntegrityAnalyzer) ||
                 descriptor.ImplementationType == typeof(DocumentsPathAnalyzer)))
                analyzerDescriptors.Add(descriptor);

        foreach (var descriptor in analyzerDescriptors) Services.Remove(descriptor);

        return this;
    }

    /// <summary>
    ///     Configures default orchestration options.
    /// </summary>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The builder for chaining.</returns>
    public OrchestrationBuilder ConfigureDefaultOptions(Action<OrchestrationOptions> configure)
    {
        Services.Configure(configure);
        return this;
    }

    /// <summary>
    ///     Uses a custom report composer implementation.
    /// </summary>
    /// <typeparam name="TComposer">The report composer type.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public OrchestrationBuilder UseReportComposer<TComposer>()
        where TComposer : class, IReportComposer
    {
        Services.RemoveAll<IReportComposer>();
        Services.AddSingleton<IReportComposer, TComposer>();
        return this;
    }
}
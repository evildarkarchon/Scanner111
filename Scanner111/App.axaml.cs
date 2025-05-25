using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Scanner111.DependencyInjection;
using Scanner111.ViewModels;
using Scanner111.Views;

namespace Scanner111;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Configure services
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();

        // Initialize the dependency injection static provider
        DependencyInjection.DependencyInjection.Initialize(_serviceProvider);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
            };
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
            singleViewPlatform.MainView = new MainView
            {
                DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
            };

        base.OnFrameworkInitializationCompleted();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Add all required services
        services.AddScanner111Services();

        // Register view models
        services.AddTransient<MainViewModel>();
    }
}
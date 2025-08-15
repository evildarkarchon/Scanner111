using Avalonia.ReactiveUI;

namespace Scanner111.GUI;

/// <summary>
///     Entry point and configuration class for the application.
/// </summary>
/// <remarks>
///     This class handles the initialization and startup process for the application,
///     defining the entry point and configuring Avalonia platform-specific settings.
///     It includes methods to build and configure the Avalonia application as well as
///     to start its lifecycle. The configuration ensures proper integration of Avalonia
///     libraries and ReactiveUI.
/// </remarks>
internal sealed class Program
{
    /// <summary>
    ///     Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application on startup.</param>
    /// <remarks>
    ///     This method serves as the starting point for the application. It initializes
    ///     the Avalonia application by calling <see cref="BuildAvaloniaApp" /> and starts the
    ///     application's main lifecycle using the classic desktop lifetime.
    /// </remarks>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    ///     Configures and builds the Avalonia application.
    /// </summary>
    /// <returns>
    ///     An <see cref="AppBuilder" /> instance configured for the application, including
    ///     platform detection, logging, font settings, and ReactiveUI integration.
    /// </returns>
    /// <remarks>
    ///     This method sets up core configurations required to initialize and run the
    ///     Avalonia application. It applies platform-specific settings, integrates ReactiveUI
    ///     for reactive programming, and configures logging and other necessary options.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();
    }
}
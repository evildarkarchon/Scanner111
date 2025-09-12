using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Configuration;
using Scanner111.CLI.Services;
using Scanner111.CLI.UI;
using Scanner111.CLI.UI.Screens;
using Scanner111.Core.Analysis;
using Scanner111.Core.Analysis.Analyzers;
using Scanner111.Core.Configuration;
using Scanner111.Core.DependencyInjection;
using Scanner111.Core.Discovery;
using Scanner111.Core.Orchestration;
using Scanner111.Core.Reporting;
using Scanner111.Core.Services;
using Serilog;
using Serilog.Events;
using Spectre.Console;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Scanner111",
            "Logs",
            "scanner111-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    // Parse command line arguments
    var result = Parser.Default.ParseArguments<AnalyzeCommand, InteractiveCommand, ConfigCommand>(args);
    
    await result.MapResult<AnalyzeCommand, InteractiveCommand, ConfigCommand, Task>(
        async (AnalyzeCommand opts) => await RunAnalyzeCommand(opts),
        async (InteractiveCommand opts) => await RunInteractiveMode(opts),
        async (ConfigCommand opts) => await RunConfigCommand(opts),
        async errs => await HandleParseErrors(errs));
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    AnsiConsole.WriteException(ex);
    Environment.Exit(1);
}
finally
{
    await Log.CloseAndFlushAsync();
}

static async Task RunAnalyzeCommand(AnalyzeCommand options)
{
    var host = CreateHost();
    var analyzeService = host.Services.GetRequiredService<IAnalyzeService>();
    
    await AnsiConsole.Status()
        .StartAsync("Analyzing log file...", async ctx =>
        {
            ctx.SpinnerStyle(Style.Parse("green"));
            await analyzeService.AnalyzeFileAsync(options.LogFile, options.Analyzers, options.OutputFile, options.Format);
        });
}

static async Task RunInteractiveMode(InteractiveCommand options)
{
    var host = CreateHost();
    var navigationService = host.Services.GetRequiredService<INavigationService>();
    
    // Start with the main menu
    await navigationService.NavigateToAsync<MainMenuScreen>();
}

static async Task RunConfigCommand(ConfigCommand options)
{
    var host = CreateHost();
    var configService = host.Services.GetRequiredService<IConfigurationService>();
    
    if (options.List)
    {
        await configService.ListSettingsAsync();
    }
    else if (!string.IsNullOrEmpty(options.Set))
    {
        await configService.SetSettingAsync(options.Set, options.Value);
    }
    else if (!string.IsNullOrEmpty(options.Get))
    {
        await configService.GetSettingAsync(options.Get);
    }
}

static async Task HandleParseErrors(IEnumerable<Error> errors)
{
    if (errors.IsVersion())
    {
        AnsiConsole.MarkupLine("[cyan]Scanner111 CLI v1.0.0[/]");
        return;
    }

    if (errors.IsHelp())
    {
        return; // Help is already displayed by CommandLineParser
    }

    AnsiConsole.MarkupLine("[red]Error parsing command line arguments[/]");
    foreach (var error in errors)
    {
        AnsiConsole.MarkupLine($"[red]  • {error}[/]");
    }

    await Task.CompletedTask;
}

static IHost CreateHost()
{
    return Host.CreateDefaultBuilder()
        .ConfigureServices((context, services) =>
        {
            // Configure logging
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog();
            });

            // Register Spectre.Console
            services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);

            // Register Core services using extension methods
            services.AddScanner111Complete(configureYamlOptions: options =>
            {
                options.DefaultGame = "Fallout4";
            });
            
            // Register CLI-specific services
            services.AddSingleton<IAdvancedReportGenerator, AdvancedReportGenerator>();
            services.AddSingleton<IReportComposer, ReportComposer>();
            services.AddSingleton<IReportGeneratorService, ReportGeneratorService>();
            services.AddSingleton<IAnalyzerOrchestrator, AnalyzerOrchestrator>();
            services.AddSingleton<IAnalyzerRegistry, AnalyzerRegistry>();

            // Register CLI services
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<ISessionManager, SessionManager>();
            services.AddSingleton<IExportService, ExportService>();
            services.AddSingleton<IFileWatcher, FileWatcher>();
            services.AddSingleton<IAnalyzeService, AnalyzeService>();
            services.AddSingleton<IConfigurationService, ConfigurationService>();

            // Register UI Screens
            services.AddTransient<MainMenuScreen>();
            services.AddTransient<AnalysisScreen>();
            services.AddTransient<ResultsScreen>();
            services.AddTransient<ConfigurationScreen>();
            services.AddTransient<HelpScreen>();

            // Register CLI Settings
            services.AddSingleton<ICliSettings, CliSettingsManager>();
        })
        .Build();
}
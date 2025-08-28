using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.Core.Configuration;
using Scanner111.Core.IO;
using Scanner111.Core.Services;
using Scanner111.Core.Caching;
using Scanner111.Core.Services.Performance;

namespace Scanner111.Test.Infrastructure;

/// <summary>
///     Base class for integration tests providing common setup and utilities.
/// </summary>
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected IServiceProvider ServiceProvider { get; private set; } = null!;
    protected string TestDirectory { get; private set; } = string.Empty;
    protected CancellationTokenSource TestCancellation { get; private set; } = null!;
    protected ILogger<IntegrationTestBase> Logger { get; private set; } = null!;

    public virtual async Task InitializeAsync()
    {
        TestDirectory = Path.Combine(Path.GetTempPath(), $"Scanner111_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestDirectory);
        
        TestCancellation = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        
        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        
        Logger = ServiceProvider.GetRequiredService<ILogger<IntegrationTestBase>>();
        
        await OnInitializeAsync().ConfigureAwait(false);
    }

    public virtual async Task DisposeAsync()
    {
        await OnDisposeAsync().ConfigureAwait(false);
        
        TestCancellation?.Dispose();
        
        if (ServiceProvider is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (ServiceProvider is IDisposable disposable)
            disposable.Dispose();
        
        if (Directory.Exists(TestDirectory))
        {
            try
            {
                Directory.Delete(TestDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to cleanup test directory {TestDirectory}: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Configure services for the test. Override to add specific services.
    /// </summary>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder => 
            builder.AddConsole()
                   .SetMinimumLevel(LogLevel.Debug));

        var mockSettingsCore = Substitute.For<IAsyncYamlSettingsCore>();
        mockSettingsCore.GetSettingAsync<string>(
            Arg.Any<YamlStore>(), 
            Arg.Any<string>(), 
            Arg.Any<string>(),
            Arg.Any<CancellationToken>())
            .Returns("false");
        services.AddSingleton(mockSettingsCore);

        services.AddTransient<IFileIoCore, TestFileIoCore>();
        services.AddTransient<LogReformatter>();
        services.AddTransient<DynamicBatchSizer>();
        services.AddTransient<PerformanceBenchmarker>();
    }

    protected virtual Task OnInitializeAsync() => Task.CompletedTask;
    protected virtual Task OnDisposeAsync() => Task.CompletedTask;

    protected async Task<T> WithTimeout<T>(
        Func<CancellationToken, Task<T>> operation, 
        int timeoutSeconds = 30)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(TestCancellation.Token);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        
        try
        {
            return await operation(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !TestCancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeoutSeconds} seconds");
        }
    }

    protected async Task<string> CreateTestFileAsync(string filename, string content)
    {
        var filePath = Path.Combine(TestDirectory, filename);
        await File.WriteAllTextAsync(filePath, content).ConfigureAwait(false);
        return filePath;
    }

    protected T GetService<T>() where T : notnull => ServiceProvider.GetRequiredService<T>();

    protected static string CreateSampleLogContent(bool includePlugins = true, bool includeBrackets = true)
    {
        var content = new List<string>
        {
            "Scanner111 v1.0.0",
            "System Information:",
            "OS: Windows 11",
            ""
        };

        if (includePlugins)
        {
            content.AddRange(new[]
            {
                "PLUGINS:",
                "    [00]     Skyrim.esm",
                "    [0A 12]  TestPlugin.esp"
            });
        }

        content.AddRange(new[]
        {
            "Call Stack:",
            "  [0] SkyrimSE.exe+0x123456",
            "End of log"
        });

        return string.Join('\n', content);
    }
}

internal sealed class TestFileIoCore : IFileIoCore
{
    public async Task<string> ReadFileAsync(string path, CancellationToken cancellationToken = default)
        => await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);

    public async Task<string[]> ReadLinesAsync(string path, CancellationToken cancellationToken = default)
        => await File.ReadAllLinesAsync(path, cancellationToken).ConfigureAwait(false);

    public async Task WriteFileAsync(string path, string content, System.Text.Encoding? encoding = null, 
        CancellationToken cancellationToken = default)
    {
        encoding ??= System.Text.Encoding.UTF8;
        await File.WriteAllTextAsync(path, content, encoding, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
        => await Task.FromResult(File.Exists(path)).ConfigureAwait(false);

    public async Task<DateTime?> GetLastWriteTimeAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return null;
        return await Task.FromResult(File.GetLastWriteTime(path)).ConfigureAwait(false);
    }

    public async Task CreateDirectoryAsync(string path, CancellationToken cancellationToken = default)
    {
        await Task.Run(() => Directory.CreateDirectory(path), cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteFileAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return false;
        
        await Task.Run(() => File.Delete(path), cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite), cancellationToken).ConfigureAwait(false);
    }
}
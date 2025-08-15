using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;

namespace Scanner111.Tests.TestHelpers;

/// <summary>
///     Helper class for creating and configuring commonly used mock services in tests.
///     Reduces duplication of mock setup code across test classes.
/// </summary>
public static class MockServiceHelper
{
    /// <summary>
    ///     Creates a mock IApplicationSettingsService with default settings.
    /// </summary>
    public static Mock<IApplicationSettingsService> CreateMockSettingsService(ApplicationSettings? settings = null)
    {
        var mock = new Mock<IApplicationSettingsService>();
        var actualSettings = settings ?? new ApplicationSettings
        {
            GamePath = @"C:\Games\Fallout4",
            FcxMode = false,
            ShowFormIdValues = false,
            SimplifyLogs = false,
            MoveUnsolvedLogs = false
        };

        mock.Setup(x => x.LoadSettingsAsync())
            .ReturnsAsync(actualSettings);
        mock.Setup(x => x.SaveSettingsAsync(It.IsAny<ApplicationSettings>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.SaveSettingAsync(It.IsAny<string>(), It.IsAny<object>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.GetDefaultSettings())
            .Returns(actualSettings);

        return mock;
    }

    /// <summary>
    ///     Creates a mock IYamlSettingsProvider for testing.
    /// </summary>
    public static Mock<IYamlSettingsProvider> CreateMockYamlProvider()
    {
        var mock = new Mock<IYamlSettingsProvider>();

        // IYamlSettingsProvider uses generic methods which are difficult to mock
        // For testing, use TestYamlSettingsProvider instead

        return mock;
    }

    /// <summary>
    ///     Creates a mock IMessageHandler that captures messages.
    /// </summary>
    public static Mock<IMessageHandler> CreateMockMessageHandler(List<string>? capturedMessages = null)
    {
        var mock = new Mock<IMessageHandler>();
        var messages = capturedMessages ?? new List<string>();

        mock.Setup(x => x.ShowInfo(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, _) => messages.Add($"INFO: {msg}"));
        mock.Setup(x => x.ShowWarning(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, _) => messages.Add($"WARNING: {msg}"));
        mock.Setup(x => x.ShowError(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, _) => messages.Add($"ERROR: {msg}"));
        mock.Setup(x => x.ShowSuccess(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, _) => messages.Add($"SUCCESS: {msg}"));
        mock.Setup(x => x.ShowDebug(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, _) => messages.Add($"DEBUG: {msg}"));
        mock.Setup(x => x.ShowCritical(It.IsAny<string>(), It.IsAny<MessageTarget>()))
            .Callback<string, MessageTarget>((msg, _) => messages.Add($"CRITICAL: {msg}"));

        mock.Setup(x => x.ShowMessage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<MessageType>(),
                It.IsAny<MessageTarget>()))
            .Callback<string, string, MessageType, MessageTarget>((msg, details, type, _) =>
            {
                messages.Add($"{type}: {msg} - {details}");
            });

        // Setup progress methods
        mock.Setup(x => x.ShowProgress(It.IsAny<string>(), It.IsAny<int>()))
            .Returns((string _, int _) => new Progress<ProgressInfo>());
        mock.Setup(x => x.CreateProgressContext(It.IsAny<string>(), It.IsAny<int>()))
            .Returns(Mock.Of<IProgressContext>());

        return mock;
    }

    /// <summary>
    ///     Creates a mock IFormIdDatabaseService.
    /// </summary>
    public static Mock<IFormIdDatabaseService> CreateMockFormIdDatabase()
    {
        var mock = new Mock<IFormIdDatabaseService>();

        mock.Setup(x => x.DatabaseExists).Returns(true);
        mock.Setup(x => x.GetEntry(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((formId, plugin) => $"MockedEntry for {formId} in {plugin}");

        return mock;
    }

    /// <summary>
    ///     Creates a mock IHashValidationService.
    /// </summary>
    public static Mock<IHashValidationService> CreateMockHashService(Dictionary<string, string>? fileHashes = null)
    {
        var mock = new Mock<IHashValidationService>();
        var hashes = fileHashes ?? new Dictionary<string, string>();

        mock.Setup(x => x.CalculateFileHashAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<string, CancellationToken>((path, _) =>
                Task.FromResult(hashes.TryGetValue(path, out var hash) ? hash : "DEFAULT_HASH"));

        return mock;
    }

    /// <summary>
    ///     Creates a mock ICacheManager.
    /// </summary>
    public static Mock<ICacheManager> CreateMockCacheManager()
    {
        var mock = new Mock<ICacheManager>();

        // ICacheManager interface methods need to be defined based on actual interface
        // For now, returning basic mock

        return mock;
    }

    /// <summary>
    ///     Creates a mock IUnsolvedLogsMover.
    /// </summary>
    public static Mock<IUnsolvedLogsMover> CreateMockUnsolvedLogsMover()
    {
        var mock = new Mock<IUnsolvedLogsMover>();

        // IUnsolvedLogsMover interface methods need to be defined based on actual interface
        // For now, returning basic mock

        return mock;
    }

    /// <summary>
    ///     Creates a mock IBackupService.
    /// </summary>
    public static Mock<IBackupService> CreateMockBackupService()
    {
        var mock = new Mock<IBackupService>();

        var defaultBackupResult = new BackupResult
        {
            Success = true,
            BackupPath = "C:\\Backups\\backup.zip",
            Message = "Backup created successfully"
        };

        mock.Setup(x => x.CreateBackupAsync(
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IProgress<BackupProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultBackupResult);

        mock.Setup(x => x.CreateFullBackupAsync(
                It.IsAny<string>(),
                It.IsAny<IProgress<BackupProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaultBackupResult);

        mock.Setup(x => x.RestoreBackupAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IProgress<BackupProgress>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        mock.Setup(x => x.ListBackupsAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<BackupInfo>());

        mock.Setup(x => x.DeleteBackupAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        return mock;
    }

    /// <summary>
    ///     Creates a test directory structure for file-based tests.
    /// </summary>
    public static string CreateTestDirectory(string? basePath = null)
    {
        var path = basePath ?? Path.Combine(Path.GetTempPath(), $"Scanner111Test_{Guid.NewGuid()}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    ///     Cleans up a test directory.
    /// </summary>
    public static void CleanupTestDirectory(string path)
    {
        if (Directory.Exists(path))
            try
            {
                Directory.Delete(path, true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
    }
}
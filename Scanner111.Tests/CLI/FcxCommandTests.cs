using Microsoft.Extensions.Logging;
using NSubstitute;
using Scanner111.CLI.Commands;
using Scanner111.CLI.Models;
using Scanner111.CLI.Services;
using Scanner111.Core.Analyzers;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Xunit;

namespace Scanner111.Tests.CLI;

public class FcxCommandTests
{
    private readonly ICliSettingsService _settingsService;
    private readonly IHashValidationService _hashService;
    private readonly IBackupService _backupService;
    private readonly IApplicationSettingsService _appSettingsService;
    private readonly IYamlSettingsProvider _yamlSettings;
    private readonly ILogger<FcxCommand> _logger;
    private readonly FcxCommand _command;
    
    public FcxCommandTests()
    {
        _settingsService = Substitute.For<ICliSettingsService>();
        _hashService = Substitute.For<IHashValidationService>();
        _backupService = Substitute.For<IBackupService>();
        _appSettingsService = Substitute.For<IApplicationSettingsService>();
        _yamlSettings = Substitute.For<IYamlSettingsProvider>();
        _logger = Substitute.For<ILogger<FcxCommand>>();
        
        _command = new FcxCommand(
            _settingsService,
            _hashService,
            _backupService,
            _appSettingsService,
            _yamlSettings,
            _logger);
    }
    
    [Fact]
    public async Task ExecuteAsync_WithValidGamePath_RunsIntegrityChecks()
    {
        // Arrange
        var options = new FcxOptions
        {
            GamePath = @"C:\Games\Fallout4",
            DisableColors = true,
            DisableProgress = true
        };
        
        var settings = new ApplicationSettings
        {
            GamePath = @"C:\Games\Fallout4"
        };
        
        _settingsService.LoadSettingsAsync().Returns(Task.FromResult(settings));
        _appSettingsService.LoadSettingsAsync().Returns(Task.FromResult(new ApplicationSettings()));
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        await _settingsService.Received(1).LoadSettingsAsync();
    }
    
    [Fact]
    public async Task ExecuteAsync_WithRestorePath_CallsBackupService()
    {
        // Arrange
        var options = new FcxOptions
        {
            RestorePath = @"C:\Backups\backup.zip",
            GamePath = @"C:\Games\Fallout4",
            DisableColors = true,
            DisableProgress = true
        };
        
        var settings = new ApplicationSettings
        {
            GamePath = @"C:\Games\Fallout4"
        };
        
        _settingsService.LoadSettingsAsync().Returns(Task.FromResult(settings));
        _backupService.RestoreBackupAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<string>?>(),
            Arg.Any<IProgress<BackupProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        Assert.Equal(0, result);
        await _backupService.Received(1).RestoreBackupAsync(
            options.RestorePath,
            settings.GamePath,
            null,
            Arg.Any<IProgress<BackupProgress>?>(),
            Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task ExecuteAsync_WithBackupOption_CreatesBackup()
    {
        // Arrange
        var options = new FcxOptions
        {
            Backup = true,
            CheckOnly = false,
            GamePath = @"C:\Games\Fallout4",
            DisableColors = true,
            DisableProgress = true
        };
        
        var settings = new ApplicationSettings
        {
            GamePath = @"C:\Games\Fallout4"
        };
        
        _settingsService.LoadSettingsAsync().Returns(Task.FromResult(settings));
        _appSettingsService.LoadSettingsAsync().Returns(Task.FromResult(new ApplicationSettings()));
        
        var backupResult = new BackupResult
        {
            Success = true,
            BackupPath = @"C:\Backups\backup-123.zip"
        };
        
        _backupService.CreateFullBackupAsync(
            Arg.Any<string>(),
            Arg.Any<IProgress<BackupProgress>?>(),
            Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(backupResult));
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        await _backupService.Received(1).CreateFullBackupAsync(
            settings.GamePath,
            Arg.Any<IProgress<BackupProgress>?>(),
            Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task ExecuteAsync_WithCheckOnlyOption_DoesNotCreateBackup()
    {
        // Arrange
        var options = new FcxOptions
        {
            Backup = true,
            CheckOnly = true,
            GamePath = @"C:\Games\Fallout4",
            DisableColors = true,
            DisableProgress = true
        };
        
        var settings = new ApplicationSettings
        {
            GamePath = @"C:\Games\Fallout4"
        };
        
        _settingsService.LoadSettingsAsync().Returns(Task.FromResult(settings));
        _appSettingsService.LoadSettingsAsync().Returns(Task.FromResult(new ApplicationSettings()));
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        await _backupService.DidNotReceive().CreateFullBackupAsync(
            Arg.Any<string>(),
            Arg.Any<IProgress<BackupProgress>?>(),
            Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task ExecuteAsync_WithoutGamePath_AttemptsDetection()
    {
        // Arrange
        var options = new FcxOptions
        {
            DisableColors = true,
            DisableProgress = true
        };
        
        var settings = new ApplicationSettings(); // No game path set
        
        _settingsService.LoadSettingsAsync().Returns(Task.FromResult(settings));
        _appSettingsService.LoadSettingsAsync().Returns(Task.FromResult(new ApplicationSettings()));
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        // Since GamePathDetection is created internally, we can't easily verify it was called
        // But we can verify that the settings were loaded
        await _settingsService.Received(1).LoadSettingsAsync();
    }
    
    [Fact]
    public async Task ExecuteAsync_AppliesCommandLineOverrides()
    {
        // Arrange
        var options = new FcxOptions
        {
            GamePath = @"C:\CustomPath\Fallout4",
            ModsFolder = @"C:\CustomMods",
            IniFolder = @"C:\CustomIni",
            DisableColors = true,
            DisableProgress = true
        };
        
        var settings = new ApplicationSettings
        {
            GamePath = @"C:\Games\Fallout4",
            ModsFolder = @"C:\Mods",
            IniFolder = @"C:\Ini"
        };
        
        _settingsService.LoadSettingsAsync().Returns(Task.FromResult(settings));
        _appSettingsService.LoadSettingsAsync().Returns(Task.FromResult(new ApplicationSettings()));
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        Assert.Equal(options.GamePath, settings.GamePath);
        Assert.Equal(options.ModsFolder, settings.ModsFolder);
        Assert.Equal(options.IniFolder, settings.IniFolder);
    }
    
    [Fact]
    public async Task ExecuteAsync_HandlesExceptions()
    {
        // Arrange
        var options = new FcxOptions
        {
            DisableColors = true,
            DisableProgress = true
        };
        
        _settingsService.LoadSettingsAsync()
            .Returns(Task.FromException<ApplicationSettings>(new Exception("Test exception")));
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        Assert.Equal(1, result);
        _logger.Received(1).LogError(
            Arg.Any<Exception>(),
            "Fatal error during FCX operation");
    }
}
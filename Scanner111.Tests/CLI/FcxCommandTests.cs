using Microsoft.Extensions.Logging;
using Moq;
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
        var settingsServiceMock = new Mock<ICliSettingsService>();
        _settingsService = settingsServiceMock.Object;
        var hashServiceMock = new Mock<IHashValidationService>();
        _hashService = hashServiceMock.Object;
        var backupServiceMock = new Mock<IBackupService>();
        _backupService = backupServiceMock.Object;
        var appSettingsServiceMock = new Mock<IApplicationSettingsService>();
        _appSettingsService = appSettingsServiceMock.Object;
        var yamlSettingsMock = new Mock<IYamlSettingsProvider>();
        _yamlSettings = yamlSettingsMock.Object;
        var loggerMock = new Mock<ILogger<FcxCommand>>();
        _logger = loggerMock.Object;
        
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
        
        Mock.Get(_settingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);
        Mock.Get(_appSettingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(new ApplicationSettings());
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        Mock.Get(_settingsService).Verify(x => x.LoadSettingsAsync(), Times.Once);
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
        
        Mock.Get(_settingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);
        Mock.Get(_backupService).Setup(x => x.RestoreBackupAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<IEnumerable<string>?>(),
            It.IsAny<IProgress<BackupProgress>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        Assert.Equal(0, result);
        Mock.Get(_backupService).Verify(x => x.RestoreBackupAsync(
            options.RestorePath,
            settings.GamePath,
            null,
            It.IsAny<IProgress<BackupProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
        
        Mock.Get(_settingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);
        Mock.Get(_appSettingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(new ApplicationSettings());
        
        var backupResult = new BackupResult
        {
            Success = true,
            BackupPath = @"C:\Backups\backup-123.zip"
        };
        
        Mock.Get(_backupService).Setup(x => x.CreateFullBackupAsync(
            It.IsAny<string>(),
            It.IsAny<IProgress<BackupProgress>?>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(backupResult);
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        Mock.Get(_backupService).Verify(x => x.CreateFullBackupAsync(
            settings.GamePath,
            It.IsAny<IProgress<BackupProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Once);
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
        
        Mock.Get(_settingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);
        Mock.Get(_appSettingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(new ApplicationSettings());
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        Mock.Get(_backupService).Verify(x => x.CreateFullBackupAsync(
            It.IsAny<string>(),
            It.IsAny<IProgress<BackupProgress>?>(),
            It.IsAny<CancellationToken>()), Times.Never);
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
        
        Mock.Get(_settingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);
        Mock.Get(_appSettingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(new ApplicationSettings());
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        // Since GamePathDetection is created internally, we can't easily verify it was called
        // But we can verify that the settings were loaded
        Mock.Get(_settingsService).Verify(x => x.LoadSettingsAsync(), Times.Once);
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
        
        Mock.Get(_settingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);
        Mock.Get(_appSettingsService).Setup(x => x.LoadSettingsAsync()).ReturnsAsync(new ApplicationSettings());
        
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
        
        Mock.Get(_settingsService).Setup(x => x.LoadSettingsAsync())
            .ThrowsAsync(new Exception("Test exception"));
        
        // Act
        var result = await _command.ExecuteAsync(options);
        
        // Assert
        Assert.Equal(1, result);
        // Note: Cannot verify logger extension methods with Moq
        // The important assertion is that error code 1 is returned
    }
}
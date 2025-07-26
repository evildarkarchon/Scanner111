using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;
using System.IO.Compression;
using System.Text;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
/// Unit tests for the <see cref="BackupService"/> class
/// </summary>
public class BackupServiceTests : IDisposable
{
    private readonly BackupService _backupService;
    private readonly TestApplicationSettingsService _settingsService;
    private readonly List<string> _tempDirectories;
    private readonly string _testGamePath;
    private readonly string _testBackupPath;
    
    public BackupServiceTests()
    {
        _settingsService = new TestApplicationSettingsService();
        _backupService = new BackupService(
            NullLogger<BackupService>.Instance,
            _settingsService);
        
        _tempDirectories = new List<string>();
        
        // Create test directories
        _testGamePath = CreateTempDirectory();
        _testBackupPath = CreateTempDirectory();
        
        // Update settings with backup directory
        var settings = _settingsService.LoadSettingsAsync().Result;
        settings.BackupDirectory = _testBackupPath;
        _settingsService.SaveSettingsAsync(settings).Wait();
    }
    
    public void Dispose()
    {
        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
        GC.SuppressFinalize(this);
    }
    
    [Fact]
    public async Task CreateBackupAsync_WithValidFiles_CreatesBackupSuccessfully()
    {
        // Arrange
        CreateTestFile(_testGamePath, "Fallout4.exe", "Game executable content");
        CreateTestFile(_testGamePath, "f4se_loader.exe", "F4SE loader content");
        CreateTestFile(_testGamePath, @"Data\test.esp", "Plugin content");
        
        var filesToBackup = new[] { "Fallout4.exe", "f4se_loader.exe", @"Data\test.esp" };
        
        // Act
        var result = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup);
        
        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));
        Assert.Equal(3, result.BackedUpFiles.Count);
        Assert.Contains("Fallout4.exe", result.BackedUpFiles);
        Assert.Contains("f4se_loader.exe", result.BackedUpFiles);
        Assert.Contains(@"Data\test.esp", result.BackedUpFiles);
        Assert.True(result.TotalSize > 0);
    }
    
    [Fact]
    public async Task CreateBackupAsync_WithInvalidGamePath_ReturnsError()
    {
        // Arrange
        var invalidPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filesToBackup = new[] { "Fallout4.exe" };
        
        // Act
        var result = await _backupService.CreateBackupAsync(invalidPath, filesToBackup);
        
        // Assert
        Assert.False(result.Success);
        Assert.Contains("Game path does not exist", result.ErrorMessage);
    }
    
    [Fact]
    public async Task CreateBackupAsync_WithMissingFiles_BacksUpExistingFilesOnly()
    {
        // Arrange
        CreateTestFile(_testGamePath, "Fallout4.exe", "Game executable content");
        var filesToBackup = new[] { "Fallout4.exe", "NonExistent.dll" };
        
        // Act
        var result = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup);
        
        // Assert
        Assert.True(result.Success);
        Assert.Single(result.BackedUpFiles);
        Assert.Contains("Fallout4.exe", result.BackedUpFiles);
        Assert.DoesNotContain("NonExistent.dll", result.BackedUpFiles);
    }
    
    [Fact]
    public async Task CreateBackupAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        CreateTestFile(_testGamePath, "file1.txt", "Content 1");
        CreateTestFile(_testGamePath, "file2.txt", "Content 2");
        CreateTestFile(_testGamePath, "file3.txt", "Content 3");
        
        var filesToBackup = new[] { "file1.txt", "file2.txt", "file3.txt" };
        var progressReports = new List<BackupProgress>();
        var progress = new Progress<BackupProgress>(p => progressReports.Add(p));
        
        // Act
        var result = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup, progress);
        
        // Assert
        Assert.True(result.Success);
        Assert.NotEmpty(progressReports);
        Assert.True(progressReports.All(p => p.TotalFiles == 3));
        Assert.Contains(progressReports, p => p.CurrentFile == "file1.txt");
        Assert.Contains(progressReports, p => p.CurrentFile == "file2.txt");
        Assert.Contains(progressReports, p => p.CurrentFile == "file3.txt");
    }
    
    [Fact]
    public async Task CreateBackupAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        CreateTestFile(_testGamePath, "Fallout4.exe", new string('A', 1024 * 1024)); // Large file
        var filesToBackup = new[] { "Fallout4.exe" };
        var cts = new CancellationTokenSource();
        cts.Cancel();
        
        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _backupService.CreateBackupAsync(_testGamePath, filesToBackup, cancellationToken: cts.Token));
    }
    
    [Fact]
    public async Task CreateFullBackupAsync_BacksUpCriticalGameFiles()
    {
        // Arrange
        CreateTestFile(_testGamePath, "Fallout4.exe", "Game executable");
        CreateTestFile(_testGamePath, "Fallout4Launcher.exe", "Launcher");
        CreateTestFile(_testGamePath, "f4se_loader.exe", "F4SE loader");
        CreateTestFile(_testGamePath, @"Data\Fallout4.esm", "Main game data");
        CreateTestFile(_testGamePath, @"Data\F4SE\Plugins\Buffout4.dll", "Buffout plugin");
        
        // Act
        var result = await _backupService.CreateFullBackupAsync(_testGamePath);
        
        // Assert
        Assert.True(result.Success);
        Assert.True(result.BackedUpFiles.Count >= 4);
        Assert.Contains("Fallout4.exe", result.BackedUpFiles);
        Assert.Contains("Fallout4Launcher.exe", result.BackedUpFiles);
        Assert.Contains("f4se_loader.exe", result.BackedUpFiles);
        Assert.Contains(@"Data\Fallout4.esm", result.BackedUpFiles);
    }
    
    [Fact]
    public async Task RestoreBackupAsync_WithValidBackup_RestoresFilesSuccessfully()
    {
        // Arrange
        CreateTestFile(_testGamePath, "Fallout4.exe", "Original content");
        CreateTestFile(_testGamePath, "f4se_loader.exe", "Original F4SE");
        
        var filesToBackup = new[] { "Fallout4.exe", "f4se_loader.exe" };
        var backupResult = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup);
        
        // Modify files after backup
        File.WriteAllText(Path.Combine(_testGamePath, "Fallout4.exe"), "Modified content");
        File.WriteAllText(Path.Combine(_testGamePath, "f4se_loader.exe"), "Modified F4SE");
        
        // Act
        var restoreSuccess = await _backupService.RestoreBackupAsync(backupResult.BackupPath, _testGamePath);
        
        // Assert
        Assert.True(restoreSuccess);
        Assert.Equal("Original content", await File.ReadAllTextAsync(Path.Combine(_testGamePath, "Fallout4.exe")));
        Assert.Equal("Original F4SE", await File.ReadAllTextAsync(Path.Combine(_testGamePath, "f4se_loader.exe")));
        
        // Check that .bak files were created
        Assert.True(File.Exists(Path.Combine(_testGamePath, "Fallout4.exe.bak")));
        Assert.True(File.Exists(Path.Combine(_testGamePath, "f4se_loader.exe.bak")));
    }
    
    [Fact]
    public async Task RestoreBackupAsync_WithNonExistentBackup_ReturnsFalse()
    {
        // Arrange
        var nonExistentBackup = Path.Combine(_testBackupPath, "nonexistent.zip");
        
        // Act
        var result = await _backupService.RestoreBackupAsync(nonExistentBackup, _testGamePath);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task RestoreBackupAsync_WithSelectiveRestore_RestoresOnlySpecifiedFiles()
    {
        // Arrange
        CreateTestFile(_testGamePath, "file1.txt", "Original 1");
        CreateTestFile(_testGamePath, "file2.txt", "Original 2");
        CreateTestFile(_testGamePath, "file3.txt", "Original 3");
        
        var filesToBackup = new[] { "file1.txt", "file2.txt", "file3.txt" };
        var backupResult = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup);
        
        // Modify all files
        File.WriteAllText(Path.Combine(_testGamePath, "file1.txt"), "Modified 1");
        File.WriteAllText(Path.Combine(_testGamePath, "file2.txt"), "Modified 2");
        File.WriteAllText(Path.Combine(_testGamePath, "file3.txt"), "Modified 3");
        
        // Act - Restore only file1.txt and file3.txt
        var filesToRestore = new[] { "file1.txt", "file3.txt" };
        var restoreSuccess = await _backupService.RestoreBackupAsync(
            backupResult.BackupPath, _testGamePath, filesToRestore);
        
        // Assert
        Assert.True(restoreSuccess);
        Assert.Equal("Original 1", await File.ReadAllTextAsync(Path.Combine(_testGamePath, "file1.txt")));
        Assert.Equal("Modified 2", await File.ReadAllTextAsync(Path.Combine(_testGamePath, "file2.txt"))); // Not restored
        Assert.Equal("Original 3", await File.ReadAllTextAsync(Path.Combine(_testGamePath, "file3.txt")));
    }
    
    [Fact]
    public async Task ListBackupsAsync_ReturnsBackupsOrderedByDate()
    {
        // Arrange
        // Create multiple backups with delays
        CreateTestFile(_testGamePath, "test.txt", "Test content");
        var filesToBackup = new[] { "test.txt" };
        
        var backup1 = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup);
        await Task.Delay(100);
        var backup2 = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup);
        await Task.Delay(100);
        var backup3 = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup);
        
        // Act
        var backups = (await _backupService.ListBackupsAsync()).ToList();
        
        // Assert
        Assert.True(backup1.Success, "Backup 1 should be successful");
        Assert.True(backup2.Success, "Backup 2 should be successful");
        Assert.True(backup3.Success, "Backup 3 should be successful");
        
        Assert.True(backups.Count >= 3, $"Expected at least 3 backups, but found {backups.Count}");
        // Backups should be ordered by creation time descending (newest first)
        for (int i = 1; i < backups.Count; i++)
        {
            Assert.True(backups[i - 1].CreatedDate >= backups[i].CreatedDate);
        }
        
        // Verify backup info
        var latestBackup = backups.First();
        Assert.NotNull(latestBackup.Name);
        Assert.True(latestBackup.Size > 0);
        Assert.True(latestBackup.FileCount > 0);
    }
    
    [Fact]
    public async Task ListBackupsAsync_WithEmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        var emptyBackupDir = CreateTempDirectory();
        
        // Act
        var backups = await _backupService.ListBackupsAsync(emptyBackupDir);
        
        // Assert
        Assert.Empty(backups);
    }
    
    [Fact]
    public async Task DeleteBackupAsync_WithExistingBackup_DeletesSuccessfully()
    {
        // Arrange
        CreateTestFile(_testGamePath, "test.txt", "Test content");
        var backup = await _backupService.CreateBackupAsync(_testGamePath, new[] { "test.txt" });
        
        // Verify backup exists
        Assert.True(File.Exists(backup.BackupPath));
        
        // Act
        var deleteResult = await _backupService.DeleteBackupAsync(backup.BackupPath);
        
        // Assert
        Assert.True(deleteResult);
        Assert.False(File.Exists(backup.BackupPath));
    }
    
    [Fact]
    public async Task DeleteBackupAsync_WithNonExistentBackup_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testBackupPath, "nonexistent.zip");
        
        // Act
        var result = await _backupService.DeleteBackupAsync(nonExistentPath);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task CreateBackupAsync_WithSubdirectories_PreservesDirectoryStructure()
    {
        // Arrange
        CreateTestFile(_testGamePath, @"Data\Scripts\test.pex", "Script content");
        CreateTestFile(_testGamePath, @"Data\F4SE\Plugins\test.dll", "Plugin content");
        CreateTestFile(_testGamePath, @"Data\Meshes\test.nif", "Mesh content");
        
        var filesToBackup = new[] 
        { 
            @"Data\Scripts\test.pex",
            @"Data\F4SE\Plugins\test.dll",
            @"Data\Meshes\test.nif"
        };
        
        // Act
        var result = await _backupService.CreateBackupAsync(_testGamePath, filesToBackup);
        
        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.BackedUpFiles.Count);
        
        // Verify zip structure
        using (var zip = ZipFile.OpenRead(result.BackupPath))
        {
            Assert.Contains(zip.Entries, e => e.FullName == "Data/Scripts/test.pex");
            Assert.Contains(zip.Entries, e => e.FullName == "Data/F4SE/Plugins/test.dll");
            Assert.Contains(zip.Entries, e => e.FullName == "Data/Meshes/test.nif");
        }
    }
    
    [Fact]
    public async Task CreateBackupAsync_WithEmptyFileList_ReturnsError()
    {
        // Arrange
        var emptyFileList = Array.Empty<string>();
        
        // Act
        var result = await _backupService.CreateBackupAsync(_testGamePath, emptyFileList);
        
        // Assert
        Assert.False(result.Success);
        Assert.Equal("No files were backed up", result.ErrorMessage);
    }
    
    [Fact]
    public async Task RestoreBackupAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        CreateTestFile(_testGamePath, "file1.txt", "Content 1");
        CreateTestFile(_testGamePath, "file2.txt", "Content 2");
        CreateTestFile(_testGamePath, "file3.txt", "Content 3");
        
        var backup = await _backupService.CreateBackupAsync(_testGamePath, 
            new[] { "file1.txt", "file2.txt", "file3.txt" });
        
        var progressReports = new List<BackupProgress>();
        var progress = new Progress<BackupProgress>(p => progressReports.Add(p));
        
        // Act
        var result = await _backupService.RestoreBackupAsync(
            backup.BackupPath, _testGamePath, progress: progress);
        
        // Assert
        Assert.True(result);
        Assert.NotEmpty(progressReports);
        Assert.True(progressReports.All(p => p.TotalFiles == 3));
    }
    
    private string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        return tempDir;
    }
    
    private void CreateTestFile(string basePath, string relativePath, string content)
    {
        var fullPath = Path.Combine(basePath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        File.WriteAllText(fullPath, content, Encoding.UTF8);
    }
}
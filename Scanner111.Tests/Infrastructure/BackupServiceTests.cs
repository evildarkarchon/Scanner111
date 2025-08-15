using System.IO.Compression;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Scanner111.Core.Infrastructure;
using Scanner111.Core.Models;
using Scanner111.Tests.TestHelpers;

namespace Scanner111.Tests.Infrastructure;

/// <summary>
///     Unit tests for the <see cref="BackupService" /> class
/// </summary>
[Collection("Backup Tests")]
public class BackupServiceTests : IDisposable
{
    private readonly BackupService _backupService;
    private readonly TestApplicationSettingsService _settingsService;
    private readonly List<string> _tempDirectories;
    private readonly string _testBackupPath;
    private readonly string _testGamePath;

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

        // Update settings with backup directory using async initialization
        InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        // Clean up temporary directories
        foreach (var dir in _tempDirectories)
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);

        GC.SuppressFinalize(this);
    }

    private async Task InitializeAsync()
    {
        var settings = await _settingsService.LoadSettingsAsync();
        settings.BackupDirectory = _testBackupPath;
        await _settingsService.SaveSettingsAsync(settings);
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
        result.Success.Should().BeTrue("backup operation should succeed");
        result.BackupPath.Should().NotBeNull("backup path should be generated");
        File.Exists(result.BackupPath).Should().BeTrue("backup file should exist");
        result.BackedUpFiles.Should().HaveCount(3, "all three files should be backed up");
        result.BackedUpFiles.Should().Contain("Fallout4.exe", "executable should be backed up");
        result.BackedUpFiles.Should().Contain("f4se_loader.exe", "F4SE loader should be backed up");
        result.BackedUpFiles.Should().Contain(@"Data\test.esp", "plugin should be backed up");
        result.TotalSize.Should().BeGreaterThan(0, "backup should have non-zero size");
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
        result.Success.Should().BeFalse("backup should fail with invalid game path");
        result.ErrorMessage.Should().Contain("Game path does not exist", "error should indicate path issue");
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
        result.Success.Should().BeTrue("backup should succeed even with missing files");
        result.BackedUpFiles.Should().ContainSingle("only existing file should be backed up");
        result.BackedUpFiles.Should().Contain("Fallout4.exe", "existing file should be backed up");
        result.BackedUpFiles.Should().NotContain("NonExistent.dll", "missing file should not be in backup");
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
        result.Success.Should().BeTrue("backup should succeed");
        progressReports.Should().NotBeEmpty("progress should be reported");
        progressReports.Should().OnlyContain(p => p.TotalFiles == 3, "all progress reports should show 3 total files");
        progressReports.Should().Contain(p => p.CurrentFile == "file1.txt", "progress should report file1");
        progressReports.Should().Contain(p => p.CurrentFile == "file2.txt", "progress should report file2");
        progressReports.Should().Contain(p => p.CurrentFile == "file3.txt", "progress should report file3");
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
        var act = () => _backupService.CreateBackupAsync(_testGamePath, filesToBackup, cancellationToken: cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>("cancelled operation should throw");
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
        result.Success.Should().BeTrue("full backup should succeed");
        result.BackedUpFiles.Count.Should().BeGreaterThanOrEqualTo(4, "critical files should be backed up");
        result.BackedUpFiles.Should().Contain("Fallout4.exe", "main executable should be backed up");
        result.BackedUpFiles.Should().Contain("Fallout4Launcher.exe", "launcher should be backed up");
        result.BackedUpFiles.Should().Contain("f4se_loader.exe", "F4SE loader should be backed up");
        result.BackedUpFiles.Should().Contain(@"Data\Fallout4.esm", "main game data should be backed up");
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
        restoreSuccess.Should().BeTrue("restore should succeed");
        (await File.ReadAllTextAsync(Path.Combine(_testGamePath, "Fallout4.exe")))
            .Should().Be("Original content", "original executable content should be restored");
        (await File.ReadAllTextAsync(Path.Combine(_testGamePath, "f4se_loader.exe")))
            .Should().Be("Original F4SE", "original F4SE content should be restored");

        // Check that .bak files were created
        File.Exists(Path.Combine(_testGamePath, "Fallout4.exe.bak"))
            .Should().BeTrue("backup of modified executable should be created");
        File.Exists(Path.Combine(_testGamePath, "f4se_loader.exe.bak"))
            .Should().BeTrue("backup of modified F4SE should be created");
    }

    [Fact]
    public async Task RestoreBackupAsync_WithNonExistentBackup_ReturnsFalse()
    {
        // Arrange
        var nonExistentBackup = Path.Combine(_testBackupPath, "nonexistent.zip");

        // Act
        var result = await _backupService.RestoreBackupAsync(nonExistentBackup, _testGamePath);

        // Assert
        result.Should().BeFalse("restore should fail for non-existent backup");
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
        restoreSuccess.Should().BeTrue("selective restore should succeed");
        (await File.ReadAllTextAsync(Path.Combine(_testGamePath, "file1.txt")))
            .Should().Be("Original 1", "file1 should be restored");
        (await File.ReadAllTextAsync(Path.Combine(_testGamePath, "file2.txt")))
            .Should().Be("Modified 2", "file2 should remain modified (not in restore list)");
        (await File.ReadAllTextAsync(Path.Combine(_testGamePath, "file3.txt")))
            .Should().Be("Original 3", "file3 should be restored");
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
        backup1.Success.Should().BeTrue("Backup 1 should be successful");
        backup2.Success.Should().BeTrue("Backup 2 should be successful");
        backup3.Success.Should().BeTrue("Backup 3 should be successful");

        backups.Count.Should().BeGreaterThanOrEqualTo(3, "at least 3 backups should be listed");
        // Backups should be ordered by creation time descending (newest first)
        backups.Should().BeInDescendingOrder(b => b.CreatedDate, "backups should be ordered newest first");

        // Verify backup info
        var latestBackup = backups.First();
        latestBackup.Name.Should().NotBeNull("backup should have a name");
        latestBackup.Size.Should().BeGreaterThan(0, "backup should have non-zero size");
        latestBackup.FileCount.Should().BeGreaterThan(0, "backup should contain files");
    }

    [Fact]
    public async Task ListBackupsAsync_WithEmptyDirectory_ReturnsEmptyList()
    {
        // Arrange
        var emptyBackupDir = CreateTempDirectory();

        // Act
        var backups = await _backupService.ListBackupsAsync(emptyBackupDir);

        // Assert
        backups.Should().BeEmpty("empty directory should return no backups");
    }

    [Fact]
    public async Task DeleteBackupAsync_WithExistingBackup_DeletesSuccessfully()
    {
        // Arrange
        CreateTestFile(_testGamePath, "test.txt", "Test content");
        var backup = await _backupService.CreateBackupAsync(_testGamePath, new[] { "test.txt" });

        // Verify backup exists
        File.Exists(backup.BackupPath).Should().BeTrue("backup file should exist before deletion");

        // Act
        var deleteResult = await _backupService.DeleteBackupAsync(backup.BackupPath);

        // Assert
        deleteResult.Should().BeTrue("deletion should succeed");
        File.Exists(backup.BackupPath).Should().BeFalse("backup file should be deleted");
    }

    [Fact]
    public async Task DeleteBackupAsync_WithNonExistentBackup_ReturnsFalse()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testBackupPath, "nonexistent.zip");

        // Act
        var result = await _backupService.DeleteBackupAsync(nonExistentPath);

        // Assert
        result.Should().BeFalse("deletion should fail for non-existent file");
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
        result.Success.Should().BeTrue("backup with subdirectories should succeed");
        result.BackedUpFiles.Should().HaveCount(3, "all files should be backed up");

        // Verify zip structure
        using (var zip = ZipFile.OpenRead(result.BackupPath))
        {
            zip.Entries.Should().Contain(e => e.FullName == "Data/Scripts/test.pex",
                "script file should preserve directory structure");
            zip.Entries.Should().Contain(e => e.FullName == "Data/F4SE/Plugins/test.dll",
                "plugin file should preserve directory structure");
            zip.Entries.Should().Contain(e => e.FullName == "Data/Meshes/test.nif",
                "mesh file should preserve directory structure");
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
        result.Success.Should().BeFalse("backup with empty file list should fail");
        result.ErrorMessage.Should().Be("No files were backed up", "error message should indicate no files");
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
        result.Should().BeTrue("restore with progress should succeed");
        progressReports.Should().NotBeEmpty("progress should be reported during restore");
        progressReports.Should().OnlyContain(p => p.TotalFiles == 3, "all progress reports should show 3 total files");
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

        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, content, Encoding.UTF8);
    }
}
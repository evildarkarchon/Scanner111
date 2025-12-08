using FluentAssertions;
using NSubstitute;
using Scanner111.Services;
using Scanner111.ViewModels;
using System.Threading;
using Xunit;

namespace Scanner111.Tests;

public class BackupsViewModelTests
{
    private static BackupsViewModel CreateViewModel()
    {
        var backupService = Substitute.For<IBackupService>();
        var settingsService = Substitute.For<ISettingsService>();
        
        // Configure mock to return a result
        backupService.BackupAsync(Arg.Any<string>())
            .Returns(new BackupResult(true, "Test backup successful", 5));
        backupService.RestoreAsync(Arg.Any<string>())
            .Returns(new BackupResult(true, "Test restore successful", 3));
        backupService.RemoveAsync(Arg.Any<string>())
            .Returns(new BackupResult(true, "Test remove successful", 2));
        backupService.GetBackupFolderPath().Returns("C:\\Test\\Backups");
        
        return new BackupsViewModel(backupService, settingsService);
    }

    [Fact]
    public void AllBackupCommands_AreNotNull()
    {
        var viewModel = CreateViewModel();
        
        // XSE
        viewModel.BackupXseCommand.Should().NotBeNull();
        viewModel.RestoreXseCommand.Should().NotBeNull();
        viewModel.RemoveXseCommand.Should().NotBeNull();
        
        // Reshade
        viewModel.BackupReshadeCommand.Should().NotBeNull();
        viewModel.RestoreReshadeCommand.Should().NotBeNull();
        viewModel.RemoveReshadeCommand.Should().NotBeNull();
        
        // Vulkan
        viewModel.BackupVulkanCommand.Should().NotBeNull();
        viewModel.RestoreVulkanCommand.Should().NotBeNull();
        viewModel.RemoveVulkanCommand.Should().NotBeNull();
        
        // ENB
        viewModel.BackupEnbCommand.Should().NotBeNull();
        viewModel.RestoreEnbCommand.Should().NotBeNull();
        viewModel.RemoveEnbCommand.Should().NotBeNull();
    }

    [Fact]
    public void OpenBackupsFolderCommand_IsNotNull()
    {
        var viewModel = CreateViewModel();
        
        viewModel.OpenBackupsFolderCommand.Should().NotBeNull();
    }

    [Fact]
    public void StatusText_InitiallyEmpty()
    {
        var viewModel = CreateViewModel();
        
        viewModel.StatusText.Should().BeEmpty();
    }

    [Fact]
    public void IsOperationInProgress_InitiallyFalse()
    {
        var viewModel = CreateViewModel();
        viewModel.IsOperationInProgress.Should().BeFalse();
    }

    [Fact]
    public void BackupCommand_CallsBackupService()
    {
        var backupService = Substitute.For<IBackupService>();
        var settingsService = Substitute.For<ISettingsService>();
        backupService.BackupAsync("XSE")
            .Returns(new BackupResult(true, "Backed up successfully", 5));
        
        var viewModel = new BackupsViewModel(backupService, settingsService);
        
        viewModel.BackupXseCommand.Execute().Subscribe();
        
        // Wait for async operation to complete
        Thread.Sleep(100);
        
        backupService.Received().BackupAsync("XSE");
    }
}

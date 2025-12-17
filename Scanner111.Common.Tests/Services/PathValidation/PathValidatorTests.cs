using FluentAssertions;
using Moq;
using Scanner111.Common.Models.Configuration;
using Scanner111.Common.Services.PathValidation;
using Scanner111.Common.Services.Settings;

namespace Scanner111.Common.Tests.Services.PathValidation;

public class PathValidatorTests
{
    private readonly Mock<IUserSettingsService> _settingsService;
    private readonly PathValidator _validator;

    public PathValidatorTests()
    {
        _settingsService = new Mock<IUserSettingsService>();
        _validator = new PathValidator(_settingsService.Object);
    }

    #region IsValidPath Tests

    [Fact]
    public void IsValidPath_WithExistingDirectory_ReturnsTrue()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();

        // Act
        var result = _validator.IsValidPath(tempDir);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidPath_WithNonExistingPath_ReturnsFalse()
    {
        // Arrange
        var fakePath = @"C:\NonExistent\Path\That\Does\Not\Exist";

        // Act
        var result = _validator.IsValidPath(fakePath);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidPath_WithNull_ReturnsFalse()
    {
        // Act
        var result = _validator.IsValidPath(null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidPath_WithEmptyString_ReturnsFalse()
    {
        // Act
        var result = _validator.IsValidPath(string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidPath_WithWhitespace_ReturnsFalse()
    {
        // Act
        var result = _validator.IsValidPath("   ");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsDirectory Tests

    [Fact]
    public void IsDirectory_WithExistingDirectory_ReturnsTrue()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();

        // Act
        var result = _validator.IsDirectory(tempDir);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDirectory_WithExistingFile_ReturnsFalse()
    {
        // Arrange
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            // Act
            var result = _validator.IsDirectory(tempFile);

            // Assert
            result.Should().BeFalse();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void IsDirectory_WithNull_ReturnsFalse()
    {
        // Act
        var result = _validator.IsDirectory(null);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsFile Tests

    [Fact]
    public void IsFile_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var tempFile = System.IO.Path.GetTempFileName();
        try
        {
            // Act
            var result = _validator.IsFile(tempFile);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void IsFile_WithExistingDirectory_ReturnsFalse()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();

        // Act
        var result = _validator.IsFile(tempDir);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsFile_WithNull_ReturnsFalse()
    {
        // Act
        var result = _validator.IsFile(null);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ContainsFile Tests

    [Fact]
    public void ContainsFile_WithExistingFile_ReturnsTrue()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();
        var tempFile = System.IO.Path.GetTempFileName();
        var fileName = System.IO.Path.GetFileName(tempFile);
        try
        {
            // Act
            var result = _validator.ContainsFile(tempDir, fileName);

            // Assert
            result.Should().BeTrue();
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ContainsFile_WithNonExistingFile_ReturnsFalse()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();

        // Act
        var result = _validator.ContainsFile(tempDir, "NonExistentFile12345.xyz");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsFile_WithNullDirectory_ReturnsFalse()
    {
        // Act
        var result = _validator.ContainsFile(null, "file.txt");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region IsRestrictedPath Tests

    [Fact]
    public void IsRestrictedPath_WithWindowsDirectory_ReturnsTrue()
    {
        // Arrange
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        // Act
        var result = _validator.IsRestrictedPath(windowsDir);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRestrictedPath_WithProgramFiles_ReturnsTrue()
    {
        // Arrange
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);

        // Act
        var result = _validator.IsRestrictedPath(programFiles);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRestrictedPath_WithTempDirectory_ReturnsFalse()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();

        // Act
        var result = _validator.IsRestrictedPath(tempDir);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsRestrictedPath_WithNull_ReturnsTrue()
    {
        // Act
        var result = _validator.IsRestrictedPath(null);

        // Assert - null is considered restricted (fail-safe)
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRestrictedPath_WithEmptyString_ReturnsTrue()
    {
        // Act
        var result = _validator.IsRestrictedPath(string.Empty);

        // Assert - empty is considered restricted (fail-safe)
        result.Should().BeTrue();
    }

    #endregion

    #region ValidatePath Tests

    [Fact]
    public void ValidatePath_WithValidDirectory_ReturnsSuccess()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();
        var options = new PathValidationOptions { RequireDirectory = true };

        // Act
        var result = _validator.ValidatePath(tempDir, options);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Error.Should().Be(PathValidationError.None);
    }

    [Fact]
    public void ValidatePath_WithNull_ReturnsNullOrEmptyError()
    {
        // Act
        var result = _validator.ValidatePath(null);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(PathValidationError.NullOrEmpty);
    }

    [Fact]
    public void ValidatePath_WithNonExistingPath_ReturnsDoesNotExistError()
    {
        // Arrange
        var fakePath = @"C:\NonExistent\Path\That\Does\Not\Exist";

        // Act
        var result = _validator.ValidatePath(fakePath);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(PathValidationError.DoesNotExist);
    }

    [Fact]
    public void ValidatePath_WithFileWhenDirectoryRequired_ReturnsNotADirectoryError()
    {
        // Arrange
        var tempFile = System.IO.Path.GetTempFileName();
        var options = new PathValidationOptions { RequireDirectory = true };
        try
        {
            // Act
            var result = _validator.ValidatePath(tempFile, options);

            // Assert
            result.IsValid.Should().BeFalse();
            result.Error.Should().Be(PathValidationError.NotADirectory);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ValidatePath_WithRestrictedPath_ReturnsRestrictedPathError()
    {
        // Arrange
        var windowsDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var options = new PathValidationOptions { CheckRestricted = true };

        // Act
        var result = _validator.ValidatePath(windowsDir, options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(PathValidationError.RestrictedPath);
    }

    [Fact]
    public void ValidatePath_WithMissingRequiredFiles_ReturnsMissingFilesError()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();
        var options = new PathValidationOptions
        {
            RequireDirectory = true,
            RequiredFiles = new[] { "NonExistentRequiredFile12345.xyz" },
            CheckRestricted = false
        };

        // Act
        var result = _validator.ValidatePath(tempDir, options);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(PathValidationError.MissingRequiredFiles);
    }

    #endregion

    #region ValidateGameRootPath Tests

    [Fact]
    public void ValidateGameRootPath_WithValidPath_ReturnsSuccess()
    {
        // Arrange - Create a temp directory with a fake exe
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var exePath = System.IO.Path.Combine(tempDir, "Fallout4.exe");
        File.WriteAllText(exePath, "fake exe");
        try
        {
            // Act
            var result = _validator.ValidateGameRootPath(tempDir, "Fallout4");

            // Assert
            result.IsValid.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void ValidateGameRootPath_WithMissingExe_ReturnsFailure()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();

        // Act
        var result = _validator.ValidateGameRootPath(tempDir, "Fallout4");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(PathValidationError.MissingRequiredFiles);
    }

    [Fact]
    public void ValidateGameRootPath_WithUnknownGame_ReturnsFailure()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();

        // Act
        var result = _validator.ValidateGameRootPath(tempDir, "UnknownGame");

        // Assert
        result.IsValid.Should().BeFalse();
        result.Error.Should().Be(PathValidationError.InvalidPath);
    }

    [Fact]
    public void ValidateGameRootPath_WithVrMode_ChecksVrExecutable()
    {
        // Arrange - Create a temp directory with VR exe
        var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var exePath = System.IO.Path.Combine(tempDir, "Fallout4VR.exe");
        File.WriteAllText(exePath, "fake exe");
        try
        {
            // Act
            var result = _validator.ValidateGameRootPath(tempDir, "Fallout4", isVrMode: true);

            // Assert
            result.IsValid.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    #endregion

    #region ValidateAllSettingsPathsAsync Tests

    [Fact]
    public async Task ValidateAllSettingsPathsAsync_WithAllValidPaths_ReturnsAllValid()
    {
        // Arrange
        var tempDir = System.IO.Path.GetTempPath();
        var settings = new UserSettings
        {
            CustomScanPath = tempDir,
            ModsFolderPath = tempDir,
            IniFolderPath = tempDir,
            DocumentsPath = tempDir,
            IsVrMode = false
        };
        _settingsService.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);

        // Act
        var result = await _validator.ValidateAllSettingsPathsAsync("Fallout4");

        // Assert
        result.AllValid.Should().BeTrue();
        result.InvalidatedSettings.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAllSettingsPathsAsync_WithInvalidPath_ClearsSettingAndReturnsFailure()
    {
        // Arrange
        var fakePath = @"C:\NonExistent\Path\12345";
        var settings = new UserSettings
        {
            CustomScanPath = fakePath,
            IsVrMode = false
        };
        _settingsService.Setup(x => x.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(settings);
        _settingsService.Setup(x => x.SetCustomScanPathAsync(null, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _validator.ValidateAllSettingsPathsAsync("Fallout4");

        // Assert
        result.AllValid.Should().BeFalse();
        result.InvalidatedSettings.Should().Contain("CustomScanPath");
        _settingsService.Verify(x => x.SetCustomScanPathAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}

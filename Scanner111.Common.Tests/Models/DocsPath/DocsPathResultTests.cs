using FluentAssertions;
using Scanner111.Common.Models.DocsPath;
using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Tests.Models.DocsPath;

/// <summary>
/// Tests for the DocsPathResult record.
/// </summary>
public class DocsPathResultTests
{
    #region Factory Method Tests

    [Fact]
    public void Success_CreatesCorrectResult()
    {
        // Arrange
        var gameType = GameType.Fallout4;
        var docsPath = @"C:\Users\Test\Documents\My Games\Fallout4";
        var detectionMethod = DocsPathDetectionMethod.Registry;

        // Act
        var result = DocsPathResult.Success(gameType, docsPath, detectionMethod);

        // Assert
        result.Found.Should().BeTrue();
        result.GameType.Should().Be(gameType);
        result.DocsPath.Should().Be(docsPath);
        result.DetectionMethod.Should().Be(detectionMethod);
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Failure_CreatesCorrectResult()
    {
        // Arrange
        var gameType = GameType.SkyrimSE;
        var errorMessage = "Could not detect documents path";

        // Act
        var result = DocsPathResult.Failure(gameType, errorMessage);

        // Assert
        result.Found.Should().BeFalse();
        result.GameType.Should().Be(gameType);
        result.DocsPath.Should().BeNull();
        result.DetectionMethod.Should().Be(DocsPathDetectionMethod.NotFound);
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void Failure_WithoutErrorMessage_CreatesCorrectResult()
    {
        // Arrange
        var gameType = GameType.Fallout4VR;

        // Act
        var result = DocsPathResult.Failure(gameType);

        // Assert
        result.Found.Should().BeFalse();
        result.GameType.Should().Be(gameType);
        result.DocsPath.Should().BeNull();
        result.DetectionMethod.Should().Be(DocsPathDetectionMethod.NotFound);
        result.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region IsOneDrivePath Tests

    [Fact]
    public void IsOneDrivePath_WhenPathContainsOneDrive_ReturnsTrue()
    {
        // Arrange
        var result = DocsPathResult.Success(
            GameType.Fallout4,
            @"C:\Users\Test\OneDrive\Documents\My Games\Fallout4",
            DocsPathDetectionMethod.Registry);

        // Act & Assert
        result.IsOneDrivePath.Should().BeTrue();
    }

    [Fact]
    public void IsOneDrivePath_WhenPathContainsOneDrive_CaseInsensitive()
    {
        // Arrange
        var result = DocsPathResult.Success(
            GameType.Fallout4,
            @"C:\Users\Test\ONEDRIVE\Documents\My Games\Fallout4",
            DocsPathDetectionMethod.Registry);

        // Act & Assert
        result.IsOneDrivePath.Should().BeTrue();
    }

    [Theory]
    [InlineData(@"C:\Users\Test\OnEdRiVe\Documents\My Games\Fallout4")]
    [InlineData(@"C:\Users\Test\onedrive\Documents\My Games\Fallout4")]
    [InlineData(@"C:\OneDrive\Users\Test\Documents")]
    [InlineData(@"D:\OneDrive User\Documents")]
    public void IsOneDrivePath_WithVariousOneDrivePaths_ReturnsTrue(string path)
    {
        // Arrange
        var result = DocsPathResult.Success(
            GameType.Fallout4,
            path,
            DocsPathDetectionMethod.Registry);

        // Act & Assert
        result.IsOneDrivePath.Should().BeTrue();
    }

    [Theory]
    [InlineData(@"C:\Users\Test\Documents\My Games\Fallout4")]
    [InlineData(@"D:\Games\Documents\My Games\Fallout4")]
    [InlineData(@"E:\Steam\steamapps\common")]
    [InlineData(@"C:\Users\OneUser\Documents")]
    public void IsOneDrivePath_WithoutOneDrive_ReturnsFalse(string path)
    {
        // Arrange
        var result = DocsPathResult.Success(
            GameType.Fallout4,
            path,
            DocsPathDetectionMethod.Registry);

        // Act & Assert
        result.IsOneDrivePath.Should().BeFalse();
    }

    [Fact]
    public void IsOneDrivePath_WhenNotFound_ReturnsFalse()
    {
        // Arrange
        var result = DocsPathResult.Failure(GameType.Fallout4, "Not found");

        // Act & Assert
        result.IsOneDrivePath.Should().BeFalse();
    }

    [Fact]
    public void IsOneDrivePath_WhenPathIsNull_ReturnsFalse()
    {
        // Arrange - Create a result with Found=true but null path (edge case)
        var result = new DocsPathResult
        {
            GameType = GameType.Fallout4,
            Found = true,
            DocsPath = null,
            DetectionMethod = DocsPathDetectionMethod.Manual
        };

        // Act & Assert
        result.IsOneDrivePath.Should().BeFalse();
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Records_WithSameValues_AreEqual()
    {
        // Arrange
        var result1 = DocsPathResult.Success(
            GameType.Fallout4,
            @"C:\Users\Test\Documents\My Games\Fallout4",
            DocsPathDetectionMethod.Registry);

        var result2 = DocsPathResult.Success(
            GameType.Fallout4,
            @"C:\Users\Test\Documents\My Games\Fallout4",
            DocsPathDetectionMethod.Registry);

        // Act & Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void Records_WithDifferentPaths_AreNotEqual()
    {
        // Arrange
        var result1 = DocsPathResult.Success(
            GameType.Fallout4,
            @"C:\Users\Test\Documents\My Games\Fallout4",
            DocsPathDetectionMethod.Registry);

        var result2 = DocsPathResult.Success(
            GameType.Fallout4,
            @"D:\Different\Path",
            DocsPathDetectionMethod.Registry);

        // Act & Assert
        result1.Should().NotBe(result2);
    }

    #endregion
}

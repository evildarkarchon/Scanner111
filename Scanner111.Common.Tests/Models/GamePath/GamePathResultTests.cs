using FluentAssertions;
using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Tests.Models.GamePath;

/// <summary>
/// Tests for the GamePathResult record.
/// </summary>
public class GamePathResultTests
{
    #region Success Factory Tests

    [Fact]
    public void Success_CreatesFoundResult()
    {
        // Arrange
        var gameType = GameType.Fallout4;
        var gamePath = @"C:\Games\Fallout 4";
        var method = GamePathDetectionMethod.Registry;

        // Act
        var result = GamePathResult.Success(gameType, gamePath, method);

        // Assert
        result.Found.Should().BeTrue();
        result.GameType.Should().Be(gameType);
        result.GamePath.Should().Be(gamePath);
        result.DetectionMethod.Should().Be(method);
        result.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region Failure Factory Tests

    [Fact]
    public void Failure_CreatesNotFoundResult()
    {
        // Arrange
        var gameType = GameType.Fallout4;
        var errorMessage = "Game not found";

        // Act
        var result = GamePathResult.Failure(gameType, errorMessage);

        // Assert
        result.Found.Should().BeFalse();
        result.GameType.Should().Be(gameType);
        result.GamePath.Should().BeNull();
        result.DetectionMethod.Should().Be(GamePathDetectionMethod.NotFound);
        result.ErrorMessage.Should().Be(errorMessage);
    }

    [Fact]
    public void Failure_WithNoErrorMessage_CreatesNotFoundResult()
    {
        // Act
        var result = GamePathResult.Failure(GameType.SkyrimSE);

        // Assert
        result.Found.Should().BeFalse();
        result.GameType.Should().Be(GameType.SkyrimSE);
        result.ErrorMessage.Should().BeNull();
    }

    #endregion

    #region Computed Properties Tests

    [Fact]
    public void DataPath_WhenFound_ReturnsCorrectPath()
    {
        // Arrange
        var result = GamePathResult.Success(
            GameType.Fallout4,
            @"C:\Games\Fallout 4",
            GamePathDetectionMethod.Registry);

        // Act & Assert
        result.DataPath.Should().Be(@"C:\Games\Fallout 4\Data");
    }

    [Fact]
    public void DataPath_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var result = GamePathResult.Failure(GameType.Fallout4);

        // Act & Assert
        result.DataPath.Should().BeNull();
    }

    [Fact]
    public void ExecutablePath_WhenFound_ReturnsCorrectPath()
    {
        // Arrange
        var result = GamePathResult.Success(
            GameType.Fallout4,
            @"C:\Games\Fallout 4",
            GamePathDetectionMethod.Registry);

        // Act & Assert
        result.ExecutablePath.Should().Be(@"C:\Games\Fallout 4\Fallout4.exe");
    }

    [Fact]
    public void ExecutablePath_ForVR_ReturnsCorrectPath()
    {
        // Arrange
        var result = GamePathResult.Success(
            GameType.Fallout4VR,
            @"C:\Games\Fallout 4 VR",
            GamePathDetectionMethod.Registry);

        // Act & Assert
        result.ExecutablePath.Should().Be(@"C:\Games\Fallout 4 VR\Fallout4VR.exe");
    }

    [Fact]
    public void ExecutablePath_WhenNotFound_ReturnsNull()
    {
        // Arrange
        var result = GamePathResult.Failure(GameType.Fallout4);

        // Act & Assert
        result.ExecutablePath.Should().BeNull();
    }

    [Fact]
    public void ExecutablePath_ForSkyrimSE_ReturnsCorrectPath()
    {
        // Arrange
        var result = GamePathResult.Success(
            GameType.SkyrimSE,
            @"C:\Games\Skyrim Special Edition",
            GamePathDetectionMethod.XseLog);

        // Act & Assert
        result.ExecutablePath.Should().Be(@"C:\Games\Skyrim Special Edition\SkyrimSE.exe");
    }

    #endregion

    #region Detection Method Tests

    [Theory]
    [InlineData(GamePathDetectionMethod.Registry)]
    [InlineData(GamePathDetectionMethod.GogRegistry)]
    [InlineData(GamePathDetectionMethod.XseLog)]
    [InlineData(GamePathDetectionMethod.Cache)]
    [InlineData(GamePathDetectionMethod.Manual)]
    public void Success_PreservesDetectionMethod(GamePathDetectionMethod method)
    {
        // Act
        var result = GamePathResult.Success(
            GameType.Fallout4,
            @"C:\Games\Fallout 4",
            method);

        // Assert
        result.DetectionMethod.Should().Be(method);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void TwoSuccessResults_WithSameValues_AreEqual()
    {
        // Arrange
        var result1 = GamePathResult.Success(
            GameType.Fallout4,
            @"C:\Games\Fallout 4",
            GamePathDetectionMethod.Registry);

        var result2 = GamePathResult.Success(
            GameType.Fallout4,
            @"C:\Games\Fallout 4",
            GamePathDetectionMethod.Registry);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public void TwoSuccessResults_WithDifferentPaths_AreNotEqual()
    {
        // Arrange
        var result1 = GamePathResult.Success(
            GameType.Fallout4,
            @"C:\Games\Fallout 4",
            GamePathDetectionMethod.Registry);

        var result2 = GamePathResult.Success(
            GameType.Fallout4,
            @"D:\Games\Fallout 4",
            GamePathDetectionMethod.Registry);

        // Assert
        result1.Should().NotBe(result2);
    }

    [Fact]
    public void SuccessAndFailure_AreNotEqual()
    {
        // Arrange
        var success = GamePathResult.Success(
            GameType.Fallout4,
            @"C:\Games\Fallout 4",
            GamePathDetectionMethod.Registry);

        var failure = GamePathResult.Failure(GameType.Fallout4);

        // Assert
        success.Should().NotBe(failure);
    }

    #endregion
}

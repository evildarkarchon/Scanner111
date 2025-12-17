using FluentAssertions;
using Scanner111.Common.Models.GamePath;

namespace Scanner111.Common.Tests.Models.GamePath;

/// <summary>
/// Tests for the GameType enum and its extension methods.
/// </summary>
public class GameTypeTests
{
    #region GetExecutableName Tests

    [Theory]
    [InlineData(GameType.Fallout4, "Fallout4.exe")]
    [InlineData(GameType.Fallout4VR, "Fallout4VR.exe")]
    [InlineData(GameType.SkyrimSE, "SkyrimSE.exe")]
    [InlineData(GameType.SkyrimVR, "SkyrimVR.exe")]
    [InlineData(GameType.Unknown, "")]
    public void GetExecutableName_ReturnsCorrectValue(GameType gameType, string expected)
    {
        // Act
        var result = gameType.GetExecutableName();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetXseAcronym Tests

    [Theory]
    [InlineData(GameType.Fallout4, "F4SE")]
    [InlineData(GameType.Fallout4VR, "F4SEVR")]
    [InlineData(GameType.SkyrimSE, "SKSE64")]
    [InlineData(GameType.SkyrimVR, "SKSEVR")]
    [InlineData(GameType.Unknown, "")]
    public void GetXseAcronym_ReturnsCorrectValue(GameType gameType, string expected)
    {
        // Act
        var result = gameType.GetXseAcronym();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetXseAcronymBase Tests

    [Theory]
    [InlineData(GameType.Fallout4, "F4SE")]
    [InlineData(GameType.Fallout4VR, "F4SE")]
    [InlineData(GameType.SkyrimSE, "SKSE")]
    [InlineData(GameType.SkyrimVR, "SKSE")]
    [InlineData(GameType.Unknown, "")]
    public void GetXseAcronymBase_ReturnsCorrectValue(GameType gameType, string expected)
    {
        // Act
        var result = gameType.GetXseAcronymBase();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetXseLogFileName Tests

    [Theory]
    [InlineData(GameType.Fallout4, "f4se.log")]
    [InlineData(GameType.Fallout4VR, "f4sevr.log")]
    [InlineData(GameType.SkyrimSE, "skse64.log")]
    [InlineData(GameType.SkyrimVR, "sksevr.log")]
    [InlineData(GameType.Unknown, "")]
    public void GetXseLogFileName_ReturnsCorrectValue(GameType gameType, string expected)
    {
        // Act
        var result = gameType.GetXseLogFileName();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetRegistryKeyName Tests

    [Theory]
    [InlineData(GameType.Fallout4, "Fallout4")]
    [InlineData(GameType.Fallout4VR, "Fallout4VR")]
    [InlineData(GameType.SkyrimSE, "Skyrim Special Edition")]
    [InlineData(GameType.SkyrimVR, "SkyrimVR")]
    [InlineData(GameType.Unknown, "")]
    public void GetRegistryKeyName_ReturnsCorrectValue(GameType gameType, string expected)
    {
        // Act
        var result = gameType.GetRegistryKeyName();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetDisplayName Tests

    [Theory]
    [InlineData(GameType.Fallout4, "Fallout 4")]
    [InlineData(GameType.Fallout4VR, "Fallout 4 VR")]
    [InlineData(GameType.SkyrimSE, "Skyrim Special Edition")]
    [InlineData(GameType.SkyrimVR, "Skyrim VR")]
    [InlineData(GameType.Unknown, "Unknown")]
    public void GetDisplayName_ReturnsCorrectValue(GameType gameType, string expected)
    {
        // Act
        var result = gameType.GetDisplayName();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region GetMyGamesFolderName Tests

    [Theory]
    [InlineData(GameType.Fallout4, "Fallout4")]
    [InlineData(GameType.Fallout4VR, "Fallout4VR")]
    [InlineData(GameType.SkyrimSE, "Skyrim Special Edition")]
    [InlineData(GameType.SkyrimVR, "SkyrimVR")]
    [InlineData(GameType.Unknown, "")]
    public void GetMyGamesFolderName_ReturnsCorrectValue(GameType gameType, string expected)
    {
        // Act
        var result = gameType.GetMyGamesFolderName();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region IsVR Tests

    [Theory]
    [InlineData(GameType.Fallout4, false)]
    [InlineData(GameType.Fallout4VR, true)]
    [InlineData(GameType.SkyrimSE, false)]
    [InlineData(GameType.SkyrimVR, true)]
    [InlineData(GameType.Unknown, false)]
    public void IsVR_ReturnsCorrectValue(GameType gameType, bool expected)
    {
        // Act
        var result = gameType.IsVR();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region IsFallout Tests

    [Theory]
    [InlineData(GameType.Fallout4, true)]
    [InlineData(GameType.Fallout4VR, true)]
    [InlineData(GameType.SkyrimSE, false)]
    [InlineData(GameType.SkyrimVR, false)]
    [InlineData(GameType.Unknown, false)]
    public void IsFallout_ReturnsCorrectValue(GameType gameType, bool expected)
    {
        // Act
        var result = gameType.IsFallout();

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region IsSkyrim Tests

    [Theory]
    [InlineData(GameType.Fallout4, false)]
    [InlineData(GameType.Fallout4VR, false)]
    [InlineData(GameType.SkyrimSE, true)]
    [InlineData(GameType.SkyrimVR, true)]
    [InlineData(GameType.Unknown, false)]
    public void IsSkyrim_ReturnsCorrectValue(GameType gameType, bool expected)
    {
        // Act
        var result = gameType.IsSkyrim();

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}

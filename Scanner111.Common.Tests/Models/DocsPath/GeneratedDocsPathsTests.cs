using FluentAssertions;
using Scanner111.Common.Models.DocsPath;

namespace Scanner111.Common.Tests.Models.DocsPath;

/// <summary>
/// Tests for the GeneratedDocsPaths record.
/// </summary>
public class GeneratedDocsPathsTests
{
    [Fact]
    public void AllPaths_AreCorrectlySet()
    {
        // Arrange
        var rootPath = @"C:\Users\Test\Documents\My Games\Fallout4";
        var xseFolderPath = @"C:\Users\Test\Documents\My Games\Fallout4\F4SE";
        var papyrusLogPath = @"C:\Users\Test\Documents\My Games\Fallout4\Logs\Script\Papyrus.0.log";
        var wryeBashPath = @"C:\Users\Test\Documents\My Games\Fallout4\ModChecker.html";
        var xseLogPath = @"C:\Users\Test\Documents\My Games\Fallout4\F4SE\f4se.log";
        var mainIniPath = @"C:\Users\Test\Documents\My Games\Fallout4\Fallout4.ini";
        var customIniPath = @"C:\Users\Test\Documents\My Games\Fallout4\Fallout4Custom.ini";
        var prefsIniPath = @"C:\Users\Test\Documents\My Games\Fallout4\Fallout4Prefs.ini";

        // Act
        var paths = new GeneratedDocsPaths
        {
            RootPath = rootPath,
            XseFolderPath = xseFolderPath,
            PapyrusLogPath = papyrusLogPath,
            WryeBashModCheckerPath = wryeBashPath,
            XseLogPath = xseLogPath,
            MainIniPath = mainIniPath,
            CustomIniPath = customIniPath,
            PrefsIniPath = prefsIniPath
        };

        // Assert
        paths.RootPath.Should().Be(rootPath);
        paths.XseFolderPath.Should().Be(xseFolderPath);
        paths.PapyrusLogPath.Should().Be(papyrusLogPath);
        paths.WryeBashModCheckerPath.Should().Be(wryeBashPath);
        paths.XseLogPath.Should().Be(xseLogPath);
        paths.MainIniPath.Should().Be(mainIniPath);
        paths.CustomIniPath.Should().Be(customIniPath);
        paths.PrefsIniPath.Should().Be(prefsIniPath);
    }

    [Fact]
    public void Records_WithSameValues_AreEqual()
    {
        // Arrange
        var paths1 = new GeneratedDocsPaths
        {
            RootPath = @"C:\Test",
            XseFolderPath = @"C:\Test\F4SE",
            PapyrusLogPath = @"C:\Test\Logs\Script\Papyrus.0.log",
            WryeBashModCheckerPath = @"C:\Test\ModChecker.html",
            XseLogPath = @"C:\Test\F4SE\f4se.log",
            MainIniPath = @"C:\Test\Fallout4.ini",
            CustomIniPath = @"C:\Test\Fallout4Custom.ini",
            PrefsIniPath = @"C:\Test\Fallout4Prefs.ini"
        };

        var paths2 = new GeneratedDocsPaths
        {
            RootPath = @"C:\Test",
            XseFolderPath = @"C:\Test\F4SE",
            PapyrusLogPath = @"C:\Test\Logs\Script\Papyrus.0.log",
            WryeBashModCheckerPath = @"C:\Test\ModChecker.html",
            XseLogPath = @"C:\Test\F4SE\f4se.log",
            MainIniPath = @"C:\Test\Fallout4.ini",
            CustomIniPath = @"C:\Test\Fallout4Custom.ini",
            PrefsIniPath = @"C:\Test\Fallout4Prefs.ini"
        };

        // Act & Assert
        paths1.Should().Be(paths2);
    }

    [Fact]
    public void Records_WithDifferentRootPath_AreNotEqual()
    {
        // Arrange
        var paths1 = new GeneratedDocsPaths
        {
            RootPath = @"C:\Test1",
            XseFolderPath = @"C:\Test1\F4SE",
            PapyrusLogPath = @"C:\Test1\Logs\Script\Papyrus.0.log",
            WryeBashModCheckerPath = @"C:\Test1\ModChecker.html",
            XseLogPath = @"C:\Test1\F4SE\f4se.log",
            MainIniPath = @"C:\Test1\Fallout4.ini",
            CustomIniPath = @"C:\Test1\Fallout4Custom.ini",
            PrefsIniPath = @"C:\Test1\Fallout4Prefs.ini"
        };

        var paths2 = new GeneratedDocsPaths
        {
            RootPath = @"C:\Test2", // Different
            XseFolderPath = @"C:\Test2\F4SE",
            PapyrusLogPath = @"C:\Test2\Logs\Script\Papyrus.0.log",
            WryeBashModCheckerPath = @"C:\Test2\ModChecker.html",
            XseLogPath = @"C:\Test2\F4SE\f4se.log",
            MainIniPath = @"C:\Test2\Fallout4.ini",
            CustomIniPath = @"C:\Test2\Fallout4Custom.ini",
            PrefsIniPath = @"C:\Test2\Fallout4Prefs.ini"
        };

        // Act & Assert
        paths1.Should().NotBe(paths2);
    }
}

using System;
using System.IO;
using System.Linq;
using Scanner111.Core.Infrastructure;
using Xunit;

namespace Scanner111.Tests.Infrastructure;

public class CrashLogDirectoryManagerTests
{
    [Fact]
    public void GetDefaultCrashLogsDirectory_ReturnsValidPath()
    {
        var result = CrashLogDirectoryManager.GetDefaultCrashLogsDirectory();
        
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("Scanner111", result);
        Assert.Contains("Crash Logs", result);
    }
    
    [Theory]
    [InlineData("C:\\Games\\Fallout4", "Fallout4")]
    [InlineData("C:\\Games\\Fallout4VR", "Fallout4VR")]
    [InlineData("C:\\Games\\SkyrimSE", "SkyrimSE")]
    [InlineData("C:\\Games\\Skyrim", "Skyrim")]
    public void DetectGameType_FromGamePath_ReturnsCorrectType(string gamePath, string expectedType)
    {
        // Create temporary directory structure
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        try
        {
            Directory.CreateDirectory(tempPath);
            var exeName = expectedType switch
            {
                "Fallout4" => "Fallout4.exe",
                "Fallout4VR" => "Fallout4VR.exe", 
                "SkyrimSE" => "SkyrimSE.exe",
                "Skyrim" => "Skyrim.exe",
                _ => "Fallout4.exe"
            };
            File.WriteAllText(Path.Combine(tempPath, exeName), "dummy");
            
            var result = CrashLogDirectoryManager.DetectGameType(tempPath);
            
            Assert.Equal(expectedType, result);
        }
        finally
        {
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
        }
    }
    
    [Fact]
    public void DetectGameType_NoGamePath_ReturnsDefaultFallout4()
    {
        var result = CrashLogDirectoryManager.DetectGameType();
        
        Assert.Equal("Fallout4", result);
    }
    
    [Fact]
    public void GetGameSpecificDirectory_CombinesPathsCorrectly()
    {
        var baseDir = "C:\\CrashLogs";
        var gameType = "Fallout4";
        
        var result = CrashLogDirectoryManager.GetGameSpecificDirectory(baseDir, gameType);
        
        Assert.Equal("C:\\CrashLogs\\Fallout4", result);
    }
    
    [Fact]
    public void EnsureDirectoryExists_CreatesDirectory()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var gameType = "Fallout4";
        
        try
        {
            var result = CrashLogDirectoryManager.EnsureDirectoryExists(tempPath, gameType);
            
            Assert.True(Directory.Exists(result));
            Assert.Equal(Path.Combine(tempPath, gameType), result);
        }
        finally
        {
            if (Directory.Exists(tempPath))
                Directory.Delete(tempPath, true);
        }
    }
    
    [Fact]
    public void GetTargetPath_ReturnsCorrectPath()
    {
        var tempBaseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var gameType = "Fallout4";
        var originalFile = "crash-2024-01-01-12-00-00.log";
        
        try
        {
            var result = CrashLogDirectoryManager.GetTargetPath(tempBaseDir, gameType, originalFile);
            
            var expectedPath = Path.Combine(tempBaseDir, gameType, originalFile);
            Assert.Equal(expectedPath, result);
            Assert.True(Directory.Exists(Path.GetDirectoryName(result)));
        }
        finally
        {
            if (Directory.Exists(tempBaseDir))
                Directory.Delete(tempBaseDir, true);
        }
    }
    
    [Fact]
    public void CopyCrashLog_CopiesFileToCorrectLocation()
    {
        var tempSourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempBaseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fileName = "crash-2024-01-01-12-00-00.log";
        var gameType = "Fallout4";
        
        try
        {
            // Create source file
            Directory.CreateDirectory(tempSourceDir);
            var sourceFile = Path.Combine(tempSourceDir, fileName);
            File.WriteAllText(sourceFile, "test crash log content");
            
            var result = CrashLogDirectoryManager.CopyCrashLog(sourceFile, tempBaseDir, gameType);
            
            var expectedPath = Path.Combine(tempBaseDir, gameType, fileName);
            Assert.Equal(expectedPath, result);
            Assert.True(File.Exists(result));
            Assert.Equal("test crash log content", File.ReadAllText(result));
        }
        finally
        {
            if (Directory.Exists(tempSourceDir))
                Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempBaseDir))
                Directory.Delete(tempBaseDir, true);
        }
    }
    
    [Fact]
    public void CopyCrashLog_AutoDetectsGameType()
    {
        var tempSourceDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var tempBaseDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fileName = "crash-2024-01-01-12-00-00.log";
        
        try
        {
            // Create source file with fallout4 content
            Directory.CreateDirectory(tempSourceDir);
            var sourceFile = Path.Combine(tempSourceDir, fileName);
            File.WriteAllText(sourceFile, "System: Fallout4.exe version 1.10.984.0");
            
            var result = CrashLogDirectoryManager.CopyCrashLog(sourceFile, tempBaseDir);
            
            // Should auto-detect as Fallout4
            var expectedPath = Path.Combine(tempBaseDir, "Fallout4", fileName);
            Assert.Equal(expectedPath, result);
            Assert.True(File.Exists(result));
        }
        finally
        {
            if (Directory.Exists(tempSourceDir))
                Directory.Delete(tempSourceDir, true);
            if (Directory.Exists(tempBaseDir))
                Directory.Delete(tempBaseDir, true);
        }
    }
    
    [Fact]
    public void DetectGameType_FromXSEPath_ReturnsCorrectType()
    {
        // Test F4SE path detection
        var f4sePath = "C:\\Users\\Test\\Documents\\My Games\\Fallout4\\F4SE\\crash-test.log";
        var result = CrashLogDirectoryManager.DetectGameType(crashLogPath: f4sePath);
        Assert.Equal("Fallout4", result);
        
        // Test F4SE VR path detection
        var f4seVRPath = "C:\\Users\\Test\\Documents\\My Games\\Fallout4VR\\F4SE\\crash-test.log";
        result = CrashLogDirectoryManager.DetectGameType(crashLogPath: f4seVRPath);
        Assert.Equal("Fallout4VR", result);
        
        // Test SKSE SE path detection
        var skseSEPath = "C:\\Users\\Test\\Documents\\My Games\\Skyrim Special Edition\\SKSE\\crash-test.log";
        result = CrashLogDirectoryManager.DetectGameType(crashLogPath: skseSEPath);
        Assert.Equal("SkyrimSE", result);
    }
    
    [Theory]
    [InlineData("SkyrimSE.exe detected", "SkyrimSE")]
    [InlineData("Skyrim Special Edition crashed", "SkyrimSE")]
    [InlineData("System Info: Fallout4VR.exe", "Fallout4VR")]
    public void DetectGameType_FromLogContent_ReturnsCorrectType(string logContent, string expectedType)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fileName = "crash-test.log";
        
        try
        {
            Directory.CreateDirectory(tempDir);
            var logFile = Path.Combine(tempDir, fileName);
            File.WriteAllText(logFile, logContent);
            
            var result = CrashLogDirectoryManager.DetectGameType(crashLogPath: logFile);
            
            Assert.Equal(expectedType, result);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
using System.Reflection;

namespace Scanner111.Test.Integration;

/// <summary>
///     Simple test to verify embedded resources are properly included in the assembly.
/// </summary>
public class SimpleEmbeddedResourceTest
{
    [Fact]
    public void VerifyEmbeddedResourcesExist()
    {
        // Get the test assembly
        var assembly = typeof(SimpleEmbeddedResourceTest).Assembly;
        
        // Get all embedded resource names
        var resourceNames = assembly.GetManifestResourceNames();
        
        // Check if we have any embedded resources
        Assert.NotEmpty(resourceNames);
        
        // Check for specific critical samples
        var expectedResources = new[]
        {
            "Scanner111.Test.Resources.EmbeddedLogs.crash-2022-06-05-12-52-17.log",
            "Scanner111.Test.Resources.EmbeddedLogs.crash-2022-06-09-07-25-03.log",
            "Scanner111.Test.Resources.EmbeddedLogs.crash-2022-06-12-07-11-38.log",
            "Scanner111.Test.Resources.EmbeddedLogs.crash-2022-06-15-10-02-51.log"
        };
        
        foreach (var expected in expectedResources)
        {
            Assert.Contains(expected, resourceNames);
        }
    }
    
    [Fact]
    public void CanReadEmbeddedResource()
    {
        // Get the test assembly
        var assembly = typeof(SimpleEmbeddedResourceTest).Assembly;
        
        // Try to read a specific resource
        var resourceName = "Scanner111.Test.Resources.EmbeddedLogs.crash-2022-06-05-12-52-17.log";
        
        using var stream = assembly.GetManifestResourceStream(resourceName);
        Assert.NotNull(stream);
        
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();
        
        Assert.NotEmpty(content);
        Assert.Contains("Unhandled exception", content);
    }
}
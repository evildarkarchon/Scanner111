using Scanner111.Application.Interfaces.Services;
using Scanner111.Core.Interfaces.Services;

namespace Scanner111.Infrastructure.Services;
public class ClassicYamlCompatibilityService : IYamlCompatibilityService
{
    private readonly IFileSystemService _fileSystemService;
    
    public ClassicYamlCompatibilityService(IFileSystemService fileSystemService)
    {
        _fileSystemService = fileSystemService;
    }
    
    public async Task<Dictionary<string, object>> LoadYamlFileAsync(string filePath)
    {
        if (!await _fileSystemService.FileExistsAsync(filePath))
            return new Dictionary<string, object>();
            
        var yamlContent = await _fileSystemService.ReadAllTextAsync(filePath);
        
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();
            
        return deserializer.Deserialize<Dictionary<string, object>>(yamlContent);
    }
    
    public async Task<T?> LoadYamlFileTypedAsync<T>(string filePath) where T : class, new()
    {
        if (!await _fileSystemService.FileExistsAsync(filePath))
            return null;
            
        var yamlContent = await _fileSystemService.ReadAllTextAsync(filePath);
        
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
            .WithNamingConvention(YamlDotNet.Serialization.NamingConventions.CamelCaseNamingConvention.Instance)
            .Build();
            
        return deserializer.Deserialize<T>(yamlContent);
    }
    
    public async Task<Dictionary<string, string>> LoadCrashSuspectsAsync(string yamlPath, string sectionName)
    {
        var yamlData = await LoadYamlFileAsync(yamlPath);
        if (!yamlData.TryGetValue(sectionName, out var section) || section is not Dictionary<string, object> sectionDict)
            return new Dictionary<string, string>();
            
        var result = new Dictionary<string, string>();
        
        foreach (var item in sectionDict)
        {
            if (item.Value is string stringValue)
            {
                // Handle the format "5 | Stack Overflow Crash: EXCEPTION_STACK_OVERFLOW"
                var parts = item.Key.Split('|', 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var severity))
                {
                    var name = parts[1].Trim();
                    result[name] = stringValue;
                }
            }
        }
        
        return result;
    }
    
    public async Task<Dictionary<string, List<string>>> LoadCrashStackCheckAsync(string yamlPath)
    {
        var yamlData = await LoadYamlFileAsync(yamlPath);
        if (!yamlData.TryGetValue("Crashlog_Stack_Check", out var section) || section is not Dictionary<string, object> sectionDict)
            return new Dictionary<string, List<string>>();
            
        var result = new Dictionary<string, List<string>>();
        
        foreach (var item in sectionDict)
        {
            if (item.Value is List<object> listValue)
            {
                // Handle the format "5 | Scaleform Gfx Crash: [...]"
                var parts = item.Key.Split('|', 2);
                if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out var severity))
                {
                    var name = parts[1].Trim();
                    result[name] = listValue.Select(v => v?.ToString() ?? string.Empty).ToList();
                }
            }
        }
        
        return result;
    }
    
    public async Task<Dictionary<string, string>> LoadModsListAsync(string yamlPath, string sectionName)
    {
        var yamlData = await LoadYamlFileAsync(yamlPath);
        if (!yamlData.TryGetValue(sectionName, out var section) || section is not Dictionary<string, object> sectionDict)
            return new Dictionary<string, string>();
            
        var result = new Dictionary<string, string>();
        
        foreach (var item in sectionDict)
        {
            if (item.Value is string stringValue)
            {
                result[item.Key] = stringValue;
            }
        }
        
        return result;
    }

    public Task SaveYamlFileAsync(string filePath, object data)
    {
        throw new NotImplementedException();
    }
}
using Bogus;
using System.Text;
using Scanner111.Core.Models;

namespace Scanner111.Test.Infrastructure.TestData;

/// <summary>
///     Generates synthetic crash log data for testing using the Bogus library.
///     Creates realistic but deterministic test data without external dependencies.
/// </summary>
public class CrashLogDataGenerator
{
    private readonly Faker _faker;
    private readonly Random _random;
    
    // Common plugin names for realistic data
    private static readonly string[] CommonPlugins = 
    {
        "Fallout4.esm", "DLCRobot.esm", "DLCworkshop01.esm", "DLCCoast.esm", 
        "DLCworkshop02.esm", "DLCworkshop03.esm", "DLCNukaWorld.esm",
        "Unofficial Fallout 4 Patch.esp", "ArmorKeywords.esm", "HUDFramework.esm",
        "WorkshopFramework.esm", "SS2.esm", "SS2_XPAC_Chapter2.esm",
        "LooksMenu.esp", "LooksMenu Customization Compendium.esp",
        "Armorsmith Extended.esp", "Vivid Fallout - All in One - Best Choice.esp",
        "Better Settlers.esp", "OCDecorator.esp", "OCDispenser.esp",
        "Homemaker.esm", "SettlementMenuManager.esp", "TransferSettlements.esp"
    };

    // Common error types
    private static readonly string[] ErrorTypes = 
    {
        "EXCEPTION_ACCESS_VIOLATION", "EXCEPTION_STACK_OVERFLOW", 
        "EXCEPTION_INT_DIVIDE_BY_ZERO", "EXCEPTION_ARRAY_BOUNDS_EXCEEDED",
        "EXCEPTION_INVALID_HANDLE", "EXCEPTION_ILLEGAL_INSTRUCTION"
    };

    // Common problematic addresses
    private static readonly string[] ProblematicAddresses = 
    {
        "Fallout4.exe+2B5E2A0", "Fallout4.exe+D6A2D9", "Fallout4.exe+1573ADD",
        "nvwgf2umx.dll+8A4FF0", "XAudio2_7.dll+7628A", "KERNELBASE.dll+2E554",
        "Buffout4.dll+5A8E20", "F4EE.dll+14FB9"
    };

    public CrashLogDataGenerator(int? seed = null)
    {
        _faker = seed.HasValue ? new Faker { Random = new Randomizer(seed.Value) } : new Faker();
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    ///     Generates a complete synthetic crash log with specified characteristics.
    /// </summary>
    public string GenerateCrashLog(CrashLogOptions? options = null)
    {
        options ??= new CrashLogOptions();
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine($"Fallout 4 v{options.GameVersion}");
        sb.AppendLine($"Buffout 4 v{options.BuffoutVersion}");
        sb.AppendLine();
        sb.AppendLine($"Unhandled exception \"{options.ErrorType}\" at {options.ErrorAddress}");
        sb.AppendLine();
        
        // System Specs
        GenerateSystemSpecs(sb, options);
        
        // Settings
        GenerateSettings(sb, options);
        
        // Plugins
        GeneratePluginList(sb, options);
        
        // Callstack
        GenerateCallstack(sb, options);
        
        // Registers (if access violation)
        if (options.ErrorType == "EXCEPTION_ACCESS_VIOLATION")
        {
            GenerateRegisters(sb);
        }
        
        // Stack
        GenerateStack(sb);
        
        return sb.ToString();
    }

    /// <summary>
    ///     Generates a minimal crash log for edge case testing.
    /// </summary>
    public string GenerateMinimalCrashLog()
    {
        return GenerateCrashLog(new CrashLogOptions
        {
            PluginCount = 5,
            CallstackDepth = 3,
            IncludeSystemSpecs = false,
            IncludeStack = false
        });
    }

    /// <summary>
    ///     Generates a crash log with specific plugin issues.
    /// </summary>
    public string GenerateCrashLogWithPluginIssues(params string[] problematicPlugins)
    {
        var options = new CrashLogOptions
        {
            PluginCount = 50,
            CallstackDepth = 15,
            ProblematicPlugins = problematicPlugins,
            ErrorType = "EXCEPTION_ACCESS_VIOLATION"
        };
        
        return GenerateCrashLog(options);
    }

    /// <summary>
    ///     Generates crash log settings section for testing validators.
    /// </summary>
    public CrashGenSettings GenerateCrashGenSettings(bool valid = true)
    {
        if (valid)
        {
            return new CrashGenSettings
            {
                CrashGenName = "Buffout",
                Version = new Version(1, 26, 2),
                Achievements = false,
                MemoryManager = true,
                ArchiveLimit = true,
                F4EE = _faker.Random.Bool()
            };
        }
        
        // Generate problematic settings
        return new CrashGenSettings
        {
            CrashGenName = "Buffout",
            Version = new Version(1, _faker.Random.Int(20, 30), _faker.Random.Int(0, 5)),
            Achievements = _faker.Random.Bool(0.7f), // 70% chance of incorrect true
            MemoryManager = _faker.Random.Bool(0.3f), // 30% chance of incorrect false
            ArchiveLimit = _faker.Random.Bool(0.2f), // 20% chance of incorrect false
            F4EE = _faker.Random.Bool()
        };
    }

    /// <summary>
    ///     Generates realistic mod detection settings.
    /// </summary>
    public ModDetectionSettings GenerateModDetectionSettings()
    {
        var xseModules = new HashSet<string>();
        
        // Randomly add XSE modules
        if (_faker.Random.Bool(0.3f))
            xseModules.Add("achievements.dll");
        if (_faker.Random.Bool(0.4f))
            xseModules.Add("f4ee.dll");
        if (_faker.Random.Bool(0.2f))
            xseModules.Add("x-cell-fo4.dll");
        if (_faker.Random.Bool(0.15f))
            xseModules.Add("bakascrapheap.dll");
        
        var crashLogPlugins = new Dictionary<string, string>();
        foreach (var plugin in CommonPlugins.Take(_faker.Random.Int(10, 50)))
        {
            crashLogPlugins[plugin] = $"v{_faker.Random.Int(1, 5)}.{_faker.Random.Int(0, 9)}";
        }
        
        return ModDetectionSettings.FromDetectionData(
            xseModules: xseModules,
            crashLogPlugins: crashLogPlugins,
            fcxMode: _faker.Random.Bool(0.5f),
            detectedGpuType: _faker.Random.ArrayElement(new[] { "NVIDIA", "AMD", "Intel" })
        );
    }

    private void GenerateSystemSpecs(StringBuilder sb, CrashLogOptions options)
    {
        if (!options.IncludeSystemSpecs) return;
        
        sb.AppendLine("SYSTEM SPECS:");
        sb.AppendLine($"\tOS: {_faker.System.Version()} Build {_faker.Random.Int(19041, 22631)}.{_faker.Random.Int(100, 3000)}");
        sb.AppendLine($"\tCPU: {GenerateCpuModel()}");
        sb.AppendLine($"\tGPU: {GenerateGpuModel()}");
        sb.AppendLine($"\tRAM: {_faker.Random.ArrayElement(new[] { "8", "16", "32", "64" })}.00 GB");
        sb.AppendLine();
    }

    private void GenerateSettings(StringBuilder sb, CrashLogOptions options)
    {
        sb.AppendLine("SETTINGS:");
        sb.AppendLine($"\tAchievements: {options.Settings.Achievements?.ToString().ToLower() ?? "false"}");
        sb.AppendLine($"\tMemoryManager: {options.Settings.MemoryManager?.ToString().ToLower() ?? "true"}");
        sb.AppendLine($"\tArchiveLimit: {options.Settings.ArchiveLimit?.ToString().ToLower() ?? "true"}");
        sb.AppendLine($"\tF4EE: {options.Settings.F4EE?.ToString().ToLower() ?? "false"}");
        sb.AppendLine();
    }

    private void GeneratePluginList(StringBuilder sb, CrashLogOptions options)
    {
        sb.AppendLine($"PLUGINS ({options.PluginCount}):");
        
        var plugins = new List<string>();
        
        // Add base game plugins
        plugins.AddRange(CommonPlugins.Take(7));
        
        // Add problematic plugins if specified
        if (options.ProblematicPlugins?.Length > 0)
        {
            plugins.AddRange(options.ProblematicPlugins);
        }
        
        // Fill remaining with random plugins
        while (plugins.Count < options.PluginCount)
        {
            var plugin = _faker.Random.ArrayElement(CommonPlugins.Skip(7).ToArray());
            if (!plugins.Contains(plugin))
            {
                plugins.Add(plugin);
            }
            else
            {
                plugins.Add($"{_faker.Lorem.Word()}.esp");
            }
        }
        
        // Generate plugin entries
        for (int i = 0; i < Math.Min(plugins.Count, options.PluginCount); i++)
        {
            var formId = $"[{i:X2}] {_faker.Random.Hexadecimal(8, string.Empty)}";
            sb.AppendLine($"\t{formId}: {plugins[i]}");
        }
        
        sb.AppendLine();
    }

    private void GenerateCallstack(StringBuilder sb, CrashLogOptions options)
    {
        sb.AppendLine("CALLSTACK:");
        
        for (int i = 0; i < options.CallstackDepth; i++)
        {
            var address = _faker.Random.ArrayElement(ProblematicAddresses);
            
            // Add problematic plugin references if specified
            if (options.ProblematicPlugins?.Length > 0 && i < 3)
            {
                var plugin = options.ProblematicPlugins[_random.Next(options.ProblematicPlugins.Length)];
                address = $"{Path.GetFileNameWithoutExtension(plugin)}.dll+{_faker.Random.Hexadecimal(6, string.Empty)}";
            }
            
            sb.AppendLine($"\t[{i}] {address}");
        }
        
        sb.AppendLine();
    }

    private void GenerateRegisters(StringBuilder sb)
    {
        sb.AppendLine("REGISTERS:");
        sb.AppendLine($"\tRAX: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine($"\tRBX: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine($"\tRCX: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine($"\tRDX: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine($"\tRSI: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine($"\tRDI: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine($"\tRBP: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine($"\tRSP: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine($"\tRIP: {_faker.Random.Hexadecimal(16, "0x")}");
        sb.AppendLine();
    }

    private void GenerateStack(StringBuilder sb)
    {
        if (!sb.ToString().Contains("STACK:"))
        {
            sb.AppendLine("STACK:");
            for (int i = 0; i < 8; i++)
            {
                sb.AppendLine($"\t[RSP+{i * 8:X2}] {_faker.Random.Hexadecimal(16, "0x")}");
            }
            sb.AppendLine();
        }
    }

    private string GenerateCpuModel()
    {
        var cpus = new[]
        {
            "Intel Core i5-10400 CPU @ 2.90GHz",
            "Intel Core i7-12700K CPU @ 3.60GHz",
            "Intel Core i9-13900K CPU @ 3.00GHz",
            "AMD Ryzen 5 5600X 6-Core Processor",
            "AMD Ryzen 7 5800X3D 8-Core Processor",
            "AMD Ryzen 9 7950X 16-Core Processor"
        };
        return _faker.Random.ArrayElement(cpus);
    }

    private string GenerateGpuModel()
    {
        var gpus = new[]
        {
            "NVIDIA GeForce GTX 1060 6GB",
            "NVIDIA GeForce RTX 2070 SUPER",
            "NVIDIA GeForce RTX 3080",
            "NVIDIA GeForce RTX 4070 Ti",
            "AMD Radeon RX 6600 XT",
            "AMD Radeon RX 7900 XTX"
        };
        return _faker.Random.ArrayElement(gpus);
    }
}

/// <summary>
///     Options for generating synthetic crash logs.
/// </summary>
public class CrashLogOptions
{
    public string GameVersion { get; set; } = "1.10.163.0";
    public string BuffoutVersion { get; set; } = "1.26.2";
    public string ErrorType { get; set; } = "EXCEPTION_ACCESS_VIOLATION";
    public string ErrorAddress { get; set; } = "0x7FF7012B5E2A0";
    public int PluginCount { get; set; } = 100;
    public int CallstackDepth { get; set; } = 10;
    public bool IncludeSystemSpecs { get; set; } = true;
    public bool IncludeStack { get; set; } = true;
    public string[]? ProblematicPlugins { get; set; }
    public CrashGenSettings Settings { get; set; } = new()
    {
        CrashGenName = "Buffout",
        Version = new Version(1, 26, 2),
        Achievements = false,
        MemoryManager = true,
        ArchiveLimit = true,
        F4EE = false
    };
}
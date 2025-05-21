using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Collections.Concurrent;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using YamlDotNet.RepresentationModel;
using Scanner111.Models; // For YAML enum

namespace Scanner111.Services
{
    public enum YamlStoreType
    {
        Main,
        Settings,
        Ignore,
        Game,
        GameLocal,
        Test
    }

    public sealed class YamlSettingsCacheService
    {
        private static readonly Lazy<YamlSettingsCacheService> _instance =
            new(() => new YamlSettingsCacheService());

        public static YamlSettingsCacheService Instance => _instance.Value;

        private readonly ConcurrentDictionary<string, IDictionary<object, object?>> _yamlDataCache = new();

        private readonly IDeserializer _yamlDeserializer;
        private readonly ISerializer _yamlSerializer;

        private static readonly HashSet<YamlStoreType> StaticYamlStores =
        [
            YamlStoreType.Main, YamlStoreType.Game
        ];

        // Configuration for paths, can be updated via ConfigurePaths
        private string _gameName = "Fallout4"; // Default, mirrors GlobalRegistry.get_game()
        private string _applicationRootPath;
        private string _classicDataBasePath = Path.Combine("CLASSIC Data", "databases");
        private string _classicDataPath = Path.Combine("CLASSIC Data");
        private string _testsPath = "tests";

        private readonly Dictionary<YAML, YamlNode> _yamlCache = new Dictionary<YAML, YamlNode>();
        private readonly Dictionary<YAML, string> _yamlFilePaths = new Dictionary<YAML, string>(); // Maps enum to actual file paths

        private YamlSettingsCacheService()
        {
            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(NullNamingConvention.Instance) // Keeps keys as defined in YAML
                .Build();
            _yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(NullNamingConvention.Instance)
                .ConfigureDefaultValuesHandling(DefaultValuesHandling.Preserve) // Preserve default values during serialization
                .Build();

            // Attempt to set a sensible default for the application root path.
            _applicationRootPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppDomain.CurrentDomain.BaseDirectory;

            InitializeStaticCaches();

            // Initialize _yamlFilePaths based on AppSettings or a configuration mechanism
            // This is crucial for mapping the YAML enum to actual file locations.
            // Example (you'll need to adapt this to your actual settings structure):
            // _yamlFilePaths[YAML.Main] = appSettings.MainYamlPath;
            // _yamlFilePaths[YAML.Game] = appSettings.GameSpecificYamlPath; 
            // ... and so on for all YAML types you intend to use.

            // For now, using placeholder paths for demonstration. Replace with actual paths.
            // Ensure these paths are correct and the files exist.
            string baseDataPath = Path.Combine(Directory.GetCurrentDirectory(), "CLASSIC Data", "databases"); // Example base path
            _yamlFilePaths[YAML.Game] = Path.Combine(baseDataPath, "CLASSIC Fallout4.yaml"); // Placeholder
            // Add other mappings here, e.g., for YAML.Main, YAML.Settings etc.

            // Preload YAML files if desired, or load them on demand.
        }

        private void InitializeStaticCaches()
        {
            foreach (YamlStoreType storeType in StaticYamlStores)
            {
                try
                {
                    string filePath = GetPathForStore(storeType);
                    LoadAndCacheYaml(filePath);
                }
                catch (Exception ex)
                {
                    // Log this error appropriately in a real application
                    Console.WriteLine($"Error pre-loading static YAML store {storeType}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Configures the base paths used for resolving YAML file locations.
        /// Should be called early in application startup if defaults are not suitable.
        /// </summary>
        public void ConfigurePaths(string applicationRootPath, string? gameName = null, string? classicDataBasePath = null, string? classicDataPath = null, string? testsPath = null)
        {
            _applicationRootPath = applicationRootPath ?? throw new ArgumentNullException(nameof(applicationRootPath));
            if (!string.IsNullOrEmpty(gameName)) _gameName = gameName;
            if (!string.IsNullOrEmpty(classicDataBasePath)) _classicDataBasePath = classicDataBasePath;
            if (!string.IsNullOrEmpty(classicDataPath)) _classicDataPath = classicDataPath;
            if (!string.IsNullOrEmpty(testsPath)) _testsPath = testsPath;

            // Re-initialize caches if paths change
            _yamlDataCache.Clear();
            InitializeStaticCaches();
        }

        public string GetPathForStore(YamlStoreType store)
        {
            return store switch
            {
                YamlStoreType.Main => Path.Combine(_applicationRootPath, _classicDataBasePath, "CLASSIC Main.yaml"),
                YamlStoreType.Settings => Path.Combine(_applicationRootPath, "CLASSIC Settings.yaml"),
                YamlStoreType.Ignore => Path.Combine(_applicationRootPath, "CLASSIC Ignore.yaml"),
                YamlStoreType.Game => Path.Combine(_applicationRootPath, _classicDataBasePath, $"CLASSIC {_gameName}.yaml"),
                YamlStoreType.GameLocal => Path.Combine(_applicationRootPath, _classicDataPath, $"CLASSIC {_gameName} Local.yaml"),
                YamlStoreType.Test => Path.Combine(_applicationRootPath, _testsPath, "test_settings.yaml"),
                _ => throw new ArgumentOutOfRangeException(nameof(store), $"Unsupported YAML store type: {store}"),
            };
        }

        private IDictionary<object, object?> LoadYamlFromFile(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Warning: YAML file not found at '{filePath}'. Returning empty settings.");
                return new Dictionary<object, object?>();
            }
            try
            {
                using var reader = new StreamReader(filePath);
                var deserialized = _yamlDeserializer.Deserialize<Dictionary<object, object?>>(reader);
                return deserialized ?? [];
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading YAML file '{filePath}': {ex.Message}");
                return new Dictionary<object, object?>();
            }
        }

        private void LoadAndCacheYaml(string filePath)
        {
            _yamlDataCache.GetOrAdd(filePath, LoadYamlFromFile);
        }

        private IDictionary<object, object?> GetOrLoadYamlData(YamlStoreType store)
        {
            string filePath = GetPathForStore(store);
            return _yamlDataCache.GetOrAdd(filePath, LoadYamlFromFile);
        }

        private object? GetValueFromPath(IDictionary<object, object?> yamlRoot, IEnumerable<string> keys)
        {
            object? current = yamlRoot;
            foreach (var keyString in keys)
            {
                object key = keyString; // In YamlDotNet, keys are often strings, but can be other types.
                                        // We assume string keys from keyPath.Split.
                if (current is IDictionary<object, object?> dict && dict.TryGetValue(key, out var val))
                {
                    current = val;
                }
                else
                {
                    return null; // Path not found
                }
            }
            return current;
        }

        public T? GetSetting<T>(YamlStoreType store, string keyPath)
        {
            var yamlData = GetOrLoadYamlData(store);
            var keys = keyPath.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
            if (!keys.Any()) return default;

            object? value = GetValueFromPath(yamlData, keys);

            if (value == null) return default;

            try
            {
                if (typeof(T) == typeof(string)) return (T?)(object?)Convert.ToString(value);
                if (typeof(T) == typeof(int)) return (T)(object)Convert.ToInt32(value);
                if (typeof(T) == typeof(bool))
                {
                    if (value is bool b) return (T)(object)b;
                    if (value is string sVal && bool.TryParse(sVal, out var parsedBool)) return (T)(object)parsedBool;
                    if (int.TryParse(Convert.ToString(value), out int intVal)) return (T)(object)(intVal != 0);
                }
                if (typeof(T) == typeof(double)) return (T)(object)Convert.ToDouble(value);
                if (typeof(T) == typeof(long)) return (T)(object)Convert.ToInt64(value);
                if (typeof(T).IsEnum && value is string enumStr)
                {
                    try { return (T)Enum.Parse(typeof(T), enumStr, true); }
                    catch { /* Fallback or log */ }
                }

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex) when (ex is InvalidCastException || ex is FormatException || ex is ArgumentNullException || ex is OverflowException)
            {
                Console.WriteLine($"Error converting setting '{keyPath}' (value: '{value}') to type {typeof(T).Name}: {ex.Message}");
                return default;
            }
        }

        private void SetValueAtPath(IDictionary<object, object?> rootDict, IEnumerable<string> keys, object? newValue)
        {
            IDictionary<object, object?> currentDict = rootDict;
            var keyList = keys.ToList();

            for (int i = 0; i < keyList.Count - 1; i++)
            {
                object key = keyList[i];
                if (!currentDict.TryGetValue(key, out var nextObj) || nextObj is not IDictionary<object, object?> nextDict)
                {
                    nextDict = new Dictionary<object, object?>();
                    currentDict[key] = nextDict;
                }
                currentDict = nextDict;
            }
            currentDict[keyList.Last()] = newValue;
        }

        public void SetSetting<T>(YamlStoreType store, string keyPath, T newValue)
        {
            string filePath = GetPathForStore(store);
            IDictionary<object, object?> yamlData = GetOrLoadYamlData(store);

            var keys = keyPath.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
            if (!keys.Any())
            {
                Console.WriteLine($"Error: Key path '{keyPath}' is empty or invalid.");
                return;
            }

            SetValueAtPath(yamlData, keys, newValue);

            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using var writer = new StreamWriter(filePath);
                _yamlSerializer.Serialize(writer, yamlData);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing YAML file '{filePath}': {ex.Message}");
            }
        }

        public YamlNode? GetYamlNode(YAML yamlStore)
        {
            if (_yamlCache.TryGetValue(yamlStore, out var cachedNode))
            {
                return cachedNode;
            }

            if (_yamlFilePaths.TryGetValue(yamlStore, out var filePath) && File.Exists(filePath))
            {
                try
                {
                    using var reader = new StreamReader(filePath);
                    var yamlStream = new YamlStream();
                    yamlStream.Load(reader);

                    if (yamlStream.Documents.Count > 0)
                    {
                        var rootNode = yamlStream.Documents[0].RootNode;
                        _yamlCache[yamlStore] = rootNode; // Cache it
                        return rootNode;
                    }
                }
                catch (Exception ex) // Catch specific YAML parsing or IO exceptions
                {
                    // Log the error (implement a logging mechanism)
                    Console.WriteLine($"Error loading YAML file {filePath}: {ex.Message}");
                    return null;
                }
            }
            else
            {
                // Log error: File path not configured or file doesn't exist
                Console.WriteLine($"YAML file path not configured or file not found for: {yamlStore}");
            }
            return null;
        }

        public YamlNode? GetNodeByPath(YamlNode? startNode, string path)
        {
            if (startNode == null) return null;

            var pathSegments = path.Split('.');
            YamlNode? currentNode = startNode;

            foreach (var segment in pathSegments)
            {
                if (currentNode is YamlMappingNode mappingNode)
                {
                    // Try to get the child node by scalar key
                    if (mappingNode.Children.TryGetValue(new YamlScalarNode(segment), out var nextNode))
                    {
                        currentNode = nextNode;
                    }
                    else
                    {
                        return null; // Path segment not found
                    }
                }
                else
                {
                    return null; // Current node is not a mapping node, so cannot traverse further
                }
            }
            return currentNode;
        }

        // Generic method to get a setting value, similar to Python's yaml_settings
        public T? GetSetting<T>(YAML yamlStore, string keyPath, T? defaultValue = default)
        {
            var rootNode = GetYamlNode(yamlStore);
            if (rootNode == null) return defaultValue;

            var targetNode = GetNodeByPath(rootNode, keyPath);
            if (targetNode is YamlScalarNode scalarNode && scalarNode.Value != null)
            {
                try
                {
                    // Attempt to deserialize/convert the scalar value to type T
                    // This might need more sophisticated conversion based on T
                    var deserializer = new DeserializerBuilder().Build();
                    return deserializer.Deserialize<T>(scalarNode.Value);
                }
                catch
                {
                    // Handle or log deserialization errors
                    return defaultValue;
                }
            }
            return defaultValue;
        }
    }
}

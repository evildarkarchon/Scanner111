using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using System.Collections.Concurrent;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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
    }
}

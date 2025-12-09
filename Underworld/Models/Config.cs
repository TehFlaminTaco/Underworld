using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Underworld.Models;

/// <summary>
/// Provides persistent configuration storage with JSON serialization.
/// Thread-safe singleton that manages key-value pairs stored in Underworld.config.json.
/// </summary>
public static class Config
{
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = true };
    
    internal static readonly object _lock = new();
    private static Dictionary<string, JsonElement> _configCache = new();

    static Config()
    {
        LoadConfig();
    }

    /// <summary>
    /// Loads configuration from disk into memory cache.
    /// Creates empty cache if file doesn't exist or is corrupted.
    /// </summary>
    private static void LoadConfig()
    {
        lock (_lock)
        {
            _configCache = new Dictionary<string, JsonElement>();
            
            if (!File.Exists(ConfigPath))
                return;

            try
            {
                var jsonText = File.ReadAllText(ConfigPath);
                var document = JsonDocument.Parse(jsonText);
                
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    _configCache[property.Name] = property.Value.Clone();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to load config: {ex.Message}. Starting with empty configuration.");
                _configCache = new Dictionary<string, JsonElement>();
            }
        }
    }

    /// <summary>
    /// Persists the current configuration cache to disk.
    /// Creates directory if it doesn't exist. Fails silently on errors.
    /// </summary>
    internal static void SaveConfig()
    {
        lock (_lock)
        {
            try
            {
                EnsureConfigDirectoryExists();
                
                var jsonText = SerializeConfigCache();
                File.WriteAllText(ConfigPath, jsonText);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to save config: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Ensures the configuration file's directory exists.
    /// </summary>
    private static void EnsureConfigDirectoryExists()
    {
        var directory = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    /// <summary>
    /// Serializes the configuration cache to JSON string.
    /// </summary>
    private static string SerializeConfigCache()
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream, WriterOptions);
        
        writer.WriteStartObject();
        foreach (var (key, value) in _configCache)
        {
            writer.WritePropertyName(key);
            value.WriteTo(writer);
        }
        writer.WriteEndObject();
        writer.Flush();

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Gets the internal configuration cache. For internal use only.
    /// </summary>
    internal static Dictionary<string, JsonElement> GetCache() => _configCache;

    /// <summary>
    /// Clears the in-memory configuration cache. Primarily for testing purposes.
    /// Does not affect the persisted configuration file.
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _configCache = new Dictionary<string, JsonElement>();
        }
    }

    /// <summary>
    /// Creates a configuration entry for the specified key with a default value.
    /// </summary>
    /// <typeparam name="T">The type of value to store</typeparam>
    /// <param name="key">The configuration key</param>
    /// <param name="default">The default value if no configuration exists</param>
    /// <returns>A ConfigEntry instance for managing this configuration value</returns>
    public static ConfigEntry<T> Setup<T>(string key, T @default)
    {
        return new ConfigEntry<T>(key, @default);
    }
}

/// <summary>
/// Represents a typed configuration entry with automatic persistence.
/// Provides thread-safe access to a specific configuration value.
/// </summary>
/// <typeparam name="T">The type of value stored in this configuration entry</typeparam>
public sealed class ConfigEntry<T>
{
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions SerializeOptions = new();
    
    private readonly string _key;
    private readonly T _default;

    internal ConfigEntry(string key, T @default)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _default = @default;
    }

    /// <summary>
    /// Gets the current value for this configuration entry.
    /// Returns the default value if not set or if deserialization fails.
    /// </summary>
    /// <returns>The configuration value or default</returns>
    public T Get()
    {
        lock (Config._lock)
        {
            var cache = Config.GetCache();
            if (!cache.TryGetValue(_key, out var element))
            {
                return _default;
            }

            return DeserializeValue(element);
        }
    }

    /// <summary>
    /// Sets a new value for this configuration entry and persists it to disk.
    /// </summary>
    /// <param name="value">The value to store</param>
    public void Set(T value)
    {
        lock (Config._lock)
        {
            var cache = Config.GetCache();
            var element = SerializeValue(value);
            cache[_key] = element;
            Config.SaveConfig();
        }
    }

    /// <summary>
    /// Checks if this configuration entry has been explicitly set.
    /// </summary>
    /// <returns>True if a value has been set, false otherwise</returns>
    public bool HasSet()
    {
        lock (Config._lock)
        {
            var cache = Config.GetCache();
            return cache.ContainsKey(_key);
        }
    }

    /// <summary>
    /// Removes this configuration entry and persists the change to disk.
    /// Subsequent Get() calls will return the default value.
    /// </summary>
    public void Reset()
    {
        lock (Config._lock)
        {
            var cache = Config.GetCache();
            cache.Remove(_key);
            Config.SaveConfig();
        }
    }

    /// <summary>
    /// Deserializes a JSON element to the target type.
    /// Returns default value if deserialization fails.
    /// </summary>
    private T DeserializeValue(JsonElement element)
    {
        try
        {
            var value = JsonSerializer.Deserialize<T>(element.GetRawText(), DeserializeOptions);
            return value ?? _default;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Failed to deserialize config key '{_key}': {ex.Message}");
            return _default;
        }
    }

    /// <summary>
    /// Serializes a value to a JSON element for storage.
    /// </summary>
    private JsonElement SerializeValue(T value)
    {
        var json = JsonSerializer.Serialize(value, SerializeOptions);
        var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

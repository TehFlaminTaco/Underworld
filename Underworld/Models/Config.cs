using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Underworld.Models;

public static class Config
{
    private static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
    internal static readonly object _lock = new();
    private static Dictionary<string, JsonElement> _configCache = new();

    static Config()
    {
        LoadConfig();
    }

    private static void LoadConfig()
    {
        lock (_lock)
        {
            _configCache = new Dictionary<string, JsonElement>();
            if (!File.Exists(ConfigPath))
                return;

            try
            {
                var txt = File.ReadAllText(ConfigPath);
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var doc = JsonDocument.Parse(txt);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    _configCache[prop.Name] = prop.Value.Clone();
                }
            }
            catch
            {
                // If config file is corrupted, start fresh
                _configCache = new Dictionary<string, JsonElement>();
            }
        }
    }

    internal static void SaveConfig()
    {
        lock (_lock)
        {
            try
            {
                var dir = Path.GetDirectoryName(ConfigPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using var stream = new MemoryStream();
                using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
                writer.WriteStartObject();
                foreach (var kvp in _configCache)
                {
                    writer.WritePropertyName(kvp.Key);
                    kvp.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
                writer.Flush();

                var txt = System.Text.Encoding.UTF8.GetString(stream.ToArray());
                File.WriteAllText(ConfigPath, txt);
            }
            catch
            {
                // Silently fail if we can't write
            }
        }
    }

    internal static Dictionary<string, JsonElement> GetCache() => _configCache;

    // For testing purposes - allows clearing the config cache
    public static void ClearCache()
    {
        lock (_lock)
        {
            _configCache = new Dictionary<string, JsonElement>();
        }
    }

    public static ConfigEntry<T> Setup<T>(string key, T @default)
    {
        return new ConfigEntry<T>(key, @default);
    }
}

public sealed class ConfigEntry<T>
{
    private readonly string _key;
    private readonly T _default;

    internal ConfigEntry(string key, T @default)
    {
        _key = key ?? throw new ArgumentNullException(nameof(key));
        _default = @default;
    }

    public T Get()
    {
        lock (Config._lock)
        {
            var cache = Config.GetCache();
            if (!cache.TryGetValue(_key, out var elem))
                return _default;

            try
            {
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var v = JsonSerializer.Deserialize<T>(elem.GetRawText(), opts);
                return v == null ? _default : v;
            }
            catch
            {
                return _default;
            }
        }
    }

    public void Set(T value)
    {
        lock (Config._lock)
        {
            var cache = Config.GetCache();
            var opts = new JsonSerializerOptions();
            var json = JsonSerializer.Serialize(value, opts);
            var elem = JsonDocument.Parse(json).RootElement;
            cache[_key] = elem.Clone();
            Config.SaveConfig();
        }
    }

    public bool HasSet()
    {
        lock (Config._lock)
        {
            var cache = Config.GetCache();
            return cache.ContainsKey(_key);
        }
    }

    // Clear the value and remove it from the config
    public void Reset()
    {
        lock (Config._lock)
        {
            var cache = Config.GetCache();
            cache.Remove(_key);
            Config.SaveConfig();
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Underworld.Models;

namespace Underworld.Tests
{
    public class ConfigTests : IDisposable
    {
        public ConfigTests()
        {
            Config.ClearCache();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            if (File.Exists(configPath))
                File.Delete(configPath);
        }

        [Fact]
        public void Config_Setup_CreatesEntry()
        {
            var entry = Config.Setup("testKey", "defaultValue");
            
            Assert.NotNull(entry);
            Assert.Equal("defaultValue", entry.Get());
        }

        [Fact]
        public void Config_SetAndGet_PersistsValue()
        {
            var entry = Config.Setup("testKey", "default");
            entry.Set("newValue");

            // Verify value is returned without cache clear
            Assert.Equal("newValue", entry.Get());
        }

        [Fact]
        public void Config_SetAndGet_ComplexType()
        {
            var defaultList = new List<string>();
            var entry = Config.Setup("listKey", defaultList);
            
            var testList = new List<string> { "item1", "item2", "item3" };
            entry.Set(testList);

            var retrieved = entry.Get();

            Assert.Equal(3, retrieved.Count);
            Assert.Contains("item1", retrieved);
            Assert.Contains("item2", retrieved);
            Assert.Contains("item3", retrieved);
        }

        [Fact]
        public void Config_HasSet_ReturnsTrueAfterSet()
        {
            var entry = Config.Setup("hasSetTest", 0);
            
            Assert.False(entry.HasSet());
            
            entry.Set(42);
            
            Assert.True(entry.HasSet());
        }

        [Fact]
        public void Config_MultipleKeys_Independent()
        {
            var entry1 = Config.Setup("key1", "value1");
            var entry2 = Config.Setup("key2", "value2");
            
            entry1.Set("changed1");
            entry2.Set("changed2");

            Assert.Equal("changed1", entry1.Get());
            Assert.Equal("changed2", entry2.Get());
        }

        [Fact]
        public void Config_FileCreation_CreatesConfigFile()
        {
            var entry = Config.Setup("fileTest", "value");
            entry.Set("test");

            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            Assert.True(File.Exists(configPath));
        }

        [Fact]
        public void Config_Reset_RemovesValue()
        {
            var entry = Config.Setup("resetTest", "default");
            entry.Set("value");
            
            Assert.True(entry.HasSet());
            
            entry.Reset();
            
            Assert.False(entry.HasSet());
            Assert.Equal("default", entry.Get());
        }

        [Fact]
        public void Config_DefaultValue_ReturnedWhenNotSet()
        {
            Config.ClearCache();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            if (File.Exists(configPath))
                File.Delete(configPath);

            var entry = Config.Setup("newKey", "defaultValue");
            Assert.Equal("defaultValue", entry.Get());
            Assert.False(entry.HasSet());
        }

        public void Dispose()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            try { if (File.Exists(configPath)) File.Delete(configPath); } catch { }
        }
    }
}

using System;
using System.IO;
using System.Linq;
using Xunit;
using Underworld.Models;

namespace Underworld.Tests
{
    public class WadListsTests : IDisposable
    {
        private readonly string _testDir;

        public WadListsTests()
        {
            _testDir = Path.Combine(AppContext.BaseDirectory, "test_wads");
            Directory.CreateDirectory(_testDir);
            WadLists.ClearWadCache();
        }

        [Fact]
        public void WadInfo_Properties_WorkCorrectly()
        {
            var wadInfo = new WadInfo
            {
                Name = "My Mod",
                Path = "/path/to/mod.pk3",
                IsPatch = false,
                HasMaps = true
            };

            Assert.Equal("My Mod", wadInfo.Name);
            Assert.Equal("/path/to/mod.pk3", wadInfo.Path);
            Assert.False(wadInfo.IsPatch);
            Assert.True(wadInfo.HasMaps);
        }

        [Fact]
        public void WadInfo_Info_CreatesSelectWadInfo()
        {
            var wadInfo = new WadInfo
            {
                Name = "Test Mod",
                Path = "/test/mod.wad",
                HasMaps = true
            };

            var selectInfo = wadInfo.Info;

            Assert.NotNull(selectInfo);
            Assert.Equal("/test/mod.wad", selectInfo.Path);
            Assert.Equal("Test Mod", selectInfo.DisplayName);
            Assert.True(selectInfo.HasMaps);
        }

        [Fact]
        public void GetAllWads_EmptyEnvironment_ReturnsEmpty()
        {
            // Set up empty data directories config
            var dataDirsConfig = Config.Setup("datadirectories", System.Array.Empty<DataDirectory>());
            dataDirsConfig.Set(Array.Empty<DataDirectory>());

            var wads = WadLists.GetAllWads();

            Assert.NotNull(wads);
            Assert.Empty(wads);
        }

        [Fact]
        public void GetWadInfo_NewPath_CreatesAndCachesInfo()
        {
            var wadPath = Path.Combine(_testDir, "test.wad");
            File.WriteAllText(wadPath, "dummy");

            var wadInfo = WadLists.GetWadInfo(wadPath);

            Assert.NotNull(wadInfo);
            Assert.Equal(wadPath, wadInfo.Path);
            Assert.Equal("test.wad", wadInfo.Name);
        }

        [Fact]
        public void GetWadInfo_CachedPath_ReturnsCachedInfo()
        {
            var wadPath = Path.Combine(_testDir, "cached.wad");
            File.WriteAllText(wadPath, "dummy");

            // First call caches
            var wadInfo1 = WadLists.GetWadInfo(wadPath);
            // Second call should return cached version
            var wadInfo2 = WadLists.GetWadInfo(wadPath);

            Assert.NotNull(wadInfo1);
            Assert.NotNull(wadInfo2);
            Assert.Equal(wadInfo1.Path, wadInfo2.Path);
            Assert.Equal(wadInfo1.Name, wadInfo2.Name);
        }

        [Fact]
        public void ClearWadCache_RepopulatesFromDataDirectories()
        {
            var wadPath = Path.Combine(_testDir, "cache_test.wad");
            File.WriteAllText(wadPath, "dummy");

            // Cache the wad manually first
            var wadInfo1 = WadLists.GetWadInfo(wadPath);
            Assert.NotNull(wadInfo1);

            // Clear cache - this internally calls GetNewWadInfos() to repopulate
            WadLists.ClearWadCache();

            // Cache should be repopulated from data directories
            var cache = WadLists.WadInfoCache.Get();
            Assert.NotNull(cache);
            // May or may not contain our test wad depending on data directories config
        }

        [Fact]
        public void GetNewWadInfos_RefreshesCache()
        {
            // Clear cache first
            WadLists.ClearWadCache();

            // GetNewWadInfos should populate cache based on current data directories
            WadLists.GetNewWadInfos();

            // Cache should be populated (though might be empty if no data dirs configured)
            var cache = WadLists.WadInfoCache.Get();
            Assert.NotNull(cache);
        }

        [Fact]
        public void ValidWADFiles_ContainsExpectedExtensions()
        {
            Assert.Contains(".wad", WadLists.ValidWADFiles);
            Assert.Contains(".pk3", WadLists.ValidWADFiles);
            Assert.Contains(".zip", WadLists.ValidWADFiles);
        }

        [Fact]
        public void DataDirectory_Properties_WorkCorrectly()
        {
            var dataDir = new DataDirectory
            {
                Path = "/path/to/wads",
                Source = "User",
                IsFromEnvironment = false
            };

            Assert.Equal("/path/to/wads", dataDir.Path);
            Assert.Equal("User", dataDir.Source);
            Assert.False(dataDir.IsFromEnvironment);
            Assert.Equal("/path/to/wads", dataDir.ToString());
        }

        [Fact]
        public void DataDirectory_Constructor_InitializesProperties()
        {
            var dataDir = new DataDirectory("/test/path", "DOOMWADDIR", true);

            Assert.Equal("/test/path", dataDir.Path);
            Assert.Equal("DOOMWADDIR", dataDir.Source);
            Assert.True(dataDir.IsFromEnvironment);
        }

        public void Dispose()
        {
            WadLists.ClearWadCache();
            try
            {
                if (Directory.Exists(_testDir))
                    Directory.Delete(_testDir, true);
            }
            catch { }
        }
    }
}

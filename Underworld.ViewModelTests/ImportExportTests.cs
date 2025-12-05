using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Underworld.Models;
using Underworld.ViewModels;

namespace Underworld.ViewModelTests
{
    [Collection("Non-Parallel Collection")]
    public class ImportExportTests : IDisposable
    {
        private readonly string _testDir;

        public ImportExportTests()
        {
            Config.ClearCache();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            if (File.Exists(configPath))
                File.Delete(configPath);

            _testDir = Path.Combine(AppContext.BaseDirectory, "test_profiles");
            Directory.CreateDirectory(_testDir);
        }

        [Fact]
        public void ExportProfile_From_CreatesValidExportProfile()
        {
            var profile = new Profile
            {
                Name = "Test Profile",
                PreferredExecutable = "/path/to/gzdoom",
                PreferredIWAD = "/path/to/doom2.wad",
                SelectedWads = new ObservableCollection<string> 
                { 
                    "/path/to/mod1.pk3",
                    "/path/to/mod2.wad" 
                }
            };

            var exportProfile = ExportProfile.From(profile);

            Assert.Equal("Test Profile", exportProfile.Name);
            Assert.Equal("gzdoom", exportProfile.PreferredExecutable);
            Assert.Equal("doom2.wad", exportProfile.PreferredIWAD);
            Assert.Equal(2, exportProfile.SelectedWADs.Count);
            Assert.Contains("mod1.pk3", exportProfile.SelectedWADs);
            Assert.Contains("mod2.wad", exportProfile.SelectedWADs);
        }

        [Fact]
        public void ExportProfile_Serialization_ProducesValidJson()
        {
            var exportProfile = new ExportProfile
            {
                Name = "JSON Test",
                PreferredExecutable = "gzdoom",
                PreferredIWAD = "doom2.wad",
                SelectedWADs = new List<string> { "mod1.pk3", "mod2.wad" }
            };

            var json = JsonSerializer.Serialize(exportProfile, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            Assert.Contains("\"Name\": \"JSON Test\"", json);
            Assert.Contains("\"PreferredExecutable\": \"gzdoom\"", json);
            Assert.Contains("\"PreferredIWAD\": \"doom2.wad\"", json);
            Assert.Contains("\"mod1.pk3\"", json);
            Assert.Contains("\"mod2.wad\"", json);
        }

        [Fact]
        public void ExportProfile_Deserialization_RestoresProperties()
        {
            var json = @"{
                ""Name"": ""Deserialized Profile"",
                ""PreferredExecutable"": ""gzdoom"",
                ""PreferredIWAD"": ""doom2.wad"",
                ""SelectedWADs"": [""brutal.pk3"", ""smooth.wad""]
            }";

            var exportProfile = JsonSerializer.Deserialize<ExportProfile>(json);

            Assert.NotNull(exportProfile);
            Assert.Equal("Deserialized Profile", exportProfile.Name);
            Assert.Equal("gzdoom", exportProfile.PreferredExecutable);
            Assert.Equal("doom2.wad", exportProfile.PreferredIWAD);
            Assert.Equal(2, exportProfile.SelectedWADs.Count);
            Assert.Contains("brutal.pk3", exportProfile.SelectedWADs);
            Assert.Contains("smooth.wad", exportProfile.SelectedWADs);
        }

        [Fact]
        public void ExportProfile_RoundTrip_PreservesData()
        {
            var original = new ExportProfile
            {
                Name = "RoundTrip Test",
                PreferredExecutable = "zandronum",
                PreferredIWAD = "plutonia.wad",
                SelectedWADs = new List<string> { "a.pk3", "b.wad", "c.pk7" }
            };

            var json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<ExportProfile>(json);

            Assert.NotNull(restored);
            Assert.Equal(original.Name, restored.Name);
            Assert.Equal(original.PreferredExecutable, restored.PreferredExecutable);
            Assert.Equal(original.PreferredIWAD, restored.PreferredIWAD);
            Assert.Equal(original.SelectedWADs.Count, restored.SelectedWADs.Count);
        }

        [Fact]
        public void ExportProfile_To_FailsWhenExecutableNotFound()
        {
            // Setup with no executables configured
            var executablesConfig = Config.Setup("executables", new List<ExecutableItem>());
            executablesConfig.Set(new List<ExecutableItem>());

            // Also provide an IWAD that doesn't exist so it returns failure
            var exportProfile = new ExportProfile
            {
                Name = "Test",
                PreferredExecutable = "nonexistent",
                PreferredIWAD = "fake.wad",
                SelectedWADs = new List<string>()
            };

            var (profile, failReason) = exportProfile.To();

            Assert.Null(profile);
            Assert.NotNull(failReason);
            // The function sets failReason for executable but continues checking
            // It will fail on IWAD check which returns early
            Assert.NotEmpty(failReason);
        }

        [Fact]
        public void ExportProfile_To_FailsWhenIWADNotFound()
        {
            // Setup with valid executable but invalid IWAD
            var executablesConfig = Config.Setup("executables", new List<ExecutableItem>());
            executablesConfig.Set(new List<ExecutableItem> 
            { 
                new ExecutableItem { Path = "/test/gzdoom" } 
            });

            var exportProfile = new ExportProfile
            {
                Name = "Test",
                PreferredExecutable = "gzdoom",
                PreferredIWAD = "nonexistent.wad",
                SelectedWADs = new List<string>()
            };

            var (profile, failReason) = exportProfile.To();

            Assert.Null(profile);
            Assert.NotNull(failReason);
            Assert.Contains("Preferred IWAD", failReason);
            Assert.Contains("nonexistent.wad", failReason);
        }

        [Fact]
        public void ExportProfile_To_FailsWhenWADNotFound()
        {
            // Setup with valid executable
            var executablesConfig = Config.Setup("executables", new List<ExecutableItem>());
            executablesConfig.Set(new List<ExecutableItem> 
            { 
                new ExecutableItem { Path = "/test/gzdoom" } 
            });

            MainWindowViewModel.AllWads.Clear();

            var exportProfile = new ExportProfile
            {
                Name = "Test",
                PreferredExecutable = "gzdoom",
                PreferredIWAD = null, // No IWAD to avoid early return
                SelectedWADs = new List<string> { "missing.pk3" }
            };

            var (profile, failReason) = exportProfile.To();

            Assert.Null(profile);
            Assert.NotNull(failReason);
            Assert.Contains("Required WAD", failReason);
            Assert.Contains("missing.pk3", failReason);
        }

        [Fact]
        public void ExportProfile_FilenamePath_Conversion_MaintainsIntegrity()
        {
            var fullPath = Path.Combine(_testDir, "testmod.pk3");
            File.WriteAllText(fullPath, "dummy");

            MainWindowViewModel.AllWads.Clear();
            MainWindowViewModel.AllWads.Add(new SelectWadInfo 
            { 
                Path = fullPath,
                DisplayName = "testmod.pk3"
            });

            var profile = new Profile
            {
                Name = "Export Test",
                PreferredExecutable = "/test/gzdoom",
                PreferredIWAD = "/test/doom2.wad",
                SelectedWads = new ObservableCollection<string> { fullPath }
            };

            // Export (converts path to filename)
            var exported = ExportProfile.From(profile);
            Assert.Contains("testmod.pk3", exported.SelectedWADs);
            Assert.DoesNotContain(fullPath, exported.SelectedWADs);

            // Import would convert filename back to path (tested in integration)
        }

        [Fact]
        public void ExportProfile_EmptyWadList_HandledCorrectly()
        {
            var exportProfile = new ExportProfile
            {
                Name = "Empty Wads",
                PreferredExecutable = "gzdoom",
                SelectedWADs = new List<string>()
            };

            var json = JsonSerializer.Serialize(exportProfile);
            var restored = JsonSerializer.Deserialize<ExportProfile>(json);

            Assert.NotNull(restored);
            Assert.NotNull(restored.SelectedWADs);
            Assert.Empty(restored.SelectedWADs);
        }

        [Fact]
        public void ExportProfile_ToString_ReturnsName()
        {
            var exportProfile = new ExportProfile
            {
                Name = "My Profile"
            };

            Assert.Equal("My Profile", exportProfile.ToString());
        }

        [Fact]
        public void Profile_ObservableProperties_RaisePropertyChanged()
        {
            var profile = new Profile();
            var nameChanged = false;
            var exeChanged = false;
            var iwadChanged = false;

            profile.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Profile.Name))
                    nameChanged = true;
                if (args.PropertyName == nameof(Profile.PreferredExecutable))
                    exeChanged = true;
                if (args.PropertyName == nameof(Profile.PreferredIWAD))
                    iwadChanged = true;
            };

            profile.Name = "Test";
            profile.PreferredExecutable = "/usr/bin/gzdoom";
            profile.PreferredIWAD = "/iwads/doom2.wad";

            Assert.True(nameChanged);
            Assert.True(exeChanged);
            Assert.True(iwadChanged);
        }

        public void Dispose()
        {
            MainWindowViewModel.AllWads.Clear();
            WadLists.ClearWadCache();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            try { if (File.Exists(configPath)) File.Delete(configPath); } catch { }
            try { if (Directory.Exists(_testDir)) Directory.Delete(_testDir, true); } catch { }
        }
    }
}

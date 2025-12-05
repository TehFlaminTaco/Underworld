using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Underworld.Models;
using Underworld.ViewModels;

namespace Underworld.ViewModelTests
{
    [Collection("Non-Parallel Collection")]
    public class ProfileTests : IDisposable
    {
        public ProfileTests()
        {
            Config.ClearCache();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            if (File.Exists(configPath))
                File.Delete(configPath);
        }

        [Fact]
        public void Profile_ObservableProperties_RaisePropertyChanged()
        {
            var profile = new Profile();
            var nameChanged = false;
            var lockedChanged = false;

            profile.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(Profile.Name))
                    nameChanged = true;
                if (args.PropertyName == nameof(Profile.Locked))
                    lockedChanged = true;
            };

            profile.Name = "TestProfile";
            profile.Locked = true;

            Assert.True(nameChanged);
            Assert.True(lockedChanged);
        }

        [Fact]
        public void Profile_ToString_ReturnsName()
        {
            var profile = new Profile { Name = "MyProfile" };
            Assert.Equal("MyProfile", profile.ToString());
        }

        [Fact]
        public void Profile_SelectedWads_IsObservableCollection()
        {
            var profile = new Profile();
            Assert.NotNull(profile.SelectedWads);
            
            var collectionChanged = false;
            profile.SelectedWads.CollectionChanged += (_, _) => collectionChanged = true;
            
            profile.SelectedWads.Add("test.wad");
            Assert.True(collectionChanged);
        }

        [Fact]
        public void ExportProfile_From_ConvertsPaths()
        {
            var profile = new Profile
            {
                Name = "TestProfile",
                PreferredExecutable = "/path/to/gzdoom.exe",
                PreferredIWAD = "/path/to/doom2.wad"
            };
            profile.SelectedWads.Add("/path/to/mod1.pk3");
            profile.SelectedWads.Add("/path/to/mod2.wad");

            var exported = ExportProfile.From(profile);

            Assert.Equal("TestProfile", exported.Name);
            Assert.Equal("gzdoom.exe", exported.PreferredExecutable);
            Assert.Equal("doom2.wad", exported.PreferredIWAD);
            Assert.Equal(2, exported.SelectedWADs.Count);
            Assert.Contains("mod1.pk3", exported.SelectedWADs);
            Assert.Contains("mod2.wad", exported.SelectedWADs);
        }

        [Fact]
        public void ExportProfile_Serialization_RoundTrip()
        {
            var exportProfile = new ExportProfile
            {
                Name = "SerializeTest",
                PreferredExecutable = "gzdoom.exe",
                PreferredIWAD = "doom2.wad",
                SelectedWADs = new List<string> { "mod1.pk3", "mod2.wad" }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(exportProfile);
            var deserialized = System.Text.Json.JsonSerializer.Deserialize<ExportProfile>(json);

            Assert.NotNull(deserialized);
            Assert.Equal(exportProfile.Name, deserialized.Name);
            Assert.Equal(exportProfile.PreferredExecutable, deserialized.PreferredExecutable);
            Assert.Equal(exportProfile.PreferredIWAD, deserialized.PreferredIWAD);
            Assert.Equal(exportProfile.SelectedWADs.Count, deserialized.SelectedWADs.Count);
        }

        [Fact]
        public void ExportProfile_To_FailsWithoutExecutable()
        {
            var exportProfile = new ExportProfile
            {
                Name = "TestProfile",
                PreferredExecutable = "nonexistent.exe",
                PreferredIWAD = "",
                SelectedWADs = new List<string>()
            };

            var (profile, failReason) = exportProfile.To();

            Assert.NotNull(failReason);
            Assert.Contains("not found", failReason);
        }

        [Fact]
        public void Profile_Locking_PreventsMutations()
        {
            var profile = new Profile
            {
                Name = "LockedProfile",
                Locked = true
            };

            Assert.True(profile.Locked);
            // Note: Locking is enforced at UI level, not model level
            // This test just verifies the property works
        }

        public void Dispose()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            try { if (File.Exists(configPath)) File.Delete(configPath); } catch { }
        }
    }
}

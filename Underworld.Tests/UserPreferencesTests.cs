using System;
using System.IO;
using Underworld.Models;
using Xunit;

namespace Underworld.Tests
{
    [Collection("Non-Parallel Collection")]
    public sealed class UserPreferencesTests : IDisposable
    {
        private readonly string _configPath;

        public UserPreferencesTests()
        {
            _configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            ResetConfig();
        }

        [Fact]
        public void Save_PersistsPreferencesInConfig()
        {
            var preferences = new UserPreferences
            {
                ShowNoProfileExitWarning = false,
                ShowNoProfileLaunchWarning = false
            };

            preferences.Save();

            Config.ClearCache();
            Config.ReloadFromDisk();
            var entry = Config.Setup("UserPreferences", new UserPreferences());
            var restored = entry.Get();

            Assert.NotNull(restored);
            Assert.False(restored!.ShowNoProfileExitWarning);
            Assert.False(restored.ShowNoProfileLaunchWarning);
        }

        [Fact]
        public void Load_ReturnsPreferencesFromConfig()
        {
            var entry = Config.Setup("UserPreferences", new UserPreferences());
            entry.Set(new UserPreferences
            {
                ShowNoProfileExitWarning = false,
                ShowNoProfileLaunchWarning = true
            });

            Config.ClearCache();
            Config.ReloadFromDisk();

            var loaded = UserPreferences.Load();

            Assert.False(loaded.ShowNoProfileExitWarning);
            Assert.True(loaded.ShowNoProfileLaunchWarning);
        }

        public void Dispose()
        {
            ResetConfig();
        }

        private void ResetConfig()
        {
            Config.ClearCache();
            if (File.Exists(_configPath))
            {
                try
                {
                    File.Delete(_configPath);
                }
                catch
                {
                    // ignore cleanup failures
                }
            }
            Config.ReloadFromDisk();
        }
    }
}

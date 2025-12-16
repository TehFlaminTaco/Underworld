using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Underworld.ViewModels;
using Underworld.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Underworld.ViewModelTests
{
    [Collection("Non-Parallel Collection")]
    public class MainWindowViewModelTests : IDisposable
    {
        public MainWindowViewModelTests()
        {
            // Clear config cache and file before each test
            Config.ClearCache();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            if (File.Exists(configPath))
                File.Delete(configPath);
        }

        [Fact]
        public void AddExecutables_InvalidFile_ReturnsInvalids()
        {
            var tmp = Path.GetTempFileName();
            try
            {
                // ensure file exists but is not executable
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // remove exec bit if present
                    var psi = new ProcessStartInfo("/bin/sh", $"-c \"chmod -x '{tmp}'\"") { UseShellExecute = false };
                    Process.Start(psi)?.WaitForExit();
                }

                var vm = new MainWindowViewModel();
                var invalids = vm.AddExecutables(new[] { tmp });

                Assert.NotEmpty(invalids);
                Assert.DoesNotContain(vm.Executables, e => e.Path == tmp);
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        [Fact]
        public void Persistence_SavesAndLoads()
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return; // Proper windows testing later.
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.WriteAllText(tmp, "echo test");
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo("/bin/sh", $"-c \"chmod +x '{tmp}'\"") { UseShellExecute = false };
                    Process.Start(psi)?.WaitForExit();
                }

                var vm = new MainWindowViewModel();
                var invalids = vm.AddExecutables(new[] { tmp });
                Assert.Empty(invalids);
                Assert.Contains(vm.Executables, e => e.Path == tmp);

                // Simulate new app start reading persisted config
                var vm2 = new MainWindowViewModel();
                Assert.Contains(vm2.Executables, e => e.Path == tmp);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
                try 
                { 
                    var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
                    if (File.Exists(configPath)) File.Delete(configPath); 
                } 
                catch { }
            }
        }

        [Fact]
        public void RemoveExecutable_RemovesFromVMCollection()
        {
            if(RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return; // Proper windows testing later.

            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.WriteAllText(tmp, "echo test");
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo("/bin/sh", $"-c \"chmod +x '{tmp}'\"") { UseShellExecute = false };
                    Process.Start(psi)?.WaitForExit();
                }

                var vm = new MainWindowViewModel();
                vm.AddExecutables(new[] { tmp });
                var item = vm.Executables.FirstOrDefault(e => e.Path == tmp);
                Assert.NotNull(item);

                vm.SelectedExecutable = item;
                var cmd = vm.RemoveSelectedExecutableCommand;
                cmd.Execute(null);

                Assert.DoesNotContain(vm.Executables, e => e.Path == tmp);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
                try 
                { 
                    var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
                    if (File.Exists(configPath)) File.Delete(configPath); 
                } 
                catch { }
            }
        }

        [Fact]
        public void AddExecutables_DuplicatePaths_NotAdded()
        {
            var tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.WriteAllText(tmp, "echo test");
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo("/bin/sh", $"-c \"chmod +x '{tmp}'\"") { UseShellExecute = false };
                    Process.Start(psi)?.WaitForExit();
                }

                var vm = new MainWindowViewModel();
                vm.AddExecutables(new[] { tmp });
                var countAfterFirst = vm.Executables.Count;

                vm.AddExecutables(new[] { tmp });
                var countAfterSecond = vm.Executables.Count;

                Assert.Equal(countAfterFirst, countAfterSecond);
            }
            finally
            {
                try { File.Delete(tmp); } catch { }
                try 
                { 
                    var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
                    if (File.Exists(configPath)) File.Delete(configPath); 
                } 
                catch { }
            }
        }

        [Fact]
        public async Task DetermineSaveFolder_SkipsPromptWhenPreferenceDisabled()
        {
            var vm = new TestableMainWindowViewModel();
            MainWindowViewModel.UserPreferences.ShowNoProfileLaunchWarning = false;

            var folder = await vm.DetermineSaveFolder();

            Assert.Equal("_unsorted", folder);
            Assert.Equal(0, vm.ConfirmCalls);
        }

        [Fact]
        public async Task DetermineSaveFolder_RespectsConfirmationResult()
        {
            var vm = new TestableMainWindowViewModel { ConfirmResult = false };
            MainWindowViewModel.UserPreferences.ShowNoProfileLaunchWarning = true;

            var folder = await vm.DetermineSaveFolder();

            Assert.Null(folder);
            Assert.Equal(1, vm.ConfirmCalls);
            Assert.Equal("Run Game", vm.LastConfirmTitle);
        }

        [Fact]
        public async Task DetermineSaveFolder_DontShowAgainDisablesFutureWarning()
        {
            var vm = new TestableMainWindowViewModel { SimulateDontShowAgain = true };
            MainWindowViewModel.UserPreferences.ShowNoProfileLaunchWarning = true;

            var folder = await vm.DetermineSaveFolder();

            Assert.Equal("_unsorted", folder);
            Assert.False(MainWindowViewModel.UserPreferences.ShowNoProfileLaunchWarning);
            Assert.Equal(1, vm.ConfirmCalls);
        }

        [Fact]
        public void BuildRunMenuEntries_OrdersBaselineOptions()
        {
            var vm = new TestableMainWindowViewModel();

            var options = vm.BuildRunMenuEntries()
                .OfType<MainWindowViewModel.RunMenuEntry.RunMenuCommand>()
                .ToList();

            Assert.Contains(options, o => o.Header == "Launch Game");
            Assert.Contains(options, o => o.Header == "Open Mini-Launcher");
            var continueOption = Assert.Single(options.Where(o => o.Header == "Continue Game"));
            Assert.False(continueOption.CanExecute?.Invoke() ?? true);
        }

        [Fact]
        public void BuildRunMenuEntries_IncludesRecentSaves()
        {
            var vm = new TestableMainWindowViewModel();
            var savesRoot = Path.Combine(AppContext.BaseDirectory, "saves", "_unsorted");
            Directory.CreateDirectory(savesRoot);

            var oldest = CreateSaveFile(savesRoot, "save_old.zds", DateTime.Now.AddHours(-2));
            var newest = CreateSaveFile(savesRoot, "save_new.zds", DateTime.Now);

            var entries = vm.BuildRunMenuEntries().ToList();

            Assert.Contains(entries, e => e is MainWindowViewModel.RunMenuEntry.RunMenuSeparator);

            var headers = entries
                .OfType<MainWindowViewModel.RunMenuEntry.RunMenuCommand>()
                .Select(o => o.Header)
                .ToList();

            Assert.Contains(headers, h => h.Contains(Path.GetFileName(newest)));
            Assert.Contains(headers, h => h.Contains(Path.GetFileName(oldest)));
        }

        [Fact]
        public void PreferredRunButtonLabel_TracksLaunchPreference()
        {
            var vm = new TestableMainWindowViewModel();

            MainWindowViewModel.UserPreferences = new UserPreferences
            {
                PreferredLaunchMethod = UserPreferences.LaunchPreference.Run
            };
            Assert.Equal("Run Game", vm.PreferredRunButtonLabel);

            MainWindowViewModel.UserPreferences = new UserPreferences
            {
                PreferredLaunchMethod = UserPreferences.LaunchPreference.LoadLastSave
            };
            Assert.Equal("Continue Game", vm.PreferredRunButtonLabel);

            MainWindowViewModel.UserPreferences = new UserPreferences
            {
                PreferredLaunchMethod = UserPreferences.LaunchPreference.ShowMiniLauncher
            };
            Assert.Equal("Show Mini-Launcher", vm.PreferredRunButtonLabel);
        }

        [Fact]
        public void CanRunPreferredLaunch_FollowsPreferenceRequirements()
        {
            var vm = new TestableMainWindowViewModel();
            PrepareForStandardLaunch(vm);

            MainWindowViewModel.UserPreferences = new UserPreferences
            {
                PreferredLaunchMethod = UserPreferences.LaunchPreference.Run
            };
            Assert.True(vm.CanRunPreferredLaunch);

            MainWindowViewModel.UserPreferences = new UserPreferences
            {
                PreferredLaunchMethod = UserPreferences.LaunchPreference.LoadLastSave
            };
            Assert.False(vm.CanRunPreferredLaunch);

            var savesRoot = Path.Combine(AppContext.BaseDirectory, "saves", "_unsorted");
            Directory.CreateDirectory(savesRoot);
            CreateSaveFile(savesRoot, "save_autogen.zds", DateTime.Now);

            Assert.True(vm.CanRunPreferredLaunch);

            MainWindowViewModel.UserPreferences = new UserPreferences
            {
                PreferredLaunchMethod = UserPreferences.LaunchPreference.ShowMiniLauncher
            };
            Assert.True(vm.CanRunPreferredLaunch);
        }

        [Fact]
        public void MiniLauncherDefaults_PersistPerProfile()
        {
            var vm = new TestableMainWindowViewModel();
            var profile = new Profile { Name = "Test" };
            vm.Profiles.Add(profile);
            vm.SelectedProfile = profile;

            var launcherVm = vm.CreateMiniLauncherViewModel();
            launcherVm.IsMultiplayerEnabled = true;
            launcherVm.HostGame = true;
            launcherVm.HostPort = "9000";
            launcherVm.HostPlayerSlots = "6";
            launcherVm.NoMonsters = true;
            launcherVm.PersistState();

            Assert.True(profile.MiniLauncherDefaults.EnableMultiplayer);
            Assert.True(profile.MiniLauncherDefaults.HostGame);
            Assert.Equal("9000", profile.MiniLauncherDefaults.HostPort);

            var hydratedVm = vm.CreateMiniLauncherViewModel();
            Assert.True(hydratedVm.IsMultiplayerEnabled);
            Assert.True(hydratedVm.HostGame);
            Assert.Equal("9000", hydratedVm.HostPort);
            Assert.Equal("6", hydratedVm.HostPlayerSlots);
        }

        [Fact]
        public void MiniLauncherDefaults_FallBackToGlobalConfig()
        {
            var vm = new TestableMainWindowViewModel();
            vm.SelectedProfile = null;

            var first = vm.CreateMiniLauncherViewModel();
            first.IsMultiplayerEnabled = true;
            first.Port = "7000";
            first.PersistState();

            var hydrated = vm.CreateMiniLauncherViewModel();
            Assert.True(hydrated.IsMultiplayerEnabled);
            Assert.Equal("7000", hydrated.Port);
        }

        private sealed class TestableMainWindowViewModel : MainWindowViewModel
        {
            public TestableMainWindowViewModel()
            {
                // Ensure each test starts without a selected profile or lingering state.
                SelectedProfile = null;
                Profiles.Clear();
                MainWindowViewModel.UserPreferences = new UserPreferences();
            }

            public int ConfirmCalls { get; private set; }
            public bool ConfirmResult { get; set; } = true;
            public bool SimulateDontShowAgain { get; set; }
            public string LastConfirmTitle { get; private set; } = string.Empty;
            public string LastConfirmMessage { get; private set; } = string.Empty;

            public override Task<bool> ShowConfirmDialogue(string title, string text, Action setDontShowAgain, string okText = "OK", string cancelText = "Cancel")
            {
                ConfirmCalls++;
                LastConfirmTitle = title;
                LastConfirmMessage = text;
                if (SimulateDontShowAgain)
                {
                    setDontShowAgain();
                }
                return Task.FromResult(ConfirmResult);
            }
        }

        public void Dispose()
        {
            // Clean up after each test
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            try { if (File.Exists(configPath)) File.Delete(configPath); } catch { }

            var savesPath = Path.Combine(AppContext.BaseDirectory, "saves");
            try { if (Directory.Exists(savesPath)) Directory.Delete(savesPath, true); } catch { }

            // Ensure subsequent tests get a fresh copy of the global preferences singleton.
            MainWindowViewModel.UserPreferences = UserPreferences.Load();
        }

        private static string CreateSaveFile(string directory, string fileName, DateTime timestamp)
        {
            var fullPath = Path.Combine(directory, fileName);
            File.WriteAllText(fullPath, "test");
            File.SetLastWriteTime(fullPath, timestamp);
            return fullPath;
        }

        private static void PrepareForStandardLaunch(MainWindowViewModel vm)
        {
            var executable = new ExecutableItem
            {
                DisplayName = "Test Executable",
                Path = "/tmp/test-exec.exe"
            };
            vm.Executables.Add(executable);
            vm.SelectedExecutable = executable;

            var iwad = new IWad
            {
                DisplayName = "Test IWAD",
                Path = "/tmp/test.wad"
            };
            vm.IWADs.Add(iwad);
            vm.SelectedIWAD = iwad;
        }
    }
}

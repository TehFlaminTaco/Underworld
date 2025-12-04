using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Underworld.ViewModels;
using Underworld.Models;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Underworld.ViewModelTests
{
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

        public void Dispose()
        {
            // Clean up after each test
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            try { if (File.Exists(configPath)) File.Delete(configPath); } catch { }
        }
    }
}

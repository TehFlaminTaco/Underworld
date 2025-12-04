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
    public class RenameTests : IDisposable
    {
        public RenameTests()
        {
            // Clear config cache and file before each test
            Config.ClearCache();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            if (File.Exists(configPath))
                File.Delete(configPath);
        }

        [Fact]
        public void RenameExecutable_UpdatesDisplayName()
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
                var item = vm.Executables.FirstOrDefault(e => e.Path == tmp);
                Assert.NotNull(item);

                // Rename the executable
                var newName = "My Custom Doom";
                item.DisplayName = newName;

                Assert.Equal(newName, item.DisplayName);
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
        public void RenameExecutable_PersistsToDisk()
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

                // First VM: add and rename
                var vm1 = new MainWindowViewModel();
                vm1.AddExecutables(new[] { tmp });
                var item = vm1.Executables.FirstOrDefault(e => e.Path == tmp);
                Assert.NotNull(item);

                var newName = "Renamed Doom";
                item.DisplayName = newName;

                // Second VM: load and verify (should persist automatically)
                var vm2 = new MainWindowViewModel();
                var reloadedItem = vm2.Executables.FirstOrDefault(e => e.Path == tmp);
                Assert.NotNull(reloadedItem);
                Assert.Equal(newName, reloadedItem.DisplayName);
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
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            try { if (File.Exists(configPath)) File.Delete(configPath); } catch { }
        }
    }
}

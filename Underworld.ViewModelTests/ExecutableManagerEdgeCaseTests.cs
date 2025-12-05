using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Xunit;
using Underworld.Models;

namespace Underworld.ViewModelTests
{
    [Collection("Non-Parallel Collection")]
    public class ExecutableManagerEdgeCaseTests : IDisposable
    {
        [Fact]
        public void ValidatePaths_NonExistentFile_ReportsInvalid()
        {
            var mgr = new ExecutableManager();
            var (valid, invalids) = mgr.ValidatePaths(new[] { "/nonexistent/path/to/exe" });

            Assert.Empty(valid);
            Assert.NotEmpty(invalids);
        }

        [Fact]
        public void ValidatePaths_MixedValidAndInvalid_SeparatesCorrectly()
        {
            var tmpValid = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var tmpInvalid = Path.GetTempFileName();

            File.WriteAllText(tmpValid, "#!/bin/bash\necho test");
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var psi = new ProcessStartInfo("/bin/sh", $"-c \"chmod +x '{tmpValid}'\"") { UseShellExecute = false };
                    Process.Start(psi)?.WaitForExit();
                    var mgr = new ExecutableManager();
                    var (valid, invalids) = mgr.ValidatePaths(new[] { tmpValid, tmpInvalid });

                    Assert.Single(valid);
                    Assert.Single(invalids);
                    Assert.Contains(valid, e => e.Path == tmpValid);
                }
                else
                {
                    // Windows, do nothing
                    // I know this is bad practice.
                }

            }
            finally
            {
                try { File.Delete(tmpValid); } catch { }
                try { File.Delete(tmpInvalid); } catch { }
            }
        }

        [Fact]
        public void Remove_NonExistentItem_ReturnsFalse()
        {
            var mgr = new ExecutableManager();
            var item = new ExecutableItem { DisplayName = "test", Path = "/nonexistent" };

            var result = mgr.Remove(item);
            Assert.False(result);
        }

        [Fact]
        public void IsExecutable_WithWindowsExtension_ReturnsTrue()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return; // skip on non-Windows

            var result = ExecutableManager.IsExecutable("C:\\Windows\\System32\\notepad.exe", out _);
            // File may or may not exist, but the logic should be Windows-extension based
            // For this test we just verify the method doesn't throw
            Assert.True(result || !File.Exists("C:\\Windows\\System32\\notepad.exe"));
        }

        public void Dispose() { }
    }
}

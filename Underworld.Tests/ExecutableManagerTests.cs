using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Underworld.Models;
using Xunit;

namespace Underworld.Tests;

public class ExecutableManagerTests
{
    private static void MakeExecutable(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // on Windows, extension determines executability; ensure .exe extension
            // do nothing else
            return;
        }
        else
        {
            var psi = new ProcessStartInfo
            {
                FileName = "/bin/chmod",
                Arguments = $"+x \"{path}\"",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p.WaitForExit();
        }
    }

    [Fact]
    public void AddValidExecutable_FileMarkedExecutable_IsAdded()
    {
        var mgr = new ExecutableManager();

        string path;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".exe");
            File.WriteAllText(path, "");
        }
        else
        {
            path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            File.WriteAllText(path, "");
            MakeExecutable(path);
        }

        try
        {
            var (valid, invalids) = mgr.ValidatePaths(new[] { path });
            Assert.Empty(invalids);
            Assert.Single(valid);

            mgr.AddValid(valid);
            Assert.Single(mgr.Executables);
            Assert.Equal(path, mgr.Executables.First().Path);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void AddInvalidExecutable_NotExecutable_IsReportedInvalid()
    {
        var mgr = new ExecutableManager();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".txt");
        File.WriteAllText(path, "not executable");

        try
        {
            var (valid, invalids) = mgr.ValidatePaths(new[] { path });
            Assert.Empty(valid);
            Assert.NotEmpty(invalids);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void RemoveExecutable_RemovesFromCollection()
    {
        var mgr = new ExecutableManager();
        var item = new ExecutableItem { DisplayName = "app", Path = "C:\\some\\app.exe" };
        mgr.AddValid(new[] { item });
        Assert.Single(mgr.Executables);

        var removed = mgr.Remove(item);
        Assert.True(removed);
        Assert.Empty(mgr.Executables);
    }
}

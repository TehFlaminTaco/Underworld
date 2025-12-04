using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Underworld.Models;

public class ExecutableManager
{
    public IList<ExecutableItem> Executables { get; } = new List<ExecutableItem>();

    /// <summary>
    /// Validate a set of file paths and return valid items + invalid reasons.
    /// </summary>
    public (List<ExecutableItem> valid, List<string> invalids) ValidatePaths(IEnumerable<string> paths)
    {
        var valid = new List<ExecutableItem>();
        var invalids = new List<string>();

        foreach (var path in paths)
        {
            try
            {
                var p = path ?? string.Empty;
                if (!File.Exists(p))
                {
                    invalids.Add($"{p}: file does not exist");
                    continue;
                }

                if (IsExecutable(p, out var reason))
                {
                    var name = System.IO.Path.GetFileName(p);
                    if (!Executables.Any(x => x.Path == p) && !valid.Any(x => x.Path == p))
                        valid.Add(new ExecutableItem { DisplayName = name, Path = p });
                }
                else
                {
                    invalids.Add($"{System.IO.Path.GetFileName(p)}: {reason}");
                }
            }
            catch (Exception ex)
            {
                invalids.Add($"{path}: {ex.Message}");
            }
        }

        return (valid, invalids);
    }

    public void AddValid(IEnumerable<ExecutableItem> items)
    {
        foreach (var it in items)
        {
            if (!Executables.Any(x => x.Path == it.Path))
                Executables.Add(it);
        }
    }

    public bool Remove(ExecutableItem item)
    {
        return Executables.Remove(item);
    }

    public static bool IsExecutable(string path, out string reason)
    {
        reason = string.Empty;
        try
        {
            if (!File.Exists(path))
            {
                reason = "file does not exist";
                return false;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                var valid = new[] { ".exe", ".bat", ".cmd", ".com", ".lnk", ".ps1" };
                if (valid.Contains(ext))
                    return true;
                reason = $"unsupported extension '{ext}'";
                return false;
            }
            else
            {
                // try `test -x` to check executable bit
                try
                {
                    var safePath = path.Replace("'", "'\\''");
                    var psi = new ProcessStartInfo
                    {
                        FileName = "/bin/sh",
                        Arguments = $"-c \"test -x '{safePath}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var p = Process.Start(psi);
                    if (p == null)
                    {
                        reason = "unable to start shell to check executable bit";
                        return false;
                    }

                    p.WaitForExit();
                    if (p.ExitCode == 0)
                        return true;
                    reason = "file is not marked executable";
                    return false;
                }
                catch
                {
                    var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".sh")
                        return true;
                    reason = "unable to verify executable bit";
                    return false;
                }
            }
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Underworld.Models;

/// <summary>
/// Manages validation and tracking of executable files for DOOM source ports.
/// </summary>
public class ExecutableManager
{
    private static readonly string[] WindowsExecutableExtensions = { ".exe", ".bat", ".cmd", ".com", ".lnk", ".ps1" };
    
    /// <summary>
    /// Gets the collection of validated executables.
    /// </summary>
    public IList<ExecutableItem> Executables { get; } = new List<ExecutableItem>();

    /// <summary>
    /// Validates a collection of file paths and separates them into valid executables and invalid paths with reasons.
    /// </summary>
    /// <param name="paths">File paths to validate</param>
    /// <returns>A tuple containing lists of valid executable items and validation error messages</returns>
    public (List<ExecutableItem> valid, List<string> invalids) ValidatePaths(IEnumerable<string> paths)
    {
        var valid = new List<ExecutableItem>();
        var invalids = new List<string>();

        foreach (var path in paths)
        {
            var validationResult = ValidateSinglePath(path);
            
            if (validationResult.IsValid)
            {
                if (!IsPathAlreadyAdded(path, valid))
                {
                    valid.Add(validationResult.Item!);
                }
            }
            else
            {
                invalids.Add(validationResult.ErrorMessage!);
            }
        }

        return (valid, invalids);
    }

    /// <summary>
    /// Adds valid executable items to the collection, avoiding duplicates.
    /// </summary>
    /// <param name="items">Items to add</param>
    public void AddValid(IEnumerable<ExecutableItem> items)
    {
        foreach (var item in items)
        {
            if (!Executables.Any(existing => existing.Path == item.Path))
            {
                Executables.Add(item);
            }
        }
    }

    /// <summary>
    /// Removes an executable item from the collection.
    /// </summary>
    /// <param name="item">Item to remove</param>
    /// <returns>True if the item was removed, false if not found</returns>
    public bool Remove(ExecutableItem item)
    {
        return Executables.Remove(item);
    }

    /// <summary>
    /// Validates a single file path and creates an ExecutableItem if valid.
    /// </summary>
    private (bool IsValid, ExecutableItem? Item, string? ErrorMessage) ValidateSinglePath(string path)
    {
        try
        {
            var normalizedPath = path ?? string.Empty;
            
            if (!File.Exists(normalizedPath))
            {
                return (false, null, $"{normalizedPath}: file does not exist");
            }

            if (IsExecutable(normalizedPath, out var reason))
            {
                var item = new ExecutableItem 
                { 
                    DisplayName = Path.GetFileName(normalizedPath),
                    Path = normalizedPath 
                };
                return (true, item, null);
            }
            else
            {
                var fileName = Path.GetFileName(normalizedPath);
                return (false, null, $"{fileName}: {reason}");
            }
        }
        catch (Exception ex)
        {
            return (false, null, $"{path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if a path is already in the manager or the valid list.
    /// </summary>
    private bool IsPathAlreadyAdded(string path, List<ExecutableItem> validList)
    {
        return Executables.Any(x => x.Path == path) || validList.Any(x => x.Path == path);
    }

    /// <summary>
    /// Determines if a file is executable based on platform-specific criteria.
    /// On Windows: checks file extension. On Unix: checks executable permission bit.
    /// </summary>
    /// <param name="path">Path to the file to check</param>
    /// <param name="reason">Output parameter containing the reason if not executable</param>
    /// <returns>True if the file is executable, false otherwise</returns>
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

            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? IsWindowsExecutable(path, out reason)
                : IsUnixExecutable(path, out reason);
        }
        catch (Exception ex)
        {
            reason = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Checks if a file is executable on Windows by validating its extension.
    /// </summary>
    private static bool IsWindowsExecutable(string path, out string reason)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        
        if (WindowsExecutableExtensions.Contains(extension))
        {
            reason = string.Empty;
            return true;
        }

        reason = $"unsupported extension '{extension}'";
        return false;
    }

    /// <summary>
    /// Checks if a file is executable on Unix systems by testing the executable permission bit.
    /// Falls back to checking for .sh extension if permission check fails.
    /// </summary>
    private static bool IsUnixExecutable(string path, out string reason)
    {
        try
        {
            var isExecutable = CheckUnixExecutableBit(path);
            
            if (isExecutable)
            {
                reason = string.Empty;
                return true;
            }

            reason = "file is not marked executable";
            return false;
        }
        catch
        {
            // Fallback: accept .sh files even if we can't verify permissions
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".sh")
            {
                reason = string.Empty;
                return true;
            }

            reason = "unable to verify executable bit";
            return false;
        }
    }

    /// <summary>
    /// Uses shell 'test -x' command to check if a file has the executable bit set.
    /// </summary>
    private static bool CheckUnixExecutableBit(string path)
    {
        var escapedPath = path.Replace("'", "'\\''");
        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            Arguments = $"-c \"test -x '{escapedPath}'\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        
        if (process == null)
        {
            throw new InvalidOperationException("Unable to start shell process");
        }

        process.WaitForExit();
        return process.ExitCode == 0;
    }
}

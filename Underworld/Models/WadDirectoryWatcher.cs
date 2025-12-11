using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;

namespace Underworld.Models;

/// <summary>
/// Watches all configured data directories for IWAD/PWAD changes and notifies listeners when
/// any relevant file is added, deleted, renamed, or modified.
/// </summary>
public static class WadDirectoryWatcher
{
    private static readonly object SyncRoot = new();
    private static readonly List<FileSystemWatcher> Watchers = new();
    private static readonly HashSet<string> PendingPaths = new(StringComparer.OrdinalIgnoreCase);

    private static Timer? _debounceTimer;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Invoked after a debounce window when any relevant files have changed on disk.
    /// </summary>
    public static event EventHandler<WadDirectoryChangedEventArgs>? WadFilesChanged;

    /// <summary>
    /// Ensures watchers are configured for every known data directory.
    /// </summary>
    public static void EnsureWatching()
    {
        lock (SyncRoot)
        {
            if (Watchers.Count > 0)
            {
                return;
            }

            ConfigureWatchers();
        }
    }

    /// <summary>
    /// Rebuilds all watchers from the current set of data directories.
    /// </summary>
    public static void RefreshWatchers()
    {
        lock (SyncRoot)
        {
            DisposeWatchers();
            ConfigureWatchers();
        }
    }

    /// <summary>
    /// Forces a full rescan notification even if no filesystem events were observed.
    /// </summary>
    public static void RequestFullRescan()
    {
        TriggerRefresh(immediate: true);
    }

    private static void ConfigureWatchers()
    {
        foreach (var directory in WadLists.GetDataDirectories())
        {
            TryWatchDirectory(directory);
        }
    }

    private static void TryWatchDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return;
        }

        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite,
                Filter = "*",
                EnableRaisingEvents = true
            };

            watcher.Created += OnWatcherEvent;
            watcher.Deleted += OnWatcherEvent;
            watcher.Changed += OnWatcherEvent;
            watcher.Renamed += OnWatcherRenamed;

            Watchers.Add(watcher);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WadDirectoryWatcher] Failed to watch '{directory}': {ex.Message}");
        }
    }

    private static void OnWatcherEvent(object sender, FileSystemEventArgs e)
    {
        if (!IsRelevant(e.FullPath))
        {
            return;
        }

        AddPendingPath(e.FullPath);
    }

    private static void OnWatcherRenamed(object sender, RenamedEventArgs e)
    {
        var wasRelevant = IsRelevant(e.OldFullPath);
        var isRelevant = IsRelevant(e.FullPath);

        if (!wasRelevant && !isRelevant)
        {
            return;
        }

        if (wasRelevant)
        {
            AddPendingPath(e.OldFullPath);
        }

        if (isRelevant)
        {
            AddPendingPath(e.FullPath);
        }
    }

    private static bool IsRelevant(string path)
    {
        return WadLists.IsPotentialWadPath(path);
    }

    private static void AddPendingPath(string path)
    {
        lock (SyncRoot)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                PendingPaths.Add(path);
            }

            TriggerRefresh(immediate: false);
        }
    }

    private static void TriggerRefresh(bool immediate)
    {
        if (immediate)
        {
            ThreadPool.QueueUserWorkItem(_ => PerformRefresh());
            return;
        }

        if (_debounceTimer == null)
        {
            _debounceTimer = new Timer(_ => PerformRefresh());
        }

        _debounceTimer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
    }

    private static void PerformRefresh()
    {
        IReadOnlyCollection<string> changes;
        lock (SyncRoot)
        {
            changes = PendingPaths.ToArray();
            PendingPaths.Clear();
        }

        try
        {
            WadFilesChanged?.Invoke(null, new WadDirectoryChangedEventArgs(changes));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WadDirectoryWatcher] Refresh failed: {ex}");
        }
    }

    private static void DisposeWatchers()
    {
        foreach (var watcher in Watchers)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Created -= OnWatcherEvent;
            watcher.Deleted -= OnWatcherEvent;
            watcher.Changed -= OnWatcherEvent;
            watcher.Renamed -= OnWatcherRenamed;
            watcher.Dispose();
        }

        Watchers.Clear();
    }
}

/// <summary>
/// Event args describing the set of filesystem paths that triggered a refresh.
/// </summary>
public sealed class WadDirectoryChangedEventArgs : EventArgs
{
    public WadDirectoryChangedEventArgs(IReadOnlyCollection<string> changedPaths)
    {
        ChangedPaths = changedPaths;
    }

    public IReadOnlyCollection<string> ChangedPaths { get; }
}

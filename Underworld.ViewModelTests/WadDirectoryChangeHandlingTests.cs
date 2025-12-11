using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Underworld.Models;
using Underworld.ViewModels;

namespace Underworld.ViewModelTests;

[Collection("Non-Parallel Collection")]
public class WadDirectoryChangeHandlingTests : IDisposable
{
    private readonly string _dataDirectory;
    private readonly ConfigEntry<List<string>> _dataDirectoriesConfig;

    public WadDirectoryChangeHandlingTests()
    {
        Config.ClearCache();

        _dataDirectory = Path.Combine(Path.GetTempPath(), $"uw-watcher-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDirectory);

        _dataDirectoriesConfig = Config.Setup("dataDirectories", new List<string>());
        _dataDirectoriesConfig.Set(new List<string> { _dataDirectory });

        Environment.SetEnvironmentVariable("DOOMWADDIR", null);
        Environment.SetEnvironmentVariable("DOOMWADPATH", null);

        WadLists.ClearWadCache();
        MainWindowViewModel.AllWads.Clear();
    }

    [Fact]
    public void ApplyChanges_AddsFolderModWithoutRebuildingCollections()
    {
        using var vm = CreateViewModel();

        var baselinePath = CreatePatchFile("baseline_mod.wad");
        vm.ApplyWadDirectoryChanges(new[] { baselinePath });
        var existingEntry = MainWindowViewModel.AllWads.Single(w => PathsEqual(w.Path, baselinePath));

        var folderModPath = CreateFolderMod("folder_mod");
        vm.ApplyWadDirectoryChanges(new[] { folderModPath });

        Assert.Contains(MainWindowViewModel.AllWads, w => PathsEqual(w.Path, folderModPath));
        Assert.Contains(existingEntry, MainWindowViewModel.AllWads);
    }

    [Fact]
    public void ApplyChanges_AddsIWadToIwadCollection()
    {
        using var vm = CreateViewModel();

        var iwadPath = CreateIwadFile("doom2.wad");
        vm.ApplyWadDirectoryChanges(new[] { iwadPath });

        Assert.Contains(vm.IWADs, i => PathsEqual(i.Path, iwadPath));
        Assert.Empty(MainWindowViewModel.AllWads);
    }

    [Fact]
    public void ApplyChanges_RenamesPatchWad()
    {
        using var vm = CreateViewModel();

        var originalPath = CreatePatchFile("skin.wad");
        vm.ApplyWadDirectoryChanges(new[] { originalPath });
        Assert.Contains(MainWindowViewModel.AllWads, w => PathsEqual(w.Path, originalPath));

        var renamedPath = Path.Combine(_dataDirectory, "skin-renamed.wad");
        File.Move(originalPath, renamedPath);

        vm.ApplyWadDirectoryChanges(new[] { originalPath, renamedPath });

        Assert.DoesNotContain(MainWindowViewModel.AllWads, w => PathsEqual(w.Path, originalPath));
        Assert.Contains(MainWindowViewModel.AllWads, w => PathsEqual(w.Path, renamedPath));
    }

    [Fact]
    public void ApplyChanges_RemovesDeletedEntries()
    {
        using var vm = CreateViewModel();

        var patchPath = CreatePatchFile("temporary.wad");
        vm.ApplyWadDirectoryChanges(new[] { patchPath });
        Assert.Contains(MainWindowViewModel.AllWads, w => PathsEqual(w.Path, patchPath));

        File.Delete(patchPath);
        vm.ApplyWadDirectoryChanges(new[] { patchPath });

        Assert.DoesNotContain(MainWindowViewModel.AllWads, w => PathsEqual(w.Path, patchPath));
    }

    [Fact]
    public void ReloadWadsFromDisk_PersistsSelectedIWadWhenStillPresent()
    {
        using var vm = CreateViewModel();
        var iwadPath = CreateIwadFile("doom2.wad");
        vm.ApplyWadDirectoryChanges(new[] { iwadPath });

        vm.SelectedIWAD = vm.IWADs.Single();

        vm.ReloadWadsFromDisk(preserveSelections: true);

        Assert.NotNull(vm.SelectedIWAD);
        Assert.Equal(iwadPath, vm.SelectedIWAD!.Path);
    }

    [Fact]
    public void ReloadWadsFromDisk_ClearsSelectedIWadWhenRemoved()
    {
        using var vm = CreateViewModel();
        var iwadPath = CreateIwadFile("doom2.wad");
        vm.ApplyWadDirectoryChanges(new[] { iwadPath });
        vm.SelectedIWAD = vm.IWADs.Single();

        File.Delete(iwadPath);
        vm.ReloadWadsFromDisk(preserveSelections: true);

        Assert.Null(vm.SelectedIWAD);
    }

    [Fact]
    public void ApplyChanges_WithEmptySet_PerformsFullReloadOnce()
    {
        using var vm = new ReloadTrackingViewModel();
        vm.ApplyWadDirectoryChanges(Array.Empty<string>());

        Assert.Equal(1, vm.ReloadCount);
    }

    private MainWindowViewModel CreateViewModel()
    {
        MainWindowViewModel.AllWads.Clear();
        WadLists.ClearWadCache();
        var vm = new MainWindowViewModel();
        vm.IWADs.Clear();
        vm.FilteredAvailableWads.Clear();
        vm.FilteredSelectedWads.Clear();
        return vm;
    }

    private string CreatePatchFile(string fileName)
    {
        var path = Path.Combine(_dataDirectory, fileName);
        File.WriteAllText(path, "not-a-real-wad");
        return path;
    }

    private string CreateFolderMod(string folderName)
    {
        var path = Path.Combine(_dataDirectory, folderName);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, "zscript.txt"), "class Test : Object {}");
        return path;
    }

    private string CreateIwadFile(string fileName)
    {
        var path = Path.Combine(_dataDirectory, fileName);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var header = System.Text.Encoding.ASCII.GetBytes("IWAD");
        fs.Write(header, 0, header.Length);
        fs.Write(new byte[8]); // placeholder for lump count/offset
        fs.Flush(true);
        return path;
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ReloadTrackingViewModel : MainWindowViewModel
    {
        public ReloadTrackingViewModel()
        {
            ReloadCount = 0;
        }

        public int ReloadCount { get; private set; }

        public override void ReloadWadsFromDisk(bool preserveSelections = true)
        {
            ReloadCount++;
            base.ReloadWadsFromDisk(preserveSelections);
        }
    }

    public void Dispose()
    {
        MainWindowViewModel.AllWads.Clear();
        WadLists.ClearWadCache();

        try
        {
            if (Directory.Exists(_dataDirectory))
            {
                Directory.Delete(_dataDirectory, recursive: true);
            }
        }
        catch
        {
        }

        var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
        try
        {
            if (File.Exists(configPath))
            {
                File.Delete(configPath);
            }
        }
        catch
        {
        }
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Underworld.Models;
using Underworld.ViewModels;

namespace Underworld.ViewModelTests;

[Collection("Non-Parallel Collection")]
public sealed class LoadOrderTests : IDisposable
{
    private readonly string _configPath;

    public LoadOrderTests()
    {
        Config.ClearCache();
        _configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
        if (File.Exists(_configPath))
        {
            File.Delete(_configPath);
        }

        MainWindowViewModel.AllWads.Clear();
        WadLists.ClearWadCache();
    }

    [Fact]
    public void SelectingProfileUsesProfileOrder()
    {
        var wads = new[]
        {
            CreateWad("Alpha"),
            CreateWad("Beta"),
            CreateWad("Gamma")
        };

        using var vm = CreateViewModel(wads);
        var profile = CreateProfile("ProfileA", wads[1], wads[0], wads[2]);
        vm.Profiles.Add(profile);

        vm.SelectedProfile = profile;

        Assert.Equal(profile.SelectedWads, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    [Fact]
    public void SelectedWadsFilterPreservesProfileOrder()
    {
        var wads = new[]
        {
            CreateWad("Ancient Aliens"),
            CreateWad("Brutal Doom"),
            CreateWad("Colorful Hell")
        };

        using var vm = CreateViewModel(wads);
        var profile = CreateProfile("FilterProfile", wads[1], wads[0], wads[2]);
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        vm.UpdateSelectedWadsFilter("a");
        Assert.Equal(profile.SelectedWads, vm.FilteredSelectedWads.Select(w => w.Path));

        vm.UpdateSelectedWadsFilter("brutal");
        var expectedSubset = new[] { wads[1].Path };
        Assert.Equal(expectedSubset, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    [Fact]
    public void SwitchingProfilesKeepsEachProfilesOrder()
    {
        var wads = new[]
        {
            CreateWad("Alfonzone"),
            CreateWad("Back To Saturn X"),
            CreateWad("Community Chest")
        };

        using var vm = CreateViewModel(wads);
        var profileOne = CreateProfile("First", wads[0], wads[2]);
        var profileTwo = CreateProfile("Second", wads[2], wads[1], wads[0]);
        vm.Profiles.Add(profileOne);
        vm.Profiles.Add(profileTwo);

        vm.SelectedProfile = profileOne;
        Assert.Equal(profileOne.SelectedWads, vm.FilteredSelectedWads.Select(w => w.Path));

        vm.SelectedProfile = profileTwo;
        Assert.Equal(profileTwo.SelectedWads, vm.FilteredSelectedWads.Select(w => w.Path));

        vm.SelectedProfile = profileOne;
        Assert.Equal(profileOne.SelectedWads, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    [Fact]
    public void AddingWadsAppendsToProfileOrder()
    {
        var wads = new[]
        {
            CreateWad("Arcadia"),
            CreateWad("Bloom"),
            CreateWad("Complex Doom")
        };

        using var vm = CreateViewModel(wads);
        var profile = CreateProfile("Adder", wads[0]);
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        vm.AddWadsFromItems(new[] { wads[2], wads[1] });

        var expectedOrder = new[] { wads[0].Path, wads[2].Path, wads[1].Path };
        Assert.Equal(expectedOrder, profile.SelectedWads);
        Assert.Equal(expectedOrder, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    [Fact]
    public void MoveSelectedWad_ReordersProfileAndView()
    {
        var wads = new[]
        {
            CreateWad("Delta"),
            CreateWad("Epsilon"),
            CreateWad("Foxtrot")
        };

        using var vm = CreateViewModel(wads);
        var profile = CreateProfile("Mover", wads[0], wads[1], wads[2]);
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        vm.MoveSelectedWad(wads[0], 3);

        var expected = new[] { wads[1].Path, wads[2].Path, wads[0].Path };
        Assert.Equal(expected, profile.SelectedWads);
        Assert.Equal(expected, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    [Fact]
    public void MoveSelectedWad_DownwardIntoMiddlePlacesCorrectly()
    {
        var wads = new[]
        {
            CreateWad("Alpha"),
            CreateWad("Beta"),
            CreateWad("Gamma"),
            CreateWad("Delta")
        };

        using var vm = CreateViewModel(wads);
        var profile = CreateProfile("DownMover", wads[0], wads[1], wads[2], wads[3]);
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        vm.MoveSelectedWad(wads[0], 2);

        var expected = new[] { wads[1].Path, wads[0].Path, wads[2].Path, wads[3].Path };
        Assert.Equal(expected, profile.SelectedWads);
        Assert.Equal(expected, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    [Fact]
    public void SwitchingProfilesWithSameWadsUpdatesLoadOrder()
    {
        var wads = new[]
        {
            CreateWad("Alpha"),
            CreateWad("Beta"),
            CreateWad("Gamma")
        };

        using var vm = CreateViewModel(wads);
        var profileOne = CreateProfile("Forward", wads[0], wads[1], wads[2]);
        var profileTwo = CreateProfile("Backward", wads[2], wads[0], wads[1]);
        vm.Profiles.Add(profileOne);
        vm.Profiles.Add(profileTwo);

        vm.SelectedProfile = profileOne;
        Assert.Equal(profileOne.SelectedWads, vm.GetSelectedWadsInLoadOrder().Select(w => w.Path));

        vm.SelectedProfile = profileTwo;
        Assert.Equal(profileTwo.SelectedWads, vm.GetSelectedWadsInLoadOrder().Select(w => w.Path));
    }

    [Fact]
    public void InsertWadIntoLoadOrder_PlacesNewSelectionCorrectly()
    {
        var wads = new[]
        {
            CreateWad("Alpha"),
            CreateWad("Beta"),
            CreateWad("Gamma")
        };

        using var vm = CreateViewModel(wads);
        var profile = CreateProfile("InsertProfile", wads[0]);
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        vm.InsertWadIntoLoadOrder(wads[2], 0);

        var expected = new[] { wads[2].Path, wads[0].Path };
        Assert.Equal(expected, profile.SelectedWads);
        Assert.Equal(expected, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    [Fact]
    public void ManualMode_MoveSelectedWadReordersList()
    {
        var wads = new[]
        {
            CreateWad("Alpha"),
            CreateWad("Beta"),
            CreateWad("Gamma")
        };

        using var vm = CreateViewModel(wads);
        vm.AddWadsFromItems(new[] { wads[0], wads[1], wads[2] });

        vm.MoveSelectedWad(wads[2], 0);

        var expected = new[] { wads[2].Path, wads[0].Path, wads[1].Path };
        Assert.Equal(expected, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    [Fact]
    public void ManualMode_InsertWadHonorsInsertionIndex()
    {
        var wads = new[]
        {
            CreateWad("Alpha"),
            CreateWad("Beta"),
            CreateWad("Gamma")
        };

        using var vm = CreateViewModel(wads);
        vm.AddWadsFromItems(new[] { wads[0] });

        vm.InsertWadIntoLoadOrder(wads[1], 0);

        var expected = new[] { wads[1].Path, wads[0].Path };
        Assert.Equal(expected, vm.FilteredSelectedWads.Select(w => w.Path));
    }

    private static LoadOrderTestViewModel CreateViewModel(params SelectWadInfo[] wads)
    {
        var vm = new LoadOrderTestViewModel();
        vm.IWADs.Clear();
        vm.FilteredAvailableWads.Clear();
        vm.FilteredSelectedWads.Clear();
        MainWindowViewModel.AllWads.Clear();

        foreach (var wad in wads)
        {
            AttachSelectionHandler(vm, wad);
            MainWindowViewModel.AllWads.Add(wad);
            vm.FilteredAvailableWads.Add(wad);
        }

        return vm;
    }

    private static SelectWadInfo CreateWad(string name)
    {
        return new SelectWadInfo
        {
            DisplayName = name,
            Path = $"/virtual/{Guid.NewGuid():N}-{name.Replace(' ', '_').ToLowerInvariant()}.wad",
            IsSelected = false
        };
    }

    private static Profile CreateProfile(string name, params SelectWadInfo[] order)
    {
        var profile = new Profile { Name = name };
        foreach (var wad in order)
        {
            profile.SelectedWads.Add(wad.Path);
        }
        return profile;
    }

    private static void AttachSelectionHandler(MainWindowViewModel vm, SelectWadInfo wad)
    {
        wad.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SelectWadInfo.IsSelected))
            {
                vm.OnWadSelectionChanged(wad);
            }
        };
    }

    private sealed class LoadOrderTestViewModel : MainWindowViewModel
    {
        public override void ReloadWadsFromDisk(bool preserveSelections = true)
        {
            // Skip disk work; tests will inject their own data.
        }
    }

    public void Dispose()
    {
        MainWindowViewModel.AllWads.Clear();
        WadLists.ClearWadCache();
        try { if (File.Exists(_configPath)) File.Delete(_configPath); } catch { }
    }
}

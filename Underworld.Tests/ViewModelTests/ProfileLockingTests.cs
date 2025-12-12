using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Underworld.Models;
using Underworld.ViewModels;
using Xunit;

namespace Underworld.ViewModelTests;

[Collection("Non-Parallel Collection")]
public class ProfileLockingTests : IDisposable
{
    private readonly string _testConfigDir;

    public ProfileLockingTests()
    {
        _testConfigDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testConfigDir);
        
        // Clear config cache before each test
        Config.ClearCache();
        var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
        if (File.Exists(configPath))
            File.Delete(configPath);
    }

    public void Dispose()
    {
        Config.ClearCache();
        MainWindowViewModel.AllWads.Clear();
        if (Directory.Exists(_testConfigDir))
        {
            Directory.Delete(_testConfigDir, true);
        }
        Console.WriteLine("CLEANUP DONE!");
    }

    [Fact]
    public void NoProfileSelected_DoesNotBlockWadSelection()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        
        // Add test WADs to AllWads
        var testWad = new SelectWadInfo
        {
            Path = "/test/mod1.wad",
            DisplayName = "Test Mod 1",
        };
        MainWindowViewModel.AllWads.Add(testWad);
        vm.FilteredAvailableWads.Add(testWad);

        // Ensure no profile is selected
        vm.SelectedProfile = null;

        // Act - Try to select the WAD
        vm.AddWadsFromItems(new[] { testWad });

        // Assert
        Assert.True(testWad.IsSelected, "WAD should be selectable when no profile is selected");
    }

    [Fact]
    public void LockedProfile_BlocksWadSelection()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var profile = new Profile { Name = "Locked Profile", Locked = true };
        vm.Profiles.Add(profile);

        var testWad = new SelectWadInfo
        {
            Path = "/test/mod1.wad",
            DisplayName = "Test Mod 1"
        };
        MainWindowViewModel.AllWads.Add(testWad);
        vm.FilteredAvailableWads.Add(testWad);

        // Select the locked profile
        vm.SelectedProfile = profile;

        // Act - Try to add WAD
        vm.AddWadsFromItems(new[] { testWad });

        // Assert
        Assert.False(testWad.IsSelected, "WAD selection should be blocked when profile is locked");
    }

    [Fact]
    public void LockedProfile_BlocksWadDeselection()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var testWad = new SelectWadInfo
        {
            Path = "/test/mod1.wad",
            DisplayName = "Test Mod 1",
            IsSelected = true
        };
        MainWindowViewModel.AllWads.Add(testWad);
        vm.FilteredSelectedWads.Add(testWad);

        var profile = new Profile
        {
            Name = "Locked Profile",
            Locked = true
        };
        profile.SelectedWads.Add(testWad.Path);
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        // Act - Try to remove WAD
        vm.RemoveWadsFromItems(new[] { testWad });

        // Assert
        Assert.True(testWad.IsSelected, "WAD deselection should be blocked when profile is locked");
    }

    [Fact]
    public void UnlockedProfile_AllowsWadSelection()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var profile = new Profile { Name = "Unlocked Profile", Locked = false };
        vm.Profiles.Add(profile);

        var testWad = new SelectWadInfo
        {
            Path = "/test/mod1.wad",
            DisplayName = "Test Mod 1"
        };
        MainWindowViewModel.AllWads.Add(testWad);
        vm.FilteredAvailableWads.Add(testWad);
        
        // Hook up PropertyChanged event like the ViewModel does
        testWad.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SelectWadInfo.IsSelected))
            {
                vm.OnWadSelectionChanged(testWad);
            }
        };

        vm.SelectedProfile = profile;

        // Act
        vm.AddWadsFromItems(new[] { testWad });

        // Assert
        Assert.True(testWad.IsSelected, "WAD should be selectable when profile is unlocked");
        Assert.Contains(testWad.Path, profile.SelectedWads);
    }

    [Fact]
    public void LockedProfile_DoesNotBlockExecutableSelection()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var profile = new Profile { Name = "Locked Profile", Locked = true };
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        var testExe = new ExecutableItem { Path = "/test/gzdoom", DisplayName = "GZDoom" };
        vm.Executables.Add(testExe);

        // Act
        vm.SelectedExecutable = testExe;

        // Assert
        Assert.Equal(testExe, vm.SelectedExecutable);
        Assert.Equal(testExe.Path, profile.PreferredExecutable);
    }

    [Fact]
    public void LockedProfile_DoesNotBlockIWADSelection()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var profile = new Profile { Name = "Locked Profile", Locked = true };
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        var testIWAD = new IWad { Path = "/test/doom2.wad", DisplayName = "DOOM II" };
        vm.IWADs.Add(testIWAD);

        // Act
        vm.SelectedIWAD = testIWAD;

        // Assert
        Assert.Equal(testIWAD, vm.SelectedIWAD);
        Assert.Equal(testIWAD.Path, profile.PreferredIWAD);
    }

    [Fact]
    public void SwitchingBetweenLockedProfiles_CorrectlySelectsWadList()
    {
        // Arrange
        var vm = new MainWindowViewModel();

        var wad1 = new SelectWadInfo { Path = "/test/mod1.wad", DisplayName = "Mod 1" };
        var wad2 = new SelectWadInfo { Path = "/test/mod2.wad", DisplayName = "Mod 2" };
        MainWindowViewModel.AllWads.Add(wad1);
        MainWindowViewModel.AllWads.Add(wad2);

        var profile1 = new Profile { Name = "Profile 1", Locked = true };
        profile1.SelectedWads.Add(wad1.Path);
        vm.Profiles.Add(profile1);

        var profile2 = new Profile { Name = "Profile 2", Locked = true };
        profile2.SelectedWads.Add(wad2.Path);
        vm.Profiles.Add(profile2);

        // Act - Switch to profile1
        vm.SelectedProfile = profile1;

        // Assert
        Assert.True(wad1.IsSelected, "WAD 1 should be selected when switching to Profile 1");
        Assert.False(wad2.IsSelected, "WAD 2 should not be selected when switching to Profile 1");

        // Act - Switch to profile2
        vm.SelectedProfile = profile2;

        // Assert
        Assert.False(wad1.IsSelected, "WAD 1 should not be selected when switching to Profile 2");
        Assert.True(wad2.IsSelected, "WAD 2 should be selected when switching to Profile 2");
    }

    [Fact]
    public void SwitchingBetweenLockedProfiles_MaintainsLockedStatus()
    {
        // Arrange
        var vm = new MainWindowViewModel();

        var profile1 = new Profile { Name = "Profile 1", Locked = true };
        var profile2 = new Profile { Name = "Profile 2", Locked = false };
        vm.Profiles.Add(profile1);
        vm.Profiles.Add(profile2);

        // Act & Assert - Switch to locked profile
        vm.SelectedProfile = profile1;
        Assert.True(vm.CurrentProfileLocked, "CurrentProfileLocked should be true for locked profile");
        Assert.True(profile1.Locked, "Profile 1 should remain locked");

        // Act & Assert - Switch to unlocked profile
        vm.SelectedProfile = profile2;
        Assert.False(vm.CurrentProfileLocked, "CurrentProfileLocked should be false for unlocked profile");
        Assert.False(profile2.Locked, "Profile 2 should remain unlocked");

        // Act & Assert - Switch back to locked profile
        vm.SelectedProfile = profile1;
        Assert.True(vm.CurrentProfileLocked, "CurrentProfileLocked should be true when switching back");
        Assert.True(profile1.Locked, "Profile 1 should still be locked");
    }

    [Fact]
    public void LockedBehaviour_IsPersisted()
    {
        // Arrange
        var profile = new Profile { Name = "Test Profile", Locked = true };
        
        // Act - Save profile through VM
        var vm1 = new MainWindowViewModel();
        vm1.Profiles.Add(profile);
        
        // Create new VM to load from config
        var vm2 = new MainWindowViewModel();

        // Assert
        Assert.Single(vm2.Profiles);
        var loadedProfile = vm2.Profiles.First();
        Assert.Equal("Test Profile", loadedProfile.Name);
        Assert.True(loadedProfile.Locked, "Locked status should be persisted");
    }

    [Fact]
    public void LockingProfile_PreservesCurrentWadSelection()
    {
        // Arrange
        var vm = new MainWindowViewModel();
        var wad = new SelectWadInfo { Path = "/test/mod1.wad", DisplayName = "Mod 1" };
        MainWindowViewModel.AllWads.Add(wad);
        vm.FilteredAvailableWads.Add(wad);

        var profile = new Profile { Name = "Profile", Locked = false };
        vm.Profiles.Add(profile);
        vm.SelectedProfile = profile;

        // Select a WAD while unlocked
        vm.AddWadsFromItems(new[] { wad });
        Assert.True(wad.IsSelected);

        // Act - Lock the profile
        profile.Locked = true;
        vm.CurrentProfileLocked = true;

        // Assert - WAD should still be selected
        Assert.True(wad.IsSelected, "Locking profile should preserve current WAD selection");
    }
}

using System;
using System.IO;
using System.Linq;
using Xunit;
using Underworld.Models;
using Underworld.ViewModels;

namespace Underworld.ViewModelTests
{
    [Collection("Non-Parallel Collection")]
    public class WadManagementTests : IDisposable
    {
        public WadManagementTests()
        {
            Config.ClearCache();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            if (File.Exists(configPath))
                File.Delete(configPath);
        }

        [Fact]
        public void SelectWadInfo_ObservableProperties_RaisePropertyChanged()
        {
            var wadInfo = new SelectWadInfo();
            var isSelectedChanged = false;
            var displayNameChanged = false;
            var pathChanged = false;

            wadInfo.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(SelectWadInfo.IsSelected))
                    isSelectedChanged = true;
                if (args.PropertyName == nameof(SelectWadInfo.DisplayName))
                    displayNameChanged = true;
                if (args.PropertyName == nameof(SelectWadInfo.Path))
                    pathChanged = true;
            };

            wadInfo.IsSelected = true;
            wadInfo.DisplayName = "TestMod";
            wadInfo.Path = "/path/to/test.wad";

            Assert.True(isSelectedChanged);
            Assert.True(displayNameChanged);
            Assert.True(pathChanged);
        }

        [Fact]
        public void SelectWadInfo_Filename_ExtractsFromPath()
        {
            var wadInfo = new SelectWadInfo
            {
                Path = "/path/to/mymod.pk3"
            };

            Assert.Equal("mymod.pk3", wadInfo.Filename);
        }

        [Fact]
        public void SelectWadInfo_ToString_ReturnsDisplayName()
        {
            var wadInfo = new SelectWadInfo
            {
                DisplayName = "My Cool Mod"
            };

            Assert.Equal("My Cool Mod", wadInfo.ToString());
        }

        [Fact]
        public void IWad_Properties_WorkCorrectly()
        {
            var iwad = new IWad
            {
                DisplayName = "DOOM II",
                Path = "/path/to/doom2.wad"
            };

            Assert.Equal("DOOM II", iwad.DisplayName);
            Assert.Equal("/path/to/doom2.wad", iwad.Path);
            Assert.Equal("DOOM II", iwad.ToString());
        }

        [Fact]
        public void MainWindowViewModel_UpdateAvailableWadsFilter_FiltersCorrectly()
        {
            var vm = new MainWindowViewModel();
            
            // Add test wads directly to AllWads
            MainWindowViewModel.AllWads.Clear();
            MainWindowViewModel.AllWads.Add(new SelectWadInfo 
            { 
                DisplayName = "Brutal Doom",
                Path = "/test/brutal.pk3",
                IsSelected = false
            });
            MainWindowViewModel.AllWads.Add(new SelectWadInfo 
            { 
                DisplayName = "Smooth Doom",
                Path = "/test/smooth.pk3",
                IsSelected = false
            });
            MainWindowViewModel.AllWads.Add(new SelectWadInfo 
            { 
                DisplayName = "Project Brutality",
                Path = "/test/pb.pk3",
                IsSelected = false
            });

            // Rebuild filtered collections
            vm.FilteredAvailableWads.Clear();
            var unselectedWads = MainWindowViewModel.AllWads.Where(w => !w.IsSelected).ToList();
            foreach (var wad in unselectedWads)
                vm.FilteredAvailableWads.Add(wad);

            Assert.Equal(3, vm.FilteredAvailableWads.Count);

            // Filter for "Brutal"
            vm.UpdateAvailableWadsFilter("Brutal");

            Assert.Equal(2, vm.FilteredAvailableWads.Count);
            Assert.All(vm.FilteredAvailableWads, w => 
                Assert.Contains("Brutal", w.DisplayName, StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void MainWindowViewModel_AddWadsFromItems_MovesWadsToSelected()
        {
            var vm = new MainWindowViewModel();
            
            MainWindowViewModel.AllWads.Clear();
            var wad1 = new SelectWadInfo 
            { 
                DisplayName = "Test Mod 1",
                Path = "/test/mod1.pk3",
                IsSelected = false
            };
            var wad2 = new SelectWadInfo 
            { 
                DisplayName = "Test Mod 2",
                Path = "/test/mod2.wad",
                IsSelected = false
            };
            
            MainWindowViewModel.AllWads.Add(wad1);
            MainWindowViewModel.AllWads.Add(wad2);
            
            vm.FilteredAvailableWads.Add(wad1);
            vm.FilteredAvailableWads.Add(wad2);

            vm.AddWadsFromItems(new[] { wad1 });

            Assert.True(wad1.IsSelected);
            Assert.False(wad2.IsSelected);
        }

        [Fact]
        public void MainWindowViewModel_RemoveWadsFromItems_MovesWadsToAvailable()
        {
            var vm = new MainWindowViewModel();
            
            MainWindowViewModel.AllWads.Clear();
            var wad = new SelectWadInfo 
            { 
                DisplayName = "Test Mod",
                Path = "/test/mod.pk3",
                IsSelected = true
            };
            
            MainWindowViewModel.AllWads.Add(wad);
            vm.FilteredSelectedWads.Add(wad);

            vm.RemoveWadsFromItems(new[] { wad });

            Assert.False(wad.IsSelected);
        }

        [Fact]
        public void DataDirectory_Properties_WorkCorrectly()
        {
            var dataDir = new DataDirectory
            {
                Path = "/path/to/wads",
                Source = "User",
                IsFromEnvironment = false
            };

            Assert.Equal("/path/to/wads", dataDir.Path);
            Assert.Equal("User", dataDir.Source);
            Assert.False(dataDir.IsFromEnvironment);
            Assert.Equal("/path/to/wads", dataDir.ToString());
        }

        [Fact]
        public void DataDirectory_Constructor_InitializesProperties()
        {
            var dataDir = new DataDirectory("/test/path", "DOOMWADDIR", true);

            Assert.Equal("/test/path", dataDir.Path);
            Assert.Equal("DOOMWADDIR", dataDir.Source);
            Assert.True(dataDir.IsFromEnvironment);
        }

        public void Dispose()
        {
            MainWindowViewModel.AllWads.Clear();
            var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
            try { if (File.Exists(configPath)) File.Delete(configPath); } catch { }
        }
    }
}

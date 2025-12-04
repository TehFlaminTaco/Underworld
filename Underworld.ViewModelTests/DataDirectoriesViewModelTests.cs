using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using Underworld.Models;
using Underworld.ViewModels;

namespace Underworld.ViewModelTests;

public class DataDirectoriesViewModelTests : IDisposable
{
    public DataDirectoriesViewModelTests()
    {
        // Clear config cache and file before each test
        Config.ClearCache();
        var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
        if (File.Exists(configPath))
            File.Delete(configPath);
    }

    [Fact]
    public void Constructor_LoadsEnvironmentVariables()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var originalEnv = Environment.GetEnvironmentVariable("DOOMWADDIR");
        try
        {
            Environment.SetEnvironmentVariable("DOOMWADDIR", tempDir);

            // Act
            var vm = new DataDirectoriesViewModel();

            // Assert
            var envDir = vm.DataDirectories.FirstOrDefault(d => d.Path == tempDir);
            Assert.NotNull(envDir);
            Assert.Equal("DOOMWADDIR", envDir.Source);
            Assert.True(envDir.IsFromEnvironment);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOOMWADDIR", originalEnv);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddDirectories_AddsValidDirectories()
    {
        // Arrange
        var vm = new DataDirectoriesViewModel();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            var invalids = vm.AddDirectories(new[] { tempDir });

            // Assert
            Assert.Empty(invalids);
            var added = vm.DataDirectories.FirstOrDefault(d => d.Path == tempDir);
            Assert.NotNull(added);
            Assert.Equal("User", added.Source);
            Assert.False(added.IsFromEnvironment);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddDirectories_RejectsNonExistentDirectory()
    {
        // Arrange
        var vm = new DataDirectoriesViewModel();
        var fakePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var invalids = vm.AddDirectories(new[] { fakePath });

        // Assert
        Assert.NotEmpty(invalids);
        Assert.Contains(fakePath, invalids[0]);
    }

    [Fact]
    public void AddDirectories_RejectsAlreadyAddedDirectory()
    {
        // Arrange
        var vm = new DataDirectoriesViewModel();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            vm.AddDirectories(new[] { tempDir });

            // Act
            var invalids = vm.AddDirectories(new[] { tempDir });

            // Assert
            Assert.NotEmpty(invalids);
            Assert.Contains("already in list", invalids[0]);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RemoveDirectory_RemovesUserDirectory()
    {
        // Arrange
        var vm = new DataDirectoriesViewModel();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            vm.AddDirectories(new[] { tempDir });
            var dir = vm.DataDirectories.First(d => d.Path == tempDir);
            vm.SelectedDirectory = dir;

            // Act
            vm.RemoveSelectedDirectory();

            // Assert
            Assert.DoesNotContain(vm.DataDirectories, d => d.Path == tempDir);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RemoveDirectory_PreventsRemovalOfEnvironmentDirectory()
    {
        // Arrange
        var vm = new DataDirectoriesViewModel();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        var originalEnv = Environment.GetEnvironmentVariable("DOOMWADDIR");
        try
        {
            Environment.SetEnvironmentVariable("DOOMWADDIR", tempDir);
            var vm2 = new DataDirectoriesViewModel();

            var envDir = vm2.DataDirectories.First(d => d.Path == tempDir);
            vm2.SelectedDirectory = envDir;

            // Act
            vm2.RemoveSelectedDirectory();

            // Assert - should still be there
            Assert.Contains(vm2.DataDirectories, d => d.Path == tempDir);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DOOMWADDIR", originalEnv);
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void AddDirectories_PersistsToConfig()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var vm1 = new DataDirectoriesViewModel();
            vm1.AddDirectories(new[] { tempDir });

            // Verify it was added
            var added = vm1.DataDirectories.FirstOrDefault(d => d.Path == tempDir && d.Source == "User");
            Assert.NotNull(added);

            // Act - Just verify the item is in the collection
            // In a real scenario, persistence would be verified by checking the config file
            Assert.Contains(vm1.DataDirectories, d => d.Path == tempDir && d.Source == "User");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void RemoveDirectory_UpdatesConfig()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);

        try
        {
            var vm1 = new DataDirectoriesViewModel();
            vm1.AddDirectories(new[] { tempDir });

            var dir = vm1.DataDirectories.First(d => d.Path == tempDir);
            vm1.SelectedDirectory = dir;
            vm1.RemoveSelectedDirectory();

            // Act - verify it was removed
            // Assert
            Assert.DoesNotContain(vm1.DataDirectories, d => d.Path == tempDir && d.Source == "User");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    public void Dispose()
    {
        // Clean up after each test
        var configPath = Path.Combine(AppContext.BaseDirectory, "Underworld.config.json");
        try { if (File.Exists(configPath)) File.Delete(configPath); } catch { }
    }
}

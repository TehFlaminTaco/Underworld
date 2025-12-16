#nullable enable

using System.Linq;
using System.Threading.Tasks;
using Underworld.Models;
using Underworld.ViewModels;
using Xunit;

namespace Underworld.Tests.ViewModelTests;

public class MiniLauncherViewModelTests
{
    [Fact]
    public void InitializesWithDefaultSkillsAndLevels()
    {
        var vm = new MiniLauncherViewModel(_ => Task.CompletedTask);

        Assert.NotEmpty(vm.Skills);
        Assert.NotNull(vm.SelectedSkill);
        Assert.NotEmpty(vm.AvailableLevels);
        Assert.Equal("MAP01", vm.AvailableLevels.First().LumpName);
    }

    [Fact]
    public async Task RunCommandPassesSelectedOptions()
    {
        MiniLauncherOptions? capturedOptions = null;
        var completion = new TaskCompletionSource();

        var levels = new[]
        {
            new LevelEntry { LumpName = "MAP01", DisplayName = "Hangar" },
            new LevelEntry { LumpName = "MAP02", DisplayName = "Plant" }
        };

        var vm = new MiniLauncherViewModel(options =>
        {
            capturedOptions = options;
            completion.SetResult();
            return Task.CompletedTask;
        },
        () => levels);

        vm.SelectedSkill = vm.Skills.Last();
        vm.SelectedLevel = levels.Last();
        vm.IsMultiplayerEnabled = true;
        vm.HostGame = false;
        vm.IPAddress = "127.0.0.1";
        vm.Port = "10666";
        vm.NoMonsters = true;
        vm.FastMonsters = true;
        vm.RespawnMonsters = true;

        vm.RunCommand.Execute(null);
        await completion.Task;

        Assert.NotNull(capturedOptions);
        Assert.Equal(vm.SelectedSkill!.Value, capturedOptions!.Skill);
        Assert.Equal("MAP02", capturedOptions.InitialLevel);
        Assert.True(capturedOptions.EnableMultiplayer);
        Assert.False(capturedOptions.HostGame);
        Assert.Equal("127.0.0.1", capturedOptions.IPAddress);
        Assert.Equal("10666", capturedOptions.Port);
        Assert.True(capturedOptions.NoMonsters);
        Assert.True(capturedOptions.FastMonsters);
        Assert.True(capturedOptions.RespawnMonsters);
    }

    [Fact]
    public async Task RunCommandPassesHostOptions()
    {
        MiniLauncherOptions? capturedOptions = null;
        var completion = new TaskCompletionSource();

        var vm = new MiniLauncherViewModel(options =>
        {
            capturedOptions = options;
            completion.SetResult();
            return Task.CompletedTask;
        });

        vm.IsMultiplayerEnabled = true;
        vm.HostGame = true;
        vm.HostPort = "7777";
        vm.HostPlayerSlots = "6";

        vm.RunCommand.Execute(null);
        await completion.Task;

        Assert.NotNull(capturedOptions);
        Assert.True(capturedOptions!.EnableMultiplayer);
        Assert.True(capturedOptions.HostGame);
        Assert.Equal("7777", capturedOptions.HostPort);
        Assert.Equal(6, capturedOptions.HostPlayerCount);
    }

    [Fact]
    public void CancelCommandPersistsState()
    {
        MiniLauncherOptions? persisted = null;
        var vm = new MiniLauncherViewModel(_ => Task.CompletedTask,
            initialOptions: new MiniLauncherOptions { EnableMultiplayer = true },
            persistHandler: options => persisted = options);

        vm.HostGame = true;
        vm.HostPlayerSlots = "8";

        vm.CancelCommand.Execute(null);

        Assert.NotNull(persisted);
        Assert.True(persisted!.HostGame);
        Assert.Equal(8, persisted.HostPlayerCount);
    }

    [Fact]
    public void InitialOptionsSelectExistingLevel()
    {
        var levels = new[]
        {
            new LevelEntry { LumpName = "MAP01", DisplayName = "Hangar" },
            new LevelEntry { LumpName = "MAP02", DisplayName = "Plant" }
        };

        var options = new MiniLauncherOptions { InitialLevel = "MAP02" };
        var vm = new MiniLauncherViewModel(_ => Task.CompletedTask,
            () => levels,
            options);

        Assert.Equal("MAP02", vm.SelectedLevel?.LumpName);

        options.InitialLevel = "E1M1";
        var vmWithMissingLevel = new MiniLauncherViewModel(_ => Task.CompletedTask,
            () => levels,
            options);

        Assert.Equal("MAP01", vmWithMissingLevel.SelectedLevel?.LumpName);
    }

    [Fact]
    public void InitializesFromProvidedOptions()
    {
        var initial = new MiniLauncherOptions
        {
            Skill = 5,
            InitialLevel = "MAP01",
            EnableMultiplayer = true,
            HostGame = true,
            IPAddress = "192.168.0.10",
            Port = "5000",
            HostPort = "6000",
            HostPlayerCount = 7,
            NoMonsters = true,
            FastMonsters = true,
            RespawnMonsters = true
        };

        var vm = new MiniLauncherViewModel(_ => Task.CompletedTask, null, initial);

        Assert.Equal(initial.Skill, vm.SelectedSkill!.Value);
        Assert.Equal(initial.InitialLevel, vm.SelectedLevel?.LumpName);
        Assert.True(vm.IsMultiplayerEnabled);
        Assert.True(vm.HostGame);
        Assert.Equal(initial.IPAddress, vm.IPAddress);
        Assert.Equal(initial.Port, vm.Port);
        Assert.Equal(initial.HostPort, vm.HostPort);
        Assert.Equal(initial.HostPlayerCount.ToString(), vm.HostPlayerSlots);
        Assert.True(vm.NoMonsters);
        Assert.True(vm.FastMonsters);
        Assert.True(vm.RespawnMonsters);
    }

    [Fact]
    public void PersistStateInvokesCallback()
    {
        MiniLauncherOptions? persisted = null;
        var vm = new MiniLauncherViewModel(_ => Task.CompletedTask, null, null, options => persisted = options);

        vm.IsMultiplayerEnabled = true;
        vm.Port = "4242";
        vm.CancelCommand.Execute(null);

        Assert.NotNull(persisted);
        Assert.Equal("4242", persisted!.Port);
        Assert.True(persisted.EnableMultiplayer);
    }
}

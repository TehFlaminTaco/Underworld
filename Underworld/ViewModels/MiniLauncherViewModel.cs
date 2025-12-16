using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Underworld.Models;

namespace Underworld.ViewModels;

public class MiniLauncherViewModel : ViewModelBase
{
    private readonly Func<MiniLauncherOptions, Task> _runHandler;
    private readonly Func<IEnumerable<LevelEntry>> _levelProvider;
    private readonly Action<MiniLauncherOptions>? _persistHandler;
    private readonly RelayCommand _runCommand;
    private readonly RelayCommand _cancelCommand;

    public MiniLauncherViewModel(
        Func<MiniLauncherOptions, Task> runHandler,
        Func<IEnumerable<LevelEntry>>? levelProvider = null,
        MiniLauncherOptions? initialOptions = null,
        Action<MiniLauncherOptions>? persistHandler = null)
    {
        _runHandler = runHandler ?? throw new ArgumentNullException(nameof(runHandler));
        _levelProvider = levelProvider ?? GetStubbedLevelList;
        _persistHandler = persistHandler;

        LoadSkills();
        LoadAvailableLevels();
        ApplyOptions(initialOptions ?? new MiniLauncherOptions());

        _runCommand = new RelayCommand(_ => RunAsync(), _ => !IsRunning);
        _cancelCommand = new RelayCommand(_ =>
        {
            PersistState();
            CloseRequested?.Invoke(this, EventArgs.Empty);
        });
    }

    public ObservableCollection<SkillOption> Skills { get; } = new();

    private SkillOption? _selectedSkill;
    public SkillOption? SelectedSkill
    {
        get => _selectedSkill;
        set => SetProperty(ref _selectedSkill, value);
    }

    public ObservableCollection<LevelEntry> AvailableLevels { get; } = new();

    private LevelEntry? _selectedLevel;
    public LevelEntry? SelectedLevel
    {
        get => _selectedLevel;
        set => SetProperty(ref _selectedLevel, value);
    }

    private bool _isMultiplayerEnabled;
    public bool IsMultiplayerEnabled
    {
        get => _isMultiplayerEnabled;
        set
        {
            if (SetProperty(ref _isMultiplayerEnabled, value))
            {
                OnPropertyChanged(nameof(IsJoinMode));
                OnPropertyChanged(nameof(IsHostMode));
            }
        }
    }

    private bool _hostGame;
    public bool HostGame
    {
        get => _hostGame;
        set
        {
            if (SetProperty(ref _hostGame, value))
            {
                OnPropertyChanged(nameof(IsJoinMode));
                OnPropertyChanged(nameof(IsHostMode));
            }
        }
    }

    public bool IsJoinMode => IsMultiplayerEnabled && !HostGame;
    public bool IsHostMode => IsMultiplayerEnabled && HostGame;

    private string _ipAddress = string.Empty;
    public string IPAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value ?? string.Empty);
    }

    private string _port = string.Empty;
    public string Port
    {
        get => _port;
        set => SetProperty(ref _port, value ?? string.Empty);
    }

    private string _hostPort = "10666";
    public string HostPort
    {
        get => _hostPort;
        set => SetProperty(ref _hostPort, value ?? string.Empty);
    }

    private string _hostPlayerSlots = "4";
    public string HostPlayerSlots
    {
        get => _hostPlayerSlots;
        set => SetProperty(ref _hostPlayerSlots, value ?? string.Empty);
    }

    private bool _noMonsters;
    public bool NoMonsters
    {
        get => _noMonsters;
        set => SetProperty(ref _noMonsters, value);
    }

    private bool _fastMonsters;
    public bool FastMonsters
    {
        get => _fastMonsters;
        set => SetProperty(ref _fastMonsters, value);
    }

    private bool _respawnMonsters;
    public bool RespawnMonsters
    {
        get => _respawnMonsters;
        set => SetProperty(ref _respawnMonsters, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value))
            {
                _runCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand RunCommand => _runCommand;
    public ICommand CancelCommand => _cancelCommand;

    public event EventHandler? CloseRequested;

    private void LoadSkills()
    {
        Skills.Clear();
        var defaults = new[]
        {
            new SkillOption("I'm Too Young to Die", 1),
            new SkillOption("Hey, Not Too Rough", 2),
            new SkillOption("Hurt Me Plenty", 3),
            new SkillOption("Ultra-Violence", 4),
            new SkillOption("Nightmare!", 5)
        };

        foreach (var skill in defaults)
        {
            Skills.Add(skill);
        }

        SelectedSkill = Skills.FirstOrDefault(s => s.Value == 3) ?? Skills.FirstOrDefault();
    }

    private void LoadAvailableLevels()
    {
        AvailableLevels.Clear();
        foreach (var level in _levelProvider())
        {
            if (!string.IsNullOrWhiteSpace(level.LumpName))
            {
                AvailableLevels.Add(level);
            }
        }

        SelectedLevel = AvailableLevels.FirstOrDefault();
    }

    private IEnumerable<LevelEntry> GetStubbedLevelList()
    {
        // Minimal placeholder implementation; will be replaced with IWAD/PWAD inspection later.
        return new[] 
        { 
            new LevelEntry { LumpName = "MAP01", DisplayName = "MAP01" }, 
            new LevelEntry { LumpName = "MAP02", DisplayName = "MAP02" }, 
            new LevelEntry { LumpName = "MAP03", DisplayName = "MAP03" } 
        };
    }

    private void ApplyOptions(MiniLauncherOptions options)
    {
        var skillValue = options.Skill <= 0 ? 3 : options.Skill;
        var desiredSkill = Skills.FirstOrDefault(s => s.Value == skillValue);
        if (desiredSkill != null)
        {
            SelectedSkill = desiredSkill;
        }

        if (!string.IsNullOrWhiteSpace(options.InitialLevel))
        {
            if (!AvailableLevels.Any(level => level.LumpName == options.InitialLevel))
            {
                SelectedLevel = AvailableLevels.FirstOrDefault();
            }else{
                SelectedLevel = AvailableLevels.FirstOrDefault(level => level.LumpName == options.InitialLevel);
            }
        }

        IsMultiplayerEnabled = options.EnableMultiplayer;
        HostGame = options.HostGame;
        IPAddress = options.IPAddress ?? string.Empty;
        Port = options.Port ?? string.Empty;
        HostPort = string.IsNullOrWhiteSpace(options.HostPort) ? "10666" : options.HostPort!;
        HostPlayerSlots = (options.HostPlayerCount <= 0 ? 4 : options.HostPlayerCount).ToString();
        NoMonsters = options.NoMonsters;
        FastMonsters = options.FastMonsters;
        RespawnMonsters = options.RespawnMonsters;
    }

    private async void RunAsync()
    {
        if (IsRunning)
        {
            return;
        }

        IsRunning = true;
        try
        {
            var options = BuildOptions();
            _persistHandler?.Invoke(options);
            await _runHandler(options);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[MiniLauncher] Failed to run game: {ex}");
        }
        finally
        {
            IsRunning = false;
        }
    }

    private MiniLauncherOptions BuildOptions()
    {
        var parsedHostSlots = 4;
        if (!int.TryParse(HostPlayerSlots, out parsedHostSlots) || parsedHostSlots < 1)
        {
            parsedHostSlots = 4;
        }

        return new MiniLauncherOptions
        {
            Skill = SelectedSkill?.Value ?? 3,
            InitialLevel = SelectedLevel?.LumpName,
            EnableMultiplayer = IsMultiplayerEnabled,
            HostGame = HostGame,
            IPAddress = (IPAddress ?? string.Empty).Trim(),
            Port = (Port ?? string.Empty).Trim(),
            HostPort = (HostPort ?? string.Empty).Trim(),
            HostPlayerCount = parsedHostSlots,
            NoMonsters = NoMonsters,
            FastMonsters = FastMonsters,
            RespawnMonsters = RespawnMonsters
        };
    }

    public void PersistState()
    {
        _persistHandler?.Invoke(BuildOptions());
    }

    public sealed class SkillOption
    {
        public SkillOption(string name, int value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }
        public int Value { get; }

        public override string ToString() => Name;
    }
}

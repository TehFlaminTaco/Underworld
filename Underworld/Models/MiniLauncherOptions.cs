namespace Underworld.Models;

/// <summary>
/// Represents the transient launch options configured through the mini launcher window.
/// </summary>
public class MiniLauncherOptions
{
    /// <summary>
    /// Numeric Doom skill level (1-5). Defaults to 3 (Hurt Me Plenty).
    /// </summary>
    public int Skill { get; set; } = 3;

    /// <summary>
    /// Optional map identifier to jump to when launching (e.g. MAP01, E1M1).
    /// </summary>
    public string? InitialLevel { get; set; }

    /// <summary>
    /// Indicates whether multiplayer flags should be enabled.
    /// </summary>
    public bool EnableMultiplayer { get; set; }

    /// <summary>
    /// IP address or hostname for multiplayer connection when joining a game.
    /// </summary>
    public string? IPAddress { get; set; }

    /// <summary>
    /// Port number for multiplayer connection when joining a game.
    /// </summary>
    public string? Port { get; set; }

    /// <summary>
    /// True if the launcher should host a game instead of joining one.
    /// </summary>
    public bool HostGame { get; set; }

    /// <summary>
    /// Port to listen on when hosting.
    /// </summary>
    public string? HostPort { get; set; } = "10666";

    /// <summary>
    /// Player slots to allocate when hosting.
    /// </summary>
    public int HostPlayerCount { get; set; } = 4;

    public bool NoMonsters { get; set; }
    public bool FastMonsters { get; set; }
    public bool RespawnMonsters { get; set; }

    public MiniLauncherOptions Clone()
    {
        return new MiniLauncherOptions
        {
            Skill = Skill,
            InitialLevel = InitialLevel,
            EnableMultiplayer = EnableMultiplayer,
            IPAddress = IPAddress,
            Port = Port,
            HostGame = HostGame,
            HostPort = HostPort,
            HostPlayerCount = HostPlayerCount,
            NoMonsters = NoMonsters,
            FastMonsters = FastMonsters,
            RespawnMonsters = RespawnMonsters
        };
    }
}

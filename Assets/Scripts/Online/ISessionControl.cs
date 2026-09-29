using System;

/// <summary>
/// How the lobby flow (OnlineLobby) starts the game's session: over Steam in the game, by IP address in tests and
/// editor tools. See docs/online.md
/// </summary>
public interface ISessionControl
{
    bool IsRunning { get; }

    /// <summary>Hosts a session. False when it can't start</summary>
    bool Host();

    /// <summary>Joins the session of a lobby's owner (their Steam ID over Steam). False when it can't start</summary>
    bool Join(ulong hostId);

    /// <summary>As OnlineSession.RoleChanged</summary>
    event Action<NetworkRole> RoleChanged;

    /// <summary>As OnlineSession.Ended: why a session ended by itself</summary>
    event Action<string> Ended;
}

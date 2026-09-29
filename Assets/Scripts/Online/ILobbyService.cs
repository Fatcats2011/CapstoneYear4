using System;

/// <summary>
/// Lobbies where friends meet before an online match: Steam's in the game (SteamLobbyService), a fake in tests. Every
/// answer comes back through an event, maybe much later. See docs/online.md
/// </summary>
public interface ILobbyService
{
    /// <summary>Whether lobbies work here (Steam is running)</summary>
    bool Available { get; }

    /// <summary>Makes a friends-only lobby for maxMembers players. Created follows</summary>
    void Create(int maxMembers);

    /// <summary>The lobby made, or 0 when it couldn't be made</summary>
    event Action<ulong> Created;

    /// <summary>Enters a lobby. Entered follows</summary>
    void Join(ulong lobby);

    /// <summary>A lobby, and whether this machine got in</summary>
    event Action<ulong, bool> Entered;

    /// <summary>The player accepted a friend's invite, or picked "Join Game" in the friends list</summary>
    event Action<ulong> JoinRequested;

    void Leave(ulong lobby);

    void SetData(ulong lobby, string key, string value);

    /// <summary>A lobby's tag, or "" when it's unknown</summary>
    string GetData(ulong lobby, string key);

    /// <summary>Who owns a lobby (a Steam ID), or 0 when it's unknown</summary>
    ulong Owner(ulong lobby);

    void SetJoinable(ulong lobby, bool joinable);

    /// <summary>Opens the invite dialog for a lobby. False when it can't open (Steam's overlay is off)</summary>
    bool Invite(ulong lobby);
}

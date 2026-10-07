#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Collections.Generic;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Steam's lobbies (Steamworks.NET): friends-only lobbies with tags, invites through Steam's overlay, and the requests
/// Steam raises when the player accepts an invite or picks "Join Game". Without Steam every call answers at once (no
/// lobby, not entered) and nothing calls Steamworks, which throws without Steam. See docs/online.md
/// </summary>
public class SteamLobbyService : ILobbyService
{
    public event Action<ulong> Created;
    public event Action<ulong, bool> Entered;
    public event Action<ulong> JoinRequested;

#if !DISABLESTEAMWORKS
    // Each call waits for its own answer, kept here until it comes: a CallResult set again drops the answer it was
    // waiting for, and one that's collected cancels it. A join answers through its call, so the LobbyEnter_t Steam
    // also sends to a lobby's creator never looks like a join
    readonly List<CallResult<LobbyCreated_t>> creates = new List<CallResult<LobbyCreated_t>>();
    readonly List<CallResult<LobbyEnter_t>> joins = new List<CallResult<LobbyEnter_t>>();
    readonly Callback<GameLobbyJoinRequested_t> joinRequested;
#endif

    public SteamLobbyService()
    {
#if !DISABLESTEAMWORKS
        if (Available)
            joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
#endif
    }

    /// <summary>Whether Steam is running</summary>
    public bool Available { get { return SteamManager.Initialized; } }

#if !DISABLESTEAMWORKS
    /// <summary>Whether Steam took a call: one it refuses at once is invalid, and no answer will ever come for it</summary>
    internal static bool Started(SteamAPICall_t call)
    {
        return call != SteamAPICall_t.Invalid;
    }
#endif

    public void Create(int maxMembers)
    {
#if !DISABLESTEAMWORKS
        if (Available)
        {
            SteamAPICall_t made = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers);
            if (!Started(made))
            {
                Created?.Invoke(0);
                return;
            }
            CallResult<LobbyCreated_t> call = CallResult<LobbyCreated_t>.Create(OnCreated);
            call.Set(made);
            creates.RemoveAll(Answered);
            creates.Add(call);
            return;
        }
#endif
        Created?.Invoke(0);
    }

    public void Join(ulong lobby)
    {
#if !DISABLESTEAMWORKS
        if (Available)
        {
            SteamAPICall_t entering = SteamMatchmaking.JoinLobby(new CSteamID(lobby));
            if (!Started(entering))
            {
                Entered?.Invoke(lobby, false);
                return;
            }
            CallResult<LobbyEnter_t> call = CallResult<LobbyEnter_t>.Create(
                delegate (LobbyEnter_t result, bool ioFailure) { OnEntered(lobby, result, ioFailure); });
            call.Set(entering);
            joins.RemoveAll(Answered);
            joins.Add(call);
            return;
        }
#endif
        Entered?.Invoke(lobby, false);
    }

    public void Leave(ulong lobby)
    {
#if !DISABLESTEAMWORKS
        if (Available)
            SteamMatchmaking.LeaveLobby(new CSteamID(lobby));
#endif
    }

    public void SetData(ulong lobby, string key, string value)
    {
#if !DISABLESTEAMWORKS
        if (Available)
            SteamMatchmaking.SetLobbyData(new CSteamID(lobby), key, value);
#endif
    }

    public string GetData(ulong lobby, string key)
    {
#if !DISABLESTEAMWORKS
        if (Available)
            return SteamMatchmaking.GetLobbyData(new CSteamID(lobby), key);
#endif
        return "";
    }

    public ulong Owner(ulong lobby)
    {
#if !DISABLESTEAMWORKS
        if (Available)
            return SteamMatchmaking.GetLobbyOwner(new CSteamID(lobby)).m_SteamID;
#endif
        return 0;
    }

    /// <summary>Whether a Steam user is in a lobby now (false without Steam)</summary>
    public bool IsMember(ulong lobby, ulong steamId)
    {
#if !DISABLESTEAMWORKS
        if (Available && lobby != 0)
        {
            CSteamID id = new CSteamID(lobby);
            int count = SteamMatchmaking.GetNumLobbyMembers(id);
            for (int i = 0; i < count; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(id, i).m_SteamID == steamId)
                    return true;
            }
        }
#endif
        return false;
    }

    public void SetJoinable(ulong lobby, bool joinable)
    {
#if !DISABLESTEAMWORKS
        if (Available)
            SteamMatchmaking.SetLobbyJoinable(new CSteamID(lobby), joinable);
#endif
    }

    public bool Invite(ulong lobby)
    {
#if !DISABLESTEAMWORKS
        if (Available && SteamUtils.IsOverlayEnabled())
        {
            SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobby));
            return true;
        }
#endif
        return false;
    }

#if !DISABLESTEAMWORKS
    void OnCreated(LobbyCreated_t result, bool ioFailure)
    {
        bool made = !ioFailure && result.m_eResult == EResult.k_EResultOK;
        Created?.Invoke(made ? result.m_ulSteamIDLobby : 0);
    }

    void OnEntered(ulong lobby, LobbyEnter_t result, bool ioFailure)
    {
        bool entered = !ioFailure
            && result.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
        Entered?.Invoke(lobby, entered);
    }

    void OnJoinRequested(GameLobbyJoinRequested_t request)
    {
        JoinRequested?.Invoke(request.m_steamIDLobby.m_SteamID);
    }

    static bool Answered<T>(CallResult<T> call)
    {
        return !call.IsActive();
    }
#endif
}

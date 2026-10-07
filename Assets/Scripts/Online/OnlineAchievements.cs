using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The session's achievements on this machine (online). The host decides who earned what (SteamFeatures.Earned):
/// - Host: unlocks its own player's here, and sends every other player's to every client (OnlineMatch.SendAchievement).
/// - Client: unlocks only its own player's (their seat), as the host sent them. A client never decides: its deliveries
///   are replays of the host's.
/// OnlineGame adds it next to OnlineCues. See docs/steam/in-game-features.md
/// </summary>
public class OnlineAchievements : MonoBehaviour
{
    OnlineSession session;
    IAchievementStore store;
    OnlineMatch listening; // the match whose achievements this machine hears

    /// <summary>
    /// Starts unlocking this session's achievements in a store (Steam's in the game)
    /// </summary>
    public void Begin(OnlineSession onlineSession, IAchievementStore achievements)
    {
        session = onlineSession;
        store = achievements;
        SteamFeatures.Earned += OnEarned;
        session.MatchSpawned += OnMatchSpawned;
        if (session.Match != null)
            OnMatchSpawned(session.Match);
    }

    // SteamFeatures.Earned is static: an ended session never keeps listening
    void OnDestroy()
    {
        SteamFeatures.Earned -= OnEarned;
        if (session != null)
            session.MatchSpawned -= OnMatchSpawned;
        StopListening();
    }

    void OnMatchSpawned(OnlineMatch match)
    {
        StopListening();
        listening = match;
        match.AchievementReceived += OnReceived;
    }

    void StopListening()
    {
        if (listening != null)
            listening.AchievementReceived -= OnReceived;
        listening = null;
    }

    // This machine's players' seats in the session, from their OnlinePlayers (several can share its screen)
    List<int> OwnSeats
    {
        get
        {
            ownSeats.Clear();
            foreach (OnlinePlayer player in session.Players)
            {
                if (player != null && player.IsOwner)
                    ownSeats.Add(player.Seat);
            }
            return ownSeats;
        }
    }

    readonly List<int> ownSeats = new List<int>();

    // Host: someone earned an achievement. The host's own player's unlocks here; anyone else's goes to every client
    void OnEarned(int seat, Achievement achievement)
    {
        OnlineMatch match = session.Match;
        if (match == null || !match.IsServer)
            return;

        if (MatchFeats.UnlocksHere(true, seat, OwnSeats))
            Unlock(achievement);
        else
            match.SendAchievement(seat, achievement);
    }

    // Client: the host says the player in a seat earned an achievement: this machine's own player's unlocks here
    void OnReceived(int seat, Achievement achievement)
    {
        if (MatchFeats.UnlocksHere(true, seat, OwnSeats))
            Unlock(achievement);
    }

    void Unlock(Achievement achievement)
    {
        if (store != null && store.Available)
            store.Unlock(achievement);
    }
}

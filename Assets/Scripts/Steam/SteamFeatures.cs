using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Steam features a player sees in the game. See docs/steam/in-game-features.md
/// - Achievements: where the rules run (this PC offline, the host online), deliveries (FeatSync) and the results count
///   towards them (MatchFeats), and each one earned is raised (Earned). Offline every seat is this PC's Steam user, so it
///   unlocks here; online OnlineAchievements unlocks each on its player's own machine.
/// - Rich Presence: every game state sets what friends see (PresenceRules).
/// Made once the first scene has loaded, so no scene needs to contain it. Steam is reached through IAchievementStore and
/// IPresence, which tests replace (Use)
/// </summary>
public class SteamFeatures : MonoBehaviour
{
    public static SteamFeatures Instance { get; private set; }

    /// <summary>
    /// The rules' machine: the player in a seat earned an achievement. Online, OnlineAchievements unlocks it on their
    /// machine
    /// </summary>
    public static event Action<int, Achievement> Earned;

    readonly MatchFeats feats = new MatchFeats();
    IAchievementStore achievements;
    IPresence presence;
    GameManager watched; // the game manager whose states this follows

    /// <summary>This PC's Steam user's achievements</summary>
    public IAchievementStore Achievements { get { return achievements; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        Earned = null;
        FeatSync.Reset();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnLaunch()
    {
        if (Instance != null)
            return;

        GameObject holder = new GameObject(nameof(SteamFeatures));
        DontDestroyOnLoad(holder);
        holder.AddComponent<SteamFeatures>();
    }

    void Awake()
    {
        Instance = this;
        Use(new SteamAchievementStore(), new SteamPresence());
        FeatSync.Delivered += OnDelivered;
    }

    /// <summary>
    /// Where achievements unlock and presence goes: Steam in the game, fakes in tests
    /// </summary>
    public void Use(IAchievementStore achievementStore, IPresence richPresence)
    {
        achievements = achievementStore;
        presence = richPresence;
    }

    void OnDestroy()
    {
        FeatSync.Delivered -= OnDelivered;
        Watch(null);
        if (Instance == this)
            Instance = null;
    }

    // The game manager can arrive after this (it lives in the menu scene), and a scene can bring a new one
    void Update()
    {
        if (GameManager.Instance != watched)
            Watch(GameManager.Instance);
    }

    void Watch(GameManager gameManager)
    {
        if (watched != null)
            watched.StateApplied -= OnStateApplied;
        watched = gameManager;
        if (watched != null)
            watched.StateApplied += OnStateApplied;
    }

    /// <summary>
    /// Unlocks an achievement for this PC's Steam user, when Steam is running
    /// </summary>
    public void Unlock(Achievement achievement)
    {
        if (achievements != null && achievements.Available)
            achievements.Unlock(achievement);
    }

    // Only the rules' machine counts deliveries: a client's are replays of the host's
    void OnDelivered(int seat, bool golden)
    {
        if (!GameAuthority.IsAuthority)
            return;

        GameState state = GameManager.Instance != null ? GameManager.Instance.MainState : GameState.Default;
        foreach (Feat feat in feats.Delivered(seat, golden, state))
            Earn(feat);
    }

    void OnStateApplied(GameState state)
    {
        if (state == GameState.Menu || state == GameState.PlayerSelect)
            feats.NewMatch();
        else if (state == GameState.Results && GameAuthority.IsAuthority)
        {
            foreach (Feat feat in feats.Results(Scores()))
                Earn(feat);
        }

        ShowPresence(state);
    }

    /// <summary>
    /// Tests: raises Earned as if the rules' machine decided it
    /// </summary>
    public static void RaiseEarned(int seat, Achievement achievement)
    {
        Earned?.Invoke(seat, achievement);
    }

    void Earn(Feat feat)
    {
        Earned?.Invoke(feat.Seat, feat.Achievement);
        if (!GameAuthority.IsOnline)
            Unlock(feat.Achievement);
    }

    void ShowPresence(GameState state)
    {
        int players = PlayerInstantiate.Instance != null ? PlayerInstantiate.Instance.PlayerCount : 0;
        string token = PresenceRules.For(state, players);
        if (token == null || presence == null || !presence.Available)
            return;

        presence.Set(PresenceRules.PLAYERS, players.ToString(System.Globalization.CultureInfo.InvariantCulture));
        presence.Set(PresenceRules.DISPLAY, token);
    }

    // Each seat's score now (the roster's players)
    static Dictionary<int, int> Scores()
    {
        Dictionary<int, int> scores = new Dictionary<int, int>();
        if (PlayerInstantiate.Instance == null)
            return scores;

        foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
        {
            OrderHandler handler = OrderSync.HandlerIn(slot.Index);
            if (handler != null)
                scores[slot.Index] = handler.Score;
        }
        return scores;
    }
}

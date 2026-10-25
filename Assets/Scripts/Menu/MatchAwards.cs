using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The end-of-match awards on this machine. Where the rules run (this PC offline, the host online) it counts what the
/// game raises (FeatSync: deliveries, steals, robberies, falls) and who holds the golden order in the golden round
/// (MatchStats), and picks the awards as the results start (AwardSync.Decide; online OnlineAwards shares them). An online
/// client counts nothing: the host's arrive. ResultsMenu shows them. A new match (GameManager.OnNewMatch) forgets the
/// last. Made once the first scene has loaded, as SteamFeatures. See docs/superpowers/specs/2026-10-24-awards-design.md
/// </summary>
public class MatchAwards : MonoBehaviour
{
    public static MatchAwards Instance { get; private set; }

    /// <summary>Whether the player in a seat holds the golden order now; tests swap it</summary>
    internal Func<int, bool> HoldsGolden = HandlerHoldsGolden;

    readonly MatchStats stats = new MatchStats();
    GameManager watched; // the game manager whose states this follows
    bool listening;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        AwardSync.Reset();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnLaunch()
    {
        if (Instance != null)
            return;

        GameObject holder = new GameObject(nameof(MatchAwards));
        DontDestroyOnLoad(holder);
        holder.AddComponent<MatchAwards>();
    }

    void Awake()
    {
        Instance = this;
        Begin();
    }

    void OnDestroy()
    {
        End();
        Watch(null);
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Starts counting what the game raises</summary>
    internal void Begin()
    {
        if (listening)
            return;
        listening = true;
        FeatSync.Delivered += OnDelivered;
        FeatSync.Stole += OnStole;
        FeatSync.Robbed += OnRobbed;
        FeatSync.Sank += OnSank;
    }

    /// <summary>Stops counting</summary>
    internal void End()
    {
        listening = false;
        FeatSync.Delivered -= OnDelivered;
        FeatSync.Stole -= OnStole;
        FeatSync.Robbed -= OnRobbed;
        FeatSync.Sank -= OnSank;
    }

    void Update()
    {
        if (GameManager.Instance != watched)
            Watch(GameManager.Instance);
        Tick(Time.deltaTime, MainState);
    }

    /// <summary>A frame went by: in the golden round, whoever holds the golden order held it that much longer</summary>
    internal void Tick(float deltaTime, GameState state)
    {
        if (state != GameState.FinalPackage || !GameAuthority.IsAuthority)
            return;
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            if (HoldsGolden(seat))
                stats.HeldGolden(seat, deltaTime);
        }
    }

    /// <summary>The results start: where the rules run, the awards for these seats are picked</summary>
    internal void Results(IEnumerable<int> seats)
    {
        if (GameAuthority.IsAuthority)
            AwardSync.Decide(AwardBoard.From(stats, seats));
    }

    /// <summary>A new match: nothing counted, no awards</summary>
    internal void NewMatch()
    {
        stats.NewMatch();
        AwardSync.Clear();
    }

    void Watch(GameManager gameManager)
    {
        if (watched != null)
        {
            watched.StateApplied -= OnStateApplied;
            watched.OnNewMatch -= NewMatch;
        }
        watched = gameManager;
        if (watched != null)
        {
            watched.StateApplied += OnStateApplied;
            watched.OnNewMatch += NewMatch;
        }
    }

    void OnStateApplied(GameState state)
    {
        if (state == GameState.Results)
            Results(RosterSeats());
    }

    // A client's are replays of the host's: only the rules' machine counts
    void OnDelivered(int seat, Constants.OrderValue value)
    {
        if (GameAuthority.IsAuthority)
            stats.Delivered(seat, value, MainState);
    }

    void OnStole(int seat, bool golden)
    {
        if (GameAuthority.IsAuthority)
            stats.Stole(seat, MainState);
    }

    void OnRobbed(int seat)
    {
        if (GameAuthority.IsAuthority)
            stats.Robbed(seat, MainState);
    }

    void OnSank(int seat)
    {
        if (GameAuthority.IsAuthority)
            stats.Sank(seat, MainState);
    }

    static GameState MainState
    {
        get { return GameManager.Instance != null ? GameManager.Instance.MainState : GameState.Default; }
    }

    static bool HandlerHoldsGolden(int seat)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        return handler != null && handler.HoldsGoldenOrder;
    }

    static List<int> RosterSeats()
    {
        List<int> seats = new List<int>();
        if (PlayerInstantiate.Instance == null)
            return seats;
        foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
            seats.Add(slot.Index);
        return seats;
    }
}

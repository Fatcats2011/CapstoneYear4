using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The end-of-match awards on this machine. Where the rules run (this PC offline, the host online) it counts what the
/// game raises (FeatSync: deliveries, steals, robberies, falls, bumps, clashes), who holds the golden order in the
/// golden round, who is alone in first place and how far everyone drives (MatchStats), and picks the awards as the
/// results start (AwardSync.Decide; online OnlineAwards shares them). An online
/// client counts nothing: the host's arrive. ResultsMenu shows them. A new match (GameManager.OnNewMatch) forgets the
/// last. Made once the first scene has loaded, as SteamFeatures. See docs/superpowers/specs/2026-10-24-awards-design.md
/// </summary>
public class MatchAwards : MonoBehaviour
{
    public static MatchAwards Instance { get; private set; }

    /// <summary>Whether the player in a seat holds the golden order now; tests swap it</summary>
    internal Func<int, bool> HoldsGolden = HandlerHoldsGolden;

    /// <summary>The money of the player in a seat, or -1 with nobody there; tests swap it</summary>
    internal Func<int, int> MoneyOf = HandlerMoney;

    /// <summary>Where the scooter of the player in a seat is (false: nobody there); tests swap it</summary>
    internal PlaceIn PlaceOf = HandlerPlace;

    internal delegate bool PlaceIn(int seat, out Vector3 place);

    /// <summary>Faster than any scooter drives: a jump this quick is a respawn or a start, not driving (metres a second)</summary>
    internal const float TELEPORT_SPEED = 80f;

    readonly Vector3[] lastPlace = new Vector3[Constants.MAX_PLAYERS];
    readonly bool[] hasLastPlace = new bool[Constants.MAX_PLAYERS];

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
        FeatSync.Bumped += OnBumped;
        FeatSync.Struck += OnStruck;
    }

    /// <summary>Stops counting</summary>
    internal void End()
    {
        listening = false;
        FeatSync.Delivered -= OnDelivered;
        FeatSync.Stole -= OnStole;
        FeatSync.Robbed -= OnRobbed;
        FeatSync.Sank -= OnSank;
        FeatSync.Bumped -= OnBumped;
        FeatSync.Struck -= OnStruck;
    }

    void Update()
    {
        if (GameManager.Instance != watched)
            Watch(GameManager.Instance);
        Tick(Time.deltaTime, MainState);
    }

    /// <summary>
    /// A frame went by. While the match is driven (not the tutorial): whoever is alone in first place led that much
    /// longer, and everyone drove as far as they moved. In the golden round, whoever holds the golden order held it
    /// that much longer
    /// </summary>
    internal void Tick(float deltaTime, GameState state)
    {
        bool driving = state == GameState.Begin || state == GameState.MainLoop || state == GameState.FinalPackage;
        if (!driving || !GameAuthority.IsAuthority)
        {
            ForgetPlaces();
            return;
        }

        int leader = -1, most = 0;
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            int money = MoneyOf(seat);
            if (money > most)
            {
                most = money;
                leader = seat;
            }
            else if (money == most)
                leader = -1; // a tie leads nobody
            CountDriving(seat, deltaTime);

            if (state == GameState.FinalPackage && HoldsGolden(seat))
                stats.HeldGolden(seat, deltaTime);
        }
        if (leader >= 0)
            stats.Led(leader, deltaTime);
    }

    // How far a seat's scooter moved over the ground since the last frame, unless it jumped (a respawn)
    void CountDriving(int seat, float deltaTime)
    {
        if (!PlaceOf(seat, out Vector3 place))
        {
            hasLastPlace[seat] = false;
            return;
        }
        if (hasLastPlace[seat] && deltaTime > 0f)
        {
            float metres = new Vector2(place.x - lastPlace[seat].x, place.z - lastPlace[seat].z).magnitude;
            if (metres <= TELEPORT_SPEED * deltaTime)
                stats.Drove(seat, metres);
        }
        lastPlace[seat] = place;
        hasLastPlace[seat] = true;
    }

    void ForgetPlaces()
    {
        for (int seat = 0; seat < hasLastPlace.Length; seat++)
            hasLastPlace[seat] = false;
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
        ForgetPlaces();
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

    void OnBumped(int bumper, int victim, float strength)
    {
        if (GameAuthority.IsAuthority)
            stats.Bumped(bumper, MainState);
    }

    // A clash (both boosting, no steal) counts for both of them
    void OnStruck(int attacker, int victim, bool steal, bool golden)
    {
        if (steal || !GameAuthority.IsAuthority)
            return;
        stats.Clashed(attacker, MainState);
        stats.Clashed(victim, MainState);
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

    static int HandlerMoney(int seat)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        return handler != null ? handler.Score : -1;
    }

    static bool HandlerPlace(int seat, out Vector3 place)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        place = handler != null ? handler.transform.position : default;
        return handler != null;
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

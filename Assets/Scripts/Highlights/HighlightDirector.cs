using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Profiling;

/// <summary>
/// The highlight reel on this machine, once per launch. Through the main round it records every seat's scooter
/// (ReelRecording), and where the rules run (this PC offline, the host online) it logs what the game raises (FeatSync:
/// strikes, bumps, deliveries, falls) as moments for the rules to pick clips from (HighlightRules). An online client logs
/// none: it can only replay clips of what the host picked. Recording starts as the main round's waves do (state Begin), and stops when its
/// time is up (OrderManager.OnMainGameFinishes, which is the scene's, so it is hooked afresh each game scene). A new
/// match forgets the last round. Made once the first scene has loaded, as MatchAwards.
/// See docs/superpowers/specs/2026-10-28-highlight-reel-design.md
/// </summary>
public class HighlightDirector : MonoBehaviour
{
    /// <summary>A seat's scooter, and what it holds</summary>
    internal struct Seat
    {
        public int Index;
        public BallDriving Driving;
        public OrderHandler Handler;
    }

    /// <summary>Samples kept per seat: 10 minutes. A ring, so a longer round keeps its latest stretch</summary>
    const int CAPACITY = 12000;

    /// <summary>Seconds after it's picked that the reel starts, so the list can reach every machine first</summary>
    internal const float START_DELAY = 0.5f;

    public static HighlightDirector Instance { get; private set; }

    /// <summary>Every seat's samples in the round being played (null before the first)</summary>
    internal ReelRecording Recording;

    /// <summary>What happened in the round, in order (the rules machine only)</summary>
    internal readonly List<Moment> Moments = new List<Moment>();

    /// <summary>Fills the list with the seats to record; tests swap it</summary>
    internal Action<List<Seat>> FindSeats = RosterSeats;

    /// <summary>A seat's ball position now; tests swap it</summary>
    internal Func<int, Vector3> PlaceOf;

    /// <summary>How long is left of the round now; tests swap it</summary>
    internal Func<float> TimeLeft = RoundTimeLeft;

    readonly List<Seat> seats = new List<Seat>();
    GameManager watched; // the game manager whose states this follows
    OrderManager hooked; // the order manager whose end of round this waits for
    bool listening;
    bool active; // a main round is being recorded
    float sinceSample;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnLaunch()
    {
        if (Instance != null)
            return;

        GameObject holder = new GameObject(nameof(HighlightDirector));
        DontDestroyOnLoad(holder);
        holder.AddComponent<HighlightDirector>();
    }

    void Awake()
    {
        Instance = this;
        PlaceOf = SeatPlace;
        Begin();
    }

    void OnDestroy()
    {
        End();
        Watch(null);
        Hook(null);
        if (Instance == this)
            Instance = null;
    }

    /// <summary>
    /// The game's clock: the host's online, so every machine dates things alike, else this machine's
    /// </summary>
    internal static double Now()
    {
        NetworkManager network = NetworkManager.Singleton;
        return network != null && network.IsListening ? network.ServerTime.Time : Time.timeAsDouble;
    }

    /// <summary>Starts logging what the game raises</summary>
    internal void Begin()
    {
        if (listening)
            return;
        listening = true;
        FeatSync.Struck += OnStruck;
        FeatSync.Bumped += OnBumped;
        FeatSync.Delivered += OnDelivered;
        FeatSync.Sank += OnSank;
    }

    /// <summary>Stops logging</summary>
    internal void End()
    {
        listening = false;
        FeatSync.Struck -= OnStruck;
        FeatSync.Bumped -= OnBumped;
        FeatSync.Delivered -= OnDelivered;
        FeatSync.Sank -= OnSank;
    }

    void Update()
    {
        if (GameManager.Instance != watched)
            Watch(GameManager.Instance);
        if (active && OrderManager.Instance != hooked)
            Hook(OrderManager.Instance);
        Tick(Time.deltaTime);
    }

    /// <summary>A frame went by: once enough time has, the seats are sampled (at most once a frame)</summary>
    internal void Tick(float deltaTime)
    {
        if (!active)
            return;

        float interval = 1f / ReelRecording.SAMPLE_RATE;
        sinceSample += deltaTime;
        if (sinceSample < interval)
            return;

        sinceSample = Mathf.Min(sinceSample - interval, interval); // a hitch doesn't earn a burst
        Sample(Now());
    }

    /// <summary>One sample of every seat's scooter at a time</summary>
    internal void Sample(double now)
    {
        if (!active || Recording == null)
            return;

        Profiler.BeginSample("Highlights.Sample");
        for (int i = 0; i < seats.Count; i++)
        {
            Seat seat = seats[i];
            if (seat.Driving == null)
                continue;

            Order held = seat.Handler != null ? seat.Handler.GetBestOrder() : null;
            Recording.Add(seat.Index, new ReelSample
            {
                Time = now,
                Pose = ScooterPose.Read(seat.Driving),
                Flags = seat.Driving.Flags,
                Carrying = held != null ? (byte)((int)held.Value + 1) : (byte)0,
            });
        }
        Profiler.EndSample();
    }

    /// <summary>The main round starts (Begin): a fresh recording of every seat, and a bump watcher on each ball offline</summary>
    void StartRound()
    {
        Moments.Clear();
        seats.Clear();
        FindSeats(seats);

        Recording = new ReelRecording(CAPACITY);
        sinceSample = 0f;
        active = true;

        // Online the host reports bumps; offline nothing else sees two scooters touch
        if (GameAuthority.IsOnline)
            return;

        foreach (Seat seat in seats)
        {
            GameObject ball = seat.Driving != null ? seat.Driving.Sphere : null;
            if (ball == null)
                continue;
            BumpWatcher watcher = ball.GetComponent<BumpWatcher>();
            if (watcher == null)
                watcher = ball.AddComponent<BumpWatcher>();
            watcher.Begin(seat.Driving, seat.Index);
        }
    }

    /// <summary>The main round's time is up: recording stops and the director stops listening for it</summary>
    internal void MainGameFinishes()
    {
        active = false;
        Hook(null);
    }

    /// <summary>
    /// The main round's reel, after its time is up: where the rules run, the round's moments become clips (HighlightRules),
    /// the director films them (ShotDirector), and the reel plays from a moment ahead until it ends or is skipped.
    /// Nothing worth showing: it ends at once, without a frame
    /// </summary>
    internal IEnumerator RunReel()
    {
        if (!GameAuthority.IsAuthority || Recording == null)
            yield break;

        List<Clip> clips = HighlightRules.Pick(HighlightRules.Clips(Moments), RosterSeatIndices());
        if (clips.Count == 0)
            yield break;

        var director = new ShotDirector(Recording, ShotLineOfSight.WorldClear());
        ReelList list = Playable(ReelList.From(clips, director, Now() + START_DELAY));
        if (list.Clips.Length == 0)
            yield break;

        Debug.Log("Highlights: reel of " + list.Clips.Length + " clips, " + list.Length.ToString("0.0") + " s");
        HighlightReel reel = HighlightReel.Play(list, Recording, GameAuthority.IsAuthority);
        while (reel != null && !reel.Done)
            yield return null;
        Debug.Log("Highlights: reel ended");
    }

    /// <summary>
    /// A reel every machine accepts (NetChecks.Sane): as it is, else with only each clip's first shot (slow shots
    /// lengthen it), else no clips at all
    /// </summary>
    internal static ReelList Playable(ReelList list)
    {
        if (NetChecks.Sane(list))
            return list;

        var plain = new ReelList { StartAt = list.StartAt, Clips = new ReelClip[list.Clips != null ? list.Clips.Length : 0] };
        for (int i = 0; i < plain.Clips.Length; i++)
        {
            ReelClip clip = list.Clips[i];
            if (clip.Shots != null && clip.Shots.Length > 1)
                clip.Shots = new[] { clip.Shots[0] };
            plain.Clips[i] = clip;
            plain.Length += clip.RealLength;
        }
        return NetChecks.Sane(plain) ? plain : new ReelList { StartAt = list.StartAt, Clips = new ReelClip[0] };
    }

    static List<int> RosterSeatIndices()
    {
        var indices = new List<int>();
        if (PlayerInstantiate.Instance != null)
        {
            foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
                indices.Add(slot.Index);
        }
        return indices;
    }

    /// <summary>A new match: nothing recorded, no moments</summary>
    internal void NewMatch()
    {
        active = false;
        Recording?.Clear();
        Moments.Clear();
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

    internal void Hook(OrderManager orders)
    {
        if (hooked != null)
            hooked.OnMainGameFinishes -= MainGameFinishes;
        hooked = orders;
        if (hooked != null)
            hooked.OnMainGameFinishes += MainGameFinishes;
    }

    /// <summary>
    /// A game state is applied. The round is recorded from Begin (when the waves start; MainLoop is only the opening
    /// cutscene's), nothing else: not the tutorial or the golden round
    /// </summary>
    internal void OnStateApplied(GameState state)
    {
        if (state == GameState.Begin)
        {
            StartRound();
            Hook(OrderManager.Instance);
        }
        else
        {
            active = false;
        }
    }

    // A client's are replays of the host's: only the rules' machine logs
    void OnStruck(int attacker, int victim, bool steal, bool golden)
    {
        MomentKind kind = !steal ? MomentKind.Clash : golden ? MomentKind.GoldenSteal : MomentKind.Steal;
        Log(kind, attacker, victim, 0f);
    }

    void OnBumped(int bumper, int victim, float strength)
    {
        Log(MomentKind.Bump, bumper, victim, strength);
    }

    void OnDelivered(int seat, Constants.OrderValue value)
    {
        Log(value == Constants.OrderValue.Hard ? MomentKind.HardDelivery : MomentKind.Delivery, seat, -1, 0f);
    }

    void OnSank(int seat)
    {
        Log(MomentKind.Fall, seat, -1, 0f);
    }

    void Log(MomentKind kind, int seat, int other, float strength)
    {
        if (!active || !GameAuthority.IsAuthority)
            return;

        Moments.Add(new Moment
        {
            Kind = kind,
            Time = Now(),
            Seat = seat,
            Other = other,
            Strength = strength,
            TimeLeft = TimeLeft(),
            Place = PlaceOf(seat),
        });
    }

    Vector3 SeatPlace(int seat)
    {
        for (int i = 0; i < seats.Count; i++)
        {
            if (seats[i].Index == seat && seats[i].Driving != null)
                return seats[i].Driving.Sphere.transform.position;
        }
        return Vector3.zero;
    }

    static float RoundTimeLeft()
    {
        return OrderManager.Instance != null ? OrderManager.Instance.GameTimer : 0f;
    }

    static void RosterSeats(List<Seat> into)
    {
        if (PlayerInstantiate.Instance == null)
            return;

        for (int index = 0; index < Constants.MAX_PLAYERS; index++)
        {
            PlayerSlot slot = PlayerInstantiate.Instance.Roster[index];
            if (slot == null || slot.Player == null)
                continue;
            into.Add(new Seat
            {
                Index = index,
                Driving = slot.Player.GetComponentInChildren<BallDriving>(true),
                Handler = slot.Player.GetComponentInChildren<OrderHandler>(true),
            });
        }
    }
}

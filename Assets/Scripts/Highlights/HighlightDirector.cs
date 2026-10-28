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

    /// <summary>How far ahead of the game's clock a client accepts a reel's start; further is nonsense that would hold the first frame</summary>
    internal const double MAX_LEAD = 5.0;

    public static HighlightDirector Instance { get; private set; }

    /// <summary>Every seat's samples in the round being played (made once, and emptied as each round starts)</summary>
    internal ReelRecording Recording;

    /// <summary>What happened in the round, in order (the rules machine only)</summary>
    internal readonly List<Moment> Moments = new List<Moment>();

    /// <summary>Fills the list with the seats to record; tests swap it</summary>
    internal Action<List<Seat>> FindSeats = RosterSeats;

    /// <summary>A seat's ball position now; tests swap it</summary>
    internal Func<int, Vector3> PlaceOf;

    /// <summary>How long is left of the round now; tests swap it</summary>
    internal Func<float> TimeLeft = RoundTimeLeft;

    /// <summary>Starts playing a reel (HighlightReel.Play); tests swap it</summary>
    internal Func<ReelList, ReelRecording, bool, HighlightReel> PlayReel = HighlightReel.Play;

    /// <summary>
    /// Whether the main round is being played (the game scene is up and its state is Begin): when a client plays an
    /// arrived list; tests swap it
    /// </summary>
    internal Func<bool> InMainRound = InMainRoundNow;

    /// <summary>The game's clock when the main round's time ran out (positive infinity: it hasn't this round)</summary>
    internal double RoundEnd = double.PositiveInfinity;

    /// <summary>The last reel was destroyed before it ended (RunReel)</summary>
    internal bool ReelCutShort { get; private set; }

    readonly List<Seat> seats = new List<Seat>();
    GameManager watched; // the game manager whose states this follows
    OrderManager hooked; // the order manager whose end of round this waits for
    bool listening;
    bool active; // a main round is being recorded
    float sinceSample;
    bool clientPlayed; // a client has played the host's reel this round

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        HighlightSync.Reset();
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
        Recording ??= new ReelRecording(CAPACITY); // once, so the waves starting don't hitch on a 3 MB allocation
        if (listening)
            return;
        listening = true;
        FeatSync.Struck += OnStruck;
        FeatSync.Bumped += OnBumped;
        FeatSync.Delivered += OnDelivered;
        FeatSync.Sank += OnSank;
        HighlightSync.Arrived += OnReelArrived;
    }

    /// <summary>Stops logging</summary>
    internal void End()
    {
        listening = false;
        FeatSync.Struck -= OnStruck;
        FeatSync.Bumped -= OnBumped;
        FeatSync.Delivered -= OnDelivered;
        FeatSync.Sank -= OnSank;
        HighlightSync.Arrived -= OnReelArrived;
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
        clientPlayed = false;
        seats.Clear();
        FindSeats(seats);

        Recording ??= new ReelRecording(CAPACITY);
        Recording.Clear();
        RoundEnd = double.PositiveInfinity;
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

    /// <summary>The main round's time is up: recording stops (at this time, RoundEnd) and the director stops listening for it</summary>
    internal void MainGameFinishes()
    {
        active = false;
        RoundEnd = Now();
        Hook(null);
    }

    /// <summary>
    /// The main round's reel, then the next scene: the reel plays (RunReel), and the load follows unless the reel was cut
    /// short by something else's scene load (a quit to the menu, a disconnect), when this game's flow has moved on already
    /// </summary>
    internal IEnumerator ReelThen(Action load)
    {
        yield return RunReel();
        if (!ReelCutShort)
            load();
    }

    /// <summary>
    /// The main round's reel, after its time is up: where the rules run, the round's moments become clips (HighlightRules),
    /// the director films them (ShotDirector), and the reel plays from a moment ahead until it ends or is skipped.
    /// Nothing worth showing, or a reel that failed to build: it ends at once, without a frame. Afterwards ReelCutShort
    /// says whether it was destroyed before it ended
    /// </summary>
    internal IEnumerator RunReel()
    {
        ReelCutShort = false;
        HighlightReel reel = StartReel();
        if (reel == null)
            yield break;

        while (reel != null && !reel.Done)
            yield return null;
        ReelCutShort = reel.CutShort;
        Debug.Log("Highlights: reel " + (ReelCutShort ? "cut short" : "ended"));
    }

    // Picks, films and starts the reel; null for none. A failure here must never strand the match: it's logged, and
    // the reel (whose Play ends itself on a throw, so the players come back) is skipped
    HighlightReel StartReel()
    {
        if (!GameAuthority.IsAuthority || Recording == null)
            return null;

        try
        {
            List<Clip> clips = HighlightRules.Pick(HighlightRules.Clips(Moments), RosterSeatIndices());
            EndByRound(clips, RoundEnd, Recording);
            if (clips.Count == 0)
                return null;

            var director = new ShotDirector(Recording, ShotLineOfSight.WorldClear());
            ReelList list = Playable(ReelList.From(clips, director, Now() + START_DELAY));
            if (list.Clips.Length == 0)
                return null;

            Debug.Log("Highlights: reel of " + list.Clips.Length + " clips, " + list.Length.ToString("0.0") + " s");
            HighlightSync.Share(list); // the clients play it from the same start time
            return PlayReel(list, Recording, GameAuthority.IsAuthority);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            return null;
        }
    }

    /// <summary>
    /// Client: the host's reel list arrived. While the main round is up it plays from where its start time puts it,
    /// without deciding the skip (votes go to the host); late, it joins at the same moment as the rest, and a reel
    /// already over plays nothing. One reel a round; nothing starting more than MAX_LEAD ahead. It ends with the scene when the host loads the next. The host never plays what it shares
    /// </summary>
    internal void OnReelArrived(ReelList list)
    {
        if (GameAuthority.IsAuthority || Recording == null || !InMainRound())
            return;
        double now = Now();
        if (clientPlayed || list.StartAt > now + MAX_LEAD || double.IsPositiveInfinity(HighlightReel.StartClock(list, now)))
            return;

        clientPlayed = true; // one reel a round, whatever else arrives
        try
        {
            PlayReel(list, Recording, false);
        }
        catch (Exception e)
        {
            Debug.LogException(e); // Play has ended the half-built reel: the match goes on without one
        }
    }

    static bool InMainRoundNow()
    {
        return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Begin;
    }

    /// <summary>
    /// A moment in the round's last seconds gets a clip running past the recording (the tail after it), where no
    /// stand-in could show. Each clip ends by the round's end or the last sample, whichever is first, starts earlier
    /// if it must to stay MIN_CLIP long (not before the first sample), keeps its key time inside, and one left with
    /// nothing to show goes. The list then carries these times to every machine
    /// </summary>
    internal static void EndByRound(List<Clip> clips, double roundEnd, ReelRecording recording)
    {
        double first = double.PositiveInfinity, last = double.NegativeInfinity;
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            if (recording.Last(seat) <= 0)
                continue; // nothing recorded for the seat
            first = Math.Min(first, recording.First(seat));
            last = Math.Max(last, recording.Last(seat));
        }
        double end = Math.Min(roundEnd, last);

        for (int i = clips.Count - 1; i >= 0; i--)
        {
            Clip clip = clips[i];
            if (clip.End <= end)
                continue;

            clip.End = end;
            if (clip.End - clip.Start < HighlightRules.MIN_CLIP)
                clip.Start = Math.Max(clip.End - HighlightRules.MIN_CLIP, first);
            if (clip.End <= clip.Start)
            {
                clips.RemoveAt(i);
                continue;
            }
            clip.KeyTime = Math.Min(Math.Max(clip.KeyTime, clip.Start), clip.End);
        }
    }

    /// <summary>
    /// A reel every machine accepts (NetChecks.Sane) and that plays within the spec's HighlightRules.MAX_REEL: as it
    /// is, else with only each clip's first shot (slow shots lengthen it), else no clips at all
    /// </summary>
    internal static ReelList Playable(ReelList list)
    {
        bool sane = NetChecks.Sane(list);
        if (sane && list.Length <= HighlightRules.MAX_REEL)
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
        if (NetChecks.Sane(plain))
            return plain;
        return sane ? list : new ReelList { StartAt = list.StartAt, Clips = new ReelClip[0] };
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
        clientPlayed = false;
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

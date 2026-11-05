using System;
using System.Globalization;
using System.IO;
using UnityEngine;

/// <summary>
/// What a recorded match's file says, one line at a time (tools/analyse_matches.py reads them)
/// </summary>
public static class MatchRecord
{
    public const string OPT_IN = "-recordmatches";
    public const string FOLDER = "matches";

    /// <summary>Whether this launch records: asked for on the command line, or playing in the editor (not its batch runs)</summary>
    public static bool Wanted(string[] args, bool editor, bool batch)
    {
        return (editor && !batch) || (args != null && Array.Exists(args, arg => string.Equals(arg, OPT_IN, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>A number as the file writes it, whatever the player's language</summary>
    public static string N(float value)
    {
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    /// <summary>The first line. A match with a golden round writes two files, its main round and that</summary>
    public static string Header(GameMode mode, string scene, DateTime start, bool golden)
    {
        return "match," + mode + "," + scene + "," + start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "," + (golden ? "golden" : "main");
    }

    /// <summary>One moment of one scooter</summary>
    public static string Sample(float time, int seat, bool bot, Vector3 at, Vector3 velocity, float heading, float steer, float accelerate, float brake, bool boosting, bool drifting, bool grounded, int score, int held, int bestWorth)
    {
        return string.Join(",", "s", N(time), seat, bot ? 1 : 0, N(at.x), N(at.y), N(at.z), N(velocity.x), N(velocity.z), N(heading), N(steer), N(accelerate), N(brake),
            boosting ? 1 : 0, drifting ? 1 : 0, grounded ? 1 : 0, score, held, bestWorth);
    }

    /// <summary>One order that's out, and what a driver could see of it</summary>
    public static string OrderLine(float time, int key, int worth, bool lode, Vector3 at, Vector3 dropoff, int holder, bool free)
    {
        return string.Join(",", "o", N(time), key, worth, lode ? 1 : 0, N(at.x), N(at.z), N(dropoff.x), N(dropoff.z), holder, free ? 1 : 0);
    }

    /// <summary>Something that happened to a seat: fell, delivered, stole, robbed</summary>
    public static string Event(float time, string kind, int seat)
    {
        return string.Join(",", "e", N(time), kind, seat);
    }
}

/// <summary>
/// Writes each match driven on this machine to a file in persistentDataPath/matches, to study how people and bots drive and
/// make bots better from it. Only when asked (MatchRecord.Wanted); nothing leaves the PC. Ten moments a second per scooter,
/// about half a megabyte a match. It only looks: nothing in the game reads what it writes
/// </summary>
public class MatchRecorder : MonoBehaviour
{
    const float SAMPLE_EVERY = 0.1f;
    const float ORDERS_EVERY = 1f;

    /// <summary>Records whatever Wanted says (the probe that plays bots against each other)</summary>
    internal static bool Forced;
    internal static string FolderOverride;

    StreamWriter writer;
    float nextSample, nextOrders;
    bool looked, wanted; // whether this launch records, looked up once

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make()
    {
        GameObject holder = new GameObject(nameof(MatchRecorder));
        DontDestroyOnLoad(holder);
        holder.AddComponent<MatchRecorder>();
    }

    void OnEnable()
    {
        FeatSync.Sank += OnSank;
        FeatSync.Delivered += OnDelivered;
        FeatSync.Stole += OnStole;
        FeatSync.Robbed += OnRobbed;
    }

    void OnDisable()
    {
        FeatSync.Sank -= OnSank;
        FeatSync.Delivered -= OnDelivered;
        FeatSync.Stole -= OnStole;
        FeatSync.Robbed -= OnRobbed;
        Close();
    }

    void OnSank(int seat) => Write(MatchRecord.Event(Time.time, "fell", seat));
    void OnDelivered(int seat, Constants.OrderValue value) => Write(MatchRecord.Event(Time.time, "delivered", seat));
    void OnStole(int seat, bool golden) => Write(MatchRecord.Event(Time.time, "stole", seat));
    void OnRobbed(int seat) => Write(MatchRecord.Event(Time.time, "robbed", seat));

    void Write(string line)
    {
        if (writer == null)
            return;
        try
        {
            writer.WriteLine(line);
        }
        catch (IOException)
        {
            Close(); // a full disk: the recording stops, the game goes on
        }
    }

    void Close()
    {
        if (writer != null)
            writer.Dispose();
        writer = null;
    }

    void Update()
    {
        GameManager game = GameManager.Instance;
        // Begin and the golden round, once they can drive: the state reads MainLoop through the opening cutscene
        // Not the freeze at time up, either
        OrderManager orders = OrderManager.Instance;
        bool frozen = game != null && game.MainState == GameState.Begin && orders != null && orders.FinalOrderActive;
        bool driving = game != null && game.MainState != GameState.MainLoop && !frozen && BotDriver.InRound(game.MainState, Time.time, PlayerInstantiate.DrivingFrom);
        if (!driving || PlayerInstantiate.Instance == null)
        {
            Close();
            return;
        }
        if (writer == null && !Open())
            return;

        if (Time.time >= nextSample)
        {
            nextSample = Time.time + SAMPLE_EVERY;
            WriteSamples();
        }
        if (Time.time >= nextOrders)
        {
            nextOrders = Time.time + ORDERS_EVERY;
            WriteOrders();
        }
    }

    // Once per match: if this launch isn't recording, it never opens
    bool Open()
    {
        if (!looked)
        {
            looked = true;
            wanted = MatchRecord.Wanted(Environment.GetCommandLineArgs(), Application.isEditor, Application.isBatchMode);
        }
        if (!Forced && !wanted)
            return false;
        try
        {
            string folder = FolderOverride ?? Path.Combine(Application.persistentDataPath, MatchRecord.FOLDER);
            Directory.CreateDirectory(folder);
            DateTime now = DateTime.Now;
            bool golden = GameManager.Instance.MainState == GameState.FinalPackage;
            writer = new StreamWriter(Path.Combine(folder, "match-" + now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + (golden ? "-golden" : "") + ".csv"));
            writer.WriteLine(MatchRecord.Header(HouseRules.Playing.Mode, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, now, golden));
            return true;
        }
        catch (Exception problem) when (problem is IOException || problem is UnauthorizedAccessException)
        {
            Debug.LogWarning("Match recording: " + problem.Message);
            Forced = false;
            enabled = false;
            return false;
        }
    }

    void WriteSamples()
    {
        foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
        {
            OrderHandler handler = OrderSync.HandlerIn(slot.Index);
            BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
            if (ball == null || ball.Sphere == null || RemoteAvatar.IsRemote(handler))
                continue; // another machine's scooter: this PC can't see its pedals
            Rigidbody body = ball.Sphere.GetComponent<Rigidbody>();
            IDriveInput input = ball.DriveInput;
            Order best = handler.GetBestOrder();
            Write(MatchRecord.Sample(Time.time, slot.Index, input is BotDriver, ball.Sphere.transform.position, body != null ? body.velocity : Vector3.zero,
                ball.transform.eulerAngles.y, input != null ? input.Steer : 0f, input != null ? input.Accelerate : 0f, input != null ? input.Brake : 0f,
                ball.Boosting, ball.Drifting, ball.Grounded, handler.Score, handler.HeldCount, best != null ? best.Worth : 0));
        }
    }

    void WriteOrders()
    {
        OrderManager orders = OrderManager.Instance;
        if (orders == null)
            return;
        foreach (Order order in orders.ActiveOrders)
        {
            if (order == null || order.DropoffPoint == null)
                continue;
            Write(MatchRecord.OrderLine(Time.time, order.Key, order.Worth, order.IsLode, order.transform.position, order.DropoffPoint.position,
                order.PlayerHolding != null ? OrderSync.SeatOf(order.PlayerHolding) : -1, order.IsActive && order.CanPickup && order.PlayerHolding == null));
        }
    }
}

using System;
using System.IO;
using UnityEngine;

/// <summary>
/// The first-time hints this machine has been given (HintRules has the words): the saved mask, and the two events around
/// it. A hint is offered to a seat when something first matters to it; the callout of that seat (StealCallout.HearHint)
/// takes it, which marks it seen and saves the mask in settings.cfg, so a hint shows once per machine. Online each
/// machine keeps its own: another machine's player earning a hint changes nothing here
/// </summary>
public static class Hints
{
    static int seen;

    /// <summary>The saved mask. Setting it (the settings load it) raises nothing</summary>
    public static int Seen
    {
        get { return seen; }
        set { seen = value; }
    }

    /// <summary>The mask changed: to save it</summary>
    public static event Action<int> SeenChanged;

    /// <summary>A hint for a seat (its player's callout may take it)</summary>
    public static event Action<int, HintKind> Offered;

    /// <summary>The game state now. Tests set it; none means the game manager's (no manager, as in tests, shows hints)</summary>
    internal static Func<GameState> State;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reset()
    {
        SeenChanged = null;
        Offered = null;
        State = null;
    }

    static bool ShowsNow()
    {
        if (State != null)
            return HintRules.ShowsIn(State());

        GameManager game = GameManager.Instance;
        return game == null || HintRules.ShowsIn(game.MainState);
    }

    /// <summary>Something first matters to a seat: offers the hint, unless it was seen, the seat is none or the state gives no hints</summary>
    public static void Offer(HintKind kind, int seat)
    {
        if (seat < 0 || HintRules.Seen(seen, kind) || !ShowsNow())
            return;
        Offered?.Invoke(seat, kind);
    }

    /// <summary>A callout takes a hint to show it: false when it was already seen. Otherwise marks it and says the mask changed</summary>
    public static bool Take(HintKind kind)
    {
        if (HintRules.Seen(seen, kind))
            return false;

        seen = HintRules.Mark(seen, kind);
        SeenChanged?.Invoke(seen);
        return true;
    }

    /// <summary>
    /// Writes the mask into settings.cfg, leaving every other line as it is. Hints are earned in a match, when the options
    /// menu may not be alive to save for us, so this reads the file and writes it back itself. A file that can't be read
    /// or written is a warning, never an error in the match
    /// </summary>
    internal static void Save(int mask)
    {
        string path = Path.Combine(Application.persistentDataPath, GameSettings.FILE_NAME);
        try
        {
            string text = File.Exists(path) ? File.ReadAllText(path) : "";
            File.WriteAllText(path, GameSettings.WithHints(text, mask));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not save hints to {path}: {e.Message}");
        }
    }
}

/// <summary>
/// Offers the hints that come from a bump or a steal, and saves the mask when it changes. It is made at launch and kept
/// between scenes, as ModeWatcher is, so no callout subscribes to these itself; the golden order's hint is offered where
/// the order is picked up (Order)
/// </summary>
public class HintWatcher : MonoBehaviour
{
    static HintWatcher instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null)
            return;
        GameObject holder = new GameObject(nameof(HintWatcher));
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<HintWatcher>();
    }

    void OnEnable()
    {
        FeatSync.Bumped += OnBumped;
        StealFeedback.Happened += OnNotice;
        Hints.SeenChanged += Hints.Save;
    }

    void OnDisable()
    {
        FeatSync.Bumped -= OnBumped;
        StealFeedback.Happened -= OnNotice;
        Hints.SeenChanged -= Hints.Save;
    }

    /// <summary>A player bumped another without boosting: they learn that boosting in is what steals</summary>
    internal static void OnBumped(int bumper, int victim, float strength)
    {
        Hints.Offer(HintKind.BoostToSteal, bumper);
    }

    /// <summary>An order changed hands: the victim learns to boost to block, the thief that a stolen order pays double</summary>
    internal static void OnNotice(BumpNotice notice)
    {
        if (notice.Outcome != BumpOutcome.Stolen)
            return;
        Hints.Offer(HintKind.BoostToBlock, notice.Victim);
        Hints.Offer(HintKind.StolenPaysDouble, notice.Attacker);
    }
}

using System;
using UnityEngine;

/// <summary>
/// Tells this machine what happened in a timed mode, so each player's callout can show it (StealCallout). ModeWatcher
/// works the events out from the mode's state and announces them here, the same way on every machine: no new message
/// </summary>
public static class ModeFeedback
{
    /// <summary>A line for the callouts: who reads it, what it says and who it names (ModeLine)</summary>
    public static event Action<ModeLine> Said;

    /// <summary>The round has 30 seconds left</summary>
    public static event Action Warned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        Said = null;
        Warned = null;
    }

    /// <summary>A mode line happened</summary>
    public static void Say(ModeLine line)
    {
        Said?.Invoke(line);
    }

    /// <summary>The round has 30 seconds left</summary>
    public static void Warn()
    {
        Warned?.Invoke();
    }
}

/// <summary>
/// Watches Kings Crown, Infection and Hot Potato for what the players should be told. Other machines receive only a mode's
/// state (ModeMatch.Changed), so this compares the state it last saw with the new one and says the lines the change gives
/// (ModeFeedbackRules.Lines), each once; and watches the round's clock for the warning 30 seconds before it ends. It is made at
/// launch and kept between scenes; in the menus there is no match and no order manager, and it does nothing
/// </summary>
public class ModeWatcher : MonoBehaviour
{
    static ModeWatcher instance;

    ModeView last = ModeView.Nothing;
    // The round's clock when last seen running: NaN when it was not, so a clock found already low is never a crossing
    float lastTime = float.NaN;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null)
            return;
        GameObject holder = new GameObject(nameof(ModeWatcher));
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<ModeWatcher>();
    }

    void OnEnable()
    {
        ModeMatch.Changed += OnChanged;
    }

    void OnDisable()
    {
        ModeMatch.Changed -= OnChanged;
    }

    void OnChanged()
    {
        See(ViewOf(ModeMatch.Current));
    }

    /// <summary>What a machine knows of a match: nothing for no match</summary>
    internal static ModeView ViewOf(ModeMatch match)
    {
        if (match == null)
            return ModeView.Nothing;

        return new ModeView
        {
            Mode = match.Mode,
            Players = match.Players,
            Crown = match.CrownSeat,
            Potato = match.PotatoSeat,
            Infected = match.Infected,
            Out = match.Out
        };
    }

    /// <summary>The mode's state is now this: says the lines the change from the last state gives</summary>
    internal void See(ModeView now)
    {
        foreach (ModeLine line in ModeFeedbackRules.Lines(last, now))
            ModeFeedback.Say(line);
        last = now;
    }

    /// <summary>
    /// The round's clock is at timeLeft seconds: warns once as it crosses 30 seconds, in the modes with a round timer. A clock
    /// that is not running is forgotten, so the warning is only for a crossing this watcher saw
    /// </summary>
    internal void SeeClock(GameMode mode, float timeLeft, bool running)
    {
        if (!running)
        {
            lastTime = float.NaN;
            return;
        }

        if (ModeFeedbackRules.WarnsIn(mode) && ModeFeedbackRules.CrossedWarning(lastTime, timeLeft))
            ModeFeedback.Warn();
        lastTime = timeLeft;
    }

    /// <summary>The match is over: forgets the last state and clock, so the next match is compared against nothing</summary>
    internal void Forget()
    {
        last = ModeView.Nothing;
        lastTime = float.NaN;
    }

    void Update()
    {
        GameManager game = GameManager.Instance;
        GameState state = game != null ? game.MainState : GameState.Default;
        OrderManager orders = OrderManager.Instance;
        if (Scoreboard.EndsMatch(state) || orders == null)
        {
            Forget();
            return;
        }

        SeeClock(HouseRules.Playing.Mode, orders.GameTimer, orders.RoundRunning);
    }
}

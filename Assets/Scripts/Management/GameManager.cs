using DG.Tweening;
using System;
using UnityEngine;

public enum GameState { 
    Menu,
    Options,
    Credits,
    PlayerSelect,
    Loading,
    StartingCutscene,
    Tutorial,
    Begin,
    MainLoop,
    GoldenCutscene,
    FinalPackage,
    Results,
    Paused,
    Default 
}

public class GameManager : SingletonMonobehaviour<GameManager>
{
    [SerializeField] GameState beginingGameState = GameState.PlayerSelect;

    [Space(10)]
    [SerializeField] private GameState mainState = GameState.Default;

    public GameState MainState { get { return mainState; } }

    // game state events
    public event Action OnSwapMenu;
    public event Action OnSwapOptions;
    public event Action OnSwapCredits;
    public event Action OnSwapPlayerSelect;
    public event Action OnSwapLoading;
    public event Action OnSwapStartingCutscene;
    public event Action OnSwapTutorial;
    public event Action OnSwapBegin;
    public event Action OnSwapMainLoop;
    public event Action OnSwapGoldenCutscene;
    public event Action OnSwapFinalPackage;
    public event Action OnSwapResults;
    public event Action OnSwapAnything;

    /// <summary>
    /// Raised for every state switched to, before that state's own event, so a state switched to from inside another
    /// state's event is told after it. Online, the host sends each one to the clients
    /// </summary>
    public event Action<GameState> StateApplied;

    /// <summary>
    /// The next match starts fresh: scores, boost, held orders, compass, sounds and match stats reset. Raised on the
    /// way to the menus (Menu, and PlayerSelect, where the menu scene opens online) and by a rematch (StartNewMatch),
    /// which skips the menu's own steps
    /// </summary>
    public event Action OnNewMatch;

    // other important events
    public event Action OnFinalOrderDelivered;

    public void Start()
    {
        SetGameState(beginingGameState);
    }

    ///<summary>
    /// Asks for a game state. The authority (this machine in a local match, the host online) switches to it; an online
    /// client ignores the request and follows the host's state instead
    ///</summary>
    public void SetGameState(GameState state)
    {
        if (!GameAuthority.IsAuthority)
            return;

        ApplyGameState(state);
    }

    ///<summary>
    /// Switches to a game state and invokes its events. Online clients call this with the state the host sent
    ///</summary>
    public void ApplyGameState(GameState state)
    {
        GameAuthority.SetTimeScale(1f);

        mainState = state;
        Raise(StateApplied, state);

        if(mainState != GameState.Tutorial)
            Raise(OnSwapAnything);

        // Modifiers last until the lobby: a rematch keeps the rules, and a new game starts from normal, the mode still picked
        if (mainState == GameState.Menu || mainState == GameState.PlayerSelect)
            HouseRules.ResetAfterMatch();

        // A match started from the menus teaches again, whatever a rematch left set
        if (mainState == GameState.Menu || mainState == GameState.PlayerSelect)
        {
            TutorialManager.SkipNextTutorial = false;
            RematchCrown.Clear(); // the winner's crown lasts a rematch, not a new game
            MatchPhysics.Reset(); // the menus fall at normal gravity, whatever the last match's mutators were
            ModeMatch.End();
            Raise(OnNewMatch);
        }

        // The podium shows everyone as they were: a timed mode's crown, colours and players out go back (ModeVisuals)
        if (mainState == GameState.Results)
            ModeMatch.End();

        // Calls an event for the certain state that is swapped to
        switch(mainState)
        {
            case GameState.Menu:
                Raise(OnSwapMenu);
                break;
            case GameState.Options:
                Raise(OnSwapOptions);
                break;
            case GameState.Credits:
                Raise(OnSwapCredits);
                break;
            case GameState.PlayerSelect:
                Raise(OnSwapPlayerSelect);
                break;
            case GameState.Loading:
                Raise(OnSwapLoading);
                break;
            case GameState.StartingCutscene:
                Raise(OnSwapStartingCutscene);
                break;
            case GameState.Tutorial:
                Raise(OnSwapTutorial);
                // A rematch skips the tutorial: its state still unfreezes the scooters and hands over the driving
                // controls and engines, then the first wave starts at once
                if (TutorialManager.Skipping && mainState == GameState.Tutorial)
                    SetGameState(GameState.Begin);
                break;
            case GameState.Begin:
                Raise(OnSwapBegin);
                break;
            case GameState.MainLoop:
                Raise(OnSwapMainLoop);
                break;
            case GameState.FinalPackage:
                Raise(OnSwapFinalPackage);
                break;
            case GameState.GoldenCutscene:
                Raise(OnSwapGoldenCutscene);
                break;
            case GameState.Results:
                DOTween.CompleteAll();
                Raise(OnSwapResults);
                break;
            default:
                break;
        }
    }

    // Each handler on its own: one that throws is logged and the rest still run. A joining machine's player threw at the
    // opening cutscene, and the handlers after it (the other players', the bots', the spawns) never ran (playtest,
    // 2026-10-31: player 2 didn't load, bots didn't work)
    static void Raise(Action handlers)
    {
        if (handlers == null)
            return;
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action)handler)();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    static void Raise(Action<GameState> handlers, GameState state)
    {
        if (handlers == null)
            return;
        foreach (Delegate handler in handlers.GetInvocationList())
        {
            try
            {
                ((Action<GameState>)handler)(state);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    ///<summary>
    /// A rematch: the next match starts fresh without going through the menus (no state change)
    ///</summary>
    public void StartNewMatch()
    {
        MatchPhysics.Reset();
        ModeMatch.End();
        OnNewMatch?.Invoke();
    }

    public void InvokeFinalOrderDelivered()
    {
        OnFinalOrderDelivered?.Invoke();
    }
}

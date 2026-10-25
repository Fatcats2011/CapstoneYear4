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
        StateApplied?.Invoke(state);

        if(mainState != GameState.Tutorial)
            OnSwapAnything?.Invoke();

        // A match started from the menus teaches again, whatever a rematch left set
        if (mainState == GameState.Menu || mainState == GameState.PlayerSelect)
        {
            TutorialManager.SkipNextTutorial = false;
            OnNewMatch?.Invoke();
        }

        // Calls an event for the certain state that is swapped to
        switch(mainState)
        {
            case GameState.Menu:
                OnSwapMenu?.Invoke();
                break;
            case GameState.Options:
                OnSwapOptions?.Invoke();
                break;
            case GameState.Credits:
                OnSwapCredits?.Invoke();
                break;
            case GameState.PlayerSelect:
                OnSwapPlayerSelect?.Invoke();
                break;
            case GameState.Loading:
                OnSwapLoading?.Invoke();
                break;
            case GameState.StartingCutscene:
                OnSwapStartingCutscene?.Invoke();
                break;
            case GameState.Tutorial:
                OnSwapTutorial?.Invoke();
                // A rematch skips the tutorial: its state still unfreezes the scooters and hands over the driving
                // controls and engines, then the first wave starts at once
                if (TutorialManager.SkipNextTutorial && mainState == GameState.Tutorial)
                    SetGameState(GameState.Begin);
                break;
            case GameState.Begin:
                OnSwapBegin?.Invoke();
                break;
            case GameState.MainLoop:
                OnSwapMainLoop?.Invoke();
                break;
            case GameState.FinalPackage:
                OnSwapFinalPackage?.Invoke();
                break;
            case GameState.GoldenCutscene:
                OnSwapGoldenCutscene?.Invoke();
                break;
            case GameState.Results:
                DOTween.CompleteAll();
                OnSwapResults?.Invoke();
                break;
            default:
                break;
        }
    }

    ///<summary>
    /// A rematch: the next match starts fresh without going through the menus (no state change)
    ///</summary>
    public void StartNewMatch()
    {
        OnNewMatch?.Invoke();
    }

    public void InvokeFinalOrderDelivered()
    {
        OnFinalOrderDelivered?.Invoke();
    }
}

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

        if(mainState != GameState.Tutorial)
            OnSwapAnything?.Invoke();

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

    public void InvokeFinalOrderDelivered()
    {
        OnFinalOrderDelivered?.Invoke();
    }
}

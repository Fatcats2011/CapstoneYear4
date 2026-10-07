using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TutorialType
{
    Boost,
    Pickup,
    Steal,
    Dropoff,
    Final
}

public class TutorialManager : SingletonMonobehaviour<TutorialManager>
{
    private bool shouldTutorialize = true;
    public bool ShouldTutorialize { get { return shouldTutorialize; } set { shouldTutorialize = value; } }

    private List<TutorialHandler> handlers = new List<TutorialHandler>();

    // The seats whose players have finished this match's tutorial (online, the host hears about other machines' players)
    private readonly HashSet<int> finished = new HashSet<int>();

    public delegate void TutorialComplete();
    public TutorialComplete OnTutorialComplete;

    internal void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += StartOver;
        GameManager.Instance.OnSwapBegin += StartOver;
        shouldTutorialize = true;
    }

    internal void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= StartOver;
        GameManager.Instance.OnSwapBegin -= StartOver;
    }

    // Each match's tutorial starts the count over, and the first wave ends it
    private void StartOver()
    {
        handlers.Clear();
        finished.Clear();
    }

    private void Update()
    {
        if (DevTools.GetKeyDown(KeyCode.S) && GameManager.Instance.MainState == GameState.Tutorial)
        {
            foreach (TutorialHandler handler in handlers)
            {
                handler.TeachHandler(TutorialType.Final);
            }
            OrderManager.Instance.DeleteActiveOrders();
        }
    }
    /// <summary>
    /// This machine's player finished the tutorial: their seat counts. Online, a client tells the host instead
    /// (TutorialSync), which starts the first wave once every seat's player has finished
    /// </summary>
    /// <param name="inHandler">The player's tutorial handler</param>
    public void IncrementAlumni(TutorialHandler inHandler)
    {
        if(!handlers.Contains(inHandler))
        {
            handlers.Add(inHandler);
        }

        int seat = OrderSync.SeatOf(inHandler);
        if (!GameAuthority.IsAuthority)
        {
            finished.Add(seat);
            TutorialSync.Finish(seat);
            return;
        }

        SeatLearnt(seat);
    }

    /// <summary>
    /// The player in a seat finished the tutorial (online, the host hears it from their machine). The tutorial ends once
    /// every seat's player has
    /// </summary>
    public void SeatLearnt(int seat)
    {
        finished.Add(seat);
        RecheckAlumni();
    }

    /// <summary>
    /// Ends the tutorial if every seat still in the match has finished it: after a player left, or when this machine
    /// went offline. Only the machine that decides the rules ends it, and only while the tutorial runs (the count of a
    /// match left mid-tutorial lasts until the next one's opening cutscene)
    /// </summary>
    public void RecheckAlumni()
    {
        if (!GameAuthority.IsAuthority || PlayerInstantiate.Instance == null || GameManager.Instance == null
            || GameManager.Instance.MainState != GameState.Tutorial)
            return;

        bool anyone = false;
        foreach (PlayerSlot player in PlayerInstantiate.Instance.Roster.Players)
        {
            if (!finished.Contains(player.Index))
                return;
            anyone = true;
        }
        if (!anyone)
            return;

        if (shouldTutorialize)
            GameManager.Instance.SetGameState(GameState.Begin);

        OnTutorialComplete?.Invoke();
    }

    /// <summary>
    /// Skips the tutorial in the event that a hotkey is pressed.
    /// </summary>
    public void SkipTutorial()
    {
        if (shouldTutorialize)
            return;

        handlers.Clear();

        OnTutorialComplete?.Invoke();
    }
}
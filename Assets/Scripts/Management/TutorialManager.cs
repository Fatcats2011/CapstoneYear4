using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

    /// <summary>
    /// Whether matches skip the tutorial: online they do, until orders are shared between machines (roadmap Task 3.5).
    /// Without it, the players start in the city instead of at the start of the tutorial (SpawnManager)
    /// </summary>
    public static bool IsSkipped { get { return GameAuthority.IsOnline; } }

    private List<TutorialHandler> handlers = new List<TutorialHandler>();

    public delegate void TutorialComplete();
    public TutorialComplete OnTutorialComplete;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapBegin += handlers.Clear;
        GameManager.Instance.OnSwapTutorial += SkipWhenOnline;
        shouldTutorialize = true;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapBegin -= handlers.Clear;
        GameManager.Instance.OnSwapTutorial -= SkipWhenOnline;
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
    /// Adds a tutorial handler to a list if it's not already there.
    /// </summary>
    /// <param name="inHandler">Handler to to be added to the list</param>
    public void IncrementAlumni(TutorialHandler inHandler)
    {
        if(!handlers.Contains(inHandler))
        {
            handlers.Add(inHandler);
        }

        if(handlers.Count >= PlayerInstantiate.Instance.PlayerCount)
        {
            if(shouldTutorialize)
                GameManager.Instance.SetGameState(GameState.Begin);
            
            OnTutorialComplete?.Invoke();
        }
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

    /// <summary>
    /// Online, the tutorial is skipped until Phase 3F. It teaches stealing too (the cardboard cutouts), which isn't shared
    /// between machines yet. And it waits for every player's handler, which only exists on that player's machine.
    /// Every machine's players finish it at once, and the host starts the first wave a frame later. Switching state
    /// inside the tutorial's own switch would run its other listeners (the tutorial orders) after the first wave's
    /// </summary>
    private void SkipWhenOnline()
    {
        if (IsSkipped)
            StartCoroutine(SkipOnline());
    }

    private IEnumerator SkipOnline()
    {
        yield return null;

        OnTutorialComplete?.Invoke();
        if (GameAuthority.IsAuthority && GameManager.Instance.MainState == GameState.Tutorial)
            GameManager.Instance.SetGameState(GameState.Begin);
    }
}
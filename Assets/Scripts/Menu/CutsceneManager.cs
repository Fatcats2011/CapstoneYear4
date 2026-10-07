using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public enum CutsceneType
{
    StartingCutscene = 0,
    FinalOrderCutscene = 1
}
public class CutsceneManager : SingletonMonobehaviour<CutsceneManager>
{
    [SerializeField] internal Camera cutsceneCamera;
    [SerializeField] Canvas cutsceneCanvas;
    [SerializeField] PlayableDirector playableDirector;
    [SerializeField] Animator cutsceneCountdownAnimation;

    [Header("Cutscene Information")]
    Coroutine cutsceneCoroutine;
    [SerializeField] int cutsceneBeingPlayed;
    [SerializeField] CutsceneInformation currentCutscene;
    [SerializeField] CutsceneInformation[] cutsceneInformation;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += StartingCutscene;
        GameManager.Instance.OnSwapGoldenCutscene += GoldenCutscene;
        GameManager.Instance.StateApplied += EndForState;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= StartingCutscene;
        GameManager.Instance.OnSwapGoldenCutscene -= GoldenCutscene;
        GameManager.Instance.StateApplied -= EndForState;
    }

    private void Update()
    {
        // Skips cutscene
        if (DevTools.GetKeyDown(KeyCode.L))
            Skip();
    }

    ///<summary>
    /// Ends the cutscene now (the developer skip, L). Only where game states are decided: offline, or on an online host,
    /// whose next state then ends the cutscene on every client. A client's skip does nothing
    ///</summary>
    public void Skip()
    {
        if (!GameAuthority.IsAuthority)
            return;

        if (cutsceneCoroutine != null)
            StopCoroutine(cutsceneCoroutine);

        EndCutscene();
    }

    ///<summary>
    /// The state the playing cutscene hands over to ends it at once, without asking for it again. Online that's the
    /// host's: the host skipped, or its cutscene ended first. A load ends it too: the game is leaving this scene (online,
    /// a session that ended mid-match takes this machine back to the menu). Other states don't: the opening plays on while
    /// the game switches to MainLoop under it (SpawnManager)
    ///</summary>
    void EndForState(GameState state)
    {
        if (cutsceneCoroutine == null)
            return;
        if (state != GameState.Loading && state != NextStateOf(cutsceneBeingPlayed))
            return;

        StopCoroutine(cutsceneCoroutine);
        cutsceneCoroutine = null;
        CloseView();
    }

    ///<summary>
    /// The state a cutscene hands over to: the tutorial after the opening, the golden round after its cutscene
    ///</summary>
    static GameState NextStateOf(int cutscene)
    {
        return cutscene == 0 ? GameState.Tutorial : GameState.FinalPackage;
    }

    ///<summary>
    /// Sets the cutscene to starting cutscene
    ///</summary>
    void StartingCutscene()
    {
        cutsceneBeingPlayed = 0;
        BeginCutsceneCoroutine();
    }

    ///<summary>
    /// Sets the cutscene to golden package cutscene
    ///</summary>
    void GoldenCutscene()
    {
        cutsceneBeingPlayed = 1;
        BeginCutsceneCoroutine();
    }

    ///<summary>
    /// Begins the starting cutscene
    ///</summary>
    void BeginCutsceneCoroutine()
    {
        currentCutscene = cutsceneInformation[cutsceneBeingPlayed];

        if (cutsceneCoroutine != null)
            StopCoroutine(cutsceneCoroutine);

        cutsceneCoroutine = StartCoroutine(CutsceneTimer());
    }

    ///<summary>
    /// Coroutine to start and stop the cutscene
    ///</summary>
    IEnumerator CutsceneTimer()
    {
        BeginCutscene();
        yield return new WaitForSeconds(currentCutscene.timeInSeconds);
        EndCutscene();
    }


    ///<summary>
    /// Begins a cutscene, the cutscene played is passed through with an enum typing
    ///</summary>
    void BeginCutscene()
    {
        foreach(GameObject camPositions in currentCutscene.cameraTransformPositions)
        {
            camPositions.SetActive(true);
        }

        playableDirector.playableAsset = currentCutscene.cutscenePlayable;

        cutsceneCamera.enabled = true;
        cutsceneCanvas.enabled = true;

        playableDirector.Play();
    }

    ///<summary>
    /// Calls method when cutscene is supposed to end
    ///</summary>
    void EndCutscene()
    {
        CloseView();
        cutsceneCoroutine = null; // the state asked for next doesn't end it again

        switch (cutsceneBeingPlayed)
        {
            case 0: // Begining Cutscene
                GameManager.Instance.SetGameState(GameState.Tutorial);

                break;
            case 1:
                GameManager.Instance.SetGameState(GameState.FinalPackage);

                break;
            default:
                break;
        }
    }   

    ///<summary>
    /// Turns the cutscene's camera, canvas and camera positions off
    ///</summary>
    void CloseView()
    {
        cutsceneCanvas.enabled = false;
        cutsceneCamera.enabled = false;

        foreach (GameObject camPositions in currentCutscene.cameraTransformPositions)
        {
            camPositions.SetActive(false);
        }
    }

    public void BeginCountdownAnimation()
    {
        cutsceneCountdownAnimation.SetTrigger(HashReference._countdownTrigger);
    }
}

[Serializable]
public class CutsceneInformation
{
    public string cutsceneName;
    public PlayableAsset cutscenePlayable;
    public float timeInSeconds;
    public GameObject[] cameraTransformPositions;
}
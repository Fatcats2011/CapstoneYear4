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
    [SerializeField] Camera cutsceneCamera;
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
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= StartingCutscene;
        GameManager.Instance.OnSwapGoldenCutscene -= GoldenCutscene;
    }

    private void Update()
    {
        // Skips cutscene
        if (DevTools.GetKeyDown(KeyCode.L))
        {
            if (cutsceneCoroutine != null)
                StopCoroutine(cutsceneCoroutine);

            EndCutscene();
        }
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
        cutsceneCanvas.enabled = false;
        cutsceneCamera.enabled = false;

        foreach (GameObject camPositions in currentCutscene.cameraTransformPositions)
        {
            camPositions.SetActive(false);
        }

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
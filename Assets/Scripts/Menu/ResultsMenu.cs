using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ResultsMenu : SingletonMonobehaviour<ResultsMenu>
{
    [SerializeField] private TMP_Text[] displayText;
    [SerializeField] Canvas resultsCanvas;

    [SerializeField] private GameObject quitInfo;

    [SerializeField] private Camera cam;
    bool canQuit = false;

    [SerializeField] private GameObject wiper;
    [SerializeField] private GameObject canvasElements;
    private Animator transitionAnimator, camAnimator;

    [Tooltip("Basically the length of the camera animation.")]
    [SerializeField] private float timeBeforeUI = 4f;

    private IEnumerator resultsRoutine;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapResults += InitResults;
        GameManager.Instance.OnFinalOrderDelivered += HideGame;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapResults -= InitResults;
        GameManager.Instance.OnFinalOrderDelivered -= HideGame;
    }

    private void Start()
    {
        cam.enabled = false;
        transitionAnimator = wiper.GetComponent<Animator>();
        camAnimator = cam.GetComponent<Animator>();
    }

    private void HideGame()
    {
        canvasElements.SetActive(false);
        resultsCanvas.enabled = true;
        wiper.SetActive(true);
        transitionAnimator.SetTrigger(HashReference._wipeInTrigger);
    }

    private void InitResults()
    {
        if (resultsRoutine != null)
        {
            StopCoroutine(resultsRoutine);
            resultsRoutine = null;
        }
        resultsRoutine = UpdateResults();
        StartCoroutine(resultsRoutine);
    }

    private IEnumerator UpdateResults()
    {
        transitionAnimator.SetTrigger(HashReference._wipeOutTrigger);
        cam.enabled = true;
        camAnimator.SetTrigger(HashReference._startTrigger);
        quitInfo.SetActive(false);

        yield return new WaitForSeconds(timeBeforeUI);
        
        canvasElements.SetActive(true);
        resultsCanvas.enabled = true;
        StartCoroutine(QuitDelay());

        for (int i = 0; i < PlayerInstantiate.Instance.PlayerCount; i++)
        {
            OrderHandler currHandler = ScoreManager.Instance.GetHandlerOfIndex(i);

            // Set player animations
            Animator playerAnim = currHandler.PlayerAnimator;
            playerAnim.SetInteger(HashReference._endStatusFloat, i + 1);

            if (currHandler != null)
            {
                displayText[i].gameObject.SetActive(true);
                displayText[i].text = "$" + currHandler.Score;
            }
        }
    }

    public void ConfirmMenu()
    {
        if (!canQuit)
        {
            return;
        }
        SceneFlow.Current.ReturnToMenu();

        for (int i = 0; i < displayText.Length; i++)
        {
            displayText[i].enabled = false;
        }

        // Reset player result animations
        for (int i = 0; i < PlayerInstantiate.Instance.PlayerCount; i++)
        {
            OrderHandler currHandler = ScoreManager.Instance.GetHandlerOfIndex(i);
            Animator playerAnim = currHandler.PlayerAnimator;
            playerAnim.SetInteger(HashReference._endStatusFloat, 0);
        }
    }

    private IEnumerator QuitDelay()
    {
        canQuit = false;
        yield return new WaitForSeconds(2f);
        quitInfo.SetActive(true);
        canQuit = true;
    }
}

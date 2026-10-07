using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;

/// <summary>
/// This is a testing class to update the score text on the player's UI for the Package Scene. This is all the commenting I'm going to bother to do for this class and it already took me longer to write this comment than to write the script.
/// </summary>
public class DynamicNumberUI : MonoBehaviour
{
    [SerializeField] GameObject waveTimerDynamic;
    [SerializeField] private NumberHandler numHandler;
    [SerializeField] TextMeshProUGUI centerText;
    // final order countdown
    [SerializeField] GameObject finalOrderNumber;
    [SerializeField] private GameObject dynamicWaveTimer;

    string timer;
    string goldenValue;
    private int currFinal = -1;
    bool inNormalGameplay; // the normal gameplay HUD is set up (SetNormalGameplay), until another mode changes it
    int shownSecond;       // the whole second the timer shows

    private void OnEnable()
    {
        GameManager.Instance.OnSwapAnything += SetNothing;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapAnything -= SetNothing;
    }

    /// <summary>
    /// Sets the UI for normal gameplay.
    /// </summary>
    public void SetNormalGameplay()
    {
        // Called every frame: the text is rebuilt only when the whole second changes
        int second = Mathf.FloorToInt(OrderManager.Instance.GameTimer);
        if (inNormalGameplay && second == shownSecond)
            return;
        inNormalGameplay = true;
        shownSecond = second;

        dynamicWaveTimer.SetActive(true);
        waveTimerDynamic.SetActive(true);
        numHandler.SetFinalCountdown(false);
        finalOrderNumber.gameObject.SetActive(false);
        centerText.text = "";

        string timeSpanString = TimerText(OrderManager.Instance.GameTimer);
        if (timer != timeSpanString)
        {
            timer = timeSpanString;
            numHandler.UpdateTimerUI(timer);
        }

        finalOrderNumber.gameObject.SetActive(false);
    }

    /// <summary>
    /// The match timer's text: minutes and seconds (m:ss)
    /// </summary>
    public static string TimerText(float seconds)
    {
        return TimeSpan.FromSeconds(seconds).ToString(TIMER_FORMAT);
    }

    const string TIMER_FORMAT = @"m\:ss";

    /// <summary>
    /// Sets the UI for the final order area.
    /// </summary>
    public void SetFinalGameplay()
    {
        inNormalGameplay = false;
        centerText.text = "";
        finalOrderNumber.gameObject.SetActive(true);

        string currentGoldenValue = OrderManager.Instance.FinalOrderValue.ToString();
        if(goldenValue != currentGoldenValue)
        {
            goldenValue = currentGoldenValue;
            numHandler.UpdateOrderValueUI(goldenValue);
        }
    }

    /// <summary>
    /// Sets the UI for the countdown between main game and final area.
    /// </summary>
    /// <param name="inTime"></param>
    public void SetFinalCountdown(int inTime)
    {
        inNormalGameplay = false;
        numHandler.SetFinalCountdown(true);
        waveTimerDynamic.SetActive(false);
        if (currFinal != inTime)
        {
            currFinal = inTime;
            numHandler.UpdateFinalCountdown(inTime);
        }
    }

    /// <summary>
    /// Sets all text to nothing.
    /// </summary>
    public void SetNothing()
    {
        inNormalGameplay = false;
        numHandler.SetFinalCountdown(false);
        waveTimerDynamic.SetActive(false);
        centerText.text = "";
    }

    private void Update()
    {
        if (OrderManager.Instance != null)
        {
            if (OrderManager.Instance.GameStarted)
            {
                if (!OrderManager.Instance.FinalOrderActive)
                {
                    if (OrderManager.Instance.GameTimer < 10f && OrderManager.Instance.GameTimer > 0f)
                    {
                        SetFinalCountdown((int)OrderManager.Instance.GameTimer);
                    }
                    else
                    {
                        SetNormalGameplay();
                    }
                }
                else
                {
                    if (GameManager.Instance.MainState == GameState.FinalPackage)
                        SetFinalGameplay();
                    else
                        SetNothing();
                }
            }
        }
    }
}

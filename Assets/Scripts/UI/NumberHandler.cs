using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class NumberHandler : MonoBehaviour
{
    [SerializeField] bool trigger;

    [Header("Sprite Information")]
    [SerializeField] Sprite[] numberSprites;

    [Header("Tip Counter")]
    [SerializeField] string moneyCount;
    [SerializeField] Image[] moneyImages;
    [SerializeField] GameObject tipHolder;
    [SerializeField] GameObject dollarSign;

    [Header("Placement")]
    [SerializeField] Image placementImage;
    [SerializeField] private TextMeshProUGUI suffix;
    [SerializeField] private TextMeshProUGUI suffixBG;

    [Header("Timer")]
    [SerializeField] string timerCount;
    [SerializeField] Image[] timerImages;

    [Header("Final Order Countdown")]
    [SerializeField] GameObject finalOrderTextObject;
    [SerializeField] Image finalOrderCountdownImage;

    [Header("Final Order Value")]
    [SerializeField] string finalOrderValue;
    [SerializeField] Image[] finalOrderImages;

    private int currPlace = -1;

    /// <summary>
    /// What the score HUD shows for a score: it has room for three digits, so anything higher reads 999 (not the last number
    /// it had room for). The results screen shows the true score
    /// </summary>
    internal static string HudScoreText(string score)
    {
        if (int.TryParse(score, out int value) && value > 999)
            return "999";

        return score;
    }

    public void UpdateScoreUI(string passedInScore)
    {
        passedInScore = HudScoreText(passedInScore);
        moneyCount = passedInScore;

        char[] splitScore = passedInScore.ToCharArray();

        // If there is 0-9 dollars
        if (splitScore.Length == 1)
        {
            moneyImages[0].enabled = false;
            moneyImages[1].enabled = false;
            StartCoroutine(animateNumber(moneyImages[2], splitScore[0]));
            dollarSign.transform.localPosition = new Vector3(0, 0, 0);
            tipHolder.transform.localPosition = new Vector3(-50, 0, 0);
            tipHolder.transform.localScale = new Vector3(1.45f, 1.45f, 1.45f);
        }
        // If there is 10-99 dollars
        else if (splitScore.Length == 2)
        {
            moneyImages[0].enabled = false;
            StartCoroutine(animateNumber(moneyImages[1], splitScore[0]));
            StartCoroutine(animateNumber(moneyImages[2], splitScore[1]));
            dollarSign.transform.localPosition = new Vector3(-65, 0, 0);
            tipHolder.transform.localPosition = new Vector3(-16, 0, 0);
            tipHolder.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
        }
        // If there is 100-999 dollars
        else if (splitScore.Length == 3)
        {
            StartCoroutine(animateNumber(moneyImages[0], splitScore[0]));
            StartCoroutine(animateNumber(moneyImages[1], splitScore[1]));
            StartCoroutine(animateNumber(moneyImages[2], splitScore[2]));
            dollarSign.transform.localPosition = new Vector3(-115, 0, 0);

            tipHolder.transform.localPosition = new Vector3(0, 0, 0);
            tipHolder.transform.localScale = new Vector3(1, 1, 1);
        }
    }

    public void UpdateTimerUI(string passedInTimer)
    {
        timerCount = passedInTimer;

        char[] splitScore = passedInTimer.ToCharArray();

        StartCoroutine(animateNumberTimer(timerImages[0], splitScore[0]));
        StartCoroutine(animateNumberTimer(timerImages[1], splitScore[2]));
        StartCoroutine(animateNumberTimer(timerImages[2], splitScore[3]));
    }

    public void UpdateFinalCountdown(int time)
    {
        StartCoroutine(animateNumber(finalOrderCountdownImage, time));
    }

    public void UpdateOrderValueUI(string passedInScore)
    {
        finalOrderValue = passedInScore;

        char[] splitScore = passedInScore.ToCharArray();

        // If there is 0-9 dollars
        if (splitScore.Length == 1)
        {
            finalOrderImages[1].enabled = false;
            finalOrderImages[2].enabled = false;
            StartCoroutine(animateNumberTimer(finalOrderImages[0], splitScore[0]));
        }
        // If there is 10-99 dollars
        else if (splitScore.Length == 2)
        {
            finalOrderImages[2].enabled = false;
            StartCoroutine(animateNumberTimer(finalOrderImages[0], splitScore[0]));
            StartCoroutine(animateNumberTimer(finalOrderImages[1], splitScore[1]));
        }
        // If there is 100-999 dollars
        else if (splitScore.Length == 3)
        {
            StartCoroutine(animateNumberTimer(finalOrderImages[0], splitScore[0]));
            StartCoroutine(animateNumberTimer(finalOrderImages[1], splitScore[1]));
            StartCoroutine(animateNumberTimer(finalOrderImages[2], splitScore[2]));
        }
    }

    public void SetFinalCountdown(bool enabled)
    {
        finalOrderTextObject.SetActive(enabled);
        finalOrderCountdownImage.transform.parent.gameObject.SetActive(enabled);
    }

    // Methods for returning the number sprite I need
    private Sprite getNumSprite(char intNum)
    {
        int i;
        int.TryParse("" + intNum, out i);
        return numberSprites[i];
    }
    private Sprite getNumSprite(int intNum)
    {
        return numberSprites[intNum];
    }

    public void UpdatePlacement(int inPlace)
    {
        if (currPlace == inPlace)
            return;

        currPlace = inPlace;
        StartCoroutine(animateNumber(placementImage, inPlace));

        switch(inPlace)
        {
            case 1:
                suffix.text = "st";
                break;
            case 2:
                suffix.text = "nd";
                break;
            case 3:
                suffix.text = "rd";
                break;
            default:
                suffix.text = "th";
                break;
        }
        suffixBG.text = suffix.text;
    }

    private IEnumerator animateNumber(Image numToShrink, char charNum)
    {
        Sprite spriteToChangeTo = getNumSprite(charNum);
        Animator numAnimator = numToShrink.transform.parent.GetComponent<Animator>();

        numAnimator.SetBool(HashReference._shrinkTrigger, false);
        numAnimator.SetBool(HashReference._growTrigger, false);
        numAnimator.SetBool(HashReference._idleTrigger, false);

        numAnimator.SetBool(HashReference._shrinkTrigger, true);
        yield return new WaitForSeconds(0.2f);
        numAnimator.SetBool(HashReference._shrinkTrigger, false);
        numToShrink.enabled = true;

        numToShrink.sprite = spriteToChangeTo;
        numAnimator.SetBool(HashReference._growTrigger, true);

        yield return new WaitForSeconds(Random.Range(0, 0.4f));
        numAnimator.SetTrigger(HashReference._idleTrigger);
        yield break;
    }

    private IEnumerator animateNumber(Image numToShrink, int intNum)
    {
        Sprite spriteToChangeTo = getNumSprite(intNum);
        Animator numAnimator = numToShrink.transform.parent.GetComponent<Animator>();

        numAnimator.SetBool(HashReference._shrinkTrigger, false);
        numAnimator.SetBool(HashReference._growTrigger, false);
        numAnimator.SetBool(HashReference._idleTrigger, false);

        // else animate number
        numAnimator.SetBool(HashReference._shrinkTrigger, true);
        yield return new WaitForSeconds(0.2f);
        numAnimator.SetBool(HashReference._shrinkTrigger, false);
        numToShrink.enabled = true;

        numToShrink.sprite = spriteToChangeTo;
        numAnimator.SetBool(HashReference._growTrigger, true);

        yield return new WaitForSeconds(Random.Range(0, 0.4f));
        numAnimator.SetTrigger(HashReference._idleTrigger);
        yield break;
    }

    private IEnumerator animateNumberTimer(Image numToShrink, char charNum)
    {
        Sprite spriteToChangeTo = getNumSprite(charNum);

        // Early Return
        if (spriteToChangeTo != numToShrink.sprite)
        {
            Animator numAnimator = numToShrink.transform.parent.GetComponent<Animator>();

            numAnimator.SetBool(HashReference._shrinkTrigger, false);
            numAnimator.SetBool(HashReference._growTrigger, false);
            numAnimator.SetBool(HashReference._idleTrigger, false);

            numAnimator.SetBool(HashReference._shrinkTrigger, true);
            yield return new WaitForSeconds(0.2f);
            numAnimator.SetBool(HashReference._shrinkTrigger, false);
            numToShrink.enabled = true;

            numToShrink.sprite = spriteToChangeTo;
            numAnimator.SetBool(HashReference._growTrigger, true);

            yield return new WaitForSeconds(Random.Range(0, 0.4f));
            numAnimator.SetTrigger(HashReference._idleTrigger);
            yield break;
        }
    }
}

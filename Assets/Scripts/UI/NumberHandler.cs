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
    [SerializeField] internal Image[] moneyImages;
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
    /// What the score HUD shows for a score: it has room for four digits, so anything higher reads 9999. The results
    /// screen shows the true score
    /// </summary>
    internal static string HudScoreText(string score)
    {
        if (int.TryParse(score, out int value) && value > 9999)
            return "9999";

        return score;
    }

    /// <summary>Where the dollar sign goes and where the whole counter sits, and how big, for a count of digits</summary>
    internal struct ScoreLayout
    {
        public float DollarX;
        public float CounterX;
        public float CounterScale;
    }

    /// <summary>
    /// The layout for a count of digits (the nearest one for a count the HUD has no room for). The counter keeps its right
    /// edge in place as digits come and go, and shrinks to fit the fourth
    /// </summary>
    internal static ScoreLayout LayoutFor(int digits)
    {
        switch (Mathf.Clamp(digits, 1, 4))
        {
            case 1: return new ScoreLayout { DollarX = 0f, CounterX = -50f, CounterScale = 1.45f };
            case 2: return new ScoreLayout { DollarX = -65f, CounterX = -16f, CounterScale = 1.2f };
            case 3: return new ScoreLayout { DollarX = -115f, CounterX = 0f, CounterScale = 1f };
            default: return new ScoreLayout { DollarX = -175f, CounterX = 18f, CounterScale = 0.82f };
        }
    }

    // The fourth digit, made the first time a score reaches 1000 by copying the leftmost digit's holder one digit further left
    private Image thousandsImage;

    internal Image ThousandsImage()
    {
        if (thousandsImage == null)
        {
            RectTransform first = (RectTransform)moneyImages[0].transform.parent;
            RectTransform second = (RectTransform)moneyImages[1].transform.parent;
            RectTransform copy = Instantiate(first, first.parent);
            copy.name = "Image Holder (thousands)";
            copy.anchoredPosition = first.anchoredPosition + (first.anchoredPosition - second.anchoredPosition);
            thousandsImage = copy.GetComponentInChildren<Image>();
        }
        return thousandsImage;
    }

    public void UpdateScoreUI(string passedInScore)
    {
        passedInScore = HudScoreText(passedInScore);
        moneyCount = passedInScore;

        char[] splitScore = passedInScore.ToCharArray();
        if (splitScore.Length == 0)
            return;

        // The digits' slots from the left; a score of n digits fills the last n, and the others hide
        int count = Mathf.Min(splitScore.Length, 4);
        Image[] slots = { count > 3 ? ThousandsImage() : thousandsImage, moneyImages[0], moneyImages[1], moneyImages[2] };
        for (int slot = 0; slot < slots.Length; slot++)
        {
            if (slots[slot] == null)
                continue;

            int digit = slot - (slots.Length - count);
            if (digit < 0)
                slots[slot].enabled = false;
            else
                StartCoroutine(animateNumber(slots[slot], splitScore[digit]));
        }

        ScoreLayout layout = LayoutFor(count);
        dollarSign.transform.localPosition = new Vector3(layout.DollarX, 0, 0);
        tipHolder.transform.localPosition = new Vector3(layout.CounterX, 0, 0);
        tipHolder.transform.localScale = new Vector3(layout.CounterScale, layout.CounterScale, layout.CounterScale);
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

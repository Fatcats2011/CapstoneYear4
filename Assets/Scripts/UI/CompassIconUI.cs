using TMPro;
using UnityEngine;
using UnityEngine.UI;

///<summary>
/// To be put on the compass icon prefab, holds info to the real world object
///</summary>
public class CompassIconUI : MonoBehaviour
{
    public Image imageRect;
    public CompassMarker objectReference;
    public Image mainIcon, leftChildIcon, rightChildIcon;
    public TMP_Text distanceText, distanceTextChildLeft, distanceTextChildRight;
    public Animator animator, animatorChildLeft, animatorChildRight;
    public int distance;

    [Tooltip("Keeps track if the value has been faded on the ui or not")]
    [SerializeField] bool faded = false;
    public bool Faded { get { return faded; }}

    ///<summary>
    /// triggers the ui fade out
    ///</summary>
    public void FadeMarkerOut()
    {
        faded = true;
        animator.SetTrigger(HashReference._fadeOutTrigger);
        animatorChildLeft.SetTrigger(HashReference._fadeOutTrigger);
        animatorChildRight.SetTrigger(HashReference._fadeOutTrigger);
    }

    ///<summary>
    /// triggers the ui fade in
    ///</summary>
    public void FadeMarkerIn()
    {
        faded = false;
        animator.SetTrigger(HashReference._fadeInTrigger);
        animatorChildLeft.SetTrigger(HashReference._fadeInTrigger);
        animatorChildRight.SetTrigger(HashReference._fadeInTrigger);
    }

    public void SetCompassIconSprite(Sprite sprite)
    {
        mainIcon.sprite = sprite;
        leftChildIcon.sprite = sprite;
        rightChildIcon.sprite = sprite;
    }

    ///<summary>
    /// Updates the distance text on the icon, appends the m on the int distance. Josh's Idea
    ///</summary>
    public void SetDistanceText()
    {
        // Called every frame: the texts change (and their canvas rebuilds) only when the whole distance does
        if (distance == shownDistance)
            return;

        shownDistance = distance;
        string text = DistanceText(distance);
        distanceText.text = text;
        distanceTextChildLeft.text = text;
        distanceTextChildRight.text = text;
    }

    /// <summary>A distance in metres as the icon shows it: whole metres, or whole feet (Units)</summary>
    public static string DistanceText(int distance)
    {
        return Units.Distance(distance);
    }

    int shownDistance = int.MinValue; // the distance the texts show

    internal const float PRICE_SIZE = 18f;
    TMP_Text[] priceLines;
    string shownPrice = "";

    /// <summary>The price the icon shows now: "" when hidden</summary>
    internal string ShownPrice { get { return shownPrice; } }

    ///<summary>
    /// A small gold price just above the main icon and both child icons. "" hides it. Made the first time it has
    /// something to say
    ///</summary>
    public void SetPrice(string price)
    {
        price = price ?? "";
        if (priceLines == null)
        {
            if (price == "")
                return;
            priceLines = MakePriceLines();
        }

        shownPrice = price;
        foreach (TMP_Text line in priceLines)
        {
            line.text = price;
            line.gameObject.SetActive(price != "");
        }
    }

    // The icons whose price lines are made, in the lines' order
    readonly System.Collections.Generic.List<Image> pricedIcons = new System.Collections.Generic.List<Image>();

    void LateUpdate()
    {
        FollowFade();
    }

    /// <summary>
    /// The fade animations change each icon's own alpha (and its distance text's), not its children's: each price
    /// takes its icon's alpha, so it goes when the mark fades close up
    /// </summary>
    internal void FollowFade()
    {
        if (priceLines == null)
            return;
        for (int i = 0; i < pricedIcons.Count && i < priceLines.Length; i++)
        {
            if (pricedIcons[i] != null && priceLines[i] != null)
                priceLines[i].alpha = pricedIcons[i].color.a;
        }
    }

    TMP_Text[] MakePriceLines()
    {
        Image[] icons = { mainIcon, leftChildIcon, rightChildIcon };
        System.Collections.Generic.List<TMP_Text> lines = new System.Collections.Generic.List<TMP_Text>();
        foreach (Image icon in icons)
        {
            if (icon != null)
            {
                lines.Add(MakePriceLine(icon.transform));
                pricedIcons.Add(icon);
            }
        }
        // A bare icon (no images) keeps its one line on itself
        if (lines.Count == 0)
            lines.Add(MakePriceLine(transform));
        return lines.ToArray();
    }

    static TMP_Text MakePriceLine(Transform parent)
    {
        TMP_Text line = GameCard.Words(parent, "Price", PRICE_SIZE, GameCard.GOLD);
        line.richText = false;
        // Sits on the top edge of its icon
        RectTransform rect = line.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(120f, PRICE_SIZE + 6f);
        return line;
    }


}

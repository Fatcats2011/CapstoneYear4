using TMPro;
using UnityEngine;

/// <summary>
/// The small price under the package icon that floats over a pickup beacon (OrderFeedbackRules.PriceText: the order's
/// worth now, so a stolen order reads more and the Mother Lode $250; the golden order reads nothing). One per order, on its
/// BeconIndicator's object, with one label under each split-screen view's icon: it turns, scales and hides with that
/// view's icon, on that view's layer, and fades with it close up
/// </summary>
public class PriceTag : MonoBehaviour
{
    internal const float WORLD_SIZE = 4f;
    /// <summary>Local y under each view's icon</summary>
    internal const float BELOW_ICON = -0.9f;
    const string BANGERS = "Fonts & Materials/Bangers SDF";
    const float OUTLINE = 0.3f;

    BeconIndicator indicator;
    TextMeshPro[] labels = new TextMeshPro[0];
    string text = "";

    /// <summary>What the labels read now: "" when hidden</summary>
    internal string Text { get { return text; } }
    /// <summary>How many labels there are: one per icon</summary>
    internal int Labels { get { return labels.Length; } }

    /// <summary>An order's worth changed (or it starts afresh): its beacon's labels say so</summary>
    internal static void Refresh(Order order, BeconIndicator indicator)
    {
        Refresh(indicator, OrderFeedbackRules.PriceText(order.Value, order.IsLode, order.Worth));
    }

    /// <summary>The labels of an indicator read a price, made the first time</summary>
    internal static void Refresh(BeconIndicator indicator, string price)
    {
        if (indicator == null)
            return;

        PriceTag tag = indicator.GetComponent<PriceTag>();
        if (tag == null)
            tag = indicator.gameObject.AddComponent<PriceTag>();
        tag.indicator = indicator;
        tag.Set(price);
    }

    void Set(string price)
    {
        text = price;
        GameObject[] icons = indicator.Icons;
        if (icons == null)
            return;

        // Made once, with the icons the indicator has now
        if (labels.Length == 0)
            labels = Make(icons);

        foreach (TextMeshPro label in labels)
        {
            if (label == null)
                continue;
            label.text = price;
            if (label.gameObject.activeSelf != (price != ""))
                label.gameObject.SetActive(price != "");
        }
    }

    static TextMeshPro[] Make(GameObject[] icons)
    {
        int count = 0;
        foreach (GameObject icon in icons)
            if (icon != null)
                count++;

        TextMeshPro[] made = new TextMeshPro[count];
        int next = 0;
        foreach (GameObject icon in icons)
        {
            if (icon == null)
                continue;
            // Made on its own and awake, then put under its icon: a view nobody plays has its icon switched off, and
            // text made under that never wakes, so it can't take a font
            GameObject holder = new GameObject("Price");
            holder.layer = icon.layer;
            TextMeshPro label = holder.AddComponent<TextMeshPro>();
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(BANGERS);
            if (font != null)
                label.font = font;
            label.fontSize = WORLD_SIZE;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.richText = false;
            label.color = GameCard.GOLD;
            // The outline gives the label a material of its own: only in play (edit mode would leak it into the scene)
            if (Application.isPlaying)
            {
                label.outlineWidth = OUTLINE;
                label.outlineColor = Color.black;
            }
            holder.transform.SetParent(icon.transform, false);
            holder.transform.localPosition = new Vector3(0f, BELOW_ICON, 0f);
            made[next++] = label;
        }
        return made;
    }

    // The icons fade close up: the prices fade with them
    void LateUpdate()
    {
        FollowIcons();
    }

    /// <summary>Each label's alpha becomes its icon's sprite alpha</summary>
    internal void FollowIcons()
    {
        if (indicator == null || indicator.Icons == null || indicator.IconSprites == null)
            return;

        int next = 0;
        for (int i = 0; i < indicator.Icons.Length && next < labels.Length; i++)
        {
            if (indicator.Icons[i] == null)
                continue;
            TextMeshPro label = labels[next++];
            if (label == null || i >= indicator.IconSprites.Length || indicator.IconSprites[i] == null)
                continue;
            Color colour = label.color;
            colour.a = indicator.IconSprites[i].color.a;
            label.color = colour;
        }
    }
}

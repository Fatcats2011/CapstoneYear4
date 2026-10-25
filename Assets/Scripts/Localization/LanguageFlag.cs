using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A little flag showing the language the game speaks (the user's ask): on the title screen's corner and beside the
/// LANGUAGE row. It follows a change of language at once
/// </summary>
public class LanguageFlag : MonoBehaviour
{
    const float ASPECT = 1.5f; // the flags are 96 x 64

    Image image;

    /// <summary>A flag under a parent, anchored (0-1 each way) at a spot, this tall</summary>
    public static LanguageFlag Create(RectTransform parent, Vector2 anchor, Vector2 position, float height)
    {
        GameObject holder = new GameObject("Language Flag", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)holder.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(height * ASPECT, height);

        LanguageFlag flag = holder.AddComponent<LanguageFlag>();
        flag.image = holder.GetComponent<Image>();
        flag.image.raycastTarget = false;
        flag.image.preserveAspect = true;
        Loc.Changed += flag.Show;
        flag.Show();
        return flag;
    }

    void Show()
    {
        FlagSet flags = FlagSet.Game;
        if (image != null && flags != null)
            image.sprite = flags.Of(Loc.Language);
    }

    void OnDestroy()
    {
        Loc.Changed -= Show;
    }
}

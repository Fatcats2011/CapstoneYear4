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

    /// <summary>A distance as the icon shows it, in whole feet</summary>
    public static string DistanceText(int distance)
    {
        return distance + "ft";
    }

    int shownDistance = int.MinValue; // the distance the texts show


}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The phase into the menu. The menu scene opens on the purple the boot intro's wiper left, and after a moment the
/// title screen wipes in through it on a slant (the MenuTransition shader). It lives on the Menu Ghost Reveal picture,
/// last on the title canvas; the title's ghost show waits until it is Done.
/// The shader shows its reveal texture where the wipe hasn't reached and is see-through where it has, so the reveal
/// texture is one purple pixel with no alpha
/// </summary>
public class MenuGhostReveal : MonoBehaviour
{
    [SerializeField] internal Image menuGhostRevealObject;
    [SerializeField] internal Material menuGhostRevealMaterialReference;
    [Tooltip("Seconds of plain purple before the wipe starts")]
    [SerializeField] internal float holdTime = 0.35f;
    [Tooltip("Seconds the title screen takes to wipe in")]
    [SerializeField] internal float revealTime = 1f;
    [Tooltip("The boot intro's wiper colour, which the menu scene opens on")]
    [SerializeField] internal Color wipeColour = new Color(0.6666667f, 0f, 0.7921569f, 1f);

    Material wipe;
    Texture2D purple;

    /// <summary>The title screen is fully in and the picture is off: the title is the show's</summary>
    public bool Done { get; private set; }

    void Start()
    {
        purple = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Menu Wipe Purple" };
        purple.SetPixel(0, 0, new Color(wipeColour.r, wipeColour.g, wipeColour.b, 0f));
        purple.Apply();

        wipe = new Material(menuGhostRevealMaterialReference);
        wipe.SetTexture("_RevealColorTex", purple);
        wipe.SetFloat("_Cutoff", 0f);
        menuGhostRevealObject.material = wipe;
        menuGhostRevealObject.enabled = true;

        StartCoroutine(Reveal());
    }

    void OnDestroy()
    {
        if (wipe != null)
            Destroy(wipe);
        if (purple != null)
            Destroy(purple);
    }

    IEnumerator Reveal()
    {
        yield return new WaitForSeconds(holdTime);
        for (float t = 0f; t < revealTime; t += Time.deltaTime)
        {
            wipe.SetFloat("_Cutoff", t / revealTime);
            yield return null;
        }
        wipe.SetFloat("_Cutoff", 1f);

        menuGhostRevealObject.enabled = false;
        Done = true;
    }
}

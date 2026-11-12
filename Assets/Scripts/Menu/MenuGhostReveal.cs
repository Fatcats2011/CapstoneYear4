using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The phase into the menu. The scene opens on the purple the boot intro's wiper left, which turns out to be a ghost
/// right up against the camera: it pulls back to show the crew in silhouette, and the title screen wipes in behind
/// them in full colour. The crew holds its pose for a beat, then rides off, and the title's own show takes over
/// </summary>
public class MenuGhostReveal : MonoBehaviour
{
    [SerializeField] float timeToStart;
    [Tooltip("Not used any more: the wipe takes revealTime, whatever the frame rate")]
    [SerializeField] float speed;
    [SerializeField] Animator animator;
    [SerializeField] Image menuGhostRevealObject;
    [SerializeField] Material menuGhostRevealMaterialReference;
    [Tooltip("Seconds the title screen takes to wipe in")]
    [SerializeField] internal float revealTime = 1.2f;
    [Tooltip("Seconds the crew holds its pose once the title screen is in")]
    [SerializeField] internal float holdTime = 1.2f;
    [Tooltip("Seconds a ghost takes to ride off, and the gap between one leaving and the next")]
    [SerializeField] internal float leaveTime = 0.9f;
    [SerializeField] internal float leaveGap = 0.15f;
    Material menuGhostRevealMaterial;

    /// <summary>The crew has gone and the picture is off: the title screen is the show's</summary>
    public bool Done { get; private set; }

    void Start()
    {
        menuGhostRevealMaterial = new Material(menuGhostRevealMaterialReference);
        menuGhostRevealObject.material = menuGhostRevealMaterial;
        menuGhostRevealMaterial.SetFloat("_Cutoff", 0);

        menuGhostRevealObject.enabled = true;

        StartCoroutine(WaitToStart());
    }

    void OnDestroy()
    {
        if (menuGhostRevealMaterial != null)
            Destroy(menuGhostRevealMaterial);
    }

    /// <summary>Called by the MenuIntro animation as the crew settles</summary>
    private void Reveal()
    {
        StartCoroutine(RevealScreen());
    }

    private IEnumerator WaitToStart()
    {
        yield return new WaitForSeconds(timeToStart);
        animator.enabled = true;
    }

    private IEnumerator RevealScreen()
    {
        for (float t = 0f; t < revealTime; t += Time.deltaTime)
        {
            menuGhostRevealMaterial.SetFloat("_Cutoff", t / revealTime);
            yield return null;
        }
        menuGhostRevealMaterial.SetFloat("_Cutoff", 1f);

        yield return new WaitForSeconds(holdTime);
        yield return Leave();
    }

    /// <summary>The crew rides off the right of the picture one after another, then the rig is switched off</summary>
    private IEnumerator Leave()
    {
        animator.enabled = false; // it would hold the lead ghost in its pose
        Camera[] cameras = GetComponentsInChildren<Camera>(true);
        Vector3 away = (cameras.Length > 0 ? cameras[0].transform.right : Vector3.right) * 40f;

        int ghosts = 0;
        foreach (Transform child in transform)
        {
            if (child.GetComponent<Camera>() != null)
                continue;
            child.DOMove(child.position + away, leaveTime).SetEase(Ease.InQuad).SetDelay(ghosts * leaveGap).SetLink(child.gameObject);
            ghosts++;
        }
        yield return new WaitForSeconds(leaveTime + Mathf.Max(0, ghosts - 1) * leaveGap + 0.1f);

        menuGhostRevealObject.enabled = false;
        foreach (Camera rigCamera in cameras)
            rigCamera.enabled = false;
        Done = true;
    }
}

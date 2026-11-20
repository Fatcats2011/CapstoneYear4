using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// This class is for displaying emotes on the player's driving canvas.
/// </summary>
public class EmoteHandler : MonoBehaviour
{
    [Header("Player Information")]
    [Tooltip("GO on the player prefab with the InputManager component.")]
    [SerializeField] internal InputManager input;
    private IDriveInput driveInput; // set when something other than this prefab's controller drives the player
    private bool listening; // while enabled: emotes follow the driver's d-pad

    [Tooltip("GO on the player prefab with the SoundPool component.")]
    [SerializeField] private SoundPool soundPool;

    [Tooltip("Sprite renderer child of emote bubble")]
    [SerializeField] private SpriteRenderer emoteBubble;

    [Header("Emote Information")]
    [Tooltip("Image on the canvas where the emotes will pop up.")]
    [SerializeField] private Image emoteImg;

    [Tooltip("Array of sprite emotes. [0]: Up, [1]: Right, [2]: Down, [3]: Left.")]
    [SerializeField] private Sprite[] emoteSprites;
    private Sprite currEmote;
    
    [Tooltip("How long the emote stays on the screen for.")]
    [SerializeField] private float emoteLifetime;

    private bool emoting = false;
    
    private IEnumerator emoteShowRoutine;
    // Start is called before the first frame update
    void Start()
    {
        ResetEmote();
    }

    internal void OnEnable()
    {
        listening = true;
        StartListening();
    }
    internal void OnDisable()
    {
        StopListening();
        listening = false;
    }

    /// <summary>
    /// Whose d-pad shows emotes: the controller on this machine unless something else is set. Can change at any time
    /// </summary>
    public IDriveInput DriveInput
    {
        get { return driveInput ?? input; }
        set
        {
            if (listening)
                StopListening();
            driveInput = value;
            if (listening)
                StartListening();
        }
    }

    private void StartListening()
    {
        IDriveInput driver = DriveInput;
        if (driver != null)
            driver.EmotePad += Emote;
    }

    private void StopListening()
    {
        IDriveInput driver = DriveInput;
        if (driver != null)
            driver.EmotePad -= Emote;
    }

    /// <summary>
    /// Resets the emote image to its original state.
    /// </summary>
    private void ResetEmote()
    {
        emoteImg.gameObject.SetActive(false);
        emoteImg.sprite = null;
        
        emoteBubble.transform.parent.gameObject.SetActive(false);
        emoteBubble.sprite = null;
        
        currEmote = null;
    }

    /// <summary>
    /// A d-pad press: its bubble and sound (index 0 up, 1 right, 2 down, 3 left: PlayerEmoteRules.PadIndex), and the
    /// player's ghost plays the matching emote (PlayerEmotes.Pad, which also sends it to the other machines online)
    /// </summary>
    public void Emote(Vector2 dpad)
    {
        if(emoting) { return; }
        int index = PlayerEmoteRules.PadIndex(dpad);
        if(index != -1)
        {
            StopEmoteShow();
            currEmote = emoteSprites[index];
            soundPool.PlayEmote(index);
            StartEmoteShow();

            PlayerEmotes ghost = GetComponentInParent<PlayerEmotes>();
            if (ghost != null)
                ghost.Pad(index);
        }
    }

    private void StartEmoteShow()
    {
        if(emoteShowRoutine == null)
        {
            emoteShowRoutine = ShowEmote();
            StartCoroutine(emoteShowRoutine);
        }
    }

    private void StopEmoteShow()
    {
        if(emoteShowRoutine != null)
        {
            StopCoroutine(emoteShowRoutine);
            emoteShowRoutine = null;
            ResetEmote();
        }
    }

    /// <summary>
    /// Shows the emote for a specified amout of time and then disable it.
    /// </summary>
    /// <returns></returns>
    private IEnumerator ShowEmote()
    {
        emoting = true;
        
        emoteImg.sprite = currEmote;
        emoteImg.gameObject.SetActive(true);

        emoteBubble.sprite = currEmote;
        emoteBubble.transform.parent.gameObject.SetActive(true);

        yield return new WaitForSeconds(emoteLifetime);
        emoteImg.sprite = null;
        ResetEmote();
        emoting = false;
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerSelectCanvas : SingletonMonobehaviour<PlayerSelectCanvas>
{
    [Header("Text Information")]
    [SerializeField] TMP_Text countdownText;
    [SerializeField] internal GameObject[] pressButtonTexts;

    [Header("Other")]
    [SerializeField] PlayerInstantiate playerInstantiate;
    [SerializeField] Animator countdown;

    /// <summary>Dispatch's banner follows these: the countdown to the match began, or was called off</summary>
    public event Action CountdownBegan;
    public event Action CountdownStopped;

    [Tooltip("Each seat's preview panel (Player 1 Cam … Player 4 Cam): Dispatch hides them to show its room")]
    [SerializeField] internal GameObject[] playerViews;
    bool[] playerViewsKept;   // each panel's state before Dispatch hid them (null: not hidden)

    [Tooltip("The old lobby's look (Player Backgrounds, Player Icons, the dividers): Dispatch hides them to show its room. Not the countdown")]
    [SerializeField] internal GameObject[] dispatchHides;
    bool[] dispatchHidesKept; // each one's state before Dispatch hid them
    [Tooltip("Canvases Dispatch stops drawing (the title screen's Main Menu Canvas): their scripts keep running")]
    [SerializeField] internal Canvas[] dispatchHideCanvases;
    bool[] dispatchCanvasesKept; // each one's enabled before Dispatch hid them
    bool dispatchHidden;

    bool joinPromptsHidden;   // Dispatch has the join prompts hidden
    bool[] joinPromptsKept;   // each prompt's state under that hiding: what comes back

    public void Start()
    {
        playerInstantiate = PlayerInstantiate.Instance;

        TogglePressButtonOnAllTexts(playerInstantiate.Roster);
    }

    public void BeginCountdown()
    {
        countdown.SetTrigger(HashReference._countdownTrigger);
        CountdownBegan?.Invoke();
    }

    public void StopCountdown()
    {
        countdown.SetTrigger(HashReference._resetTrigger);
        CountdownStopped?.Invoke();
    }

    // Toggles the press button text on one player's position
    public void TogglePressButtonTexts(int position, bool toggleOnOff)
    {
        // Hidden by Dispatch: the prompt returns with the rest
        if (joinPromptsHidden)
        {
            joinPromptsKept[position] = toggleOnOff;
            return;
        }

        pressButtonTexts[position].SetActive(toggleOnOff);
    }

    // Hides the join prompt of every taken slot
    public void TogglePressButtonOnAllTexts(PlayerRoster roster)
    {
        foreach (PlayerSlot player in roster.Players)
            TogglePressButtonTexts(player.Index, false);
    }

    /// <summary>
    /// Dispatch (online) hides every join prompt; false puts each back as it was (or as a join or leave since made it)
    /// </summary>
    public void SetJoinPromptsHidden(bool hidden)
    {
        if (hidden == joinPromptsHidden)
            return;

        if (hidden)
        {
            joinPromptsKept = new bool[pressButtonTexts.Length];
            for (int i = 0; i < pressButtonTexts.Length; i++)
            {
                joinPromptsKept[i] = pressButtonTexts[i].activeSelf;
                pressButtonTexts[i].SetActive(false);
            }
            joinPromptsHidden = true;
            return;
        }

        joinPromptsHidden = false;
        for (int i = 0; i < pressButtonTexts.Length; i++)
            pressButtonTexts[i].SetActive(joinPromptsKept[i]);
    }

    /// <summary>
    /// Dispatch hides the old lobby's look (dispatchHides) and stops other canvases drawing (dispatchHideCanvases, the
    /// title screen's): they draw over every camera. False puts each back as it was
    /// </summary>
    public void SetDispatchHidden(bool hidden)
    {
        if (hidden == dispatchHidden)
            return;
        dispatchHidden = hidden;

        if (hidden)
        {
            dispatchHidesKept = new bool[dispatchHides == null ? 0 : dispatchHides.Length];
            for (int i = 0; i < dispatchHidesKept.Length; i++)
            {
                if (dispatchHides[i] == null)
                    continue;
                dispatchHidesKept[i] = dispatchHides[i].activeSelf;
                dispatchHides[i].SetActive(false);
            }
            dispatchCanvasesKept = new bool[dispatchHideCanvases == null ? 0 : dispatchHideCanvases.Length];
            for (int i = 0; i < dispatchCanvasesKept.Length; i++)
            {
                if (dispatchHideCanvases[i] == null)
                    continue;
                dispatchCanvasesKept[i] = dispatchHideCanvases[i].enabled;
                dispatchHideCanvases[i].enabled = false;
            }
            return;
        }

        for (int i = 0; i < dispatchHidesKept.Length; i++)
        {
            if (dispatchHides[i] != null)
                dispatchHides[i].SetActive(dispatchHidesKept[i]);
        }
        for (int i = 0; i < dispatchCanvasesKept.Length; i++)
        {
            if (dispatchHideCanvases[i] != null)
                dispatchHideCanvases[i].enabled = dispatchCanvasesKept[i];
        }
    }

    /// <summary>A seat's preview (Player N Cam's picture: the ghost and its colour and hat sliders), or null</summary>
    public Texture ViewPicture(int seat)
    {
        if (playerViews == null || seat < 0 || seat >= playerViews.Length || playerViews[seat] == null)
            return null;
        RawImage picture = playerViews[seat].GetComponent<RawImage>();
        return picture == null ? null : picture.texture;
    }

    /// <summary>
    /// Dispatch shows the room, not each player's preview panel (Player N Cam): on Dispatch every panel is hidden except
    /// the one whose player has the sliders open (-1: none). Off Dispatch they come back as they were
    /// </summary>
    public void SetPlayerViews(bool dispatch, int customizingSeat)
    {
        if (playerViews == null)
            return;

        if (dispatch && playerViewsKept == null)
        {
            playerViewsKept = new bool[playerViews.Length];
            for (int i = 0; i < playerViews.Length; i++)
                playerViewsKept[i] = playerViews[i] != null && playerViews[i].activeSelf;
        }

        if (!dispatch)
        {
            if (playerViewsKept == null)
                return;
            for (int i = 0; i < playerViews.Length; i++)
            {
                if (playerViews[i] != null)
                    playerViews[i].SetActive(playerViewsKept[i]);
            }
            playerViewsKept = null;
            return;
        }

        for (int i = 0; i < playerViews.Length; i++)
        {
            bool on = i == customizingSeat;
            if (playerViews[i] != null && playerViews[i].activeSelf != on)
                playerViews[i].SetActive(on);
        }
    }
}

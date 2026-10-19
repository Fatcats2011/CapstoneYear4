using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

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
}

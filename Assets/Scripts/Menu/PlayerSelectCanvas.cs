using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerSelectCanvas : SingletonMonobehaviour<PlayerSelectCanvas>
{
    [Header("Text Information")]
    [SerializeField] TMP_Text countdownText;
    [SerializeField] GameObject[] pressButtonTexts;

    [Header("Other")]
    [SerializeField] PlayerInstantiate playerInstantiate;
    [SerializeField] Animator countdown;

    public void Start()
    {
        playerInstantiate = PlayerInstantiate.Instance;

        TogglePressButtonOnAllTexts(playerInstantiate.Roster);
    }

    public void BeginCountdown()
    {
        countdown.SetTrigger(HashReference._countdownTrigger);
    }

    public void StopCountdown()
    {
        countdown.SetTrigger(HashReference._resetTrigger);
    }

    // Toggles the press button text on one player's position
    public void TogglePressButtonTexts(int position, bool toggleOnOff)
    {
        pressButtonTexts[position].SetActive(toggleOnOff);
    }

    // Hides the join prompt of every taken slot
    public void TogglePressButtonOnAllTexts(PlayerRoster roster)
    {
        foreach (PlayerSlot player in roster.Players)
            TogglePressButtonTexts(player.Index, false);
    }

}

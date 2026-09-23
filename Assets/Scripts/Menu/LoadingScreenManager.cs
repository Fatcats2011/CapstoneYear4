using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenManager : SingletonMonobehaviour<LoadingScreenManager>
{
    [Header("Loading Screen Information")]
    [SerializeField] GameObject textConfirm;
    [SerializeField] Transform[] buttonPositions;

    [SerializeField] GameObject[] ButtonGameobjects;
    [SerializeField] GameObject[] ButtonColors;

    ///<summary>
    /// Shows a confirm button for every player, packed left to right in slot order
    ///</summary>
    public void InitalizeButtonGameobjects(PlayerRoster roster)
    {
        int buttons = 0;

        foreach (PlayerSlot player in roster.Players)
        {
            ButtonGameobjects[player.Index].gameObject.transform.position = buttonPositions[buttons].transform.position;

            // Enables buttons required for players
            ButtonGameobjects[player.Index].gameObject.SetActive(true);

            buttons++;
        }

        textConfirm.SetActive(true);
    }

    ///<summary>
    /// Disables all buttons after they are not needed
    ///</summary>
    public IEnumerator DisableButtonGameobjects()
    {
        yield return new WaitForSeconds(1f);

        // Set Buttons to false
        for (int i = 0; i < 4; i++)
        {
            ButtonGameobjects[i].gameObject.SetActive(false);
            ButtonColors[i].gameObject.SetActive(false);
        }

        textConfirm.SetActive(false);
    }

    ///<summary>
    /// Confirms button for one player
    ///</summary>
    public void ConfirmButton(int playerPos)
    {
        ButtonColors[playerPos].gameObject.SetActive(true);
    }

}

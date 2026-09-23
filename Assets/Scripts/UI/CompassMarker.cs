using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

///<summary>
/// To be added to any world object that we want to be tracked by compass
///</summary>
public class CompassMarker : MonoBehaviour
{
    public Sprite icon;
    PlayerInstantiate playerInstantiate;

    public void Start()
    {
        playerInstantiate = PlayerInstantiate.Instance;
    }

    private void OnEnable()
    {
        //GameManager.Instance.OnSwapResults += RemoveCompassUIFromAllPlayers;
    }

    private void OnDisable()
    {
        //GameManager.Instance.OnSwapResults -= RemoveCompassUIFromAllPlayers;
    }

    ///<summary>
    /// Initalizes the compass ui on all players
    ///</summary>
    public void InitalizeCompassUIOnAllPlayers()
    {
        if(playerInstantiate == null)
            playerInstantiate = PlayerInstantiate.Instance;

        // Every split-screen view on this machine has a compass
        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            player.Player.GetComponentInChildren<Compass>().AddCompassMarker(this);
    }

    ///<summary>
    /// Removes the icon for all players
    ///</summary>
    public void RemoveCompassUIFromAllPlayers()
    {
        if (playerInstantiate == null)
            playerInstantiate = PlayerInstantiate.Instance;

        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            player.Player.GetComponentInChildren<Compass>().RemoveCompassMarker(this);
    }

    ///<summary>
    /// Switches the icon on the compass ui
    /// isCarried indicates a scooter icon
    /// !isCarried indicates a floor icon
    ///</summary>
    public void SwitchCompassUIForPlayers(bool isCarried)
    {
        if (playerInstantiate == null)
            playerInstantiate = PlayerInstantiate.Instance;

        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            player.Player.GetComponentInChildren<Compass>().ChangeCompassMarkerIcon(this, isCarried);
    }
}

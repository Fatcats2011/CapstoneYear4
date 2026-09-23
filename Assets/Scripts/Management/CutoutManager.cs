using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns the cutouts based on number of players in the scene.
/// </summary>
public class CutoutManager : MonoBehaviour
{
    [Tooltip("Refs to all the cutouts in the game.")]
    [SerializeField] private CutoutHandler[] cutouts;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += SpawnCutouts;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= SpawnCutouts;
    }

    private void SpawnCutouts()
    {
        foreach (PlayerSlot player in PlayerInstantiate.Instance.Roster.Players)
        {
            cutouts[player.Index].gameObject.SetActive(true);
            cutouts[player.Index].InitCutout();
        }
    }
}

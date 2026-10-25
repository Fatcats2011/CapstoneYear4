using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Special functionality for the final order. I'm amazed this script isn't being written earlier.
/// </summary>
public class FinalOrder : MonoBehaviour
{
    [SerializeField] private Order finalOrder;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapAnything += EraseFinal;
    }
    private void OnDisable()
    {
        GameManager.Instance.OnSwapAnything -= EraseFinal;
    }
    /// <summary>Whether the golden order is out in the world (tests)</summary>
    internal bool Live => finalOrder != null && finalOrder.IsActive;

    void Start()
    {
        // No golden round (the match rules): this scene only shows the results. Only the host's matters: it spawns orders
        if (!HouseRules.Playing.GoldenOrder || GameManager.Instance.MainState == GameState.Results)
            return;
        finalOrder.InitOrder();
    }

    private void EraseFinal()
    {
        if(GameManager.Instance.MainState != GameState.GoldenCutscene && GameManager.Instance.MainState != GameState.FinalPackage && finalOrder.IsActive)
        {
            finalOrder.EraseGoldWithoutDelivering();
        }
    }
}

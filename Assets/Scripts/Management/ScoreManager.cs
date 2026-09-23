using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Manages the scores of all players and assigns placements.
/// </summary>
public class ScoreManager : SingletonMonobehaviour<ScoreManager>
{
    [SerializeField] private List<OrderHandler> orderHandlers = new List<OrderHandler>(); // list of order handlers in the scene

    private void OnEnable()
    {
        GameManager.Instance.OnSwapMenu += ResetScore;
        //GameManager.Instance.OnSwapBegin += ResetScore;
        GameManager.Instance.OnSwapResults += UpdatePlacement;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapMenu -= ResetScore;
        //GameManager.Instance.OnSwapBegin -= ResetScore;
        GameManager.Instance.OnSwapResults -= UpdatePlacement;
    }

    /// <summary>
    /// Adds an OrderHandler to the list if they're not already in the list.
    /// </summary>
    /// <param name="inOrderHandler">OrderHandler to be added</param>
    public void AddOrderHandler(OrderHandler inOrderHandler)
    {
        if (!orderHandlers.Contains(inOrderHandler))
        {
            orderHandlers.Add(inOrderHandler);
            UpdatePlacement();
        }
    }

    /// <summary>
    /// Recounts the order handlers from the roster, to resize the list in case a player left
    /// </summary>
    public void UpdateOrderHandlers(PlayerRoster roster)
    {
        orderHandlers.Clear();

        foreach (PlayerSlot player in roster.Players)
            orderHandlers.Add(player.Player.GetComponentInChildren<OrderHandler>());
    }

    /// <summary>
    /// Sorts the orderHandlers list based on their scores and assigns them placements.
    /// Called when a new OrderHandler is added to the list and when a delivery is made.
    /// </summary>
    public void UpdatePlacement()
    {
        orderHandlers.Sort((i, j) => j.Score.CompareTo(i.Score));

        for (int i = 0; i < orderHandlers.Count; i++)
        {
            if (i > 0)
            {
                if (orderHandlers[i].Score == orderHandlers[i - 1].Score) // checks if score is the same with previous OH, basically allows for ties
                {
                    orderHandlers[i].Placement = orderHandlers[i - 1].Placement;
                }
                else
                {
                    orderHandlers[i].Placement = i + 1;
                }
            }
            else
            {
                orderHandlers[i].Placement = i + 1;
            }
            orderHandlers[i].UpdatePlacement();
        }
    }

    /// <summary>
    /// Returns an OrderHandler at a specific index, or null if index is invalid.
    /// </summary>
    /// <param name="index">Specified index</param>
    /// <returns></returns>
    public OrderHandler GetHandlerOfIndex(int index)
    {
        OrderHandler outHandler;
        try
        {
            outHandler = orderHandlers[index];
        }
        catch
        {
            outHandler = null;
        }
        return outHandler;
    }

    /// <summary>
    /// This method resets the score of all players and recalculates their placements.
    /// </summary>
    private void ResetScore()
    {
        foreach (OrderHandler handler in orderHandlers)
        {
            handler.Score = 0;
        }
        UpdatePlacement();
    }
}

/// <summary>
/// Decides what happens when a player's controller disconnects mid-match
/// </summary>
public static class ControllerDisconnectPolicy
{
    ///<summary>
    /// Pauses only while players are driving (their pause menu is active) and the game isn't already paused
    ///</summary>
    public static bool ShouldPause(bool pauseMenuIsActive, bool gameIsPaused)
    {
        return pauseMenuIsActive && !gameIsPaused;
    }

    ///<summary>
    /// The first player who still has a controller runs the pause menu; if nobody does, the disconnected player
    ///</summary>
    public static int PickPauseHost(bool[] slotHasController, int disconnectedSlot)
    {
        for (int i = 0; i < slotHasController.Length; i++)
        {
            if (slotHasController[i] && i != disconnectedSlot)
                return i;
        }

        return disconnectedSlot;
    }

    ///<summary>
    /// Which player a newly pressed controller belongs to: the first one missing a controller, or -1 if nobody is
    ///</summary>
    public static int PickSlotForReplacement(bool[] slotIsMissingController)
    {
        for (int i = 0; i < slotIsMissingController.Length; i++)
        {
            if (slotIsMissingController[i])
                return i;
        }

        return -1;
    }
}

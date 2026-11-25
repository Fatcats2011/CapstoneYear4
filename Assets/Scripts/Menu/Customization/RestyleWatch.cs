/// <summary>
/// When a ghost shows off a new colour or hat in player select (PlayerEmotes.Restyled), so scrolling through the choices
/// doesn't set it off at every step (playtest, 2026-11-25). Pure logic: CustomizationSelector and OnlineGame ask it.
/// - With a DONE (Dispatch's customize pop-up): once as the pop-up closes, if the look changed since it opened (Take).
/// - Without one (Local Play, whose sliders are always there): once the new look has stayed put SETTLE seconds (Settled).
/// - Another machine's player: once as their pop-up closes, from what their machine shares (Closed).
/// A look scrolled away from and back again is no change.
/// </summary>
public class RestyleWatch
{
    /// <summary>Seconds a new look stays put before a ghost with no DONE shows it off</summary>
    public const float SETTLE = 1.2f;

    int markedColour = -1, markedHat = -1; // the look shown off last, or the pop-up opened with (-1: none yet)
    int lastColour = -1, lastHat = -1;     // the look last seen by Settled
    float changedAt;                       // when Settled last saw it change
    bool open;                             // Closed: their pop-up is open

    /// <summary>The look now is the one to compare with from here (the pop-up opened, or it was shown off)</summary>
    public void Mark(int colour, int hat)
    {
        markedColour = colour;
        markedHat = hat;
        lastColour = colour;
        lastHat = hat;
    }

    /// <summary>Whether the look differs from the marked one (nothing marked yet: no)</summary>
    public bool Changed(int colour, int hat)
    {
        return markedColour >= 0 && (colour != markedColour || hat != markedHat);
    }

    /// <summary>The pop-up closes: whether the look changed since it opened. The look now is marked either way</summary>
    public bool Take(int colour, int hat)
    {
        bool changed = Changed(colour, hat);
        Mark(colour, hat);
        return changed;
    }

    /// <summary>
    /// Asked every frame with the look now: true once, when a changed look has stayed put SETTLE seconds (then it's marked)
    /// </summary>
    public bool Settled(int colour, int hat, float now)
    {
        if (colour != lastColour || hat != lastHat)
        {
            lastColour = colour;
            lastHat = hat;
            changedAt = now;
            return false;
        }
        if (!Changed(colour, hat) || now - changedAt < SETTLE)
            return false;

        Mark(colour, hat);
        return true;
    }

    /// <summary>
    /// Another machine's player, as their machine shares them: true once, as their pop-up closes, when their look changed
    /// while it was open. Not when they readied as they closed it (A): they cheer for that instead
    /// </summary>
    public bool Closed(bool customizing, int colour, int hat, bool ready)
    {
        if (customizing == open)
            return false;

        open = customizing;
        if (customizing)
        {
            Mark(colour, hat);
            return false;
        }
        return Take(colour, hat) && !ready;
    }
}

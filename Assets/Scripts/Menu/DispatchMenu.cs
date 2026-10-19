/// <summary>
/// The buttons a player has in Dispatch (online player select)
/// </summary>
public enum DispatchButton { South, East, West, North, Pad }

/// <summary>
/// What a Dispatch button press does
/// </summary>
public enum DispatchAction { None, OpenSliders, CloseSliders, Scroll, Ready, CloseAndReady, Unready, Leave, Invite }

/// <summary>
/// Dispatch's button rules: X opens the customization sliders, which stay closed until then. While they are open, the d-pad
/// scrolls them and B closes them before it does anything else. Pure logic: MenuInteractions does what Press returns
/// </summary>
public class DispatchMenu
{
    /// <summary>
    /// Whether the sliders are open
    /// </summary>
    public bool Open { get; private set; }

    /// <summary>
    /// What a press does for a player who is (not) ready. Updates Open
    /// </summary>
    public DispatchAction Press(DispatchButton button, bool readied)
    {
        switch (button)
        {
            case DispatchButton.West:
                if (Open)
                {
                    Open = false;
                    return DispatchAction.CloseSliders;
                }
                // No customizing while ready
                if (readied)
                    return DispatchAction.None;
                Open = true;
                return DispatchAction.OpenSliders;

            case DispatchButton.Pad:
                return Open ? DispatchAction.Scroll : DispatchAction.None;

            case DispatchButton.East:
                if (Open)
                {
                    Open = false;
                    return DispatchAction.CloseSliders;
                }
                return readied ? DispatchAction.Unready : DispatchAction.Leave;

            case DispatchButton.South:
                if (readied)
                    return DispatchAction.None;
                if (Open)
                {
                    Open = false;
                    return DispatchAction.CloseAndReady;
                }
                return DispatchAction.Ready;

            case DispatchButton.North:
                return DispatchAction.Invite;
        }
        return DispatchAction.None;
    }

    /// <summary>
    /// Closes the sliders
    /// </summary>
    public void Reset()
    {
        Open = false;
    }
}

/// <summary>
/// How the game plays online. See docs/online.md
/// </summary>
public static class OnlineRules
{
    /// <summary>
    /// Online, one player per machine plays (the Dispatch lobby seats a machine, not a controller). A test seam: the couch
    /// seat tests (Phase 3J) set it false for their setup and true again after
    /// </summary>
    public static bool OnePlayerPerMachine = true;
}

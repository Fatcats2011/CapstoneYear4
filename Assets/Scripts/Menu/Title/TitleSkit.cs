using System.Collections;

/// <summary>
/// One of the title screen's little shows (TitleStage plays them in a shuffled order): a few ghosts ride on from off
/// screen, act something out and leave. The stage dresses the cast first and parks it afterwards; a skit only moves
/// its cast and the packages it takes from the stage, and ends with everyone past the screen's edges
/// </summary>
public abstract class TitleSkit
{
    public abstract string Name { get; }

    /// <summary>How many ghosts it needs (2 to TitleStage.POOL)</summary>
    public abstract int Cast { get; }

    /// <summary>Whether this member of the cast must wear a hat that shows (the Hat Thief's victim)</summary>
    public virtual bool NeedsHat(int castIndex) { return false; }

    public abstract IEnumerator Play(TitleStage stage, TitleActor[] cast);
}

/// <summary>
/// Which emote may start while another plays, and how soon it can come again
/// </summary>
public static class GhostEmoteRules
{
    /// <summary>Higher wins. Every lobby, countdown and results action is 50</summary>
    public static int Priority(GhostAction action)
    {
        switch (action)
        {
            case GhostAction.FistShake: return 100;
            case GhostAction.Cackle: return 90;
            case GhostAction.SeeingStars: return 80;
            case GhostAction.JumpScare: return 70;
            case GhostAction.DeliveryFlourish:
            case GhostAction.Materialise: return 60;
            // A wall hit cuts into a Wheee or anything lower, never a steal's, a bump's, a scare's or a delivery's
            case GhostAction.WallBump: return 55;
            case GhostAction.Wheee: return 50;
            case GhostAction.HoldYourHat: return 40;
            case GhostAction.TreasureHold: return 30;
            case GhostAction.Taunt:
            case GhostAction.BigWave:
            case GhostAction.Raspberry: return 25;
            case GhostAction.SideEye: return 20;
            case GhostAction.Scheme: return 15;
            case GhostAction.Bored: return 10;
            default: return 50;
        }
    }

    /// <summary>Seconds before the same action can start again</summary>
    public static float Cooldown(GhostAction action)
    {
        switch (action)
        {
            // Short ones, so a button held or mashed can't loop the same emote back to back
            case GhostAction.Taunt:
            case GhostAction.BigWave:
            case GhostAction.Raspberry:
            // Scraping along a wall hits it again and again: one sway, not a stutter
            case GhostAction.WallBump: return 1.5f;
            case GhostAction.Cackle:
            case GhostAction.FistShake:
            case GhostAction.DeliveryFlourish: return 2f;
            case GhostAction.SeeingStars:
            case GhostAction.Wheee: return 3f;
            // Bots and players boost every few seconds: one in every few boosts, not each one
            case GhostAction.HoldYourHat: return 20f;
            case GhostAction.JumpScare: return 5f;
            case GhostAction.TreasureHold:
            case GhostAction.SideEye: return 8f;
            case GhostAction.Scheme: return 10f;
            case GhostAction.Bored: return 12f;
            default: return 0f;
        }
    }

    /// <summary>
    /// False while cooling down. With nothing playing it is true. Otherwise only a higher priority interrupts
    /// </summary>
    public static bool CanStart(GhostAction next, GhostAction? playing, float cooldownLeft)
    {
        if (cooldownLeft > 0f)
        {
            return false;
        }

        if (!playing.HasValue)
        {
            return true;
        }

        return Priority(next) > Priority(playing.Value);
    }
}

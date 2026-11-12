using UnityEngine;

/// <summary>
/// The winner's crown: when the players vote for a rematch, whoever won the last match wears a small crown on their
/// picture for the whole of the next one (the scoreboard's row, and the Steam picture over their scooter on other
/// machines). Every machine works it out for itself from the results it shows:
/// - the results note who won (Won: the seat alone in first place; a tie crowns nobody);
/// - a rematch hands that seat the crown (Rematch); a restart from the pause menu keeps the crown as it was;
/// - going back to the menus takes it off (Clear).
/// The picture is Assets/Resources/UI/WinnerCrown.png: swap the file to change the art
/// </summary>
public static class RematchCrown
{
    /// <summary>Nobody</summary>
    public const int NONE = -1;

    const string ART = "UI/WinnerCrown";

    static int won = NONE;
    static Sprite art;

    /// <summary>The seat wearing the crown this match (NONE: nobody)</summary>
    public static int Seat { get; private set; } = NONE;

    /// <summary>Whether a seat wears the crown this match</summary>
    public static bool Wears(int seat)
    {
        return seat >= 0 && seat == Seat;
    }

    /// <summary>The results are up: this seat won (NONE for a tie)</summary>
    public static void Won(int seat)
    {
        won = seat < 0 ? NONE : seat;
    }

    /// <summary>
    /// A rematch starts: the winner of the match just finished wears the crown. With no match finished since the last
    /// one (a restart mid-match), the crown stays where it was
    /// </summary>
    public static void Rematch()
    {
        if (won == NONE)
            return;
        Seat = won;
        won = NONE;
    }

    /// <summary>Back at the menus: nobody won, nobody wears it</summary>
    public static void Clear()
    {
        Seat = NONE;
        won = NONE;
    }

    /// <summary>
    /// The seat alone in first place, from each seat's finishing place (1 = first; ties share a place) in finishing
    /// order: NONE when first place is shared or nobody played
    /// </summary>
    public static int Winner(int[] seats, int[] placements)
    {
        if (seats == null || placements == null || seats.Length == 0 || placements.Length < seats.Length)
            return NONE;
        if (seats.Length > 1 && placements[1] == placements[0])
            return NONE;
        return seats[0];
    }

    /// <summary>The crown's picture (the PNG, or the drawn crown without it)</summary>
    public static Sprite Art()
    {
        if (art == null)
            art = Resources.Load<Sprite>(ART);
        return art != null ? art : ModeIcons.Crown();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Clear();
        art = null;
    }
}

using System;

/// <summary>
/// The order the title screen skits play in: every skit once per round in a shuffled order, a new shuffle each round,
/// and never the same skit twice running (the first of a round isn't the last of the one before)
/// </summary>
public class TitlePlaylist
{
    readonly int skits;
    readonly Random random;
    int[] round;
    int position;
    int last = -1;

    public TitlePlaylist(int skits, Random random)
    {
        if (skits < 1) throw new ArgumentOutOfRangeException(nameof(skits));

        this.skits = skits;
        this.random = random ?? throw new ArgumentNullException(nameof(random));
        position = skits;   // so the first Next starts a round
    }

    /// <summary>
    /// The index of the next skit to play
    /// </summary>
    public int Next()
    {
        if (position >= skits)
            NewRound();

        last = round[position++];
        return last;
    }

    void NewRound()
    {
        round = new int[skits];

        for (int i = 0; i < skits; i++)
            round[i] = i;

        for (int i = skits - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int swap = round[i];
            round[i] = round[j];
            round[j] = swap;
        }

        // Swap the first with a later one when it would repeat the last skit of the round before
        if (skits > 1 && round[0] == last)
        {
            int j = 1 + random.Next(skits - 1);
            int swap = round[0];
            round[0] = round[j];
            round[j] = swap;
        }

        position = 0;
    }
}

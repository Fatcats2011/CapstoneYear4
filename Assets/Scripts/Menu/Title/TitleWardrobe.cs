using System;
using System.Collections.Generic;

/// <summary>
/// What one ghost wears in a title screen skit: a hat, a ghost colour and a company scooter, each an index into the
/// stage's asset arrays
/// </summary>
public struct TitleOutfit
{
    public int hat, colour, company;
}

/// <summary>
/// Deals the outfits for the title screen skits. Every ghost in a skit has a different hat and a different colour, and
/// no (hat, colour) pair is worn again until every pair has been worn (a shuffled deck), and none is carried from one
/// skit to the next. The company is random per ghost
/// </summary>
public class TitleWardrobe
{
    // Candidate deals looked at for one that leaves the deck completable before taking the first one found
    const int LOOKAHEAD_LIMIT = 500;

    // Steps the search for a full split of the rest of the deck may take before it is taken as possible (it is only a
    // preference, and the deck is tiny, so this is rarely reached)
    const int PARTITION_BUDGET = 20000;
    int budget;

    readonly int hats, colours, companies;
    readonly Random random;

    // The pairs not yet dealt this pass of the deck, as hat * colours + colour
    List<int> deck = new List<int>();
    readonly HashSet<int> previous = new HashSet<int>();

    public TitleWardrobe(int hats, int colours, int companies, Random random)
    {
        if (hats < 1) throw new ArgumentOutOfRangeException(nameof(hats));
        if (colours < 1) throw new ArgumentOutOfRangeException(nameof(colours));
        if (companies < 1) throw new ArgumentOutOfRangeException(nameof(companies));

        this.hats = hats;
        this.colours = colours;
        this.companies = companies;
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    /// <summary>
    /// The outfits for one skit: hats all differ, colours all differ, no (hat, colour) pair repeats until every pair has
    /// been dealt, and none was worn in the previous Deal. When what is left of the deck can't make a deal, it is
    /// reshuffled with every pair back in except the previous deal's
    /// </summary>
    public TitleOutfit[] Deal(int count)
    {
        if (count < 1 || count > hats || count > colours)
            throw new ArgumentOutOfRangeException(nameof(count), "A skit needs 1 to " + Math.Min(hats, colours) + " ghosts");

        List<int> picked = Find(count);

        if (picked == null)
        {
            Reshuffle();
            picked = Find(count);

            if (picked == null)
                throw new InvalidOperationException("No outfit deal of " + count + " exists");
        }

        // Spend the dealt pairs from the deck
        foreach (int pair in picked)
            deck.Remove(pair);

        previous.Clear();
        TitleOutfit[] outfits = new TitleOutfit[count];

        for (int i = 0; i < count; i++)
        {
            previous.Add(picked[i]);
            outfits[i] = new TitleOutfit { hat = picked[i] / colours, colour = picked[i] % colours, company = random.Next(companies) };
        }

        return outfits;
    }

    /// <summary>
    /// All the pairs back in, shuffled, except the previous deal's
    /// </summary>
    void Reshuffle()
    {
        deck = new List<int>();

        for (int pair = 0; pair < hats * colours; pair++)
            if (!previous.Contains(pair))
                deck.Add(pair);

        Shuffle(deck);
    }

    void Shuffle(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            int swap = list[i];
            list[i] = list[j];
            list[j] = swap;
        }
    }

    /// <summary>
    /// A valid deal from the deck (null if it has none), preferring one that leaves the rest of the deck dealable too
    /// </summary>
    List<int> Find(int count)
    {
        if (deck.Count == 0)
            return null;

        List<int> first = null;
        List<int> accepted = null;
        int looked = 0;

        Search(count, 0, new List<int>(), new bool[hats], new bool[colours], picked =>
        {
            if (first == null)
                first = new List<int>(picked);

            looked++;

            if (Completable(picked, count))
            {
                accepted = new List<int>(picked);
                return true;
            }

            return looked >= LOOKAHEAD_LIMIT;
        });

        return accepted ?? first;
    }

    /// <summary>
    /// Walks the deals in deck order (a deal is count pairs with different hats and colours, none from the previous
    /// deal), handing each to done until it returns true. Returns whether it did
    /// </summary>
    bool Search(int count, int start, List<int> picked, bool[] hatUsed, bool[] colourUsed, Func<List<int>, bool> done)
    {
        if (picked.Count == count)
            return done(picked);

        for (int i = start; i < deck.Count; i++)
        {
            int pair = deck[i];
            int hat = pair / colours, colour = pair % colours;

            if (hatUsed[hat] || colourUsed[colour] || previous.Contains(pair))
                continue;

            hatUsed[hat] = colourUsed[colour] = true;
            picked.Add(pair);
            bool finished = Search(count, i + 1, picked, hatUsed, colourUsed, done);
            picked.RemoveAt(picked.Count - 1);
            hatUsed[hat] = colourUsed[colour] = false;

            if (finished)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Whether the deck, once this deal is taken out, can still be dealt out in full: when what is left divides into
    /// deals of this size, every pair must find a deal (otherwise some pair would wait for a reshuffle and be worn out
    /// of turn); when it doesn't divide, any remainder will do
    /// </summary>
    bool Completable(List<int> deal, int count)
    {
        List<int> rest = new List<int>(deck.Count);

        foreach (int pair in deck)
            if (!deal.Contains(pair))
                rest.Add(pair);

        if (rest.Count == 0 || rest.Count % count != 0)
            return true;

        budget = PARTITION_BUDGET;
        return Partition(rest, count, new HashSet<int>(deal));
    }

    /// <summary>
    /// Whether these pairs split into valid deals of this size, the first deal avoiding the blocked pairs
    /// </summary>
    bool Partition(List<int> rest, int count, HashSet<int> blocked)
    {
        if (rest.Count == 0 || budget <= 0)
            return true;

        // The first pair has to be in some deal, so try every deal that holds it
        int anchor = rest[0];

        if (blocked.Contains(anchor))
            return false;

        List<int> group = new List<int> { anchor };
        bool[] hatUsed = new bool[hats], colourUsed = new bool[colours];
        hatUsed[anchor / colours] = colourUsed[anchor % colours] = true;
        return Group(rest, count, 1, group, hatUsed, colourUsed, blocked);
    }

    bool Group(List<int> rest, int count, int start, List<int> group, bool[] hatUsed, bool[] colourUsed, HashSet<int> blocked)
    {
        if (group.Count == count)
        {
            List<int> left = new List<int>(rest.Count);

            foreach (int pair in rest)
                if (!group.Contains(pair))
                    left.Add(pair);

            return Partition(left, count, new HashSet<int>(group));
        }

        for (int i = start; i < rest.Count; i++)
        {
            budget--;
            int pair = rest[i];
            int hat = pair / colours, colour = pair % colours;

            if (hatUsed[hat] || colourUsed[colour] || blocked.Contains(pair))
                continue;

            hatUsed[hat] = colourUsed[colour] = true;
            group.Add(pair);
            bool ok = Group(rest, count, i + 1, group, hatUsed, colourUsed, blocked);
            group.RemoveAt(group.Count - 1);
            hatUsed[hat] = colourUsed[colour] = false;

            if (ok)
                return true;
        }

        return false;
    }
}

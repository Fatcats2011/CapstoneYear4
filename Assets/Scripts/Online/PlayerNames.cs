using Unity.Collections;

/// <summary>
/// The name each player shows online: their Steam name, cleaned to fit what's synced, or "Player 3" for their seat
/// when they have none. See docs/online.md
/// </summary>
public static class PlayerNames
{
    /// <summary>The name for a seat with no name of its own: seat 0 is "Player 1"</summary>
    public static string Fallback(int seat)
    {
        return "Player " + (seat + 1);
    }

    /// <summary>
    /// Drops control characters, trims a name, then cuts it to what a synced string holds (in UTF-8 bytes), never between the two halves of a
    /// character that is a surrogate pair. Null gives ""
    /// </summary>
    public static string Clean(string raw)
    {
        if (raw == null)
            return "";

        string trimmed = StripControls(raw).Trim();
        int bytes = 0;
        int end = 0;
        while (end < trimmed.Length)
        {
            // UTF-8 size by code unit: 1, 2 or 3 bytes, 4 for a surrogate pair, 3 for a lone surrogate (it encodes as U+FFFD)
            char c = trimmed[end];
            bool pair = char.IsHighSurrogate(c) && end + 1 < trimmed.Length && char.IsLowSurrogate(trimmed[end + 1]);
            int length = pair ? 2 : 1;
            int size = pair ? 4 : c < 0x80 ? 1 : c < 0x800 ? 2 : 3;
            if (bytes + size > FixedString32Bytes.UTF8MaxLengthInBytes)
                break;

            bytes += size;
            end += length;
        }
        return end == trimmed.Length ? trimmed : trimmed.Substring(0, end).Trim();
    }

    private static string StripControls(string raw)
    {
        System.Text.StringBuilder builder = new System.Text.StringBuilder(raw.Length);
        foreach (char c in raw)
        {
            if (!char.IsControl(c))
                builder.Append(c);
        }
        return builder.ToString();
    }

    /// <summary>
    /// The name to show for a player: the synced one cleaned again (another machine's game may not have), or the seat's
    /// fallback when nothing is left
    /// </summary>
    public static string Shown(string synced, int seat)
    {
        string name = Clean(synced);
        return name.Length == 0 ? Fallback(seat) : name;
    }
}

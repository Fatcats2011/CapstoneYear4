using System;
using Unity.Netcode;

/// <summary>
/// How each machine's connection looks: the bars a round trip shows, and when the host measures them. See
/// docs/online.md (Connection info)
/// </summary>
public static class PingRules
{
    /// <summary>How often the host measures and shares the round trips, in seconds</summary>
    public const float EVERY = 2f;

    /// <summary>Three bars under 80 ms, two to 150 ms, one above; none when unknown</summary>
    public static int Bars(int ms)
    {
        if (ms < 0)
            return 0;
        return ms < 80 ? 3 : ms <= 150 ? 2 : 1;
    }

    public const string LIT = "<alpha=#FF>";
    public const string DIM = "<alpha=#44>";

    /// <summary>
    /// Three bars, "III", the unlit ones dimmed (rich text; the game's font has no bar glyphs), and the milliseconds:
    /// "II" + dim "I" + " 96MS". Nothing when unknown
    /// </summary>
    public static string Line(int ms)
    {
        int bars = Bars(ms);
        if (bars == 0)
            return "";
        return LIT + new string('I', bars) + (bars < 3 ? DIM + new string('I', 3 - bars) : "") + LIT + " " + ms + "MS";
    }

    /// <summary>Whether the host measures again now</summary>
    public static bool Due(bool host, float sinceLast)
    {
        return host && sinceLast >= EVERY;
    }
}

/// <summary>
/// Each seat's (0-3) round trip to the host, in milliseconds, as the host shares it every PingRules.EVERY seconds; the
/// host's own seats are 0. 65535 is unknown. Clients check it on arrival (NetChecks.Sane)
/// </summary>
public struct PingBoard : INetworkSerializable
{
    public const ushort MAX_MS = 9999;
    const ushort UNKNOWN = ushort.MaxValue;

    public ushort Ms0, Ms1, Ms2, Ms3;
    bool made; // a fresh board's seats are unknown

    /// <summary>A board with every seat unknown</summary>
    public static PingBoard Unknown => new PingBoard { Ms0 = UNKNOWN, Ms1 = UNKNOWN, Ms2 = UNKNOWN, Ms3 = UNKNOWN, made = true };

    public void Set(int seat, long ms)
    {
        if (!made)
            this = Unknown;
        ushort value = (ushort)Math.Min(Math.Max(ms, 0), MAX_MS);
        switch (seat)
        {
            case 0: Ms0 = value; break;
            case 1: Ms1 = value; break;
            case 2: Ms2 = value; break;
            case 3: Ms3 = value; break;
        }
    }

    /// <summary>A seat's round trip, or -1 when unknown</summary>
    public int Of(int seat)
    {
        if (!made)
            return -1;
        ushort value = seat == 0 ? Ms0 : seat == 1 ? Ms1 : seat == 2 ? Ms2 : seat == 3 ? Ms3 : UNKNOWN;
        return value == UNKNOWN ? -1 : value;
    }

    /// <summary>Whether every seat is a round trip a board can carry, or unknown</summary>
    internal bool InRange
    {
        get
        {
            foreach (ushort ms in new[] { Ms0, Ms1, Ms2, Ms3 })
            {
                if (ms > MAX_MS && ms != UNKNOWN)
                    return false;
            }
            return true;
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Ms0);
        serializer.SerializeValue(ref Ms1);
        serializer.SerializeValue(ref Ms2);
        serializer.SerializeValue(ref Ms3);
        made = true;
    }
}

/// <summary>The round trips on their way from the network to the pause menu, and the last ones</summary>
public static class PingSync
{
    /// <summary>The host's board arrived (or the host measured its own)</summary>
    public static event Action<PingBoard> Arrived;

    public static PingBoard Last { get; private set; } = PingBoard.Unknown;

    /// <summary>Each seat's player's name, as this machine knows it ("" for an empty seat)</summary>
    public static readonly string[] Names = { "", "", "", "" };

    public static void Arrive(PingBoard board)
    {
        Last = board;
        Arrived?.Invoke(board);
    }

    /// <summary>Forgets the listeners and the last board (tests, a session's end)</summary>
    public static void Reset()
    {
        Arrived = null;
        Last = PingBoard.Unknown;
        for (int seat = 0; seat < Names.Length; seat++)
            Names[seat] = "";
    }
}

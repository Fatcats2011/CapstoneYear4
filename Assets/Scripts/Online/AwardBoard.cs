using System;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// The awards as the host shares them with every machine at the results: each seat's (0-3) award and its count. A seat
/// nobody plays has None. Clients check it on arrival (NetChecks.Sane)
/// </summary>
public struct AwardBoard : INetworkSerializable, IEquatable<AwardBoard>
{
    /// <summary>The highest count a card shows</summary>
    public const ushort MAX_COUNT = 9999;

    public Award Award0, Award1, Award2, Award3;
    public ushort Count0, Count1, Count2, Count3;

    /// <summary>The awards AwardRules picks for these seats, with their counts</summary>
    public static AwardBoard From(MatchStats stats, IEnumerable<int> seats)
    {
        AwardBoard board = new AwardBoard();
        foreach (KeyValuePair<int, Award> seatAward in AwardRules.Pick(stats, seats))
        {
            int count = Math.Min(stats.Count(seatAward.Key, seatAward.Value), MAX_COUNT);
            board.Set(seatAward.Key, seatAward.Value, (ushort)count);
        }
        return board;
    }

    public Award AwardOf(int seat)
    {
        switch (seat)
        {
            case 0: return Award0;
            case 1: return Award1;
            case 2: return Award2;
            case 3: return Award3;
            default: return Award.None;
        }
    }

    public int CountOf(int seat)
    {
        switch (seat)
        {
            case 0: return Count0;
            case 1: return Count1;
            case 2: return Count2;
            case 3: return Count3;
            default: return 0;
        }
    }

    void Set(int seat, Award award, ushort count)
    {
        switch (seat)
        {
            case 0: Award0 = award; Count0 = count; break;
            case 1: Award1 = award; Count1 = count; break;
            case 2: Award2 = award; Count2 = count; break;
            case 3: Award3 = award; Count3 = count; break;
        }
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        Award0 = Serialize(serializer, Award0);
        Award1 = Serialize(serializer, Award1);
        Award2 = Serialize(serializer, Award2);
        Award3 = Serialize(serializer, Award3);
        serializer.SerializeValue(ref Count0);
        serializer.SerializeValue(ref Count1);
        serializer.SerializeValue(ref Count2);
        serializer.SerializeValue(ref Count3);
    }

    static Award Serialize<T>(BufferSerializer<T> serializer, Award award) where T : IReaderWriter
    {
        byte value = (byte)award;
        serializer.SerializeValue(ref value);
        return (Award)value;
    }

    public bool Equals(AwardBoard other)
    {
        return Award0 == other.Award0 && Award1 == other.Award1 && Award2 == other.Award2 && Award3 == other.Award3
            && Count0 == other.Count0 && Count1 == other.Count1 && Count2 == other.Count2 && Count3 == other.Count3;
    }

    public override bool Equals(object obj) { return obj is AwardBoard other && Equals(other); }

    public override int GetHashCode()
    {
        return ((int)Award0 | (int)Award1 << 8 | (int)Award2 << 16 | (int)Award3 << 24) ^ (Count0 + Count1 * 31 + Count2 * 961 + Count3 * 29791);
    }
}

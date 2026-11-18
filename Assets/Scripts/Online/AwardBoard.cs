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

    /// <summary>The most money a card shows for one source: more is clamped, never wrapped</summary>
    public const ushort MAX_MONEY = ushort.MaxValue;

    // Money by source (MoneySource order), four per seat: Money{seat}{source}
    public ushort Money00, Money01, Money02, Money03;
    public ushort Money10, Money11, Money12, Money13;
    public ushort Money20, Money21, Money22, Money23;
    public ushort Money30, Money31, Money32, Money33;

    /// <summary>The awards AwardRules picks for these seats, with their counts, and the money each seat earned if it is counted</summary>
    public static AwardBoard From(MatchStats stats, IEnumerable<int> seats, MoneyTally money = null)
    {
        AwardBoard board = new AwardBoard();
        foreach (KeyValuePair<int, Award> seatAward in AwardRules.Pick(stats, seats))
        {
            int count = Math.Min(stats.Count(seatAward.Key, seatAward.Value), MAX_COUNT);
            board.Set(seatAward.Key, seatAward.Value, (ushort)count);
        }
        if (money != null)
            for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
                for (int source = 0; source < MoneyRules.SOURCES; source++)
                    board.SetMoney(seat, (MoneySource)source, money.Of(seat, (MoneySource)source));
        return board;
    }

    /// <summary>What a seat earned from a source; 0 for a seat that doesn't exist</summary>
    public int MoneyOf(int seat, MoneySource source)
    {
        int index = MoneyIndex(seat, source);
        return index < 0 ? 0 : GetMoney(index);
    }

    /// <summary>Sets what a seat earned from a source, clamped to 0..MAX_MONEY</summary>
    public void SetMoney(int seat, MoneySource source, int amount)
    {
        int index = MoneyIndex(seat, source);
        if (index >= 0)
            PutMoney(index, (ushort)Math.Min(Math.Max(amount, 0), MAX_MONEY));
    }

    static int MoneyIndex(int seat, MoneySource source)
    {
        if (seat < 0 || seat >= Constants.MAX_PLAYERS || (int)source >= MoneyRules.SOURCES)
            return -1;
        return seat * MoneyRules.SOURCES + (int)source;
    }

    ushort GetMoney(int index)
    {
        switch (index)
        {
            case 0: return Money00;
            case 1: return Money01;
            case 2: return Money02;
            case 3: return Money03;
            case 4: return Money10;
            case 5: return Money11;
            case 6: return Money12;
            case 7: return Money13;
            case 8: return Money20;
            case 9: return Money21;
            case 10: return Money22;
            case 11: return Money23;
            case 12: return Money30;
            case 13: return Money31;
            case 14: return Money32;
            default: return Money33;
        }
    }

    void PutMoney(int index, ushort value)
    {
        switch (index)
        {
            case 0: Money00 = value; break;
            case 1: Money01 = value; break;
            case 2: Money02 = value; break;
            case 3: Money03 = value; break;
            case 4: Money10 = value; break;
            case 5: Money11 = value; break;
            case 6: Money12 = value; break;
            case 7: Money13 = value; break;
            case 8: Money20 = value; break;
            case 9: Money21 = value; break;
            case 10: Money22 = value; break;
            case 11: Money23 = value; break;
            case 12: Money30 = value; break;
            case 13: Money31 = value; break;
            case 14: Money32 = value; break;
            default: Money33 = value; break;
        }
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
        for (int index = 0; index < Constants.MAX_PLAYERS * MoneyRules.SOURCES; index++)
        {
            ushort money = GetMoney(index);
            serializer.SerializeValue(ref money);
            PutMoney(index, money);
        }
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
            && Count0 == other.Count0 && Count1 == other.Count1 && Count2 == other.Count2 && Count3 == other.Count3
            && SameMoney(other);
    }

    bool SameMoney(AwardBoard other)
    {
        for (int index = 0; index < Constants.MAX_PLAYERS * MoneyRules.SOURCES; index++)
            if (GetMoney(index) != other.GetMoney(index))
                return false;
        return true;
    }

    public override bool Equals(object obj) { return obj is AwardBoard other && Equals(other); }

    public override int GetHashCode()
    {
        int hash = ((int)Award0 | (int)Award1 << 8 | (int)Award2 << 16 | (int)Award3 << 24) ^ (Count0 + Count1 * 31 + Count2 * 961 + Count3 * 29791);
        unchecked
        {
            for (int index = 0; index < Constants.MAX_PLAYERS * MoneyRules.SOURCES; index++)
                hash = hash * 31 + GetMoney(index);
        }
        return hash;
    }
}

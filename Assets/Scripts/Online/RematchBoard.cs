using System;
using Unity.Netcode;

/// <summary>
/// The results screen's rematch vote as the host shows it to every machine: the seats still in, who voted Rematch,
/// the seconds left, and the outcome once decided. Bit n of a mask is seat n (0-3). Clients check it on arrival
/// (NetChecks.Sane)
/// </summary>
public struct RematchBoard : INetworkSerializable, IEquatable<RematchBoard>
{
    public byte InMask;
    public byte VotedMask;
    public byte SecondsLeft;
    public RematchOutcome Outcome;

    /// <summary>The board of a vote, now</summary>
    public static RematchBoard From(RematchVote vote, double now)
    {
        RematchBoard board = new RematchBoard
        {
            SecondsLeft = (byte)vote.SecondsLeft(now),
            Outcome = vote.Decide(now),
        };
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            if (vote.IsIn(seat))
                board.InMask |= (byte)(1 << seat);
            if (vote.HasVoted(seat))
                board.VotedMask |= (byte)(1 << seat);
        }
        return board;
    }

    public bool IsIn(int seat) { return seat >= 0 && seat < 8 && (InMask & (1 << seat)) != 0; }

    public bool HasVoted(int seat) { return seat >= 0 && seat < 8 && (VotedMask & (1 << seat)) != 0; }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref InMask);
        serializer.SerializeValue(ref VotedMask);
        serializer.SerializeValue(ref SecondsLeft);
        byte outcome = (byte)Outcome;
        serializer.SerializeValue(ref outcome);
        Outcome = (RematchOutcome)outcome;
    }

    public bool Equals(RematchBoard other)
    {
        return InMask == other.InMask && VotedMask == other.VotedMask && SecondsLeft == other.SecondsLeft && Outcome == other.Outcome;
    }

    public override bool Equals(object obj) { return obj is RematchBoard other && Equals(other); }

    public override int GetHashCode() { return InMask | VotedMask << 8 | SecondsLeft << 16 | (int)Outcome << 24; }
}

using System;
using Unity.Netcode;

/// <summary>
/// The cutscene skip vote as the host shows it to every machine: the seats in it and who voted. Bit n of a mask is seat
/// n (0-3). Clients check it on arrival (NetChecks.Sane)
/// </summary>
public struct SkipBoard : INetworkSerializable, IEquatable<SkipBoard>
{
    public byte InMask;
    public byte VotedMask;

    public static SkipBoard From(SkipVote vote)
    {
        SkipBoard board = new SkipBoard();
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

    public int Voted { get { return Count(VotedMask); } }

    public int Total { get { return Count(InMask); } }

    static int Count(byte mask)
    {
        int count = 0;
        for (int bits = mask; bits != 0; bits >>= 1)
            count += bits & 1;
        return count;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref InMask);
        serializer.SerializeValue(ref VotedMask);
    }

    public bool Equals(SkipBoard other) { return InMask == other.InMask && VotedMask == other.VotedMask; }

    public override bool Equals(object obj) { return obj is SkipBoard other && Equals(other); }

    public override int GetHashCode() { return InMask | VotedMask << 8; }
}

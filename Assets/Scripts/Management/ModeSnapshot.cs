using UnityEngine;

/// <summary>
/// What other machines need to show a timed mode (ModeMatch), as the host shares it (OnlineMatch): the seats in one int,
/// where the crown lies, and the potato's fuse (seconds left and its length; each machine counts down on its own clock)
/// </summary>
public readonly struct ModeSnapshot
{
    public readonly GameMode Mode;
    public readonly int Players, CrownSeat, PotatoSeat, FirstHunter, Infected, Out;
    public readonly Vector3 CrownSpot;
    public readonly float FuseLeft, FuseLength;

    public ModeSnapshot(GameMode mode, int players, int crownSeat, int potatoSeat, int firstHunter, int infected, int out_,
        Vector3 crownSpot, float fuseLeft, float fuseLength)
    {
        Mode = mode;
        Players = players & 0xF;
        CrownSeat = Seat(crownSeat);
        PotatoSeat = Seat(potatoSeat);
        FirstHunter = Seat(firstHunter);
        Infected = infected & 0xF;
        Out = out_ & 0xF;
        CrownSpot = crownSpot;
        FuseLeft = Sane(fuseLeft);
        FuseLength = Sane(fuseLength);
    }

    // A seat 0-3, or NONE for anything else (another machine could send anything)
    static int Seat(int seat) => seat >= 0 && seat < Constants.MAX_PLAYERS ? seat : ModeMatch.NONE;
    static float Sane(float seconds) => float.IsNaN(seconds) || seconds < 0f ? 0f : Mathf.Min(seconds, 600f);

    /// <summary>Mode, seats and masks in one int: 3 bits each for the mode and three seats (7 = none), 4 bits per mask</summary>
    public int PackSeats()
    {
        return (int)Mode | Bits(CrownSeat) << 3 | Bits(PotatoSeat) << 6 | Bits(FirstHunter) << 9 | Players << 12 | Infected << 16 | Out << 20;
    }

    static int Bits(int seat) => seat == ModeMatch.NONE ? 7 : seat;
    static int FromBits(int bits) => bits == 7 ? ModeMatch.NONE : bits;

    public static ModeSnapshot Unpack(int seats, Vector3 crownSpot, float fuseLeft, float fuseLength)
    {
        int mode = seats & 7;
        return new ModeSnapshot(mode > (int)GameMode.HotPotato ? GameMode.Standard : (GameMode)mode, seats >> 12 & 0xF,
            FromBits(seats >> 3 & 7), FromBits(seats >> 6 & 7), FromBits(seats >> 9 & 7), seats >> 16 & 0xF, seats >> 20 & 0xF,
            IsFinite(crownSpot) ? crownSpot : Vector3.zero, fuseLeft, fuseLength);
    }

    static bool IsFinite(Vector3 v) => !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
}

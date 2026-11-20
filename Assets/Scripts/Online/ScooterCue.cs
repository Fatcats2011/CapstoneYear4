using Unity.Netcode;

/// <summary>
/// What a player's one-shot is (ScooterCue). New kinds go on the end: an older game refuses a kind it doesn't know
/// (NetChecks.Defined, where a cue arrives), so the numbers already sent must not move.
/// - Emote0..Emote3: the player's d-pad emote, by its index (EmoteHandler: up, right, down, left).
/// - Dizzy: the player was bumped hard and sees stars. Only the host sends it (it judges bumps): a client can't report it
/// </summary>
public enum CueKind : byte { Boost, DriftBoost, HornReady, Phase, PhaseEnd, Death, Rise, Emote0, Emote1, Emote2, Emote3, Dizzy }

/// <summary>
/// A player's one-shot as it travels online: something their scooter does in a moment (a boost, a drift boost, a full
/// horn, phasing through a building and out, falling in the water, rising from a grave, a d-pad emote), or the host's
/// word that they were bumped hard (Dizzy). The machine that drives the
/// scooter sends it, the host passes it on, and every other machine plays it on that player's scooter (OnlineCues). Order
/// sounds aren't cues: every machine replays the host's order changes (OnlineOrders). Players go by their seat
/// </summary>
public struct ScooterCue : INetworkSerializable
{
    public CueKind Kind;
    /// <summary>A rise's respawn point (its index: RespawnManager.PointAt); -1 for every other kind</summary>
    public int Point;

    /// <summary>A one-shot that names no place: any kind but Rise</summary>
    public static ScooterCue Of(CueKind kind)
    {
        return new ScooterCue { Kind = kind, Point = -1 };
    }

    /// <summary>The player's d-pad emote with that index (0 up, 1 right, 2 down, 3 left: PlayerEmoteRules.PAD_EMOTES)</summary>
    public static ScooterCue Emote(int index)
    {
        return Of((CueKind)((int)CueKind.Emote0 + index));
    }

    /// <summary>A d-pad emote's index (0 up, 1 right, 2 down, 3 left), or -1 for any other kind</summary>
    public static int EmoteIndex(CueKind kind)
    {
        return kind >= CueKind.Emote0 && kind <= CueKind.Emote3 ? kind - CueKind.Emote0 : -1;
    }

    /// <summary>A kind only the host may send: a client's report of it is refused</summary>
    public static bool HostOnly(CueKind kind)
    {
        return kind == CueKind.Dizzy;
    }

    /// <summary>The player rises from their grave at that respawn point</summary>
    public static ScooterCue Rise(int point)
    {
        return new ScooterCue { Kind = CueKind.Rise, Point = point };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Kind);
        serializer.SerializeValue(ref Point);
    }

    public override string ToString()
    {
        return Kind == CueKind.Rise ? "Rise at " + Point : Kind.ToString();
    }
}

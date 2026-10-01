using Unity.Netcode;

/// <summary>
/// What a player's one-shot is (ScooterCue)
/// </summary>
public enum CueKind : byte { Boost, DriftBoost, HornReady, Phase, PhaseEnd, Death, Rise }

/// <summary>
/// A player's one-shot as it travels online: something their scooter does in a moment (a boost, a drift boost, a full
/// horn, phasing through a building and out, falling in the water, rising from a grave). The machine that drives the
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

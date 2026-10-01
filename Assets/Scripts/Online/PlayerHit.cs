using Unity.Netcode;

/// <summary>
/// What a hit between two players is (PlayerHit)
/// </summary>
public enum HitKind : byte { Steal, Clash }

/// <summary>
/// A steal or clash the host decided, as it travels online (StealRules). Every machine hears it, and bounces only its own
/// player: a robbed player away from the thief, both players in a clash (OnlineSteals). What a steal does to orders
/// travels as order changes (OrderChange.Steal, then the victim's Drop). Players go by their seat
/// </summary>
public struct PlayerHit : INetworkSerializable
{
    public HitKind Kind;
    /// <summary>The player who boosted in: a steal's thief</summary>
    public int Attacker;
    /// <summary>The player they hit</summary>
    public int Victim;

    /// <summary>The thief takes the victim's best order (the order changes carry that), and the victim bounces away</summary>
    public static PlayerHit Steal(int thief, int victim)
    {
        return new PlayerHit { Kind = HitKind.Steal, Attacker = thief, Victim = victim };
    }

    /// <summary>Both were boosting: each bounces off the other</summary>
    public static PlayerHit Clash(int attacker, int victim)
    {
        return new PlayerHit { Kind = HitKind.Clash, Attacker = attacker, Victim = victim };
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Kind);
        serializer.SerializeValue(ref Attacker);
        serializer.SerializeValue(ref Victim);
    }

    public override string ToString()
    {
        return Kind == HitKind.Steal ? "Steal " + Attacker + " from " + Victim : "Clash " + Attacker + " with " + Victim;
    }
}

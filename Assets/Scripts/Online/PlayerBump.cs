using Unity.Netcode;

/// <summary>
/// A bump the host let through, as it travels online (BumpRules): the bumped player (the victim) is pushed away from the
/// bumper, as hard as Push says, on their own machine only. Players go by their seat
/// </summary>
public struct PlayerBump : INetworkSerializable
{
    /// <summary>The player who drove into the other</summary>
    public int Bumper;
    /// <summary>The player pushed</summary>
    public int Victim;
    /// <summary>How hard (an impulse, BumpRules.Push)</summary>
    public float Push;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Bumper);
        serializer.SerializeValue(ref Victim);
        serializer.SerializeValue(ref Push);
    }

    public override string ToString()
    {
        return "Bump " + Bumper + " into " + Victim + " (" + Push + ")";
    }
}

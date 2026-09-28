using System.Collections.Generic;

/// <summary>
/// Host, online: one scene load across the session's machines. The host shows the scene once every machine that was in
/// the session when the load began has it loaded, or has left (OnlineSceneFlow)
/// </summary>
public class LoadRound
{
    readonly HashSet<ulong> waitingFor;

    /// <summary>The scene being loaded</summary>
    public MatchScene Scene { get; private set; }

    /// <param name="machines">The machines in the session now (Netcode client ids, the host's included)</param>
    public LoadRound(MatchScene scene, IEnumerable<ulong> machines)
    {
        Scene = scene;
        waitingFor = new HashSet<ulong>(machines);
    }

    /// <summary>A machine has the scene loaded (one outside the round, or heard twice, changes nothing)</summary>
    public void Loaded(ulong machine)
    {
        waitingFor.Remove(machine);
    }

    /// <summary>A machine left the session: the round stops waiting for it</summary>
    public void Left(ulong machine)
    {
        waitingFor.Remove(machine);
    }

    /// <summary>Whether every machine has the scene loaded, or has left</summary>
    public bool Ready { get { return waitingFor.Count == 0; } }
}

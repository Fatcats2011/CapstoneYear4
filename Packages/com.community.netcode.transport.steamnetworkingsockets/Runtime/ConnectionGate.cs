// DoA (CHANGES-DOA.md): the decision on each incoming connection, kept free of Steam so it can be tested
using System;
using System.Collections.Generic;

namespace Netcode.Transports
{
    /// <summary>What the transport does with an incoming connection</summary>
    public enum PeerDecision { Accept, Wait, Refuse }

    /// <summary>
    /// Decides each incoming connection: a peer the game accepts (a member of the host's lobby) is accepted; one it
    /// doesn't yet waits up to GRACE seconds, since the host's copy of the lobby can lag the friend's join, then is
    /// refused; a Steam ID that's already connected can't open a second connection (and take a second seat); a Steam ID
    /// the host kicked can't connect again this session (Steam lobbies have no kick, so it's still a member)
    /// </summary>
    public class ConnectionGate
    {
        /// <summary>How long a peer the game doesn't accept yet may wait, in seconds</summary>
        public const float GRACE = 3f;

        readonly Dictionary<ulong, float> waitingSince = new Dictionary<ulong, float>();
        readonly HashSet<ulong> banned = new HashSet<ulong>();

        /// <summary>Refuses a Steam ID from now on (the host kicked it), until ClearBans</summary>
        public void Ban(ulong steamId)
        {
            banned.Add(steamId);
            waitingSince.Remove(steamId);
        }

        /// <summary>Forgets every ban: a new session starts clean</summary>
        public void ClearBans()
        {
            banned.Clear();
        }

        public PeerDecision Decide(ulong steamId, float now, Func<ulong, bool> accepts, bool alreadyConnected)
        {
            if (alreadyConnected || banned.Contains(steamId))
            {
                waitingSince.Remove(steamId);
                return PeerDecision.Refuse;
            }

            if (accepts == null || accepts(steamId))
            {
                waitingSince.Remove(steamId);
                return PeerDecision.Accept;
            }

            if (!waitingSince.TryGetValue(steamId, out float since))
            {
                waitingSince[steamId] = now;
                return PeerDecision.Wait;
            }

            if (now - since >= GRACE)
            {
                waitingSince.Remove(steamId);
                return PeerDecision.Refuse;
            }
            return PeerDecision.Wait;
        }
    }
}

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
    /// refused; a Steam ID that's already connected can't open a second connection (and take a second seat)
    /// </summary>
    public class ConnectionGate
    {
        /// <summary>How long a peer the game doesn't accept yet may wait, in seconds</summary>
        public const float GRACE = 3f;

        readonly Dictionary<ulong, float> waitingSince = new Dictionary<ulong, float>();

        public PeerDecision Decide(ulong steamId, float now, Func<ulong, bool> accepts, bool alreadyConnected)
        {
            if (alreadyConnected)
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

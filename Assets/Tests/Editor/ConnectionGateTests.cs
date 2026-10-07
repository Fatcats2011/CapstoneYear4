using NUnit.Framework;
using Netcode.Transports;

namespace DoA.Tests
{
    /// <summary>
    /// The Steam transport's decision on each incoming connection (ConnectionGate, in the patched transport): a lobby member
    /// is accepted; someone not in the lobby yet waits a moment (the host's copy of the lobby can lag the join) and is
    /// refused only after the grace time; a Steam ID that's already connected can't open a second connection (and take a
    /// second seat). See docs/online-safety.md
    /// </summary>
    public class ConnectionGateTests
    {
        static bool Members(ulong id)
        {
            return id == 7;
        }

        static bool Nobody(ulong id)
        {
            return false;
        }

        [Test]
        public void AMember_IsAccepted()
        {
            Assert.AreEqual(PeerDecision.Accept, new ConnectionGate().Decide(7, 0f, Members, false));
        }

        [Test]
        public void NoGate_AcceptsEveryone()
        {
            Assert.AreEqual(PeerDecision.Accept, new ConnectionGate().Decide(9, 0f, null, false));
        }

        [Test]
        public void SomeoneNotInTheLobbyYet_Waits_ThenIsAcceptedOnceTheyAre()
        {
            ConnectionGate gate = new ConnectionGate();
            Assert.AreEqual(PeerDecision.Wait, gate.Decide(7, 0f, Nobody, false), "the host's lobby copy hasn't caught up");
            Assert.AreEqual(PeerDecision.Wait, gate.Decide(7, 1f, Nobody, false));

            Assert.AreEqual(PeerDecision.Accept, gate.Decide(7, 1.5f, Members, false), "now a member");
        }

        [Test]
        public void AStranger_IsRefusedAfterTheGraceTime()
        {
            ConnectionGate gate = new ConnectionGate();
            Assert.AreEqual(PeerDecision.Wait, gate.Decide(9, 10f, Members, false));
            Assert.AreEqual(PeerDecision.Wait, gate.Decide(9, 10f + ConnectionGate.GRACE - 0.1f, Members, false));

            Assert.AreEqual(PeerDecision.Refuse, gate.Decide(9, 10f + ConnectionGate.GRACE, Members, false));
        }

        [Test]
        public void AStrangerTryingAgain_WaitsAfresh()
        {
            ConnectionGate gate = new ConnectionGate();
            gate.Decide(9, 0f, Members, false);
            gate.Decide(9, ConnectionGate.GRACE, Members, false); // refused

            Assert.AreEqual(PeerDecision.Wait, gate.Decide(9, 100f, Members, false), "a new attempt gets its own grace");
        }

        [Test]
        public void ASecondConnectionFromAConnectedSteamId_IsRefused()
        {
            Assert.AreEqual(PeerDecision.Refuse, new ConnectionGate().Decide(7, 0f, Members, true));
        }

        [Test]
        public void Ban_RefusesThatSteamId_UntilCleared()
        {
            ConnectionGate gate = new ConnectionGate();
            gate.Ban(7); // kicked for flooding: still in the Steam lobby, which has no kick

            Assert.AreEqual(PeerDecision.Refuse, gate.Decide(7, 0f, Members, false), "the kicked member can't reconnect");
            Assert.AreEqual(PeerDecision.Accept, gate.Decide(8, 0f, id => true, false), "others still can");

            gate.ClearBans(); // a new session
            Assert.AreEqual(PeerDecision.Accept, gate.Decide(7, 0f, Members, false));
        }
    }
}

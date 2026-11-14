using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace DoA.Tests
{
    /// <summary>
    /// A steal or clash the host decided, as it travels online: every kind arrives as it was sent
    /// </summary>
    public class PlayerHitTests
    {
        static IEnumerable<PlayerHit> EveryKind()
        {
            yield return PlayerHit.Steal(2, 3);
            yield return PlayerHit.Clash(0, 1);
            yield return PlayerHit.Steal(2, 3, BumpOutcome.SlotsFull, 0);
            yield return PlayerHit.Steal(1, 0, BumpOutcome.Stolen, 14);
            yield return new PlayerHit { Kind = HitKind.Steal, Attacker = 0, Victim = 1, Outcome = 200 }; // a newer host's outcome
        }

        [TestCaseSource(nameof(EveryKind))]
        public void AHit_ArrivesAsItWasSent(PlayerHit sent)
        {
            using (FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(sent);
                using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out PlayerHit received);
                    Assert.AreEqual(sent, received);
                }
            }
        }

        [Test]
        public void ASteal_CarriesWhatItDidAndWhatItPaid()
        {
            PlayerHit hit = PlayerHit.Steal(1, 0, BumpOutcome.Stolen, 14);

            Assert.AreEqual((byte)BumpOutcome.Stolen, hit.Outcome);
            Assert.AreEqual(14, hit.Paid);
            Assert.AreNotEqual(hit, PlayerHit.Steal(1, 0, BumpOutcome.SlotsFull, 0));
        }

        [Test]
        public void ASteal_PaidMoreThanAByteHolds_CarriesTheMostItCan()
        {
            Assert.AreEqual(255, PlayerHit.Steal(0, 1, BumpOutcome.Stolen, 999).Paid);
            Assert.AreEqual(0, PlayerHit.Steal(0, 1, BumpOutcome.Stolen, -5).Paid);
        }
    }
}

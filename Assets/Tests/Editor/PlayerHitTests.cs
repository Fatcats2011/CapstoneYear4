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
    }
}

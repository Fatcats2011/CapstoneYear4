using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode;

namespace DoA.Tests
{
    /// <summary>
    /// A player's one-shot as it travels online: every kind arrives as it was sent, and only a rise names a respawn point
    /// </summary>
    public class ScooterCueTests
    {
        static IEnumerable<ScooterCue> EveryKind()
        {
            foreach (CueKind kind in new[] { CueKind.Boost, CueKind.DriftBoost, CueKind.HornReady, CueKind.Phase, CueKind.PhaseEnd, CueKind.Death })
                yield return ScooterCue.Of(kind);
            yield return ScooterCue.Rise(17);
        }

        [TestCaseSource(nameof(EveryKind))]
        public void ACue_ArrivesAsItWasSent(ScooterCue sent)
        {
            using (FastBufferWriter writer = new FastBufferWriter(64, Allocator.Temp))
            {
                writer.WriteNetworkSerializable(sent);
                using (FastBufferReader reader = new FastBufferReader(writer, Allocator.Temp))
                {
                    reader.ReadNetworkSerializable(out ScooterCue received);
                    Assert.AreEqual(sent, received);
                }
            }
        }

        [Test]
        public void OnlyARise_NamesAPoint()
        {
            Assert.AreEqual(-1, ScooterCue.Of(CueKind.Boost).Point);
            Assert.AreEqual(CueKind.Rise, ScooterCue.Rise(17).Kind);
            Assert.AreEqual(17, ScooterCue.Rise(17).Point);
        }
    }
}

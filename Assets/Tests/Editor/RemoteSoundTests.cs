using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// How another machine's scooter sounds here, online: its one-shots are heard in full close to this machine's player,
    /// fading to silence farther away, each with its owner's sound
    /// </summary>
    public class RemoteSoundTests
    {
        [Test]
        public void AnotherMachinesScooter_IsHeardInFullNearby_FadingToSilenceFarAway()
        {
            Assert.AreEqual(1f, RemoteSound.Volume(0f));
            Assert.AreEqual(1f, RemoteSound.Volume(RemoteSound.NEAR));
            Assert.AreEqual(0.5f, RemoteSound.Volume((RemoteSound.NEAR + RemoteSound.FAR) / 2f), 0.001f);
            Assert.AreEqual(0f, RemoteSound.Volume(RemoteSound.FAR));
            Assert.AreEqual(0f, RemoteSound.Volume(RemoteSound.FAR + 500f));
        }

        [Test]
        public void EachSoundCue_PlaysItsOwnersSound()
        {
            Assert.AreEqual("boost_used", RemoteSound.KeyFor(CueKind.Boost));
            Assert.AreEqual("mini", RemoteSound.KeyFor(CueKind.DriftBoost));
            Assert.AreEqual("boost_charged", RemoteSound.KeyFor(CueKind.HornReady));
            Assert.AreEqual("phasing", RemoteSound.KeyFor(CueKind.Phase));
            Assert.AreEqual("death", RemoteSound.KeyFor(CueKind.Death));
            Assert.IsNull(RemoteSound.KeyFor(CueKind.PhaseEnd), "it stops the phasing sound");
            Assert.IsNull(RemoteSound.KeyFor(CueKind.Rise), "a gravestone, not a sound");
        }

        [Test]
        public void WithoutAPlayerOnThisMachine_EverythingIsHeardInFull()
        {
            Assert.AreEqual(1f, RemoteSound.VolumeAt(new Vector3(500, 0, 0))); // no PlayerInstantiate in EditMode
        }
    }
}

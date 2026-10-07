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
        public void NearestDistance_IsToTheClosestOfThisMachinesPlayers()
        {
            // Two players share this machine's screen: a sound is as loud as it is for the nearer one
            Vector3[] listeners = { new Vector3(0, 0, 0), new Vector3(100, 0, 0) };

            Assert.AreEqual(10f, RemoteSound.NearestDistance(new Vector3(90, 0, 0), listeners), 1e-4f);
            Assert.AreEqual(float.PositiveInfinity, RemoteSound.NearestDistance(Vector3.zero, new Vector3[0]), "nobody here");
        }

        [Test]
        public void WithoutAPlayerOnThisMachine_EverythingIsHeardInFull()
        {
            Assert.AreEqual(1f, RemoteSound.VolumeAt(new Vector3(500, 0, 0))); // no PlayerInstantiate in EditMode
        }
    }
}

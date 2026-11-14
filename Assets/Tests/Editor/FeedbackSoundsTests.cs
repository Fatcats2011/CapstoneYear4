using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The feedback sounds (FeedbackSounds): the robbed, nothing-taken, cursed-package and 30-second sounds are in the
    /// game, short, and set quieter
    /// than their files (they are mastered loud); the steal keeps the game's own sound; and a sound the game doesn't
    /// have is no sound, never an error
    /// </summary>
    public class FeedbackSoundsTests
    {
        [Test]
        public void ASoundThatIsntThere_IsNoSound()
        {
            Assert.IsNull(FeedbackSounds.Clip("no-such-sound"));
            Assert.IsNull(FeedbackSounds.Clip("no-such-sound"), "asked twice: still none");
            Assert.IsNull(FeedbackSounds.Clip(null));
        }

        [Test]
        public void TheSounds_HaveTheirNames()
        {
            Assert.AreEqual("steal", FeedbackSounds.STEAL);
            Assert.AreEqual("robbed", FeedbackSounds.ROBBED);
            Assert.AreEqual("thud", FeedbackSounds.THUD);
        }

        [TestCase(FeedbackSounds.ROBBED)]
        [TestCase(FeedbackSounds.THUD)]
        [TestCase(FeedbackSounds.CURSED)]
        [TestCase(FeedbackSounds.WARNING)]
        public void TheSounds_AreInTheGame_AndShort(string name)
        {
            AudioClip clip = FeedbackSounds.Clip(name);

            Assert.IsNotNull(clip, "Resources/Audio/Feedback/" + name);
            Assert.Less(clip.length, 2f);
        }

        [Test]
        public void TheSteal_KeepsTheGamesOwnSound()
        {
            Assert.IsNull(FeedbackSounds.Clip(FeedbackSounds.STEAL), "no file: SoundPool plays the whoosh");
        }

        [Test]
        public void EachSound_IsHeardOverTheMusic_WithoutDrowningIt()
        {
            // Their files peak at about -1 dB; the game's other pickups peak between -12 and -5 dB as played
            Assert.AreEqual(0.45f, FeedbackSounds.Volume(FeedbackSounds.ROBBED));
            Assert.AreEqual(0.6f, FeedbackSounds.Volume(FeedbackSounds.THUD));
            Assert.AreEqual(0.45f, FeedbackSounds.Volume("anything else"));
        }

        [Test]
        public void TheModeSounds_HaveTheirNamesAndVolumes()
        {
            Assert.AreEqual("cursed", FeedbackSounds.CURSED);
            Assert.AreEqual("warning", FeedbackSounds.WARNING);
            Assert.AreEqual(0.5f, FeedbackSounds.Volume(FeedbackSounds.CURSED));
            Assert.AreEqual(0.6f, FeedbackSounds.Volume(FeedbackSounds.WARNING));
        }
    }
}

using System.Text;
using NUnit.Framework;
using Unity.Collections;

namespace DoA.Tests
{
    /// <summary>
    /// The name each player shows online: the fallback for an empty one, and cleaning a Steam name so it fits what's synced
    /// </summary>
    public class PlayerNamesTests
    {
        [Test]
        public void Fallback_IsPlayerAndTheSeatNumber()
        {
            Assert.AreEqual("Player 1", PlayerNames.Fallback(0));
            Assert.AreEqual("Player 4", PlayerNames.Fallback(3));
        }

        [Test]
        public void Clean_TrimsAndTurnsNullIntoEmpty()
        {
            Assert.AreEqual("Ryan", PlayerNames.Clean("  Ryan  "));
            Assert.AreEqual("", PlayerNames.Clean(null));
        }

        [Test]
        public void Clean_DropsControlCharacters()
        {
            Assert.AreEqual("Ryan", PlayerNames.Clean("Ry\tan\n"));
        }

        [Test]
        public void Clean_CutsALongNameToWhatFitsTheSyncedString()
        {
            Assert.AreEqual(FixedString32Bytes.UTF8MaxLengthInBytes, PlayerNames.Clean(new string('a', 100)).Length);
        }

        [Test]
        public void Clean_NeverSplitsASurrogatePair()
        {
            string cleaned = PlayerNames.Clean(new string('a', 3) + string.Concat(System.Linq.Enumerable.Repeat("\U0001F47B", 20)));

            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(cleaned), FixedString32Bytes.UTF8MaxLengthInBytes);
            for (int i = 0; i < cleaned.Length; i++)
            {
                if (char.IsHighSurrogate(cleaned[i]))
                {
                    Assert.IsTrue(i + 1 < cleaned.Length && char.IsSurrogatePair(cleaned[i], cleaned[i + 1]), "pair at " + i);
                    i++;
                }
                else
                    Assert.IsFalse(char.IsLowSurrogate(cleaned[i]), "lone low surrogate at " + i);
            }
        }

        [Test]
        public void Clean_ALoneSurrogate_DoesNotThrowAndStillFits()
        {
            foreach (string raw in new[] { "\uD83D", "a\uD83D", "\uDC7B" + new string('b', 40) })
            {
                string cleaned = PlayerNames.Clean(raw);
                FixedString32Bytes synced = default;
                Assert.DoesNotThrow(() => synced = cleaned, raw);
                Assert.LessOrEqual(Encoding.UTF8.GetByteCount(cleaned), FixedString32Bytes.UTF8MaxLengthInBytes);
            }
        }

        [Test]
        public void Shown_FallsBackOnlyWhenTheNameIsEmpty()
        {
            Assert.AreEqual("Player 3", PlayerNames.Shown("", 2));
            Assert.AreEqual("Player 1", PlayerNames.Shown("   ", 0));
            Assert.AreEqual("Alex", PlayerNames.Shown("Alex", 1));
        }

        [Test]
        public void Shown_CleansWhatAnotherMachineSent()
        {
            // A modified game can sync a name that never went through Clean
            Assert.AreEqual("Alexforged", PlayerNames.Shown("Alex\nforged", 1), "no control characters");
            Assert.AreEqual("Player 2", PlayerNames.Shown("\n\t\r", 1), "nothing left: the fallback");
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The accents the fonts lack (AccentRules): each of the 24 characters is its base letter plus a drawn mark; only
    /// characters the font lacks are replaced; capital sharp s is SS; text without them is left alone
    /// </summary>
    public class AccentRulesTests
    {
        static bool HasNone(char c) { return c < 128 || "ÉÀÜ".IndexOf(c) >= 0; }

        [TestCase('Ă', 'A', AccentMark.Breve)]
        [TestCase('Ą', 'A', AccentMark.Ogonek)]
        [TestCase('Ć', 'C', AccentMark.Acute)]
        [TestCase('Č', 'C', AccentMark.Caron)]
        [TestCase('Ď', 'D', AccentMark.Caron)]
        [TestCase('Ę', 'E', AccentMark.Ogonek)]
        [TestCase('Ě', 'E', AccentMark.Caron)]
        [TestCase('Ğ', 'G', AccentMark.Breve)]
        [TestCase('İ', 'I', AccentMark.Dot)]
        [TestCase('Ł', 'L', AccentMark.LStroke)]
        [TestCase('Ń', 'N', AccentMark.Acute)]
        [TestCase('Ň', 'N', AccentMark.Caron)]
        [TestCase('Ő', 'O', AccentMark.DoubleAcute)]
        [TestCase('Ř', 'R', AccentMark.Caron)]
        [TestCase('Ś', 'S', AccentMark.Acute)]
        [TestCase('Ş', 'S', AccentMark.CommaBelow)]
        [TestCase('Ť', 'T', AccentMark.Caron)]
        [TestCase('Ů', 'U', AccentMark.Ring)]
        [TestCase('Ű', 'U', AccentMark.DoubleAcute)]
        [TestCase('Ź', 'Z', AccentMark.Acute)]
        [TestCase('Ż', 'Z', AccentMark.Dot)]
        [TestCase('Ș', 'S', AccentMark.CommaBelow)]
        [TestCase('Ț', 'T', AccentMark.CommaBelow)]
        [TestCase('ž', 'z', AccentMark.Caron)]
        public void TryGet_BaseAndMark(char c, char letter, AccentMark mark)
        {
            Assert.IsTrue(AccentRules.TryGet(c, out Accented accented));
            Assert.AreEqual(letter, accented.Base);
            Assert.AreEqual(mark, accented.Mark);
        }

        [Test]
        public void TryGet_APlainLetter_IsFalse()
        {
            Assert.IsFalse(AccentRules.TryGet('C', out _));
            Assert.IsFalse(AccentRules.TryGet('É', out _), "the fonts have the common ones");
        }

        [Test]
        public void Replace_OnlyWhatTheFontLacks()
        {
            Assert.AreEqual("ÉC", AccentRules.Replace("ÉČ", HasNone));
        }

        [Test]
        public void Replace_SharpS_IsSS()
        {
            Assert.AreEqual("STRASSE", AccentRules.Replace("STRAẞE", HasNone));
        }

        [Test]
        public void Replace_PlainText_Untouched()
        {
            string text = "PRESS <b>A</b> TO BOOST";
            Assert.AreSame(text, AccentRules.Replace(text, HasNone));
        }
    }
}

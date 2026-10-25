using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The game's text lookup (Loc), on a test CSV: the current language's text, a regional language falling back to
    /// its base, a missing translation falling back to English, arguments filled in, an unknown key, and Changed raised
    /// only on a real change
    /// </summary>
    public class LocTests
    {
        static int changes;
        static void Changed() { changes++; }

        StringsCsv saved;

        [SetUp]
        public void SetUp()
        {
            saved = Loc.Csv;
            Loc.Csv = StringsCsv.Parse(
                "key,note,en,fr,es,es-419,reviewed\n" +
                "hello,,HELLO,BONJOUR,HOLA,,\n" +
                "steals,,{0} STEALS,{0} VOLS,{0} ROBOS,{0} ROBOS (LATAM),\n" +
                "onlyenglish,,ONLY,,,,\n" +
                "pair,,{0} OF {1},{0} SUR {1},,,\n");
            Loc.SetLanguage(LocaleCodes.ENGLISH);
            changes = 0;
            Loc.Changed += Changed;
        }

        [TearDown]
        public void TearDown()
        {
            Loc.Changed -= Changed;
            Loc.Csv = saved;
            Loc.SetLanguage(LocaleCodes.ENGLISH);
        }

        [Test]
        public void Get_TooFewArguments_ShowsTheTextUnfilled()
        {
            string shown = null;

            Assert.DoesNotThrow(() => shown = Loc.Get("pair", "1"));
            Assert.AreEqual("{0} OF {1}", shown);
        }

        [Test]
        public void Get_TheCurrentLanguage()
        {
            Assert.AreEqual("HELLO", Loc.Get("hello"));
            Loc.SetLanguage("fr");
            Assert.AreEqual("BONJOUR", Loc.Get("hello"));
        }

        [Test]
        public void Get_RegionalFallsBackToBase()
        {
            Loc.SetLanguage("es-419");

            Assert.AreEqual("HOLA", Loc.Get("hello"), "no Latin American cell: Spain's");
            Assert.AreEqual("3 ROBOS (LATAM)", Loc.Get("steals", 3));
        }

        [Test]
        public void Get_MissingTranslation_IsEnglish()
        {
            Loc.SetLanguage("fr");

            Assert.AreEqual("ONLY", Loc.Get("onlyenglish"));
        }

        [Test]
        public void Get_FormatsArguments()
        {
            Loc.SetLanguage("fr");

            Assert.AreEqual("3 VOLS", Loc.Get("steals", 3));
        }

        [Test]
        public void Get_UnknownKey_IsTheKey()
        {
            Assert.AreEqual("no.such.key", Loc.Get("no.such.key"));
        }

        [Test]
        public void SetLanguage_RaisesChanged_OnlyOnAChange()
        {
            Loc.SetLanguage("en");
            Loc.SetLanguage("fr");
            Loc.SetLanguage("fr");

            Assert.AreEqual(1, changes);
            Assert.AreEqual("fr", Loc.Language);
        }

        [Test]
        public void SetLanguage_Unknown_IsEnglish()
        {
            Loc.SetLanguage("fr");
            Loc.SetLanguage("xx");

            Assert.AreEqual("en", Loc.Language);
        }
    }
}

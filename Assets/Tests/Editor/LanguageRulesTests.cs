using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Which language the game speaks (LanguageRules): Steam's language names and the PC's to the game's locales, the
    /// saved choice first, the LANGUAGE row's steps, and each language's name in itself
    /// </summary>
    public class LanguageRulesTests
    {
        [TestCase("english", "en")]
        [TestCase("french", "fr")]
        [TestCase("german", "de")]
        [TestCase("italian", "it")]
        [TestCase("dutch", "nl")]
        [TestCase("spanish", "es")]
        [TestCase("latam", "es-419")]
        [TestCase("portuguese", "pt")]
        [TestCase("brazilian", "pt-BR")]
        [TestCase("polish", "pl")]
        [TestCase("turkish", "tr")]
        [TestCase("czech", "cs")]
        [TestCase("romanian", "ro")]
        [TestCase("hungarian", "hu")]
        [TestCase("swedish", "sv")]
        [TestCase("danish", "da")]
        public void FromSteam_EveryLanguage(string steam, string code)
        {
            Assert.AreEqual(code, LanguageRules.FromSteam(steam));
        }

        [Test]
        public void FromSteam_Unknown_IsNull()
        {
            Assert.IsNull(LanguageRules.FromSteam("japanese"));
            Assert.IsNull(LanguageRules.FromSteam(null));
        }

        [Test]
        public void FromSystem_TheSameSet()
        {
            Assert.AreEqual("de", LanguageRules.FromSystem(SystemLanguage.German));
            Assert.AreEqual("es", LanguageRules.FromSystem(SystemLanguage.Spanish));
            Assert.AreEqual("pt", LanguageRules.FromSystem(SystemLanguage.Portuguese));
            Assert.IsNull(LanguageRules.FromSystem(SystemLanguage.Japanese));
        }

        [Test]
        public void Pick_SavedFirst_ThenSteam_ThenSystem_ThenEnglish()
        {
            Assert.AreEqual("fr", LanguageRules.Pick("fr", "german", SystemLanguage.Italian));
            Assert.AreEqual("de", LanguageRules.Pick("", "german", SystemLanguage.Italian));
            Assert.AreEqual("it", LanguageRules.Pick("", null, SystemLanguage.Italian));
            Assert.AreEqual("en", LanguageRules.Pick("", null, SystemLanguage.Japanese));
        }

        [Test]
        public void SavedLanguage_Unknown_FallsBackToSteamThenEnglish()
        {
            Assert.AreEqual("de", LanguageRules.Pick("xx", "german", SystemLanguage.Japanese));
            Assert.AreEqual("en", LanguageRules.Pick("xx", null, SystemLanguage.Japanese));
        }

        [Test]
        public void Next_StopsAtTheEnds()
        {
            Assert.AreEqual("fr", LanguageRules.Next("en", true));
            Assert.AreEqual("en", LanguageRules.Next("en", false));
            Assert.AreEqual("da", LanguageRules.Next("da", true));
            Assert.AreEqual("es", LanguageRules.Next("es-419", false));
        }

        [TestCase("en", "ENGLISH")]
        [TestCase("fr", "FRANÇAIS")]
        [TestCase("de", "DEUTSCH")]
        [TestCase("es-419", "ESPAÑOL (LATAM)")]
        [TestCase("pt-BR", "PORTUGUÊS (BRASIL)")]
        [TestCase("cs", "ČEŠTINA")]
        [TestCase("tr", "TÜRKÇE")]
        [TestCase("ro", "ROMÂNĂ")]
        public void Name_IsInItsOwnLanguage(string code, string name)
        {
            Assert.AreEqual(name, LanguageRules.Name(code));
        }

        [Test]
        public void Name_EveryLocaleHasOne()
        {
            foreach (string code in LocaleCodes.All)
                Assert.IsNotEmpty(LanguageRules.Name(code), code);
        }
    }
}

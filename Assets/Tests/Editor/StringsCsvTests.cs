using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The game's strings (StringsCsv, LocaleCodes): reading the CSV (quotes, commas, line breaks in a cell), the locale
    /// order and fallbacks, and the real file's health: every locale's column, every cell filled, every translation
    /// keeping English's placeholders, no key twice
    /// </summary>
    public class StringsCsvTests
    {
        const string HEADER = "key,note,en,fr,reviewed\n";

        [Test]
        public void Parse_QuotedCellsWithCommasAndQuotes()
        {
            StringsCsv csv = StringsCsv.Parse(HEADER + "a,\"a note, with a comma\",\"a, \"\"b\"\"\",x,\n");

            Assert.AreEqual("a, \"b\"", csv.Get("a", "en"));
            Assert.AreEqual("a note, with a comma", csv.Note("a"));
            Assert.AreEqual("x", csv.Get("a", "fr"));
        }

        [Test]
        public void Parse_NewlineInsideQuotes()
        {
            StringsCsv csv = StringsCsv.Parse(HEADER + "a,,\"one\ntwo\",x,\r\nb,,B,Bf,fr\r\n");

            Assert.AreEqual("one\ntwo", csv.Get("a", "en"));
            Assert.AreEqual("Bf", csv.Get("b", "fr"));
            CollectionAssert.AreEqual(new[] { "a", "b" }, csv.Keys);
        }

        [Test]
        public void Get_AbsentKeyOrEmptyCell_IsNull()
        {
            StringsCsv csv = StringsCsv.Parse(HEADER + "a,,A,,\n");

            Assert.IsNull(csv.Get("a", "fr"));
            Assert.IsNull(csv.Get("nope", "en"));
            Assert.IsNull(csv.Get("a", "de"), "no such column");
        }

        [Test]
        public void Reviewed_TheLocalesListed()
        {
            StringsCsv csv = StringsCsv.Parse(HEADER + "a,,A,Af,fr de\n");

            Assert.IsTrue(csv.Reviewed("a", "fr"));
            Assert.IsFalse(csv.Reviewed("a", "en"));
        }

        [TestCase("es-419", "es")]
        [TestCase("pt-BR", "pt")]
        [TestCase("de", "en")]
        [TestCase("en", null)]
        public void FallbackOf_RegionalToBase_ElseEnglish(string code, string fallback)
        {
            Assert.AreEqual(fallback, LocaleCodes.FallbackOf(code));
        }

        [Test]
        public void LocaleCodes_TheSixteen_InOrder()
        {
            CollectionAssert.AreEqual(new[] { "en", "fr", "de", "it", "nl", "es", "es-419", "pt", "pt-BR", "pl", "tr", "cs", "ro", "hu", "sv", "da" },
                LocaleCodes.All);
        }

        [Test]
        public void TheFile_HasEveryLocaleColumn_InOrder()
        {
            StringsCsv csv = StringsCsv.Load();

            Assert.IsNotNull(csv, "Resources/Localization/strings");
            List<string> expected = new List<string> { "key", "note" };
            expected.AddRange(LocaleCodes.All);
            expected.Add("reviewed");
            CollectionAssert.AreEqual(expected, csv.Header);
        }

        [Test]
        public void TheFile_EveryCellFilled()
        {
            StringsCsv csv = StringsCsv.Load();
            List<string> empty = new List<string>();
            foreach (string key in csv.Keys)
                foreach (string locale in LocaleCodes.All)
                    if (csv.Get(key, locale) == null)
                        empty.Add(key + " " + locale);

            Assert.IsEmpty(empty);
        }

        [Test]
        public void TheFile_PlaceholdersMatchEnglish()
        {
            StringsCsv csv = StringsCsv.Load();
            List<string> wrong = new List<string>();
            foreach (string key in csv.Keys)
            {
                string english = Placeholders(csv.Get(key, "en"));
                foreach (string locale in LocaleCodes.All)
                    if (Placeholders(csv.Get(key, locale)) != english)
                        wrong.Add(key + " " + locale);
            }

            Assert.IsEmpty(wrong);
        }

        [Test]
        public void TheFile_NoDuplicateKeys()
        {
            StringsCsv csv = StringsCsv.Load();
            HashSet<string> seen = new HashSet<string>();
            List<string> twice = new List<string>();
            foreach (string key in csv.Keys)
                if (!seen.Add(key))
                    twice.Add(key);

            Assert.IsEmpty(twice);
        }

        [Test]
        public void TheFile_TranslationsDifferFromEnglish()
        {
            StringsCsv csv = StringsCsv.Load();
            foreach (string locale in LocaleCodes.All)
            {
                if (locale == LocaleCodes.ENGLISH)
                    continue;
                int differ = 0;
                foreach (string key in csv.Keys)
                    if (csv.Get(key, locale) != csv.Get(key, LocaleCodes.ENGLISH))
                        differ++;
                // Some words are the same in a language (MENU, VSYNC, TURBO): most must not be
                Assert.GreaterOrEqual(differ, csv.Keys.Count * 0.9f, locale + " is mostly English still");
            }
        }

        [Test]
        public void TheFile_DrawnWordsFitAtTheSmallestSize()
        {
            StringsCsv csv = StringsCsv.Load();
            List<string> tooLong = new List<string>();
            foreach (string key in csv.Keys)
            {
                if (!key.StartsWith("word."))
                    continue;
                // A short English word (GO!, ON) is sized by its rect's height: there's room beside it
                float room = Mathf.Max(csv.Get(key, LocaleCodes.ENGLISH).Length, 4) / DrawnWords.SMALLEST;
                foreach (string locale in LocaleCodes.All)
                    if (csv.Get(key, locale).Length > room)
                        tooLong.Add(key + " " + locale + " " + csv.Get(key, locale));
            }

            Assert.IsEmpty(tooLong);
        }

        static string Placeholders(string text)
        {
            List<string> found = new List<string>();
            foreach (Match match in Regex.Matches(text ?? "", @"\{\d+\}"))
                found.Add(match.Value);
            found.Sort();
            return string.Join(",", found);
        }
    }
}

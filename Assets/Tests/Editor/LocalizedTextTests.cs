using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Text that follows the language (LocalizedText, and the panels the code builds): it shows its key's text, changes
    /// with the language even while it's on screen, and every key the code asks for is in the strings CSV
    /// </summary>
    public class LocalizedTextTests
    {
        readonly TestObjects objects = new TestObjects();
        StringsCsv saved;

        [SetUp]
        public void SetUp()
        {
            saved = Loc.Csv;
            Loc.Csv = StringsCsv.Parse(
                "key,note,en,fr,reviewed\n" +
                "hello,,HELLO,BONJOUR,\n" +
                "steals,,{0} STEALS,{0} VOLS,\n" +
                "rules.title,,MATCH RULES,RÈGLES,\n" +
                "rules.length,,LENGTH,DURÉE,\n" +
                "rules.length.normal,,NORMAL,NORMALE,\n");
            Loc.SetLanguage("en");
        }

        [TearDown]
        public void TearDown()
        {
            Loc.SetLanguage("en");
            Loc.Csv = saved;
            objects.DestroyAll();
        }

        [Test]
        public void LocalizedText_ShowsTheKeysText()
        {
            TMP_Text text = objects.Add<TextMeshProUGUI>();

            LocalizedText.On(text, "steals", 3);

            Assert.AreEqual("3 STEALS", text.text);
        }

        [Test]
        public void ChangingLanguage_UpdatesTextAlreadyShown()
        {
            TMP_Text text = objects.Add<TextMeshProUGUI>();
            LocalizedText.On(text, "hello");
            RulesPanel panel = RulesPanel.Build(objects.NewGameObject("Canvas").AddComponent<RectTransform>());
            panel.Kept = null;
            panel.Open(HouseRules.Normal);

            Loc.SetLanguage("fr");

            Assert.AreEqual("BONJOUR", text.text);
            Assert.AreEqual("RÈGLES", panel.Title);
            Assert.AreEqual("NORMALE", panel.ValueOf(0));
            Assert.AreEqual("DURÉE", panel.NameOf(0));
        }

        [Test]
        public void Set_ChangesTheKey()
        {
            TMP_Text text = objects.Add<TextMeshProUGUI>();
            LocalizedText shown = LocalizedText.On(text, "hello");

            shown.Set("steals", 2);

            Assert.AreEqual("2 STEALS", text.text);
        }

        [Test]
        public void CodeKeys_AllExistInTheCsv()
        {
            StringsCsv csv = StringsCsv.Load();
            HashSet<string> keys = new HashSet<string>(csv.Keys);
            List<string> missing = new List<string>();
            Regex keyed = new Regex(@"""((?:word|options|pause|controls|rules|award|results|cutscene|dispatch|matchmaking|online|prompt|hud|menu)\.[a-z0-9.]+)""");
            foreach (string file in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
            {
                string code = File.ReadAllText(file);
                foreach (Match match in keyed.Matches(code))
                    // A key ending in "." is a prefix the code completes (award.line. + "steal" + .one); their own tests cover those
                    if (!match.Groups[1].Value.EndsWith(".") && !keys.Contains(match.Groups[1].Value))
                        missing.Add(Path.GetFileName(file) + ": " + match.Groups[1].Value);
            }

            Assert.IsEmpty(missing);
        }
    }
}

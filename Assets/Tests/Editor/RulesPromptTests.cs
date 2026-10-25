using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The bottom prompt for MATCH RULES: Start on a pad, M on a keyboard (playtest, 2026-10-24: Escape opened it, and
    /// nothing said so)
    /// </summary>
    public class RulesPromptTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void Build_ShowsTheStartButton_WithTheMKeyOnAKeyboard()
        {
            GameObject parent = objects.NewGameObject("Row");
            parent.AddComponent<RectTransform>();

            GameObject prompt = RulesPrompt.Build(parent.transform, null, 56, 36);

            ButtonGlyph glyph = prompt.GetComponentInChildren<ButtonGlyph>();
            Assert.IsNotNull(glyph, "a button");
            Assert.AreEqual("Xbox_Start", glyph.xbox.name);
            Assert.AreEqual("PS_Options", glyph.playStation.name);
            Assert.AreEqual("Key_M", glyph.keyboard.name);
            Assert.AreEqual(Loc.Get("rules.title"), prompt.GetComponentInChildren<TMP_Text>().text);
        }
    }
}

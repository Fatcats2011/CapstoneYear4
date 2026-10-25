using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The flag showing the language spoken (LanguageFlag, FlagSet): every language has one, and the flag on screen
    /// changes with the language
    /// </summary>
    public class LanguageFlagTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            Loc.SetLanguage("en");
            objects.DestroyAll();
        }

        [Test]
        public void FlagSet_HasEveryLocale()
        {
            FlagSet flags = FlagSet.Game;

            Assert.IsNotNull(flags);
            foreach (string code in LocaleCodes.All)
                Assert.IsNotNull(flags.Of(code), code);
        }

        [Test]
        public void Flag_ShowsTheCurrentLanguage_AndFollowsAChange()
        {
            RectTransform parent = objects.NewGameObject("Canvas").AddComponent<RectTransform>();
            Loc.SetLanguage("en");

            LanguageFlag flag = LanguageFlag.Create(parent, new Vector2(1f, 1f), new Vector2(-60f, -50f), 48f);
            Image image = flag.GetComponent<Image>();
            Assert.AreSame(FlagSet.Game.Of("en"), image.sprite);
            Assert.AreEqual(48f, ((RectTransform)flag.transform).sizeDelta.y, 0.01f);

            Loc.SetLanguage("pt-BR");
            Assert.AreSame(FlagSet.Game.Of("pt-BR"), image.sprite);
        }
    }
}

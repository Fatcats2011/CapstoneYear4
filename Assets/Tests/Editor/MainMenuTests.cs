using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The title screen's menus follow the game state, whoever switched it: pressing Play here, or (online) the host
    /// </summary>
    public class MainMenuTests
    {
        readonly TestObjects objects = new TestObjects();
        GameManager game;
        MainMenu menu;
        Canvas playerSelect, options, credits;

        [SetUp]
        public void SetUp()
        {
            game = objects.Add<GameManager>();
            Reflect.SetSingleton(game);
            menu = objects.Add<MainMenu>();
            playerSelect = objects.Add<Canvas>();
            options = objects.Add<Canvas>();
            credits = objects.Add<Canvas>();
            Reflect.SetField(menu, "PlayerSelectCanvas", playerSelect);
            Reflect.SetField(menu, "OptionsCanvas", options);
            Reflect.SetField(menu, "CreditsCanvas", credits);
            playerSelect.enabled = false;
            menu.OnEnable();
        }

        [TearDown]
        public void TearDown()
        {
            Reflect.SetSingleton<GameManager>(null);
            objects.DestroyAll();
        }

        [Test]
        public void PlayerSelect_OpensItsMenu_ThoughNobodyPressedPlayHere()
        {
            game.ApplyGameState(GameState.PlayerSelect); // an online client following the host

            Assert.IsTrue(playerSelect.enabled);
        }

        [Test]
        public void TitleScreen_ClosesPlayerSelectOptionsAndCredits()
        {
            playerSelect.enabled = options.enabled = credits.enabled = true;

            game.ApplyGameState(GameState.Menu);

            Assert.IsFalse(playerSelect.enabled, "player select");
            Assert.IsFalse(options.enabled, "options");
            Assert.IsFalse(credits.enabled, "credits");
        }

        [Test]
        public void EnabledThenDisabled_LeavesNoGameStateHandlers()
        {
            Reflect.Invoke(menu, "OnDisable");

            Assert.AreEqual(0, Reflect.HandlerCount(game, "OnSwapPlayerSelect", menu));
            Assert.AreEqual(0, Reflect.HandlerCount(game, "OnSwapMenu", menu));
        }
    }
}

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
            GameManager.instance = game;
            menu = objects.Add<MainMenu>();
            playerSelect = objects.Add<Canvas>();
            options = objects.Add<Canvas>();
            credits = objects.Add<Canvas>();
            menu.PlayerSelectCanvas = playerSelect;
            menu.OptionsCanvas = options;
            menu.CreditsCanvas = credits;
            playerSelect.enabled = false;
            menu.OnEnable();
        }

        [TearDown]
        public void TearDown()
        {
            GameManager.instance = null;
            objects.DestroyAll();
            GameAuthority.Role = NetworkRole.Offline;
        }

        [Test]
        public void Start_Offline_OpensOnTheTitleScreen()
        {
            menu.Start();

            Assert.AreEqual(GameState.Menu, game.MainState);
        }

        [Test]
        public void Start_Online_OpensOnPlayerSelect_TheLobby()
        {
            GameAuthority.Role = NetworkRole.Host;

            menu.Start();

            Assert.AreEqual(GameState.PlayerSelect, game.MainState);
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
            menu.OnDisable();

            Assert.AreEqual(0, Reflect.HandlerCount(game, nameof(GameManager.OnSwapPlayerSelect), menu));
            Assert.AreEqual(0, Reflect.HandlerCount(game, nameof(GameManager.OnSwapMenu), menu));
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class GameAuthorityTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            GameAuthority.Role = NetworkRole.Offline;
            Time.timeScale = 1f;
            objects.DestroyAll();
        }

        [TestCase(NetworkRole.Offline, true, false)]
        [TestCase(NetworkRole.Host, true, true)]
        [TestCase(NetworkRole.Client, false, true)]
        public void Role_SaysWhoDecidesTheRulesAndWhetherTheMatchIsOnline(NetworkRole role, bool decidesRules, bool online)
        {
            GameAuthority.Role = role;

            Assert.AreEqual(decidesRules, GameAuthority.IsAuthority, "decides the rules");
            Assert.AreEqual(online, GameAuthority.IsOnline, "online");
        }

        [Test]
        public void SetTimeScale_InALocalMatch_ChangesTheClock()
        {
            GameAuthority.SetTimeScale(0f);

            Assert.AreEqual(0f, Time.timeScale);
        }

        [TestCase(NetworkRole.Host)]
        [TestCase(NetworkRole.Client)]
        public void SetTimeScale_Online_LeavesTheSharedClockRunning(NetworkRole role)
        {
            GameAuthority.Role = role;

            GameAuthority.SetTimeScale(0f);

            Assert.AreEqual(1f, Time.timeScale);
        }

        [Test]
        public void GameplayScripts_ChangeTheClockOnlyThroughGameAuthority()
        {
            string[] offenders = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/OUTDATED/") && Path.GetFileName(f) != "GameAuthority.cs")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"Time\.timeScale\s*=(?!=)"))
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void SetGameState_InALocalMatch_SwitchesTheStateAndTellsListeners()
        {
            GameManager game = objects.Add<GameManager>();
            int optionsOpened = 0;
            game.OnSwapOptions += () => optionsOpened++;

            game.SetGameState(GameState.Options);

            Assert.AreEqual(GameState.Options, game.MainState);
            Assert.AreEqual(1, optionsOpened);
        }

        [Test]
        public void SetGameState_OnAnOnlineClient_ChangesNothing()
        {
            GameAuthority.Role = NetworkRole.Client;
            GameManager game = objects.Add<GameManager>();
            int optionsOpened = 0;
            game.OnSwapOptions += () => optionsOpened++;

            game.SetGameState(GameState.Options);

            Assert.AreEqual(GameState.Default, game.MainState);
            Assert.AreEqual(0, optionsOpened);
        }

        [Test]
        public void ApplyGameState_OnAnOnlineClient_SwitchesToTheHostsState()
        {
            GameAuthority.Role = NetworkRole.Client;
            GameManager game = objects.Add<GameManager>();
            int optionsOpened = 0;
            game.OnSwapOptions += () => optionsOpened++;

            game.ApplyGameState(GameState.Options);

            Assert.AreEqual(GameState.Options, game.MainState);
            Assert.AreEqual(1, optionsOpened);
        }

        [TestCase(NetworkRole.Offline, 0.5f)]
        [TestCase(NetworkRole.Host, 1f)]
        public void EndOfTheLastWave_SlowsTheClock_OnlyInALocalMatch(NetworkRole role, float expectedTimeScale)
        {
            GameAuthority.Role = role;
            OrderManager orders = objects.Add<OrderManager>();

            IEnumerator linger = (IEnumerator)Reflect.Invoke(orders, "PostGameClarity", false);
            linger.MoveNext(); // up to the half-speed pause

            Assert.AreEqual(expectedTimeScale, Time.timeScale);
        }
    }
}

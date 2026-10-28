using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Online bots (port 7836, menu scene, then the game scene): a host alone in its lobby starts the match and the three
    /// empty seats fill with CPU drivers, which drive on the host and show as host-owned players. With the Bots rule off,
    /// nobody is added. They load the game scene: about a minute each
    /// </summary>
    public class OnlineBotsNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7836;
        const string VERSION = "test";
        const float LOADING = 180f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
            HouseRules.Current = HouseRules.Normal;
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        // Hosts alone in player select with the Bots rule as given, and starts the match
        static OnlineSession host; // static: no lambda may capture a local after EnterPlayMode

        static IEnumerator StartAlone(bool botsOn)
        {
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            HouseRules.Current = new HouseRules(MatchLength.Normal, true, true, botsOn);
            host = OnlineSession.Create(VERSION);
            host.FillsWithBots = true;
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            deadline = Time.realtimeSinceStartup + 15;
            while (host.Match == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            // The match is started by hand: a ready host alone would also start player select's own countdown
            SceneFlow.Current.LoadGameScene();
        }

        [UnityTest]
        public IEnumerator Hosting_AloneWithBotsOn_TheThreeEmptySeatsFillWithCpuDrivers()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode(); // only a test itself may yield this
            yield return StartAlone(true);

            float deadline = Time.realtimeSinceStartup + LOADING;
            while (!(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GAME && State() == GameState.MainLoop)
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, State(), "the match started");

            Assert.AreEqual(4, host.PlayersIn, "four seats are taken");
            int bots = 0;
            foreach (OnlinePlayer player in host.Players)
            {
                if (player.IsBot)
                    bots++;
            }
            Assert.AreEqual(3, bots, "three of them are CPU drivers");

            PlayerRoster roster = PlayerInstantiate.Instance.Roster;
            Assert.IsFalse(roster[0].IsBot, "the host's own seat is a person");
            for (int seat = 1; seat < 4; seat++)
            {
                Assert.IsNotNull(roster[seat], "seat " + seat + " is filled");
                Assert.IsTrue(roster[seat].IsBot, "seat " + seat + " is a CPU driver");
                Assert.IsFalse(roster[seat].IsLocal, "a CPU driver has no view");
                BallDriving driving = roster[seat].Player.GetComponentInChildren<BallDriving>(true);
                Assert.IsTrue(driving.enabled, "its scooter runs on the host");
                Assert.IsInstanceOf<BotDriver>(driving.DriveInput, "a bot is at its wheel");
            }

            // Back to the lobby: the CPU drivers go, and their seats are free
            SceneFlow.Current.ReturnToMenu();
            deadline = Time.realtimeSinceStartup + 60;
            while ((UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == GAME || State() != GameState.PlayerSelect)
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.PlayerSelect, State(), "back in the lobby");
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(1, host.PlayersIn, "the CPU drivers gave up their seats");
            Assert.IsNull(PlayerInstantiate.Instance.Roster[1], "seat 2 is empty again");
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Hosting_AloneWithBotsOff_NobodyIsAdded()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode(); // only a test itself may yield this
            yield return StartAlone(false);

            float deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Loading && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSeconds(2f);

            Assert.AreEqual(1, host.PlayersIn, "only the host sits");
            Assert.IsNull(PlayerInstantiate.Instance.Roster[1], "seat 2 stays empty");
            host.Leave();
            yield return null;
        }
    }
}

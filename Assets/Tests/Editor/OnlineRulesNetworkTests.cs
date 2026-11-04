using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The match rules online, on this computer (127.0.0.1): the host shares its mode and mutators, and the other machine
    /// plays them, physics included, whatever its own saved rules. This machine hosts with the game; the other is a
    /// session without the game. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineRulesNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7837;
        const string VERSION = "test";
        const float WAIT = 15f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
            HouseRules.Current = HouseRules.Playing = HouseRules.Normal;
            MatchPhysics.Reset();
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static IEnumerator Wait(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        static HouseRules Mutated => new HouseRules(MatchLength.Long, true, false, true, GameMode.Standard, 5, 150, true);

        [UnityTest]
        public IEnumerator Hosting_TheRulesReachTheOtherMachine_AndItPlaysThem()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION);
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(PlayerInstantiate.Instance.Roster[1] != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(other.Match, "joined");
            Assert.AreEqual(HouseRules.Normal.Pack(), other.Match.Rules, "normal until the host shares");

            host.Match.ShareRules(Mutated);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (other.Match.Rules != Mutated.Pack() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(Mutated, HouseRules.Unpack(other.Match.Rules), "the host's rules arrived");

            // The other machine's saved rules are normal; at Begin it plays the host's
            HouseRules.FollowHost(other.Match.Rules);
            Assert.AreEqual(Mutated, HouseRules.Playing);
            Assert.AreEqual(-6.5f, Physics.gravity.y, 0.001f);
            Assert.AreEqual(1.5f, MatchPhysics.SpeedScale);

            // A timed mode's state: who holds the potato, who's out, the fuse
            int version = other.Match.ModeVersion;
            ModeSnapshot potato = new ModeSnapshot(GameMode.HotPotato, 0b0011, ModeMatch.NONE, 1, ModeMatch.NONE, 0, 0b0001,
                new Vector3(4f, 5f, 6f), 12f, 20f);
            host.Match.ShareMode(potato);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (other.Match.ModeVersion == version && Time.realtimeSinceStartup < deadline)
                yield return null;
            ModeSnapshot shown = other.Match.ModeShared;
            Assert.AreEqual(GameMode.HotPotato, shown.Mode);
            Assert.AreEqual(1, shown.PotatoSeat);
            Assert.AreEqual(0b0001, shown.Out);
            Assert.AreEqual(ModeMatch.NONE, shown.CrownSeat);
            Assert.AreEqual(new Vector3(4f, 5f, 6f), shown.CrownSpot);
            Assert.AreEqual(12f, shown.FuseLeft, 0.001f);

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return Wait(1f);
            log.Dispose();
        }
    }
}

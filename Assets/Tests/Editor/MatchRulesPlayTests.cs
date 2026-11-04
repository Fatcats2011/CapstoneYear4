using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The match rules in a real local match: with the Golden Order off, the end of the last wave goes to the results with
    /// no golden round; a short match's waves are shorter. Enters Play Mode and loads both match scenes (a minute or so).
    /// No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class MatchRulesPlayTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const float LOADING = 90f;

        static readonly List<GameState> seen = new List<GameState>();

        static void Saw(GameState state) { seen.Add(state); }
        static bool IsNullReference(string problem) { return problem.Contains("NullReferenceException"); }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.StateApplied -= Saw;
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            SceneFlow.Current = null;
            HouseRules.Current = HouseRules.Playing = HouseRules.Normal;
            seen.Clear();
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        [UnityTest]
        public IEnumerator NoGoldenOrder_TheLastWaveEndsInTheResults()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            HouseRules.Current = new HouseRules(MatchLength.Short, true, false);

            // Into a match the quick way: a rematch skips the loading screen's A and the tutorial
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "in the match");
            yield return null;
            Assert.IsFalse(HouseRules.Playing.GoldenOrder, "the match plays by the rules picked");
            Assert.AreEqual(MatchLength.Short, HouseRules.Playing.Length);

            // Past the last wave
            GameManager.Instance.StateApplied += Saw;
            OrderManager orders = OrderManager.Instance;
            orders.wave = Mathf.Max(orders.maxEasy.Length, orders.maxMedium.Length, orders.maxHard.Length);
            orders.InitWave();

            // No golden round: the loading screen doesn't wait for anyone's A
            LogCollector log = new LogCollector();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Results && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Results, State(), "the results, without waiting for A; states seen: " + string.Join(", ", seen));
            for (float until = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < until;)
                yield return null; // past the camera's move and timeBeforeUI
            Assert.IsTrue(ResultsMenu.Instance.ResultsCameraOn, "the results camera");
            Assert.IsTrue(ResultsMenu.Instance.ScoresShown, "the scores came up");
            FinalOrder golden = Object.FindObjectOfType<FinalOrder>();
            Assert.IsTrue(golden == null || !golden.Live, "no golden order over the podium");
            log.Dispose();
            foreach (string problem in log.Problems)
                Assert.IsFalse(IsNullReference(problem), problem);
            CollectionAssert.DoesNotContain(seen, GameState.GoldenCutscene, "no golden round");
            CollectionAssert.DoesNotContain(seen, GameState.FinalPackage);
            Assert.IsNotNull(ResultsMenu.Instance, "the golden scene's results");
        }

        [UnityTest]
        public IEnumerator SixWaves_LastSixWaveLengths_AndEndAfterTheSixth()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            HouseRules.Current = new HouseRules(MatchLength.Short, true, false, true, GameMode.Standard, 6);

            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "in the match");
            yield return null;

            OrderManager orders = OrderManager.Instance;
            Assert.AreEqual(orders.WaveLength * 6, orders.TotalGameTime, 0.01f, "six waves' time");

            orders.wave = 5;
            orders.InitWave();
            Assert.IsFalse(orders.FinalOrderActive, "the sixth wave still plays");
            yield return null; // a frame of the sixth wave reads its quotas without throwing

            orders.wave = 6;
            orders.InitWave();
            Assert.IsTrue(orders.FinalOrderActive, "past the sixth wave");
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Results && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Results, State(), "no golden order: the results");
        }

        [UnityTest]
        public IEnumerator GoldRush_AScoreDrains_UntilADelivery()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            HouseRules.Current = new HouseRules(MatchLength.Normal, false, true, true, GameMode.GoldRush);

            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "in the match");
            yield return null;
            Assert.IsTrue(HouseRules.Playing.StealsOn, "Gold Rush locks steals on");

            OrderHandler player = ScoreManager.Instance.orderHandlers[0];
            player.Score = 100;
            for (float until = Time.realtimeSinceStartup + GoldRushRules.GRACE_SECONDS - 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(100, player.Score, "nothing drains during the grace");

            for (float until = Time.realtimeSinceStartup + 4f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.Less(player.Score, 100, "drained after the grace");
            Assert.Greater(player.Score, 85, "about two points a second");

            int before = player.Score;
            player.StartDrainClock(); // as a delivery does
            for (float until = Time.realtimeSinceStartup + 2f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(before, player.Score, "a delivery restarts the grace");
        }

        [UnityTest]
        public IEnumerator OneWave_EndsAfterTheFirst_IntoTheGoldenRound()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            HouseRules.Current = new HouseRules(MatchLength.Normal, true, true, true, GameMode.Standard, 1);

            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "in the match");
            yield return null;

            OrderManager orders = OrderManager.Instance;
            Assert.AreEqual(orders.WaveLength, orders.TotalGameTime, 0.01f, "one wave's time");
            Assert.IsFalse(orders.FinalOrderActive);

            orders.wave = 1;
            orders.InitWave();

            Assert.IsTrue(orders.FinalOrderActive, "past the only wave: the golden round is next");
        }
    }
}

using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Kings Crown, Infection and Hot Potato in a real local match with two players: the round runs on the Round Timer with
    /// no orders, and Infection and Hot Potato end early once decided, through the reel to the results, where everyone shows
    /// again. Enters Play Mode and loads both match scenes (a minute or so each). No lambda captures a local
    /// </summary>
    public class ModesPlayTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const float LOADING = 90f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            SceneFlow.Current = null;
            HouseRules.Current = HouseRules.Playing = HouseRules.Normal;
            ModeMatch.End();
            MatchPhysics.Reset();
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static HouseRules Rules(GameMode mode) => new HouseRules(MatchLength.Normal, true, true, true, mode, 3, 100, false, 2);

        // Two players into a match of a mode, the quick way (a rematch skips the loading screen's A)
        static IEnumerator IntoMatch(GameMode mode)
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            HouseRules.Current = Rules(mode);

            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
        }

        static IEnumerator UntilResults()
        {
            float deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Results && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        [UnityTest]
        public IEnumerator KingsCrown_PlaysOnTheRoundTimer_WithNoOrders_AndTheCrownShows()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            yield return IntoMatch(GameMode.KingsCrown);
            Assert.AreEqual(GameState.Begin, State(), "in the match");
            Assert.AreEqual(2, PlayerInstantiate.Instance.PlayerCount);

            Assert.IsNotNull(ModeMatch.Current, "a Kings Crown match");
            Assert.AreEqual(GameMode.KingsCrown, ModeMatch.Current.Mode);
            Assert.AreEqual(120f, OrderManager.Instance.TotalGameTime, 0.01f, "the 2-minute Round Timer");

            for (float until = Time.realtimeSinceStartup + 5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(0, OrderManager.Instance.ActiveOrders.Count, "no orders spawn");
            GameObject crown = GameObject.Find("Crown");
            Assert.IsNotNull(crown, "the crown shows");
            Assert.IsTrue(crown.activeSelf);
        }

        [UnityTest]
        public IEnumerator Infection_CatchingTheLastThief_EndsTheRoundIntoTheResults()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            yield return IntoMatch(GameMode.Infection);
            Assert.AreEqual(GameState.Begin, State(), "in the match");

            ModeMatch match = ModeMatch.Current;
            int hunter = match.FirstHunter;
            int thief = 1 - hunter;
            ModeReferee.Strike(OrderSync.HandlerIn(hunter), OrderSync.HandlerIn(thief));
            Assert.IsTrue(match.IsInfected(thief));
            Assert.AreEqual(TimedModeRules.CATCH_POINTS, OrderSync.HandlerIn(hunter).Score, 2, "the catch's points");

            yield return UntilResults();
            Assert.AreEqual(GameState.Results, State(), "decided: the round ends early, no golden round");
            Assert.IsNull(ModeMatch.Current, "over by the podium");
        }

        [UnityTest]
        public IEnumerator HotPotato_TheHolderGoesOut_AndShowsAgainOnThePodium()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            yield return IntoMatch(GameMode.HotPotato);
            Assert.AreEqual(GameState.Begin, State(), "in the match");

            ModeMatch match = ModeMatch.Current;
            float deadline = Time.realtimeSinceStartup + TimedModeRules.POTATO_DELAY + 3f;
            while (match.PotatoSeat == ModeMatch.NONE && Time.realtimeSinceStartup < deadline)
                yield return null;
            int holder = match.PotatoSeat;
            Assert.AreNotEqual(ModeMatch.NONE, holder, "the potato is lit");
            Assert.IsTrue(GameObject.Find("Cursed Package").activeSelf);

            match.BurnFuse(Time.time);
            yield return null;
            yield return null;
            Assert.IsTrue(match.IsOut(holder));
            Renderer ghost = Ghost(holder);
            Assert.IsFalse(ghost.enabled, "out: hidden");

            yield return UntilResults();
            Assert.AreEqual(GameState.Results, State(), "one left: the round ends early");
            Assert.IsTrue(ghost.enabled, "everyone shows on the podium");
        }

        static Renderer Ghost(int seat)
        {
            foreach (Renderer renderer in OrderSync.HandlerIn(seat).GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "Player_Model")
                    return renderer;
            foreach (Renderer renderer in PlayerInstantiate.Instance.Roster[seat].Player.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "Player_Model")
                    return renderer;
            return null;
        }
    }
}

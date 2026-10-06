using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The Steam features on this machine (SteamFeatures), with fake stores: a delivery in a local match unlocks its
    /// achievement here, a client's replay of the host's delivery counts for nothing, an online host's achievements are
    /// earned but left to OnlineAchievements, and each game state sets what friends see. In an empty Play Mode scene with
    /// a GameManager; each test enters Play Mode (a few seconds). No lambda here captures a local: after EnterPlayMode
    /// even assigning a captured local throws
    /// </summary>
    public class SteamFeaturesTests
    {
        /// <summary>Achievements unlocked here, in order</summary>
        class FakeAchievements : IAchievementStore
        {
            public readonly List<Achievement> Unlocked = new List<Achievement>();
            public bool Available { get { return true; } }
            public void Unlock(Achievement achievement) { Unlocked.Add(achievement); }
        }

        /// <summary>The Rich Presence keys set here</summary>
        class FakePresence : IPresence
        {
            public readonly Dictionary<string, string> Values = new Dictionary<string, string>();
            public bool Available { get { return true; } }
            public void Set(string key, string value) { Values[key] = value; }
        }

        /// <summary>The achievements SteamFeatures said were earned (each one's seat and achievement, in order)</summary>
        class EarnedRecorder
        {
            public readonly List<int> Seats = new List<int>();
            public readonly List<Achievement> Achievements = new List<Achievement>();

            public void Heard(int seat, Achievement achievement)
            {
                Seats.Add(seat);
                Achievements.Add(achievement);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameAuthority.Role = NetworkRole.Offline;
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            GameAuthority.Role = NetworkRole.Offline;
        }

        static IEnumerator Frame()
        {
            yield return null;
            yield return null;
        }

        static void NewEmptyScene()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // A game manager, then a frame or two: SteamFeatures watches it from its next Update
        static IEnumerator AddAGameManager()
        {
            new GameObject("GameManager").AddComponent<GameManager>();
            yield return Frame();
        }

        [UnityTest]
        public IEnumerator OfflineDelivery_InTheMatch_UnlocksFirstDelivery()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            yield return AddAGameManager();
            FakeAchievements achievements = new FakeAchievements();
            SteamFeatures.Instance.Use(achievements, new FakePresence());
            GameManager.Instance.ApplyGameState(GameState.MainLoop);

            FeatSync.Deliver(0, false);

            CollectionAssert.AreEqual(new[] { Achievement.FirstDelivery }, achievements.Unlocked);
        }

        [UnityTest]
        public IEnumerator ReplayedDelivery_RaisesNoFeat()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            yield return AddAGameManager();
            FakeAchievements achievements = new FakeAchievements();
            SteamFeatures.Instance.Use(achievements, new FakePresence());
            EarnedRecorder earned = new EarnedRecorder();
            SteamFeatures.Earned += earned.Heard;
            GameManager.Instance.ApplyGameState(GameState.MainLoop);
            GameAuthority.Role = NetworkRole.Client;

            FeatSync.Deliver(1, false);

            SteamFeatures.Earned -= earned.Heard;
            CollectionAssert.IsEmpty(achievements.Unlocked);
            CollectionAssert.IsEmpty(earned.Achievements, "a client never decides");
        }

        [UnityTest]
        public IEnumerator OnlineHostDelivery_IsEarnedButNotUnlockedHere()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            yield return AddAGameManager();
            FakeAchievements achievements = new FakeAchievements();
            SteamFeatures.Instance.Use(achievements, new FakePresence());
            EarnedRecorder earned = new EarnedRecorder();
            SteamFeatures.Earned += earned.Heard;
            GameManager.Instance.ApplyGameState(GameState.MainLoop);
            GameAuthority.Role = NetworkRole.Host;

            FeatSync.Deliver(2, false);

            SteamFeatures.Earned -= earned.Heard;
            CollectionAssert.AreEqual(new[] { 2 }, earned.Seats);
            CollectionAssert.AreEqual(new[] { Achievement.FirstDelivery }, earned.Achievements);
            CollectionAssert.IsEmpty(achievements.Unlocked, "online, OnlineAchievements unlocks");
        }

        [UnityTest]
        public IEnumerator States_SetRichPresence()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            yield return AddAGameManager();
            FakePresence presence = new FakePresence();
            SteamFeatures.Instance.Use(new FakeAchievements(), presence);

            GameManager.Instance.ApplyGameState(GameState.Menu);
            Assert.AreEqual("#Menus", presence.Values[PresenceRules.DISPLAY]);

            GameManager.Instance.ApplyGameState(GameState.MainLoop);
            Assert.AreEqual("#DeliveringSolo", presence.Values[PresenceRules.DISPLAY], "no roster here: no players");
            Assert.AreEqual("0", presence.Values[PresenceRules.PLAYERS]);

            GameManager.Instance.ApplyGameState(GameState.Loading);
            Assert.AreEqual("#DeliveringSolo", presence.Values[PresenceRules.DISPLAY], "a load changes nothing");
        }

        [UnityTest]
        public IEnumerator ANewGameManager_IsWatchedToo()
        {
            NewEmptyScene();
            yield return new EnterPlayMode();
            yield return AddAGameManager();
            FakePresence presence = new FakePresence();
            SteamFeatures.Instance.Use(new FakeAchievements(), presence);

            Object.Destroy(GameManager.Instance.gameObject);
            yield return Frame();
            new GameObject("GameManager").AddComponent<GameManager>();
            yield return Frame();
            GameManager.Instance.ApplyGameState(GameState.Results);

            Assert.AreEqual("#Results", presence.Values[PresenceRules.DISPLAY]);
        }
    }
}

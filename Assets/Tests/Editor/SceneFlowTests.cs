using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// A scene flow that only counts what the game asked of it
    /// </summary>
    class FakeSceneFlow : ISceneFlow
    {
        public int GameLoads, FinalOrderLoads, MenuReturns, Confirms;

        public event Action OnReturnToMenu;
        public bool WaitingForConfirm { get; set; }
        public bool LeavingForMenu { get; set; }

        public void LoadGameScene() { GameLoads++; }
        public void LoadFinalOrderScene() { FinalOrderLoads++; }
        public void ReturnToMenu() { MenuReturns++; OnReturnToMenu?.Invoke(); }
        public void ConfirmLoad() { Confirms++; }
    }

    public class SceneFlowTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
            Time.timeScale = 1f;
            SceneManager.instance = null;
            GameManager.instance = null;
            SoundManager.instance = null;
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        [Test]
        public void Current_WhenNothingElseIsSet_IsTheLocalLoader()
        {
            SceneManager loader = objects.Add<SceneManager>();
            SceneManager.instance = loader;

            Assert.AreSame(loader, SceneFlow.Current);
        }

        [Test]
        public void Current_SetToAnotherFlow_IsThatFlowUntilCleared()
        {
            SceneManager loader = objects.Add<SceneManager>();
            SceneManager.instance = loader;
            FakeSceneFlow online = new FakeSceneFlow();

            SceneFlow.Current = online;
            Assert.AreSame(online, SceneFlow.Current, "while set");

            SceneFlow.Current = null;
            Assert.AreSame(loader, SceneFlow.Current, "after clearing");
        }

        [Test]
        public void LocalLoader_ReturnToMenu_TellsListenersTheGameIsGoingBackToTheMenu()
        {
            ISceneFlow loader = objects.Add<SceneManager>();
            int returns = 0;
            loader.OnReturnToMenu += () => returns++;

            loader.ReturnToMenu();

            Assert.AreEqual(1, returns);
        }

        [Test]
        public void LocalLoader_WaitingForConfirm_IsTheLoadingScreenAskingForA()
        {
            SceneManager loader = objects.Add<SceneManager>();
            ISceneFlow flow = loader;

            Assert.IsFalse(flow.WaitingForConfirm, "before the loading screen asks");
            loader.enableConfirm = true;
            Assert.IsTrue(flow.WaitingForConfirm, "while it asks");
        }

        [Test]
        public void GameplayScripts_ReachTheSceneLoaderOnlyThroughSceneFlow()
        {
            string[] offenders = Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/OUTDATED/"))
                .Where(f => Path.GetFileName(f) != "SceneManager.cs" && Path.GetFileName(f) != "ISceneFlow.cs")
                .Where(f => Regex.IsMatch(File.ReadAllText(f), @"\bSceneManager\.Instance\b"))
                .ToArray();

            CollectionAssert.IsEmpty(offenders);
        }

        [Test]
        public void ResultsScreen_ReturnButton_GoesBackToTheMenuThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            ResultsUI results = objects.Add<ResultsUI>();

            results.ResetGame();

            Assert.AreEqual(1, flow.MenuReturns);
        }

        [Test]
        public void ResultsScreen_OnlineOnAClient_WaitsForTheHostToTakeEveryoneBack()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>(); // no players to reset
            ResultsMenu results = objects.Add<ResultsMenu>();
            results.displayText = new TMPro.TMP_Text[0];
            results.canQuit = true;

            GameAuthority.Role = NetworkRole.Client;
            results.ConfirmMenu();
            Assert.AreEqual(0, flow.MenuReturns, "a client waits: the host takes everyone back");

            GameAuthority.Role = NetworkRole.Host;
            results.ConfirmMenu();
            Assert.AreEqual(1, flow.MenuReturns, "the host goes, and takes everyone");
        }

        [Test]
        public void PauseMenu_MainMenu_RestartsTheClockAndGoesBackThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            SoundManager.instance = objects.Add<SoundManager>(); // no mixer snapshots, so the music change does nothing
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
            PauseMenu pause = objects.Add<PauseMenu>();
            Time.timeScale = 0f; // paused

            pause.ReturnToMenu();

            Assert.AreEqual(1f, Time.timeScale, "clock running again");
            Assert.AreEqual(1, flow.MenuReturns);
        }

        [Test]
        public void EndOfTheLastWave_LoadsTheGoldenOrderSceneThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            OrderManager orders = objects.Add<OrderManager>();

            IEnumerator linger = orders.PostGameClarity(false);
            linger.MoveNext(); // the half-speed pause
            linger.MoveNext(); // then the golden-order scene

            Assert.AreEqual(1, flow.FinalOrderLoads);
        }

        [Test]
        public void LoadingScreen_EveryoneConfirmed_ConfirmsThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            PlayerInstantiate players = objects.Add<PlayerInstantiate>();
            players.Roster.JoinLocal(objects.Add<PlayerInput>());
            players.playerLoadingConfirm = new[] { true, false, false, false };

            players.CheckLoadingConfirmCount();

            Assert.AreEqual(1, flow.Confirms);
        }

        [Test]
        public void ReadyUpCountdown_StartsTheMatchThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            SceneManager loader = objects.Add<SceneManager>();

            loader.StartMatch(); // what OnReadiedUp calls when the countdown ends

            Assert.AreEqual(1, flow.GameLoads);
        }

        [Test]
        public void OrderHandler_EnabledThenDisabled_LeavesTheFlowItListenedTo()
        {
            GameManager.instance = objects.Add<GameManager>();
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            OrderHandler handler = objects.Add<OrderHandler>();
            handler.ball = objects.Add<BallDriving>();

            handler.OnEnable();
            Assert.AreEqual(1, Reflect.HandlerCount(flow, nameof(FakeSceneFlow.OnReturnToMenu), handler), "while enabled");

            SceneFlow.Current = null; // e.g. an online session ended while the handler was enabled
            handler.OnDisable();
            Assert.AreEqual(0, Reflect.HandlerCount(flow, nameof(FakeSceneFlow.OnReturnToMenu), handler), "after disabling");
        }
    }
}

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
            SceneFlow.Current = null;
            Time.timeScale = 1f;
            Reflect.SetSingleton<SceneManager>(null);
            Reflect.SetSingleton<GameManager>(null);
            Reflect.SetSingleton<SoundManager>(null);
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
        }

        [Test]
        public void Current_WhenNothingElseIsSet_IsTheLocalLoader()
        {
            SceneManager loader = objects.Add<SceneManager>();
            Reflect.SetSingleton(loader);

            Assert.AreSame(loader, SceneFlow.Current);
        }

        [Test]
        public void Current_SetToAnotherFlow_IsThatFlowUntilCleared()
        {
            SceneManager loader = objects.Add<SceneManager>();
            Reflect.SetSingleton(loader);
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
            Reflect.SetField(loader, "enableConfirm", true);
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

            Reflect.Invoke(results, "ResetGame");

            Assert.AreEqual(1, flow.MenuReturns);
        }

        [Test]
        public void PauseMenu_MainMenu_RestartsTheClockAndGoesBackThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            Reflect.SetSingleton(objects.Add<SoundManager>()); // no mixer snapshots, so the music change does nothing
            Reflect.SetSingleton(objects.Add<PlayerInstantiate>());
            PauseMenu pause = objects.Add<PauseMenu>();
            Time.timeScale = 0f; // paused

            Reflect.Invoke(pause, "ReturnToMenu");

            Assert.AreEqual(1f, Time.timeScale, "clock running again");
            Assert.AreEqual(1, flow.MenuReturns);
        }

        [Test]
        public void EndOfTheLastWave_LoadsTheGoldenOrderSceneThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            OrderManager orders = objects.Add<OrderManager>();

            IEnumerator linger = (IEnumerator)Reflect.Invoke(orders, "PostGameClarity", false);
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
            Reflect.SetField(players, "playerLoadingConfirm", new[] { true, false, false, false });

            players.CheckLoadingConfirmCount();

            Assert.AreEqual(1, flow.Confirms);
        }

        [Test]
        public void ReadyUpCountdown_StartsTheMatchThroughTheSceneFlow()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            SceneManager loader = objects.Add<SceneManager>();

            Reflect.Invoke(loader, "StartMatch"); // what OnReadiedUp calls when the countdown ends

            Assert.AreEqual(1, flow.GameLoads);
        }

        [Test]
        public void OrderHandler_EnabledThenDisabled_LeavesTheFlowItListenedTo()
        {
            Reflect.SetSingleton(objects.Add<GameManager>());
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            OrderHandler handler = objects.Add<OrderHandler>();
            Reflect.SetField(handler, "ball", objects.Add<BallDriving>());

            Reflect.Invoke(handler, "OnEnable");
            Assert.AreEqual(1, Reflect.HandlerCount(flow, "OnReturnToMenu", handler), "while enabled");

            SceneFlow.Current = null; // e.g. an online session ended while the handler was enabled
            Reflect.Invoke(handler, "OnDisable");
            Assert.AreEqual(0, Reflect.HandlerCount(flow, "OnReturnToMenu", handler), "after disabling");
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    // The local loader's held loads, as the online flow sees them
    class FakeMatchLoader : IMatchLoader
    {
        public readonly List<MatchScene> HeldLoads = new List<MatchScene>();
        public int Shows;

        public event Action HeldSceneReady;
        public event Action SceneUp;

        public void LoadHeld(MatchScene scene) { HeldLoads.Add(scene); }
        public void ShowHeld() { Shows++; }

        public void RaiseHeldSceneReady() { HeldSceneReady?.Invoke(); }
        public void RaiseSceneUp() { SceneUp?.Invoke(); }
    }

    // A session's match, as the online flow sees it: what this machine sent, and messages to raise
    class FakeMatchLink : IMatchLink
    {
        public bool IsHost { get; set; }
        public readonly List<MatchScene> Loads = new List<MatchScene>();
        public readonly List<MatchScene> Shows = new List<MatchScene>();
        public readonly List<MatchScene> Reported = new List<MatchScene>();
        public int Returns;

        public event Action<MatchScene> LoadRequested;
        public event Action<MatchScene> ShowRequested;
        public event Action ReturnRequested;
        public event Action<ulong, MatchScene> MachineLoaded;

        public void RequestLoad(MatchScene scene) { Loads.Add(scene); }
        public void RequestShow(MatchScene scene) { Shows.Add(scene); }
        public void RequestReturn() { Returns++; }
        public void ReportLoaded(MatchScene scene) { Reported.Add(scene); }

        public void RaiseLoadRequested(MatchScene scene) { LoadRequested?.Invoke(scene); }
        public void RaiseShowRequested(MatchScene scene) { ShowRequested?.Invoke(scene); }
        public void RaiseReturnRequested() { ReturnRequested?.Invoke(); }
        public void RaiseMachineLoaded(ulong machine, MatchScene scene) { MachineLoaded?.Invoke(machine, scene); }
    }

    /// <summary>
    /// The scene flow while online, with a fake loader and a fake session:
    /// - The host starts every load on every machine, and shows the scene once every machine has it loaded or has left.
    /// - A client loads what the host asks, says when it's ready, shows it when told, and is "changing" until the new
    ///   scene is up.
    /// - Only the host starts loads. The host takes everyone back to the menu; a client going back alone leaves the
    ///   session first.
    /// - The loading screen never waits for A.
    /// </summary>
    public class OnlineSceneFlowTests
    {
        const ulong HOST = 0;
        const ulong CLIENT = 1;

        FakeSceneFlow local;
        FakeMatchLoader loader;
        FakeMatchLink link;
        OnlineSceneFlow flow;
        int leaves;
        int sceneChanges;
        float now;            // the clock the flow times loads by
        List<string> reports; // the load times it reported

        [SetUp]
        public void SetUp()
        {
            local = new FakeSceneFlow();
            loader = new FakeMatchLoader();
            link = new FakeMatchLink();
            now = 0f;
            flow = new OnlineSceneFlow(local, loader, Now);
            leaves = 0;
            sceneChanges = 0;
            reports = new List<string>();
            flow.SceneChanged += CountSceneChange;
            flow.LoadTimed += reports.Add;
        }

        float Now() { return now; }

        void CountSceneChange() { sceneChanges++; }

        void Leave() { leaves++; }

        static IEnumerable<ulong> BothMachines() { return new[] { HOST, CLIENT }; }

        void LinkAsHost()
        {
            link.IsHost = true;
            flow.Link(link, BothMachines, HOST, Leave);
        }

        void LinkAsClient()
        {
            link.IsHost = false;
            flow.Link(link, BothMachines, CLIENT, Leave);
        }

        [Test]
        public void Host_LoadingTheGame_AsksEveryMachine_AndHoldsItHereToo()
        {
            LinkAsHost();

            flow.LoadGameScene();

            CollectionAssert.AreEqual(new[] { MatchScene.Game }, link.Loads, "every machine is asked");
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, loader.HeldLoads, "it loads here, held behind the loading screen");
            Assert.AreEqual(0, loader.Shows, "nothing shown yet");
            CollectionAssert.IsEmpty(link.Shows);
        }

        [Test]
        public void Host_ShowsTheScene_OnlyOnceEveryMachineHasItLoaded()
        {
            LinkAsHost();
            flow.LoadGameScene();

            loader.RaiseHeldSceneReady();
            Assert.AreEqual(0, loader.Shows, "the other machine is still loading");

            link.RaiseMachineLoaded(CLIENT, MatchScene.Game);
            Assert.AreEqual(1, loader.Shows, "shown here");
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, link.Shows, "and on every machine");

            loader.RaiseSceneUp();
            Assert.AreEqual(0, sceneChanges, "the host holds no states for its scene");
        }

        [Test]
        public void Host_AReportForAnotherScene_DoesntCount()
        {
            LinkAsHost();
            flow.LoadGameScene();
            loader.RaiseHeldSceneReady();

            link.RaiseMachineLoaded(CLIENT, MatchScene.FinalOrder);

            Assert.AreEqual(0, loader.Shows);
        }

        [Test]
        public void Host_AMachineThatLeavesDuringTheLoad_IsNoLongerWaitedFor()
        {
            LinkAsHost();
            flow.LoadGameScene();
            loader.RaiseHeldSceneReady();

            flow.MachineLeft(CLIENT);

            Assert.AreEqual(1, loader.Shows, "the match starts without it");
        }

        [Test]
        public void Host_TheGoldenRoundsScene_LoadsTheSameWay()
        {
            LinkAsHost();

            flow.LoadFinalOrderScene();
            link.RaiseMachineLoaded(CLIENT, MatchScene.FinalOrder);
            loader.RaiseHeldSceneReady();

            CollectionAssert.AreEqual(new[] { MatchScene.FinalOrder }, link.Loads);
            CollectionAssert.AreEqual(new[] { MatchScene.FinalOrder }, loader.HeldLoads);
            Assert.AreEqual(1, loader.Shows);
        }

        [Test]
        public void Host_ReturnToMenu_TakesEveryoneBack()
        {
            LinkAsHost();

            flow.ReturnToMenu();

            Assert.AreEqual(1, link.Returns, "every machine");
            Assert.AreEqual(1, local.MenuReturns, "and this one");
            Assert.AreEqual(0, leaves, "the host stays in its session");
        }

        [Test]
        public void Client_NeverStartsALoadItself()
        {
            LinkAsClient();

            flow.LoadGameScene();
            flow.LoadFinalOrderScene();

            CollectionAssert.IsEmpty(link.Loads, "the host starts every load");
            CollectionAssert.IsEmpty(loader.HeldLoads);
        }

        [Test]
        public void Client_LoadsWhatTheHostAsks_AndSaysWhenItsReady()
        {
            LinkAsClient();

            link.RaiseLoadRequested(MatchScene.Game);
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, loader.HeldLoads, "held behind the loading screen");
            CollectionAssert.IsEmpty(link.Reported, "not loaded yet");

            loader.RaiseHeldSceneReady();
            CollectionAssert.AreEqual(new[] { MatchScene.Game }, link.Reported, "the host hears it's ready");
            Assert.AreEqual(0, loader.Shows, "until the host says");
        }

        [Test]
        public void Client_ShowsWhenTheHostSays_AndIsChangingUntilTheSceneIsUp()
        {
            LinkAsClient();
            link.RaiseLoadRequested(MatchScene.Game);
            loader.RaiseHeldSceneReady();

            link.RaiseShowRequested(MatchScene.Game);
            Assert.AreEqual(1, loader.Shows, "shown");
            Assert.IsTrue(flow.Changing, "the scene is coming up");
            Assert.AreEqual(0, sceneChanges);

            loader.RaiseSceneUp();
            Assert.IsFalse(flow.Changing);
            Assert.AreEqual(1, sceneChanges, "the held states can go");
        }

        [Test]
        public void Client_TheHostTakingEveryoneBack_BringsItBackToo()
        {
            LinkAsClient();

            link.RaiseReturnRequested();
            Assert.AreEqual(1, local.MenuReturns, "back to the menu");
            Assert.IsTrue(flow.Changing, "the menu is coming up");
            Assert.AreEqual(0, leaves, "still in the session");

            loader.RaiseSceneUp();
            Assert.IsFalse(flow.Changing);
            Assert.AreEqual(1, sceneChanges);
        }

        [Test]
        public void Client_ReturnToMenu_LeavesTheSessionFirst()
        {
            LinkAsClient();

            flow.ReturnToMenu();

            Assert.AreEqual(1, leaves, "it can't come back into the host's match");
            Assert.AreEqual(1, local.MenuReturns);
            Assert.AreEqual(0, link.Returns, "only the host takes everyone");
        }

        [Test]
        public void Unlinked_ItFollowsNoSessionAnyMore()
        {
            LinkAsClient();
            flow.Unlink();

            link.RaiseLoadRequested(MatchScene.Game);
            loader.RaiseHeldSceneReady();
            flow.LoadGameScene();

            CollectionAssert.IsEmpty(loader.HeldLoads);
            CollectionAssert.IsEmpty(link.Reported);
        }

        [Test]
        public void ReturnToMenu_ListenersHearTheLocalLoader()
        {
            int heard = 0;
            flow.OnReturnToMenu += () => heard++;

            flow.ReturnToMenu();

            Assert.AreEqual(1, local.MenuReturns, "not in a session: straight back");
            Assert.AreEqual(1, heard, "listeners of the online flow hear the local loader");
        }

        [Test]
        public void TheLoadingScreen_NeverWaitsForA()
        {
            FakeSceneFlow waiting = new FakeSceneFlow { WaitingForConfirm = true };
            OnlineSceneFlow online = new OnlineSceneFlow(waiting, loader, Now);

            Assert.IsFalse(online.WaitingForConfirm);
            online.ConfirmLoad();
            Assert.AreEqual(0, waiting.Confirms);
        }

        [Test]
        public void LeavingForMenu_IsTheLocalLoaders()
        {
            Assert.IsFalse(flow.LeavingForMenu);

            local.LeavingForMenu = true; // a match scene shown only on the way back to the menu

            Assert.IsTrue(flow.LeavingForMenu, "online too, it starts nothing");
        }

        [Test]
        public void Client_TimesItsLoad_AndReportsItOnceTheSceneIsUp()
        {
            LinkAsClient();
            now = 10f; link.RaiseLoadRequested(MatchScene.Game);
            now = 13f; flow.Tick();                         // a 3 s frame
            now = 14f; loader.RaiseHeldSceneReady();        // ready after 4 s
            link.RaiseShowRequested(MatchScene.Game);
            loader.RaiseSceneUp();
            now = 16.5f; flow.Tick();                       // the activation: 3.5 s
            now = 16.6f; flow.Tick();

            CollectionAssert.AreEqual(new[] { LoadWatch.Report(MatchScene.Game, 4f, 3.5f) }, reports);
        }

        [Test]
        public void Host_ReportsHowLongItsOwnLoadTook_NotTheWaitForOthers()
        {
            LinkAsHost();
            now = 10f; flow.LoadGameScene();
            now = 11f; flow.Tick();
            now = 12f; loader.RaiseHeldSceneReady();        // ready here after 2 s
            for (now = 12f; now <= 30f; now += 1f)
                flow.Tick();                                // the other machine takes its time
            link.RaiseMachineLoaded(CLIENT, MatchScene.Game);
            loader.RaiseSceneUp();
            now = 31.5f; flow.Tick();
            now = 32f; flow.Tick();

            CollectionAssert.AreEqual(new[] { LoadWatch.Report(MatchScene.Game, 2f, 1.5f) }, reports);
        }

        [Test]
        public void Client_SessionEndsMidLoad_ReportsNothing()
        {
            LinkAsClient();
            now = 10f; link.RaiseLoadRequested(MatchScene.Game);
            flow.Unlink();
            now = 20f; loader.RaiseHeldSceneReady();
            loader.RaiseSceneUp();
            now = 21f; flow.Tick();
            now = 22f; flow.Tick();

            CollectionAssert.IsEmpty(reports);
        }
    }
}

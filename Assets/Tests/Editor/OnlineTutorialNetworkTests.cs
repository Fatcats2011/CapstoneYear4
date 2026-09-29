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
    /// The tutorial in an online match on this computer (127.0.0.1):
    /// - Hosting: this machine hosts with the game. Its player starts at the start of their lane, both lanes' tutorial
    ///   orders come out, and the first wave begins once every machine's player has finished, or left. The other
    ///   machine is a session without the game, which reports its player when the test says.
    /// - Joining: this machine joins with the game. Its player does the tutorial here, and the host (a session without
    ///   the game) hears when they finish.
    /// Loads the game scene: a minute or two each. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineTutorialNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7801;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

        /// <summary>
        /// Remembers the order changes a machine heard from the host
        /// </summary>
        class ChangeRecorder
        {
            public readonly List<OrderChange> Changes = new List<OrderChange>();

            public ChangeRecorder(OnlineMatch match)
            {
                match.OrderReceived += OnChange;
            }

            void OnChange(OrderChange change)
            {
                Changes.Add(change);
            }
        }

        /// <summary>
        /// Remembers the last seat message the host heard (who sent it, which seat), and how many it heard
        /// </summary>
        class SeatRecorder
        {
            public ulong Machine;
            public int Seat = -1;
            public int Count;

            public void Heard(ulong machine, int seat)
            {
                Machine = machine;
                Seat = seat;
                Count++;
            }
        }

        // Which machines told the host they have the game loaded
        class ReportRecorder
        {
            public readonly List<ulong> Machines = new List<ulong>();

            public ReportRecorder(OnlineMatch match)
            {
                match.MachineLoaded += OnLoaded;
            }

            void OnLoaded(ulong machine, MatchScene scene)
            {
                if (scene == MatchScene.Game)
                    Machines.Add(machine);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
            OrderSync.Reset();
            TutorialSync.Reset();
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static string ActiveScene()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static BallDriving ScooterIn(int slot)
        {
            return Slot(slot).Player.GetComponentInChildren<BallDriving>(true);
        }

        // The orders of the scooter in a seat, here
        static OrderHandler Handler(int seat)
        {
            return Slot(seat).Player.GetComponentInChildren<OrderHandler>(true);
        }

        static TutorialHandler TutorialHandlerOf(int seat)
        {
            return Slot(seat).Player.GetComponentInChildren<TutorialHandler>();
        }

        // Where the players start: the start of their seat's tutorial lane
        static GameObject[] TutorialStarts()
        {
            return (GameObject[])Reflect.GetField(SpawnManager.Instance, "gameSpawnPositions");
        }

        // A player on this machine drives their ball there and stops
        static void PutBallAt(int seat, Vector3 spot)
        {
            Rigidbody ball = Slot(seat).Player.GetComponentInChildren<Rigidbody>();
            ball.velocity = Vector3.zero;
            ball.position = spot;
            ball.transform.position = spot;
        }

        // Where a player who finished the tutorial waits: a city respawn point far from every order's beacons
        static Vector3 CitySpot()
        {
            Vector3 best = Vector3.zero;
            float bestGap = -1f;
            Order[] orders = Object.FindObjectsOfType<Order>(true);
            foreach (RespawnPoint point in Object.FindObjectsOfType<RespawnPoint>())
            {
                float gap = float.MaxValue;
                foreach (Order order in orders)
                {
                    if (order.PickupPoint != null)
                        gap = Mathf.Min(gap, Vector3.Distance(point.transform.position, order.PickupPoint.position));
                    if (order.DropoffPoint != null)
                        gap = Mathf.Min(gap, Vector3.Distance(point.transform.position, order.DropoffPoint.position));
                }
                if (gap > bestGap)
                {
                    bestGap = gap;
                    best = point.transform.position;
                }
            }
            return best;
        }

        // A player on this machine finishes the tutorial and waits in the city, as a player who drove out of it does
        // (the tutorial area goes a second into the first wave)
        static void FinishTutorialInTheCity(int seat)
        {
            PutBallAt(seat, CitySpot());
            TutorialHandlerOf(seat).TeachHandler(TutorialType.Final);
        }

        // The order a cutout holds until a player steals it
        static Order CutoutOrder(CutoutHandler cutout)
        {
            return (Order)Reflect.GetField(cutout, "order");
        }

        // The barrier behind a cutout, which blocks its lane until the cutout is stolen from
        static BoxCollider Barrier(CutoutHandler cutout)
        {
            return ((GameObject)Reflect.GetField(cutout, "barrier")).GetComponent<BoxCollider>();
        }

        // The middle of a cutout's light, where a boosting scooter steals from it
        static Vector3 CutoutCenter(CutoutHandler cutout)
        {
            return cutout.GetComponent<Collider>().bounds.center;
        }

        [UnityTest]
        public IEnumerator Hosting_TheTutorialRunsOnline_AndTheFirstWaveWaitsForEveryMachinesPlayer()
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
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            ChangeRecorder heard = new ChangeRecorder(other.Match);

            // The match starts on both machines: the opening cutscene puts this machine's player at the start of their lane
            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, State(), "the opening cutscene");
            Assert.Less(Vector3.Distance(TutorialStarts()[0].transform.position, ScooterIn(0).Sphere.transform.position), 1.5f,
                "this machine's scooter at the start of its lane");

            // The tutorial: both lanes' tutorial orders come out
            deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "the tutorial, after the opening cutscene");
            Order[] lessons = (Order[])Reflect.GetField(OrderManager.Instance, "tutorialOrders");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(heard.Changes.Contains(OrderChange.Spawn(lessons[0].Key, false)) && heard.Changes.Contains(OrderChange.Spawn(lessons[1].Key, false)))
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Changes.Contains(OrderChange.Spawn(lessons[1].Key, false)), "the other machine's lane has its tutorial order");
            Assert.IsTrue(heard.Changes.Contains(OrderChange.Spawn(lessons[0].Key, false)), "so does this machine's");

            // The cutouts: asking for another lane's does nothing
            CutoutHandler myCutout = CutoutHandler.InSeat(0), theirCutout = CutoutHandler.InSeat(1);
            Order myOrder = CutoutOrder(myCutout), theirOrder = CutoutOrder(theirCutout);
            other.Match.AskCutout(0);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsFalse(myCutout.IsOpen, "not their lane's cutout");

            // The other machine's player boosts into theirs: their machine asks, and the host hands them its order
            other.Match.AskCutout(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Changes.Contains(OrderChange.Pickup(theirOrder.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Changes.Contains(OrderChange.Pickup(theirOrder.Key, 1)), "the other machine hears its player took it");
            Assert.Less(heard.Changes.IndexOf(OrderChange.Spawn(theirOrder.Key, false)), heard.Changes.IndexOf(OrderChange.Pickup(theirOrder.Key, 1)),
                "it comes out of the cutout, then onto their scooter");
            Assert.AreSame(Handler(1), theirOrder.PlayerHolding, "on the other machine's scooter here");
            Assert.IsFalse(Barrier(theirCutout).enabled, "their lane's barrier is down here");

            // This machine's player boosts into theirs: the host takes it at once, as in a local match
            ScooterIn(0).AutoBoost();
            PutBallAt(0, CutoutCenter(myCutout));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Changes.Contains(OrderChange.Pickup(myOrder.Key, 0)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Changes.Contains(OrderChange.Pickup(myOrder.Key, 0)), "the other machine hears this machine's player took theirs");
            Assert.IsTrue(myCutout.IsOpen, "this machine's lane is open");

            // The other machine's player finishes first: the first wave waits for this machine's
            other.Match.ReportLearnt(1);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "this machine's player is still in the tutorial");
            other.Match.ReportLearnt(0);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "a report for a seat that isn't theirs counts for nothing");

            // This machine's player drives out into the city: everyone has finished
            FinishTutorialInTheCity(0);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "everyone has finished: the first wave");

            // The tutorial orders go at the first wave
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(heard.Changes.Contains(OrderChange.Erase(lessons[0].Key)) && heard.Changes.Contains(OrderChange.Erase(lessons[1].Key)))
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Changes.Contains(OrderChange.Erase(lessons[0].Key)) && heard.Changes.Contains(OrderChange.Erase(lessons[1].Key)),
                "the tutorial orders go");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Hosting_APlayerWhoLeavesMidTutorial_DoesntHoldUpTheOthers()
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
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");

            // The match starts on both machines, with the tutorial
            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, State(), "the opening cutscene");
            Assert.Less(Vector3.Distance(TutorialStarts()[0].transform.position, ScooterIn(0).Sphere.transform.position), 1.5f,
                "this machine's scooter at the start of its lane");
            deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "the tutorial");

            // This machine's player finishes: the first wave waits for the other machine's
            FinishTutorialInTheCity(0);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "waits for the other machine's player");

            // They leave before finishing: everyone left has finished
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "everyone left has finished: the first wave");

            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_ThisMachinesPlayerDoesTheTutorial_AndTheHostHearsWhenTheyFinish()
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

            OnlineSession host = OnlineSession.Create(VERSION); // another machine hosting: a session without the game
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession mine = OnlineSession.Create(VERSION);
            OnlineGame.Attach(mine);
            mine.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && Slot(1).IsLocal && Slot(0) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player moved to seat 2");
            ReportRecorder reports = new ReportRecorder(host.Match);

            // The host's match comes up here: the opening cutscene puts this machine's player at the start of their lane
            host.Match.RequestLoad(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Match.RequestShow(MatchScene.Game);
            host.Match.SendState(GameState.StartingCutscene);
            host.Match.SendState(GameState.MainLoop);
            deadline = Time.realtimeSinceStartup + 60;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, State(), "the host's match is up here");
            Assert.Less(Vector3.Distance(TutorialStarts()[1].transform.position, ScooterIn(1).Sphere.transform.position), 1.5f,
                "this machine's scooter at the start of seat 2's lane");

            // The host starts the tutorial: it runs here
            host.Match.SendState(GameState.Tutorial);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "the tutorial");
            Assert.IsFalse(TutorialHandlerOf(1).HasLearnt, "the tutorial runs here");

            // This machine's cutout holds its order, and its barrier is up
            CutoutHandler myCutout = CutoutHandler.InSeat(1), hostsCutout = CutoutHandler.InSeat(0);
            Order myOrder = CutoutOrder(myCutout), hostsOrder = CutoutOrder(hostsCutout);
            Assert.IsTrue(myOrder.StealActive, "this machine's cutout holds its order");
            Assert.IsTrue(Barrier(myCutout).enabled, "its barrier is up");

            // This machine's player boosts into it: the barrier opens at once, and the host is asked for the order
            SeatRecorder asked = new SeatRecorder();
            host.Match.CutoutAsked += asked.Heard;
            ScooterIn(1).AutoBoost();
            PutBallAt(1, CutoutCenter(myCutout));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(mine.Network.LocalClientId, asked.Machine, "this machine asks");
            Assert.AreEqual(1, asked.Seat, "for its lane's cutout");
            Assert.IsFalse(Barrier(myCutout).enabled, "open at once, before the host answers");
            Assert.IsNull(myOrder.PlayerHolding, "the host hands out the order");

            // The host hands it out: it rides on this machine's scooter
            host.Match.SendOrder(OrderChange.Spawn(myOrder.Key, false));
            host.Match.SendOrder(OrderChange.Pickup(myOrder.Key, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (myOrder.PlayerHolding != Handler(1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), myOrder.PlayerHolding, "on this machine's scooter");
            Assert.IsFalse(myOrder.StealActive, "the cutout doesn't hold it any more");

            // The host's player steals theirs: that cutout opens here too
            host.Match.SendOrder(OrderChange.Spawn(hostsOrder.Key, false));
            host.Match.SendOrder(OrderChange.Pickup(hostsOrder.Key, 0));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!hostsCutout.IsOpen && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(hostsCutout.IsOpen, "the host's lane's cutout opens here");
            Assert.IsFalse(Barrier(hostsCutout).enabled, "its barrier is down here");

            // This machine's player drives out into the city: the host hears they finished, and starts the first wave
            SeatRecorder learnt = new SeatRecorder();
            host.Match.MachineLearnt += learnt.Heard;
            FinishTutorialInTheCity(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (learnt.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(mine.Network.LocalClientId, learnt.Machine, "from this machine");
            Assert.AreEqual(1, learnt.Seat, "for its player's seat");
            Assert.AreEqual(GameState.Tutorial, State(), "the host starts the first wave");
            host.Match.SendState(GameState.Begin);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave, as the host says");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            mine.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}

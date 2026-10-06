using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.VFX;

namespace DoA.Tests
{
    /// <summary>
    /// Orders in an online match on this computer (127.0.0.1):
    /// - Hosting: this machine hosts with the game. It decides every scooter's pickups, deliveries and drops, another
    ///   machine's too, and shares them with the scores. The other machine is a session without the game, driven
    ///   through its match and its scooter. It leaves mid-delivery, or holding the golden order (the golden round).
    /// - Joining: this machine joins with the game. The host is a session without the game, whose order changes the
    ///   test sends: they show here on whichever scooter holds the order, and this machine changes none by itself.
    /// Loads the game scene: a minute or two. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineOrdersNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string MENU = "Alex Player Testing";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7799;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

        /// <summary>
        /// Remembers the deliveries the rules' machine counted for achievements (FeatSync): each one's seat and whether it
        /// was golden
        /// </summary>
        class FeatRecorder
        {
            public readonly List<int> Seats = new List<int>();
            public readonly List<bool> Golden = new List<bool>();

            public void Heard(int seat, bool golden)
            {
                Seats.Add(seat);
                Golden.Add(golden);
            }
        }

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
        /// Remembers the last drop request the host heard, and how many it heard
        /// </summary>
        class DropRecorder
        {
            public ulong Machine;
            public int Seat;
            public Vector3 Spot1, Spot2;
            public bool SpinOut;
            public int Count;

            public DropRecorder(OnlineMatch match)
            {
                match.DropAsked += OnDrop;
            }

            void OnDrop(ulong machine, int seat, Vector3 spot1, Vector3 spot2, bool spinOut)
            {
                Machine = machine;
                Seat = seat;
                Spot1 = spot1;
                Spot2 = spot2;
                SpinOut = spinOut;
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

        // A machine without the game that has every scene the host asks for at once
        class LoadAnswerer
        {
            readonly OnlineMatch match;

            public LoadAnswerer(OnlineMatch match)
            {
                this.match = match;
                match.LoadRequested += OnLoadRequested;
            }

            void OnLoadRequested(MatchScene scene)
            {
                match.ReportLoaded(scene);
            }
        }

        // What the Hosting_ tests' opening (HostAMatch) leaves them
        class HostedMatch
        {
            public OnlineSession Host;
            public OnlineSession Other;  // another machine: a session without the game
            public ChangeRecorder Heard; // the order changes the other machine heard
            public readonly List<int> Taken = new List<int>(); // the orders the test has used
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

        // The layer of an order's beacon glow: which players' cameras show it
        static int GlowLayer(Order order)
        {
            OrderBeacon beacon = (OrderBeacon)Reflect.GetField(order, "beacon");
            return ((VisualEffect)Reflect.GetField(beacon, "beaconFX")).gameObject.layer;
        }

        // Whether an order's beacon marks its pickup (not its dropoff)
        static bool IsPickupBeacon(Order order)
        {
            return ((OrderBeacon)Reflect.GetField(order, "beacon")).IsPickup;
        }

        // The layers a seat's camera shows, on this machine
        static int CameraMask(int seat)
        {
            return Slot(seat).Input.GetComponent<PlayerCameraResizer>().PlayerReferenceCamera.cullingMask;
        }

        // A player on this machine drives their ball there and stops
        static void PutBallAt(int seat, Vector3 spot)
        {
            Rigidbody ball = Slot(seat).Player.GetComponentInChildren<Rigidbody>();
            ball.velocity = Vector3.zero;
            ball.position = spot;
            ball.transform.position = spot;
        }

        static bool AnyActive(List<Order> orders)
        {
            foreach (Order order in orders)
            {
                if (order.IsActive)
                    return true;
            }
            return false;
        }

        // Where the players start: the start of their seat's tutorial lane
        static GameObject[] TutorialStarts()
        {
            return (GameObject[])Reflect.GetField(SpawnManager.Instance, "gameSpawnPositions");
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
            Slot(seat).Player.GetComponentInChildren<TutorialHandler>().TeachHandler(TutorialType.Final);
        }

        static OnlineScooter ScooterInSeat(OnlineSession session, int seat)
        {
            foreach (OnlineScooter scooter in session.Scooters)
            {
                if (scooter.Seat == seat)
                    return scooter;
            }
            return null;
        }

        static OnlinePlayer PlayerInSeat(OnlineSession session, int seat)
        {
            foreach (OnlinePlayer player in session.Players)
            {
                if (player.Seat == seat)
                    return player;
            }
            return null;
        }

        // The orders of the scooter in a seat, here
        static OrderHandler Handler(int seat)
        {
            return Slot(seat).Player.GetComponentInChildren<OrderHandler>(true);
        }

        // The machine that owns a scooter moves it there, as its OnlineDriving would
        static void ShareAt(OnlineScooter scooter, Vector3 spot)
        {
            scooter.Share(new ScooterPose { Ball = spot, Heading = 0, ModelPosition = spot, ModelRotation = Quaternion.identity }, default(DriveFlags));
        }

        // The first order spawn heard that isn't one of these orders, or null
        static OrderChange? FirstNewSpawn(ChangeRecorder recorder, List<int> skip)
        {
            foreach (OrderChange change in recorder.Changes)
            {
                if (change.Kind == OrderChangeKind.Spawn && !skip.Contains(change.Order))
                    return change;
            }
            return null;
        }

        static int CountOf(ChangeRecorder recorder, OrderChangeKind kind)
        {
            int count = 0;
            foreach (OrderChange change in recorder.Changes)
            {
                if (change.Kind == kind)
                    count++;
            }
            return count;
        }

        static OrderChange LastOf(ChangeRecorder recorder, OrderChangeKind kind)
        {
            OrderChange last = default;
            foreach (OrderChange change in recorder.Changes)
            {
                if (change.Kind == kind)
                    last = change;
            }
            return last;
        }

        // The Hosting_ tests' opening, once the menu scene is up: this machine hosts with the game, and another machine (a
        // session without the game) takes seat 2. The match starts on both machines, both players finish the tutorial
        // (this machine's waits in the city), and the first wave begins
        static IEnumerator HostAMatch(HostedMatch match)
        {
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            match.Host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(match.Host);
            Assert.IsTrue(match.Host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            match.Other = OnlineSession.Create(VERSION);
            match.Other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && match.Other.Match != null && ScooterInSeat(match.Other, 1) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            match.Heard = new ChangeRecorder(match.Other.Match);

            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            match.Other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING + 60;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "the tutorial");
            FinishTutorialInTheCity(0);
            match.Other.Match.ReportLearnt(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave");

            // The tutorial's orders were out for both lanes until the first wave: they aren't the wave's
            foreach (Order tutorial in (Order[])Reflect.GetField(OrderManager.Instance, "tutorialOrders"))
                match.Taken.Add(tutorial.Key);
        }

        [UnityTest]
        public IEnumerator Hosting_TheHostDecidesEveryScootersOrders_AndSharesThemWithTheScores()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            HostedMatch match = new HostedMatch();
            yield return HostAMatch(match);
            OnlineSession host = match.Host, other = match.Other;
            ChangeRecorder heard = match.Heard;
            List<int> taken = match.Taken;
            FeatRecorder feats = new FeatRecorder();
            FeatSync.Delivered += feats.Heard;

            // The first wave's first order: spawned here, and the other machine hears it
            OrderChange? spawn = null;
            float deadline = Time.realtimeSinceStartup + WAIT;
            while ((spawn = FirstNewSpawn(heard, taken)) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(spawn.HasValue, "the other machine hears an order spawn");
            Order first = OrderBook.Find(spawn.Value.Order);
            Assert.IsTrue(first != null && first.IsActive, "spawned here");
            taken.Add(first.Key);

            // The other machine's scooter drives into its light: the host sees it there and gives it the order
            ShareAt(ScooterInSeat(other, 1), first.transform.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Changes.Contains(OrderChange.Pickup(first.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Changes.Contains(OrderChange.Pickup(first.Key, 1)), "the other machine hears its scooter picked it up");
            Assert.AreSame(Handler(1), first.PlayerHolding, "on the other machine's scooter, here");

            // To the dropoff: delivered, erased after its throw, and its player scores its value (on their OnlinePlayer)
            ShareAt(ScooterInSeat(other, 1), first.DropoffPoint.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(heard.Changes.Contains(OrderChange.Deliver(first.Key, 1)) && heard.Changes.Contains(OrderChange.Erase(first.Key)))
                && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Changes.Contains(OrderChange.Deliver(first.Key, 1)), "delivered");
            Assert.Greater(heard.Changes.IndexOf(OrderChange.Erase(first.Key)), heard.Changes.IndexOf(OrderChange.Deliver(first.Key, 1)),
                "erased after its throw");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (PlayerInSeat(other, 1).Score != (int)first.Value && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual((int)first.Value, PlayerInSeat(other, 1).Score, "their score, from the host");
            FeatSync.Delivered -= feats.Heard;
            Assert.IsTrue(feats.Seats.Contains(1), "the host counts the other machine's delivery for its achievements");
            Assert.IsFalse(feats.Golden[feats.Seats.IndexOf(1)], "an ordinary order");

            // Another order: its player falls in the water holding it. The other machine asks, and the host drops it where asked
            deadline = Time.realtimeSinceStartup + WAIT;
            while ((spawn = FirstNewSpawn(heard, taken)) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(spawn.HasValue, "a second order");
            Order second = OrderBook.Find(spawn.Value.Order);
            taken.Add(second.Key);
            ShareAt(ScooterInSeat(other, 1), second.transform.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Changes.Contains(OrderChange.Pickup(second.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), second.PlayerHolding, "the second order on their scooter");

            Vector3 spot = TutorialStarts()[1].transform.position;
            other.Match.AskDrop(1, spot, spot, false);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (CountOf(heard, OrderChangeKind.Drop) == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            OrderChange drop = LastOf(heard, OrderChangeKind.Drop);
            Assert.AreEqual(OrderChangeKind.Drop, drop.Kind, "the host dropped them");
            Assert.AreEqual(1, drop.Seat);
            Assert.AreEqual(OrderBook.NONE, drop.Order, "its first rack was empty");
            Assert.AreEqual(second.Key, drop.Order2, "a first pickup rides on the second rack");
            Assert.AreEqual(spot, drop.Spot2);
            Assert.That(drop.Height2, Is.InRange(Order.DROP_HEIGHT_MIN, Order.DROP_HEIGHT_MAX), "the host picked how high it flies");
            Assert.IsFalse(Handler(1).HasOrder, "dropped here too");

            // Asking for another seat's drop does nothing
            other.Match.AskDrop(0, spot, spot, false);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(1, CountOf(heard, OrderChangeKind.Drop), "not their seat");

            // The golden order's value reaches the other machine
            OrderManager.Instance.FinalOrderValue = 90;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (other.Match.GoldenValue != 90 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(90, other.Match.GoldenValue, "the golden order's value");

            // The other machine leaves holding an order: here it goes back to the pool (not destroyed with their scooter)
            deadline = Time.realtimeSinceStartup + WAIT;
            while ((spawn = FirstNewSpawn(heard, taken)) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(spawn.HasValue, "a third order");
            Order third = OrderBook.Find(spawn.Value.Order);
            taken.Add(third.Key);
            ShareAt(ScooterInSeat(other, 1), third.transform.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Changes.Contains(OrderChange.Pickup(third.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), third.PlayerHolding, "the third order on their scooter");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;
            Assert.IsTrue(third != null, "still here");
            Assert.IsFalse(third.IsActive, "back in the pool");

            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Hosting_AMachineLeavesMidDelivery_ItsOrderIsStillErasedAfterItsThrow()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            HostedMatch match = new HostedMatch();
            yield return HostAMatch(match);
            OnlineSession host = match.Host, other = match.Other;

            // The first wave's first order
            OrderChange? spawn = null;
            float deadline = Time.realtimeSinceStartup + WAIT;
            while ((spawn = FirstNewSpawn(match.Heard, match.Taken)) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(spawn.HasValue, "the other machine hears an order spawn");
            Order first = OrderBook.Find(spawn.Value.Order);

            // Their scooter picks it up, then reaches its dropoff: the host decides the delivery
            ShareAt(ScooterInSeat(other, 1), first.transform.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!match.Heard.Changes.Contains(OrderChange.Pickup(first.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), first.PlayerHolding, "on the other machine's scooter");
            ShareAt(ScooterInSeat(other, 1), first.DropoffPoint.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!match.Heard.Changes.Contains(OrderChange.Deliver(first.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(match.Heard.Changes.Contains(OrderChange.Deliver(first.Key, 1)), "delivered");

            // They leave at once, while the order flies to its customer
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;) // the throw takes 0.25 s
                yield return null;

            Assert.IsFalse(first.IsActive, "erased after its throw: back in the pool");
            Assert.IsNull(first.PlayerHolding);
            Assert.AreEqual(1, PlayerInstantiate.Instance.PlayerCount, "their scooter went");
            Assert.IsTrue(OrderManager.Instance.GameStarted, "the waves go on");

            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Hosting_AMachineLeavesHoldingTheGoldenOrder_ItGoesBackToItsStart_AndTheRoundGoesOn()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            HostedMatch match = new HostedMatch();
            yield return HostAMatch(match);
            OnlineSession host = match.Host, other = match.Other;

            // Time up: the host's golden round loads on both machines (the other machine has it at once), and its
            // cutscene is skipped
            new LoadAnswerer(other.Match);
            Reflect.SetField(OrderManager.Instance, "wave", 99);
            OrderManager.Instance.InitWave();
            float deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.GoldenCutscene && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.GoldenCutscene, State(), "the golden round");
            CutsceneManager.Instance.Skip();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.FinalPackage && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.FinalPackage, State());

            // The other machine's player takes the golden order
            Order golden = (Order)Reflect.GetField(OrderManager.Instance, "finalOrder");
            Vector3 start = golden.PickupPoint.position;
            ShareAt(ScooterInSeat(other, 1), start);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!match.Heard.Changes.Contains(OrderChange.Pickup(golden.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), golden.PlayerHolding, "the other machine's player holds the golden order");
            OrderManager.Instance.FinalOrderValue = 90; // it grew while they held it

            // They leave holding it
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return null;

            Assert.AreEqual(GameState.FinalPackage, State(), "the golden round goes on");
            Assert.IsTrue(golden.IsActive, "out again");
            Assert.IsNull(golden.PlayerHolding);
            Assert.Less(Vector3.Distance(start, golden.transform.position), 0.01f, "back at its start");
            Assert.AreEqual((int)Constants.OrderValue.Golden, OrderManager.Instance.FinalOrderValue, "at its starting value");
            Assert.IsTrue(OrderManager.Instance.FinalOrderActive, "its clock and every HUD stay on the golden round");

            // A delivery ends the round a moment later (the order manager's post-game linger): this was none
            for (float until = Time.realtimeSinceStartup + 3f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(GameState.FinalPackage, State(), "still on a moment later: the leaver didn't deliver it");

            // This machine's player can take it now, and it grows again while they hold it
            PutBallAt(0, start);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (golden.PlayerHolding != Handler(0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(0), golden.PlayerHolding, "the round goes on for the players still here");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (OrderManager.Instance.FinalOrderValue == (int)Constants.OrderValue.Golden && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Greater(OrderManager.Instance.FinalOrderValue, (int)Constants.OrderValue.Golden, "its value grows again");

            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_TheHostsOrdersShowHere_OnWhicheverScooterHoldsThem()
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
            DropRecorder asked = new DropRecorder(host.Match);

            // The host's match comes up here: the tutorial (this machine's player finishes it and waits in the city), then
            // the first wave
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
            host.Match.SendState(GameState.Tutorial);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            FinishTutorialInTheCity(1);
            host.Match.SendState(GameState.Begin);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave, as the host says");
            List<Order> orders = (List<Order>)Reflect.GetField(OrderManager.Instance, "normalOrders");
            Order first = orders[0], second = orders[1], third = orders[2], fourth = orders[3], fifth = orders[4];

            // Nothing spawns here by itself (the order manager spawns one every 4 s)
            for (float until = Time.realtimeSinceStartup + 5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsFalse(AnyActive(orders), "this machine's order manager doesn't spawn");

            // The host spawns one: it shows at its pickup
            host.Match.SendOrder(OrderChange.Spawn(first.Key, true));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!first.IsActive && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(first.IsActive, "the host spawns one: it shows here");
            Assert.Less(Vector3.Distance(first.PickupPoint.position, first.transform.position), 0.01f, "at its pickup");

            // This machine's scooter in its light picks up nothing by itself: the host decides
            PutBallAt(1, first.transform.position);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsNull(first.PlayerHolding, "picks up nothing by itself");

            // The host gives it to this machine's player: it rides on their scooter, and its dropoff glows for their camera
            host.Match.SendOrder(OrderChange.Pickup(first.Key, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Handler(1).HasOrder && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), first.PlayerHolding, "on this machine's scooter");
            Assert.AreEqual(18, GlowLayer(first), "Player2Cam: player 2's own");

            // The host's scores show here, and replaying a delivery doesn't add to them
            PlayerInSeat(host, 1).ShareScore(150);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Handler(1).Score != 150 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(150, Handler(1).Score, "the host's score shows here");
            host.Match.SendOrder(OrderChange.Deliver(first.Key, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Handler(1).HasOrder && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsFalse(Handler(1).HasOrder, "delivered");
            Assert.AreEqual(150, Handler(1).Score, "the host's score, not 150 plus the order");
            Assert.IsTrue(first.IsActive, "its throw doesn't erase it here: the host does");
            host.Match.SendOrder(OrderChange.Erase(first.Key));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (first.IsActive && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(first.IsActive, "erased when the host says");

            // An order on the host's scooter rides on it here, and its dropoff doesn't glow for this machine's camera
            host.Match.SendOrder(OrderChange.Spawn(second.Key, true));
            host.Match.SendOrder(OrderChange.Pickup(second.Key, 0));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (second.PlayerHolding != Handler(0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(0), second.PlayerHolding, "on the host's scooter here");
            Assert.AreEqual(0, CameraMask(1) & (1 << GlowLayer(second)), "not on this machine's camera");

            // This machine's player falls in the water holding one: the host is asked, and drops it where asked
            host.Match.SendOrder(OrderChange.Spawn(third.Key, true));
            host.Match.SendOrder(OrderChange.Pickup(third.Key, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Handler(1).HasOrder && Time.realtimeSinceStartup < deadline)
                yield return null;
            Vector3 spot = TutorialStarts()[1].transform.position;
            Handler(1).DropEverything(spot, spot, false); // as Respawn does without the host's answer
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(mine.Network.LocalClientId, asked.Machine);
            Assert.AreEqual(1, asked.Seat);
            Assert.AreEqual(spot, asked.Spot1);
            Assert.IsTrue(Handler(1).HasOrder, "the host drops it");
            // The host drops it where it says: here 20 m off the spot this machine asked for, to tell the two apart
            Vector3 hostSpot = spot + new Vector3(0, 0, 20f);
            host.Match.SendOrder(OrderChange.Drop(1, OrderBook.NONE, hostSpot, 5f, third.Key, hostSpot, 5f, false)); // its second rack
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Handler(1).HasOrder && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(Handler(1).HasOrder, "dropped");
            deadline = Time.realtimeSinceStartup + 10; // it falls for Order's pickup cooldown (3 s)
            while (!third.CanPickup && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            // A dropped order flies up along its tilt (Order.Drop), so it lands within its flight's height of the spot
            Vector3 landed = third.transform.position;
            Assert.AreEqual(hostSpot.y, landed.y, 0.01f, "landed at the height of the host's spot");
            Assert.LessOrEqual(Vector2.Distance(new Vector2(hostSpot.x, hostSpot.z), new Vector2(landed.x, landed.z)), 5f, "landed where the host said");

            // The host gives back an order this machine still shows falling (a player who respawned beside their orders):
            // its fall and its pickup cooldown end at once, so the cooldown's end can't take it off their scooter later
            host.Match.SendOrder(OrderChange.Spawn(fifth.Key, true));
            host.Match.SendOrder(OrderChange.Pickup(fifth.Key, 1));
            host.Match.SendOrder(OrderChange.Drop(1, OrderBook.NONE, spot, 5f, fifth.Key, spot, 5f, false));
            host.Match.SendOrder(OrderChange.Pickup(fifth.Key, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(fifth.PlayerHolding == Handler(1) && fifth.CanPickup) && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 4f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreSame(Handler(1), fifth.PlayerHolding, "still on this machine's scooter after the drop's cooldown");
            Assert.IsFalse(IsPickupBeacon(fifth), "its dropoff still shows");

            // The golden order's value is the host's
            host.Match.ShareGoldenValue(75);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (OrderManager.Instance.FinalOrderValue != 75 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(75, OrderManager.Instance.FinalOrderValue, "the host's golden value");

            // A change for an order this machine doesn't have does nothing (the log stays clean)
            host.Match.SendOrder(OrderChange.Erase(123456));
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;

            // This machine's player holds an order when the host takes everyone back: it goes with the game scene
            host.Match.SendOrder(OrderChange.Spawn(fourth.Key, true));
            host.Match.SendOrder(OrderChange.Pickup(fourth.Key, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (fourth.PlayerHolding != Handler(1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), fourth.PlayerHolding, "the fourth order on this machine's scooter");
            int fourthKey = fourth.Key;
            host.Match.RequestReturn();
            deadline = Time.realtimeSinceStartup + 60;
            while (ActiveScene() != MENU && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(MENU, ActiveScene(), "back in the menu");
            Assert.IsNull(Slot(1).Player.GetComponentInChildren<Order>(true), "no order rode into the menu");
            host.Match.SendOrder(OrderChange.Erase(fourthKey)); // the host's erase comes late (the log stays clean)
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;

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

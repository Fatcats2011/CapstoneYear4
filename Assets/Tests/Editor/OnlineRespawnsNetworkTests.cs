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
    /// Respawns in an online match on this computer (127.0.0.1):
    /// - Hosting: this machine hosts with the game and picks where every player who falls in the water rises: its own
    ///   player's point as in a local match, and another machine's when that machine asks. No two players rise on one
    ///   point, and each one's orders drop on their own point. The other machine is a session without the game.
    /// - Joining: this machine joins with the game. Its player who falls in the water waits, hidden, for the host's point
    ///   and rises there. Without an answer it rises on its own point after a short wait, and asks the host to drop its
    ///   orders there.
    /// Loads the game scene: a minute or two each. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineRespawnsNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7805;
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
        /// Remembers the last respawn request the host heard (who sent it, their seat, where they were last on the
        /// ground), and how many it heard
        /// </summary>
        class RespawnAskRecorder
        {
            public ulong Machine;
            public int Seat = -1;
            public Vector3 LastGrounded;
            public int Count;

            public RespawnAskRecorder(OnlineMatch match)
            {
                match.RespawnAsked += OnAsked;
            }

            void OnAsked(ulong machine, int seat, Vector3 lastGrounded)
            {
                Machine = machine;
                Seat = seat;
                LastGrounded = lastGrounded;
                Count++;
            }
        }

        /// <summary>
        /// Remembers the last respawn point a machine heard from the host (the seat, the point's index), and how many
        /// </summary>
        class PointRecorder
        {
            public int Seat = -1;
            public int Point = -1;
            public int Count;

            public PointRecorder(OnlineMatch match)
            {
                match.RespawnReceived += OnPoint;
            }

            void OnPoint(int seat, int point)
            {
                Seat = seat;
                Point = point;
                Count++;
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
            RespawnSync.Reset();
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

        // Where a player's ball is, here
        static Vector3 BallOf(OrderHandler player)
        {
            return player.GetComponent<BallDriving>().Sphere.transform.position;
        }

        // The respawn (on the ball) of the scooter in a seat, here
        static Respawn RespawnOf(int seat)
        {
            return ScooterIn(seat).Sphere.GetComponent<Respawn>();
        }

        // How far apart two places are, ignoring height
        static float Flat(Vector3 a, Vector3 b)
        {
            return Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
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

        // The respawn point farthest from a place
        static int FarthestPointFrom(Vector3 spot)
        {
            int farthest = -1;
            float gap = -1f;
            for (int i = 0; RespawnManager.Instance.PointAt(i) != null; i++)
            {
                float distance = Vector3.Distance(RespawnManager.Instance.PointAt(i).PlayerSpawn, spot);
                if (distance > gap)
                {
                    gap = distance;
                    farthest = i;
                }
            }
            return farthest;
        }

        // The drop a machine heard that carried an order (on either rack); Kind stays Spawn when there's none
        static OrderChange DropCarrying(ChangeRecorder recorder, int order)
        {
            foreach (OrderChange change in recorder.Changes)
            {
                if (change.Kind == OrderChangeKind.Drop && (change.Order == order || change.Order2 == order))
                    return change;
            }
            return default;
        }

        [UnityTest]
        public IEnumerator Hosting_TheHostPicksWhereEveryPlayerRises_NeverTwoOnOnePoint()
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
            while (!(Slot(1) != null && other.Match != null && ScooterInSeat(other, 1) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            ChangeRecorder heard = new ChangeRecorder(other.Match);
            PointRecorder rise = new PointRecorder(other.Match);

            // The match starts on both machines. Both players finish the tutorial (this machine's waits in the city), and
            // the first wave begins
            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING + 60;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "the tutorial");
            FinishTutorialInTheCity(0);
            other.Match.ReportLearnt(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave");

            // The other machine's player picks up an order (the tutorial's orders aren't the wave's), then waits up in the
            // air, away from every beacon
            List<int> taken = new List<int>();
            foreach (Order tutorial in (Order[])Reflect.GetField(OrderManager.Instance, "tutorialOrders"))
                taken.Add(tutorial.Key);
            OrderChange? spawn = null;
            deadline = Time.realtimeSinceStartup + WAIT;
            while ((spawn = FirstNewSpawn(heard, taken)) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(spawn.HasValue, "an order spawns");
            Order first = OrderBook.Find(spawn.Value.Order);
            ShareAt(ScooterInSeat(other, 1), first.transform.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Changes.Contains(OrderChange.Pickup(first.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), first.PlayerHolding, "their player holds it");
            Vector3 spot = CitySpot();
            ShareAt(ScooterInSeat(other, 1), spot + Vector3.up * 30f);

            // This machine's player falls in the water: it rises on the nearest point, as in a local match
            RespawnPoint mine = RespawnManager.Instance.GetRespawnPoint(spot);
            RespawnOf(0).LastGroundedPos = spot;
            RespawnOf(0).StartRespawnCoroutine();
            Assert.IsTrue(mine.InUse, "this machine's player rises there");

            // The other machine's player falls in at the same place, twice over: the host picks each time, never a point
            // someone is rising from
            other.Match.AskRespawn(1, spot);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (rise.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, rise.Seat, "the host answers for their seat");
            int theirPoint = rise.Point;
            other.Match.AskRespawn(1, spot);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (rise.Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, rise.Count, "a second answer");

            RespawnPoint theirs = RespawnManager.Instance.PointAt(theirPoint);
            Assert.IsNotNull(theirs, "a point in this scene");
            Assert.AreNotSame(mine, theirs, "never the point this machine's player rises from");
            RespawnPoint next = RespawnManager.Instance.PointAt(rise.Point);
            Assert.IsTrue(next != null && next != mine && next != theirs, "the host holds a point while its player rises");

            // Their order dropped on their first point (the host drops before it answers, and the second drop is empty)
            OrderChange drop = DropCarrying(heard, first.Key);
            Assert.AreEqual(OrderChangeKind.Drop, drop.Kind, "the host dropped their order");
            Assert.AreEqual(1, drop.Seat);
            Assert.AreEqual(first.Key, drop.Order2, "a first pickup rides on the second rack");
            Assert.AreEqual(theirs.Order2Spawn, drop.Spot2, "their order lands on their point");
            Assert.IsFalse(drop.Flag, "no spin-out");

            // Asking for another seat does nothing
            other.Match.AskRespawn(0, spot);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(2, rise.Count, "not the sender's seat");

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
        public IEnumerator Joining_ThisMachinesPlayerRisesWhereTheHostSays_OrOnItsOwnPointWithoutAnAnswer()
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
            RespawnAskRecorder asked = new RespawnAskRecorder(host.Match);
            DropRecorder drops = new DropRecorder(host.Match);

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
            Vector3 spot = CitySpot();
            PutBallAt(1, spot);

            // This machine's player falls in the water: it asks the host where to rise, and waits for the answer, hidden
            Respawn respawn = RespawnOf(1);
            respawn.LastGroundedPos = spot;
            respawn.StartRespawnCoroutine();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(mine.Network.LocalClientId, asked.Machine);
            Assert.AreEqual(1, asked.Seat);
            Assert.AreEqual(spot, asked.LastGrounded);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsTrue(respawn.IsRespawning && respawn.RiderHidden, "hidden, waiting for the host's point");

            // The host names a point far away: it rises there
            int far = FarthestPointFrom(spot);
            host.Match.SendRespawn(1, far);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (respawn.IsRespawning && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(respawn.IsRespawning, "risen");
            Assert.Less(Flat(BallOf(Handler(1)), RespawnManager.Instance.PointAt(far).PlayerSpawn), 2f, "it rose where the host said");
            Assert.AreEqual(0, drops.Count, "the host drops the orders");

            // Without an answer, it rises on its own point after a short wait, and asks the host to drop its orders there
            respawn.LastGroundedPos = spot;
            respawn.StartRespawnCoroutine();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, asked.Count, "it asked again");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (respawn.IsRespawning && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(respawn.IsRespawning, "risen without an answer");
            RespawnPoint own = RespawnManager.Instance.GetRespawnPoint(spot);
            Assert.Less(Flat(BallOf(Handler(1)), own.PlayerSpawn), 2f, "it rose on its own point after a short wait");
            Assert.AreEqual(1, drops.Count, "and asked the host to drop its orders there");
            Assert.AreEqual(own.Order1Spawn, drops.Spot1);

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

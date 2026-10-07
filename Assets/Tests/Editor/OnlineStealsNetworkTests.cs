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
    /// Steals and clashes in an online match on this computer (127.0.0.1):
    /// - Hosting: this machine hosts with the game and decides every hit. Two other machines (sessions without the game,
    ///   each over a 150 ms connection) ask about their players' hits, even at the same moment: one steal wins, and every
    ///   machine hears the same one. This machine's own player's hit is judged at once.
    /// - Joining: this machine joins with the game. Its player's hit goes to the host (a session without the game), and
    ///   the host's steals and clashes show here: the order moves, this machine's player spins out and bounces away.
    /// Loads the game scene: a minute or two each. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineStealsNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7804;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode
        const int LAG_MS = 150;     // the roadmap's bad connection

        static readonly DriveFlags BOOSTING = new DriveFlags(true, false, false, 0, true, false);
        static readonly DriveFlags HIDDEN = new DriveFlags(false, false, false, 0, true, false, true);

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
        /// Remembers the hits a machine heard from the host, in order
        /// </summary>
        class HitRecorder
        {
            public readonly List<PlayerHit> Hits = new List<PlayerHit>();

            public HitRecorder(OnlineMatch match)
            {
                match.HitReceived += OnHit;
            }

            void OnHit(PlayerHit hit)
            {
                Hits.Add(hit);
            }
        }

        /// <summary>
        /// Remembers the last steal request the host heard (who sent it, the attacker's and the victim's seats), and how
        /// many it heard
        /// </summary>
        class StealRecorder
        {
            public ulong Machine;
            public int Attacker = -1;
            public int Victim = -1;
            public int Count;

            public StealRecorder(OnlineMatch match)
            {
                match.StealAsked += OnAsked;
            }

            void OnAsked(ulong machine, int attacker, int victim)
            {
                Machine = machine;
                Attacker = attacker;
                Victim = victim;
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
            StealSync.Reset();
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

        // The machine that owns a scooter moves it there, doing something (as its OnlineDriving would)
        static void ShareAt(OnlineScooter scooter, Vector3 spot, DriveFlags flags)
        {
            scooter.Share(new ScooterPose { Ball = spot, Heading = 0, ModelPosition = spot, ModelRotation = Quaternion.identity }, flags);
        }

        // ... doing nothing in particular
        static void ShareAt(OnlineScooter scooter, Vector3 spot)
        {
            ShareAt(scooter, spot, default(DriveFlags));
        }

        // Whether the host shows a scooter there, here
        static bool ShowsAt(int seat, Vector3 spot)
        {
            return Vector3.Distance(BallOf(Handler(seat)), spot) < 1f;
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

        // The changes of one kind a machine heard, in order
        static List<OrderChange> OfKind(ChangeRecorder recorder, OrderChangeKind kind)
        {
            List<OrderChange> found = new List<OrderChange>();
            foreach (OrderChange change in recorder.Changes)
            {
                if (change.Kind == kind)
                    found.Add(change);
            }
            return found;
        }

        [UnityTest]
        public IEnumerator Hosting_TheHostDecidesStealsAndClashes_AndOneStealWinsWhenTwoMachinesTryAtOnce()
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

            // Two other machines (sessions without the game), each over a 150 ms connection, one after the other
            OnlineSession x = OnlineSession.Create(VERSION);
            x.Direct.SetDebugSimulatorParameters(LAG_MS, 0, 0);
            x.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && x.Match != null && ScooterInSeat(x, 1) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "the first machine's scooter is in seat 2");
            OnlineSession y = OnlineSession.Create(VERSION);
            y.Direct.SetDebugSimulatorParameters(LAG_MS, 0, 0);
            y.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(2) != null && y.Match != null && ScooterInSeat(y, 2) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(2), "the second machine's scooter is in seat 3");
            ChangeRecorder heardX = new ChangeRecorder(x.Match);
            ChangeRecorder heardY = new ChangeRecorder(y.Match);
            HitRecorder hitsX = new HitRecorder(x.Match);
            HitRecorder hitsY = new HitRecorder(y.Match);

            // The match starts on every machine. Everyone finishes the tutorial (this machine's player waits in the city),
            // and the first wave begins
            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            x.Match.ReportLoaded(MatchScene.Game);
            y.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING + 60;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "the tutorial");
            FinishTutorialInTheCity(0);
            x.Match.ReportLearnt(1);
            y.Match.ReportLearnt(2);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave");

            // Each other machine's player picks up an order (the tutorial's orders aren't the wave's)
            List<int> taken = new List<int>();
            foreach (Order tutorial in OrderManager.Instance.tutorialOrders)
                taken.Add(tutorial.Key);
            OrderChange? spawn = null;
            deadline = Time.realtimeSinceStartup + WAIT;
            while ((spawn = FirstNewSpawn(heardX, taken)) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(spawn.HasValue, "an order spawns");
            Order first = OrderBook.Find(spawn.Value.Order);
            taken.Add(first.Key);
            ShareAt(ScooterInSeat(x, 1), first.transform.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heardX.Changes.Contains(OrderChange.Pickup(first.Key, 1)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), first.PlayerHolding, "seat 2's player holds the first order");

            deadline = Time.realtimeSinceStartup + WAIT;
            while ((spawn = FirstNewSpawn(heardX, taken)) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(spawn.HasValue, "a second order");
            Order second = OrderBook.Find(spawn.Value.Order);
            taken.Add(second.Key);
            ShareAt(ScooterInSeat(y, 2), second.transform.position);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heardY.Changes.Contains(OrderChange.Pickup(second.Key, 2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(2), second.PlayerHolding, "seat 3's player holds the second order");

            // Up in the air, side by side, away from every beacon and from this machine's player
            Vector3 air = CitySpot() + Vector3.up * 30f;
            Vector3 xSpot = air, ySpot = air + Vector3.right * 2f;
            ShareAt(ScooterInSeat(x, 1), xSpot);
            ShareAt(ScooterInSeat(y, 2), ySpot);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(ShowsAt(1, xSpot) && ShowsAt(2, ySpot)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ShowsAt(1, xSpot) && ShowsAt(2, ySpot), "the host shows both there");

            // A machine speaks only for its own player
            x.Match.AskSteal(2, 1);
            for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsEmpty(hitsX.Hits, "not the sender's seat");

            // A respawning player can't be hit
            ShareAt(ScooterInSeat(y, 2), ySpot, HIDDEN);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!RespawnOf(2).RiderHidden && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(RespawnOf(2).RiderHidden, "the host shows them hidden");
            x.Match.AskSteal(1, 2);
            for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsEmpty(hitsX.Hits, "a respawning player can't be hit");
            ShareAt(ScooterInSeat(y, 2), ySpot);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (RespawnOf(2).RiderHidden && Time.realtimeSinceStartup < deadline)
                yield return null;

            // Both machines ask to steal from each other at the same moment: one steal wins, the same everywhere
            x.Match.AskSteal(1, 2);
            y.Match.AskSteal(2, 1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(hitsX.Hits.Count >= 1 && hitsY.Hits.Count >= 1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            List<OrderChange> steals = OfKind(heardX, OrderChangeKind.Steal);
            Assert.AreEqual(1, steals.Count, "one steal");
            CollectionAssert.AreEqual(steals, OfKind(heardY, OrderChangeKind.Steal), "the same everywhere");
            OrderChange steal = steals[0];
            Assert.IsTrue((steal.Seat == 1 && steal.Seat2 == 2) || (steal.Seat == 2 && steal.Seat2 == 1), "one of them robbed the other: " + steal);
            CollectionAssert.AreEqual(new[] { PlayerHit.Steal(steal.Seat, steal.Seat2) }, hitsX.Hits);
            CollectionAssert.AreEqual(hitsX.Hits, hitsY.Hits);
            Assert.AreSame(Handler(steal.Seat), first.PlayerHolding, "the winner holds both orders here");
            Assert.AreSame(Handler(steal.Seat), second.PlayerHolding);
            Assert.IsFalse(Handler(steal.Seat2).HasOrder);

            // Once that pair can be hit again: both boosting, a request is a clash, and no order moves
            for (float until = Time.realtimeSinceStartup + StealRules.PAIR_COOLDOWN + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            ShareAt(ScooterInSeat(x, 1), xSpot, BOOSTING);
            ShareAt(ScooterInSeat(y, 2), ySpot, BOOSTING);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Handler(1).IsBoosting && Handler(2).IsBoosting) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Handler(1).IsBoosting && Handler(2).IsBoosting, "the host shows both boosting");
            x.Match.AskSteal(1, 2);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(hitsX.Hits.Count >= 2 && hitsY.Hits.Count >= 2) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, hitsX.Hits.Count, "a second hit");
            Assert.AreEqual(PlayerHit.Clash(1, 2), hitsX.Hits[1]);
            Assert.AreEqual(PlayerHit.Clash(1, 2), hitsY.Hits[1]);
            Assert.AreEqual(1, OfKind(heardX, OrderChangeKind.Steal).Count, "a clash moves no order");

            // This machine's player boosts into the winner: the host judges its own player's hit at once
            int winner = steal.Seat, loser = steal.Seat2;
            Vector3 winnerSpot = winner == 1 ? xSpot : ySpot;
            Vector3 away = air + Vector3.right * 30f;
            ShareAt(ScooterInSeat(winner == 1 ? x : y, winner), winnerSpot);
            ShareAt(ScooterInSeat(loser == 1 ? x : y, loser), away);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(!Handler(winner).IsBoosting && ShowsAt(loser, away)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Order best = Handler(winner).GetBestOrder();
            Assert.IsNotNull(best, "the winner holds orders");
            ScooterIn(0).AutoBoost();
            PutBallAt(0, BallOf(Handler(winner)) + Vector3.forward);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heardX.Changes.Contains(OrderChange.Steal(best.Key, 0, winner)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heardX.Changes.Contains(OrderChange.Steal(best.Key, 0, winner)), "the other machines hear this machine's player took it");
            Assert.AreSame(Handler(0), best.PlayerHolding, "on this machine's scooter");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (hitsX.Hits.Count < 3 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(PlayerHit.Steal(0, winner), hitsX.Hits[hitsX.Hits.Count - 1]);

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            x.Leave();
            y.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_ThisMachinesPlayerAsksToSteal_AndTheHostsStealsAndClashesShowHere()
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
            StealRecorder asked = new StealRecorder(host.Match);

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
            OnlineScooter hosts = ScooterInSeat(host, 0);

            // This machine's player boosts into the host's scooter (up in the air, away from everything): it asks the host
            Vector3 air = spot + Vector3.up * 30f;
            ShareAt(hosts, air);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!ShowsAt(0, air) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ShowsAt(0, air), "the host's scooter is there here");
            ScooterIn(1).AutoBoost();
            PutBallAt(1, air + Vector3.forward);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (asked.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(mine.Network.LocalClientId, asked.Machine);
            Assert.AreEqual(1, asked.Attacker);
            Assert.AreEqual(0, asked.Victim);

            // The host's player robs this machine's. First an order for this machine's player
            List<Order> orders = OrderManager.Instance.normalOrders;
            Order order = orders[0];
            host.Match.SendOrder(OrderChange.Spawn(order.Key, true));
            host.Match.SendOrder(OrderChange.Pickup(order.Key, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (order.PlayerHolding != Handler(1) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(1), order.PlayerHolding, "this machine's player holds it");
            Vector3 beside = spot + Vector3.right * 4f;
            ShareAt(hosts, beside);
            PutBallAt(1, spot);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!ShowsAt(0, beside) && Time.realtimeSinceStartup < deadline)
                yield return null;
            PutBallAt(1, spot);

            host.Match.SendOrder(OrderChange.Steal(order.Key, 0, 1));
            host.Match.SendOrder(OrderChange.Drop(1, OrderBook.NONE, spot, 1f, OrderBook.NONE, spot, 1f, true));
            host.Match.SendHit(PlayerHit.Steal(0, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (order.PlayerHolding != Handler(0) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreSame(Handler(0), order.PlayerHolding, "the host's player has it, here too");
            Assert.IsFalse(Handler(1).HasOrder);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!ScooterIn(1).spinningOut && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(ScooterIn(1).spinningOut, "it spins out");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (ScooterIn(1).Sphere.GetComponent<Rigidbody>().velocity.x > -1f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Less(ScooterIn(1).Sphere.GetComponent<Rigidbody>().velocity.x, -1f, "it bounces away from the thief");

            // A clash: this machine's player bounces off the host's scooter
            for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            PutBallAt(1, spot);
            host.Match.SendHit(PlayerHit.Clash(0, 1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (ScooterIn(1).Sphere.GetComponent<Rigidbody>().velocity.x > -1f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.Less(ScooterIn(1).Sphere.GetComponent<Rigidbody>().velocity.x, -1f, "it bounces off the host's scooter");

            // A hit for a seat nobody sits in does nothing here
            host.Match.SendHit(PlayerHit.Clash(0, 3));
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

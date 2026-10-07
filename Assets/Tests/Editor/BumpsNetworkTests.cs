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
    /// Bumps between machines' scooters (Phase 5A), this machine hosting on this computer (127.0.0.1): another machine's
    /// player bumps the host's, the host checks it, and the host's player is pushed away from them, here and on every
    /// machine's say. A bump for a seat the sender doesn't hold, one with a speed that isn't a number, and a flood of them
    /// do nothing. The other machine is a session without the game. Loads the game scene: a minute or two
    /// </summary>
    public class BumpsNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7815;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

        // The bumps a machine heard from the host, in order
        class BumpRecorder
        {
            public readonly List<PlayerBump> Bumps = new List<PlayerBump>();

            public BumpRecorder(OnlineMatch match)
            {
                match.BumpReceived += OnBump;
            }

            void OnBump(PlayerBump bump)
            {
                Bumps.Add(bump);
            }
        }

        // How many bump requests got past the host's rate limit
        class AskRecorder
        {
            public int Asked;

            public AskRecorder(OnlineMatch match)
            {
                match.BumpAsked += OnAsked;
            }

            void OnAsked(ulong machine, int bumper, int victim, float speed)
            {
                Asked++;
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
            BumpSync.Reset();
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static BallDriving ScooterIn(int slot)
        {
            return Slot(slot).Player.GetComponentInChildren<BallDriving>(true);
        }

        static Rigidbody BallBody(int seat)
        {
            return ScooterIn(seat).Sphere.GetComponent<Rigidbody>();
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

        static float Flat(Vector3 v)
        {
            return new Vector3(v.x, 0f, v.z).magnitude;
        }

        static IEnumerator WaitSeconds(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        [UnityTest]
        public IEnumerator Hosting_AnotherMachinesBump_PushesTheHostsPlayer_AndOnlyAnHonestOne()
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
            OnlineSession x = OnlineSession.Create(VERSION); // another machine: a session without the game
            x.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && x.Match != null && ScooterInSeat(x, 1) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            BumpRecorder heard = new BumpRecorder(x.Match);
            AskRecorder asked = new AskRecorder(host.Match);

            // The match starts; both players finish the tutorial, and the first wave begins
            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            x.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING + 60;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Tutorial, State(), "the tutorial");
            Vector3 spot = CitySpot();
            PutBallAt(0, spot);
            Slot(0).Player.GetComponentInChildren<TutorialHandler>().TeachHandler(TutorialType.Final);
            x.Match.ReportLearnt(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave");

            // Their scooter stands 3 m east of the host's player, who stands still
            Vector3 theirSpot = spot + Vector3.right * 3f;
            ShareAt(ScooterInSeat(x, 1), theirSpot);
            yield return WaitSeconds(1f);
            PutBallAt(0, spot);
            yield return WaitSeconds(0.5f);
            Assert.Less(Flat(BallBody(0).velocity), 0.5f, "standing still");

            // They bump the host's player at 10 m/s: pushed away from them, west, and every machine hears it
            x.Match.AskBump(1, 0, 10f);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Flat(BallBody(0).velocity) < 0.5f && Time.realtimeSinceStartup < deadline)
                yield return null;
            Vector3 pushed = BallBody(0).velocity;
            Assert.Greater(Flat(pushed), 0.5f, "pushed");
            Assert.Less(pushed.x, 0f, "away from them");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Bumps.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, heard.Bumps.Count, "every machine hears it");
            Assert.AreEqual(1, heard.Bumps[0].Bumper);
            Assert.AreEqual(0, heard.Bumps[0].Victim);

            // A bump for a seat they don't hold, and one that isn't a number: nothing
            yield return WaitSeconds(1f);
            x.Match.AskBump(0, 1, 10f);
            x.Match.AskBump(1, 0, float.NaN);
            yield return WaitSeconds(1f);
            Assert.AreEqual(1, heard.Bumps.Count, "neither counts");

            // A flood: the host takes at most its allowance (and one bump per pair per half second)
            int before = asked.Asked;
            for (int i = 0; i < 100; i++)
                x.Match.AskBump(1, 0, 10f);
            yield return WaitSeconds(1f);
            Assert.LessOrEqual(asked.Asked - before, 10, "the rate limit");
            Assert.LessOrEqual(heard.Bumps.Count, 3, "one per pair per half second");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            x.Leave();
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

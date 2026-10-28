using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The highlight reel's director on this machine (HighlightDirector): it records every seat through the main round,
    /// and where the rules run turns what the game raises (FeatSync) into moments. A client logs none. EditMode, on
    /// instances of PlayerAvatar.prefab (their scripts don't run)
    /// </summary>
    public class HighlightDirectorTests
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        readonly TestObjects objects = new TestObjects();
        HighlightDirector director;
        BallDriving[] scooters;

        [SetUp]
        public void SetUp()
        {
            scooters = new BallDriving[2];
            for (int seat = 0; seat < scooters.Length; seat++)
            {
                GameObject avatar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR));
                objects.Keep(avatar);
                scooters[seat] = avatar.GetComponentInChildren<BallDriving>(true);
                scooters[seat].Sphere.transform.position = new Vector3(seat * 10, 1, 0);
            }

            director = objects.Add<HighlightDirector>();
            director.FindSeats = seats =>
            {
                for (int seat = 0; seat < scooters.Length; seat++)
                    seats.Add(new HighlightDirector.Seat { Index = seat, Driving = scooters[seat] });
            };
            director.TimeLeft = () => 42f;
            director.PlaceOf = seat => new Vector3(seat, 0, 7);
            director.Begin();
        }

        [TearDown]
        public void TearDown()
        {
            director.End();
            objects.DestroyAll();
            GameAuthority.Role = NetworkRole.Offline;
        }

        [Test]
        public void Sample_RecordsEverySeatsPoseAndFlags()
        {
            director.OnStateApplied(GameState.Begin);

            director.Sample(10.0);

            for (int seat = 0; seat < scooters.Length; seat++)
            {
                Assert.IsTrue(director.Recording.TryGet(seat, 10.0, out ReelSample sample), "seat " + seat + " recorded");
                Assert.AreEqual(new Vector3(seat * 10, 1, 0), sample.Pose.Ball, "seat " + seat + "'s ball");
                Assert.AreEqual(scooters[seat].Flags.Value, sample.Flags.Value, "seat " + seat + "'s flags");
                Assert.AreEqual(0, sample.Carrying, "nothing carried");
            }
        }

        [Test]
        public void Struck_DuringTheRound_OnAuthority_AddsASteal()
        {
            director.OnStateApplied(GameState.Begin);
            double before = HighlightDirector.Now();

            FeatSync.Strike(1, 0, true, false);

            Assert.AreEqual(1, director.Moments.Count);
            Moment moment = director.Moments[0];
            Assert.AreEqual(MomentKind.Steal, moment.Kind);
            Assert.AreEqual(1, moment.Seat);
            Assert.AreEqual(0, moment.Other);
            Assert.AreEqual(42f, moment.TimeLeft);
            Assert.AreEqual(new Vector3(1, 0, 7), moment.Place, "the attacker's place");
            Assert.GreaterOrEqual(moment.Time, before);
            Assert.LessOrEqual(moment.Time, HighlightDirector.Now());
        }

        [Test]
        public void TheRealFlow_StartingCutscene_MainLoop_Tutorial_Begin_RecordsAndLogs()
        {
            director.OnStateApplied(GameState.StartingCutscene);
            director.OnStateApplied(GameState.MainLoop);
            director.OnStateApplied(GameState.Tutorial);
            FeatSync.Sink(0);
            Assert.AreEqual(0, director.Moments.Count, "nothing before Begin");

            director.OnStateApplied(GameState.Begin);
            director.Sample(5.0);
            FeatSync.Sink(0);

            Assert.IsTrue(director.Recording.TryGet(1, 5.0, out _));
            Assert.AreEqual(1, director.Moments.Count);
        }

        [Test]
        public void ARematch_Tutorial_Begin_RecordsAndLogs()
        {
            director.OnStateApplied(GameState.Tutorial);
            director.OnStateApplied(GameState.Begin);

            director.Sample(5.0);
            FeatSync.Sink(1);

            Assert.IsTrue(director.Recording.TryGet(0, 5.0, out _));
            Assert.AreEqual(1, director.Moments.Count);
        }

        [Test]
        public void MainLoopAfterBegin_IsNotTheRound()
        {
            director.OnStateApplied(GameState.Begin);
            director.OnStateApplied(GameState.MainLoop);

            FeatSync.Sink(0);

            Assert.AreEqual(0, director.Moments.Count);
        }

        static void Raise(OrderManager orders)
        {
            // The event has no public raiser; its backing field is the delegate
            var field = typeof(OrderManager).GetField("OnMainGameFinishes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            ((Action)field.GetValue(orders))?.Invoke();
        }

        OrderManager NewOrders()
        {
            GameObject holder = objects.NewGameObject("Orders");
            holder.SetActive(false); // its OnEnable needs the game's managers, which these tests haven't got
            return holder.AddComponent<OrderManager>();
        }

        [Test]
        public void Hook_FollowsOnlyTheLatestOrderManager()
        {
            OrderManager first = NewOrders();
            OrderManager second = NewOrders();
            director.OnStateApplied(GameState.Begin);

            director.Hook(first);
            director.Hook(second);
            Raise(first);
            FeatSync.Sink(0);
            Assert.AreEqual(1, director.Moments.Count, "the first manager no longer ends the round");

            Raise(second);
            FeatSync.Sink(0);
            Assert.AreEqual(1, director.Moments.Count, "the second one does");
        }

        [Test]
        public void Hook_Null_AfterTheManagerIsDestroyed_DoesNotThrow()
        {
            OrderManager orders = NewOrders();
            director.Hook(orders);
            UnityEngine.Object.DestroyImmediate(orders.gameObject);

            Assert.DoesNotThrow(() => director.Hook(null));
        }

        [Test]
        public void EachEvent_BecomesItsKindOfMoment()
        {
            director.OnStateApplied(GameState.Begin);

            FeatSync.Strike(0, 1, true, true);
            FeatSync.Strike(0, 1, false, false);
            FeatSync.Bump(0, 1, 0.5f);
            FeatSync.Deliver(0, Constants.OrderValue.Hard);
            FeatSync.Deliver(0, Constants.OrderValue.Easy);
            FeatSync.Sink(1);

            MomentKind[] expected =
            {
                MomentKind.GoldenSteal, MomentKind.Clash, MomentKind.Bump,
                MomentKind.HardDelivery, MomentKind.Delivery, MomentKind.Fall,
            };
            Assert.AreEqual(expected.Length, director.Moments.Count);
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], director.Moments[i].Kind, "moment " + i);
            Assert.AreEqual(0.5f, director.Moments[2].Strength, 0.0001f);
            Assert.AreEqual(1, director.Moments[2].Other);
            Assert.AreEqual(-1, director.Moments[3].Other, "a delivery has no second player");
            Assert.AreEqual(-1, director.Moments[5].Other, "a fall has no second player");
        }

        [Test]
        public void Events_OnAClient_AddNoMoments()
        {
            GameAuthority.Role = NetworkRole.Client;
            director.OnStateApplied(GameState.Begin);

            FeatSync.Strike(1, 0, true, false);
            FeatSync.Sink(0);

            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void Events_InTheTutorial_AddNoMoments()
        {
            director.OnStateApplied(GameState.Tutorial);

            FeatSync.Strike(1, 0, true, false);
            FeatSync.Deliver(0, Constants.OrderValue.Easy);

            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void Events_InTheGoldenRound_AddNoMoments()
        {
            director.OnStateApplied(GameState.Begin);
            director.OnStateApplied(GameState.FinalPackage);

            FeatSync.Strike(1, 0, true, false);

            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void MainGameFinishes_StopsSampling_AndLogging()
        {
            director.OnStateApplied(GameState.Begin);
            director.Sample(1.0);

            director.MainGameFinishes();
            director.Sample(2.0);
            FeatSync.Sink(0);

            Assert.IsFalse(director.Recording.TryGet(0, 2.0, out _), "nothing after the finish");
            Assert.IsTrue(director.Recording.TryGet(0, 1.0, out _), "what came before stays");
            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void NewMatch_ClearsRecordingAndMoments()
        {
            director.OnStateApplied(GameState.Begin);
            director.Sample(1.0);
            FeatSync.Sink(0);

            director.NewMatch();

            Assert.IsFalse(director.Recording.TryGet(0, 1.0, out _));
            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void EnteringBegin_StartsFresh()
        {
            director.OnStateApplied(GameState.Begin);
            director.Sample(1.0);
            FeatSync.Sink(0);

            director.OnStateApplied(GameState.Begin);

            Assert.IsFalse(director.Recording.TryGet(0, 1.0, out _));
            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void EnteringBegin_ReusesTheRecording_AndEmptiesIt()
        {
            director.OnStateApplied(GameState.Begin);
            ReelRecording first = director.Recording;
            director.Sample(1.0);

            director.OnStateApplied(GameState.Begin);

            Assert.IsNotNull(first);
            Assert.AreSame(first, director.Recording, "the 3 MB ring isn't allocated every round");
            Assert.IsFalse(director.Recording.TryGet(0, 1.0, out _), "but it starts empty");
        }

        [Test]
        public void Begin_Offline_PutsABumpWatcherOnEveryBall_Once()
        {
            director.OnStateApplied(GameState.Begin);
            director.OnStateApplied(GameState.Begin);

            foreach (BallDriving scooter in scooters)
                Assert.AreEqual(1, scooter.Sphere.GetComponents<BumpWatcher>().Length);
        }

        [Test]
        public void Begin_Online_AddsNoBumpWatcher()
        {
            GameAuthority.Role = NetworkRole.Host;

            director.OnStateApplied(GameState.Begin);

            foreach (BallDriving scooter in scooters)
                Assert.AreEqual(0, scooter.Sphere.GetComponents<BumpWatcher>().Length);
        }

        [Test]
        public void Sample_AllocatesNothing()
        {
            director.OnStateApplied(GameState.Begin);
            director.Sample(0.5); // warms it up

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 100; i++)
                director.Sample(1.0 + i * 0.05);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "bytes allocated over 100 samples");
        }
    }
}

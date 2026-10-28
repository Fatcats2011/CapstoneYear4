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
            director.OnStateApplied(GameState.MainLoop);

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
        public void Struck_DuringMainLoop_OnAuthority_AddsASteal()
        {
            director.OnStateApplied(GameState.MainLoop);
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
        public void EachEvent_BecomesItsKindOfMoment()
        {
            director.OnStateApplied(GameState.MainLoop);

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
            director.OnStateApplied(GameState.MainLoop);

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
            director.OnStateApplied(GameState.MainLoop);
            director.OnStateApplied(GameState.FinalPackage);

            FeatSync.Strike(1, 0, true, false);

            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void MainGameFinishes_StopsSampling_AndLogging()
        {
            director.OnStateApplied(GameState.MainLoop);
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
            director.OnStateApplied(GameState.MainLoop);
            director.Sample(1.0);
            FeatSync.Sink(0);

            director.NewMatch();

            Assert.IsFalse(director.Recording.TryGet(0, 1.0, out _));
            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void EnteringMainLoop_StartsFresh()
        {
            director.OnStateApplied(GameState.MainLoop);
            director.Sample(1.0);
            FeatSync.Sink(0);

            director.OnStateApplied(GameState.MainLoop);

            Assert.IsFalse(director.Recording.TryGet(0, 1.0, out _));
            Assert.AreEqual(0, director.Moments.Count);
        }

        [Test]
        public void MainLoop_Offline_PutsABumpWatcherOnEveryBall_Once()
        {
            director.OnStateApplied(GameState.MainLoop);
            director.OnStateApplied(GameState.MainLoop);

            foreach (BallDriving scooter in scooters)
                Assert.AreEqual(1, scooter.Sphere.GetComponents<BumpWatcher>().Length);
        }

        [Test]
        public void MainLoop_Online_AddsNoBumpWatcher()
        {
            GameAuthority.Role = NetworkRole.Host;

            director.OnStateApplied(GameState.MainLoop);

            foreach (BallDriving scooter in scooters)
                Assert.AreEqual(0, scooter.Sphere.GetComponents<BumpWatcher>().Length);
        }

        [Test]
        public void Sample_AllocatesNothing()
        {
            director.OnStateApplied(GameState.MainLoop);
            director.Sample(0.5); // warms it up

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 1; i <= 100; i++)
                director.Sample(1.0 + i * 0.05);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, allocated, "bytes allocated over 100 samples");
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The results screen's remake, without Play Mode: the extra things a match counts and their awards (MatchStats,
    /// AwardRules, MatchAwards), the ghosts' podium acts (PodiumActs,
    /// PodiumPerformer) and the rematch winner's crown (RematchCrown)
    /// </summary>
    public class ResultsRemakeTests
    {
        readonly TestObjects objects = new TestObjects();
        static readonly Dictionary<int, int> money = new Dictionary<int, int>();
        static readonly Dictionary<int, Vector3> places = new Dictionary<int, Vector3>();

        [TearDown]
        public void TearDown()
        {
            PodiumPerformer.StopAll();
            objects.DestroyAll();
            RematchCrown.Clear();
            AwardSync.Reset();
            money.Clear();
            places.Clear();
        }

        static int MoneyOf(int seat) { return money.TryGetValue(seat, out int has) ? has : -1; }

        static bool PlaceOf(int seat, out Vector3 place) { return places.TryGetValue(seat, out place); }

        static bool NobodyHoldsGold(int seat) { return false; }

        // ---- what a match counts

        [Test]
        public void Stats_BumpsClashesLeadAndDistance_Count_ButNotInTheTutorial()
        {
            MatchStats stats = new MatchStats();
            stats.Bumped(0, GameState.Begin);
            stats.Bumped(0, GameState.Tutorial);
            stats.Clashed(1, GameState.Begin);
            stats.Led(2, 12.4f);
            stats.Drove(3, 250.6f);

            Assert.AreEqual(1, stats.Count(0, Award.RoadHog));
            Assert.AreEqual(1, stats.Count(1, Award.Hothead));
            Assert.AreEqual(12, stats.Count(2, Award.FrontRunner));
            Assert.AreEqual(251, stats.Count(3, Award.RoadTrip));

            stats.NewMatch();
            Assert.AreEqual(0, stats.Count(3, Award.RoadTrip));
        }

        [Test]
        public void Awards_RoadTrip_GoesToOneDriverOnly_TheRestAreTourists()
        {
            MatchStats stats = new MatchStats();
            stats.Drove(0, 900f);
            stats.Drove(1, 1200f);
            stats.Drove(2, 1100f);

            Dictionary<int, Award> awards = AwardRules.Pick(stats, new[] { 0, 1, 2 });

            Assert.AreEqual(Award.RoadTrip, awards[1], "the furthest");
            Assert.AreEqual(Award.Tourist, awards[0]);
            Assert.AreEqual(Award.Tourist, awards[2]);
        }

        [Test]
        public void Awards_TheNewOnes_HaveTheirWords()
        {
            Assert.AreEqual("FRONT RUNNER", AwardRules.Name(Award.FrontRunner));
            Assert.AreEqual("led for 42s", AwardRules.Line(Award.FrontRunner, 42));
            Assert.AreEqual("HOTHEAD", AwardRules.Name(Award.Hothead));
            Assert.AreEqual("1 clash", AwardRules.Line(Award.Hothead, 1));
            Assert.AreEqual("ROAD HOG", AwardRules.Name(Award.RoadHog));
            Assert.AreEqual("3 bumps", AwardRules.Line(Award.RoadHog, 3));
            Assert.AreEqual("ROAD TRIP", AwardRules.Name(Award.RoadTrip));
            Assert.AreEqual("drove 800 m", AwardRules.Line(Award.RoadTrip, 800));
        }

        [Test]
        public void MatchAwards_Counts_TheLeadTheDrivingAndTheKnocks()
        {
            MatchAwards awards = objects.Add<MatchAwards>();
            awards.HoldsGolden = NobodyHoldsGold;
            awards.MoneyOf = MoneyOf;
            awards.PlaceOf = PlaceOf;
            awards.Begin();
            try
            {
                money[0] = 100;
                money[1] = 100;
                places[0] = Vector3.zero;
                places[1] = Vector3.zero;
                awards.Tick(1f, GameState.Begin);                // a tie leads nobody
                money[1] = 150;
                for (int i = 1; i <= 10; i++)
                {
                    places[0] = new Vector3(i * 5f, 0f, 0f);     // 5 m a second
                    awards.Tick(1f, GameState.MainLoop);
                }
                places[0] = new Vector3(5000f, 0f, 0f);          // a respawn, not a drive
                awards.Tick(1f, GameState.MainLoop);
                awards.Tick(30f, GameState.Tutorial);            // the tutorial counts for nothing
                FeatSync.Bump(0, 1, 0.5f);
                FeatSync.Strike(0, 1, false, false);             // a clash: both
                FeatSync.Strike(0, 1, true, false);              // a steal isn't one

                awards.Results(new[] { 0, 1 });

                AwardBoard board = AwardSync.Last;
                Assert.AreEqual(Award.FrontRunner, board.AwardOf(1));
                Assert.AreEqual(11, board.CountOf(1), "seconds alone in first");
                Assert.AreEqual(1, board.CountOf(0), "one bump");
                Assert.AreEqual(Award.RoadHog, board.AwardOf(0), "the clash is shared, the bump is theirs alone");
            }
            finally
            {
                awards.End();
            }
        }

        // ---- the podium acts

        [Test]
        public void EveryAct_StartsAndEndsStandingStill_AndNeverSinksIntoThePodium()
        {
            foreach (PodiumMood mood in new[] { PodiumMood.Winner, PodiumMood.Runner, PodiumMood.Last })
            {
                PodiumAct[] acts = PodiumActs.For(mood);
                Assert.GreaterOrEqual(acts.Length, 6, mood + ": enough acts that a results screen rarely repeats");
                foreach (PodiumAct act in acts)
                {
                    foreach (float toWinner in new[] { -70f, 0f, 30f })
                    {
                        foreach (float end in new[] { 0f, 1f })
                        {
                            PodiumPose pose = act.At(end, toWinner);
                            Assert.AreEqual(0f, pose.Lift, 1e-3f, act.Name);
                            Assert.AreEqual(0f, pose.Nose, 1e-2f, act.Name);
                            Assert.AreEqual(0f, Mathf.DeltaAngle(0f, pose.Yaw), 1e-2f, act.Name);
                            Assert.AreEqual(0f, pose.Lean, 1e-2f, act.Name);
                            Assert.AreEqual(1f, pose.Tall, 1e-3f, act.Name);
                            Assert.AreEqual(1f, pose.Wide, 1e-3f, act.Name);
                        }
                        for (float t = 0f; t <= 1f; t += 0.02f)
                        {
                            PodiumPose pose = act.At(t, toWinner);
                            Assert.GreaterOrEqual(pose.Lift, -1e-4f, act.Name + " at " + t);
                            Assert.Greater(pose.Tall, 0.5f, act.Name);
                            Assert.Greater(pose.Wide, 0.5f, act.Name);
                        }
                    }
                    Assert.Greater(act.Length, 0.5f, act.Name);
                }
            }
        }

        [Test]
        public void Moods_AndPoses_ByFinishingPlace()
        {
            Assert.AreEqual(PodiumMood.Winner, PodiumActs.MoodOf(0, 4));
            Assert.AreEqual(PodiumMood.Runner, PodiumActs.MoodOf(1, 2));
            Assert.AreEqual(PodiumMood.Runner, PodiumActs.MoodOf(2, 4));
            Assert.AreEqual(PodiumMood.Last, PodiumActs.MoodOf(3, 4));
            Assert.AreEqual(PodiumActs.HOORAY, PodiumActs.PoseOf(0));
            Assert.AreEqual(PodiumActs.LOSE, PodiumActs.PoseOf(3));
        }

        [Test]
        public void APerformer_PlaysActAfterAct_NeverTheSameTwiceRunning_AndStopsWhereItStood()
        {
            Transform model = objects.NewGameObject("Model").transform;
            model.localPosition = new Vector3(1f, 2f, 3f);
            model.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Vector3 position = model.localPosition;
            Quaternion rotation = model.localRotation;

            PodiumPerformer performer = PodiumPerformer.On(model, null, 0, 4, null, new System.Random(7));
            Assert.AreEqual(1, PodiumPerformer.Count);

            List<string> played = new List<string>();
            bool moved = false;
            for (int step = 0; step < 4000; step++)
            {
                performer.Tick(0.05f);
                if (performer.Playing != null && (played.Count == 0 || played[played.Count - 1] != performer.Playing.Name || !wasPlaying))
                    played.Add(performer.Playing.Name);
                wasPlaying = performer.Playing != null;
                moved |= (model.localPosition - position).sqrMagnitude > 1e-4f || Quaternion.Angle(model.localRotation, rotation) > 1f;
                if (performer.Playing == null)
                {
                    Assert.AreEqual(position, model.localPosition, "between acts it stands as it did");
                    Assert.Less(Quaternion.Angle(model.localRotation, rotation), 0.01f);
                }
            }

            Assert.IsTrue(moved);
            Assert.Greater(played.Count, 20);
            for (int i = 1; i < played.Count; i++)
                Assert.AreNotEqual(played[i - 1], played[i], "act " + i);
            Assert.AreEqual(PodiumActs.For(PodiumMood.Winner).Length, new HashSet<string>(played).Count, "every act gets its turn");

            performer.Begin(PodiumActs.For(PodiumMood.Winner)[1]);
            performer.Tick(0.4f);
            PodiumPerformer.StopAll();

            Assert.AreEqual(position, model.localPosition);
            Assert.Less(Quaternion.Angle(model.localRotation, rotation), 0.01f);
            Assert.AreEqual(Vector3.one, model.localScale);
            Assert.AreEqual(0, PodiumPerformer.Count);
            Assert.IsTrue(model.GetComponent<PodiumPerformer>() == null);
        }

        static bool wasPlaying;

        // ---- the winner's crown

        [Test]
        public void Winner_IsTheSeatAloneInFirst()
        {
            Assert.AreEqual(2, RematchCrown.Winner(new[] { 2, 0, 1 }, new[] { 1, 2, 3 }));
            Assert.AreEqual(RematchCrown.NONE, RematchCrown.Winner(new[] { 2, 0, 1 }, new[] { 1, 1, 3 }), "a tie crowns nobody");
            Assert.AreEqual(3, RematchCrown.Winner(new[] { 3 }, new[] { 1 }));
            Assert.AreEqual(RematchCrown.NONE, RematchCrown.Winner(new int[0], new int[0]));
        }

        [Test]
        public void TheCrown_GoesOnAtARematch_StaysThroughARestart_AndComesOffAtTheMenus()
        {
            RematchCrown.Won(2);
            Assert.IsFalse(RematchCrown.Wears(2), "not until the rematch");

            RematchCrown.Rematch();
            Assert.IsTrue(RematchCrown.Wears(2));
            Assert.IsFalse(RematchCrown.Wears(0));

            RematchCrown.Rematch(); // a restart from the pause menu: nobody has won since
            Assert.IsTrue(RematchCrown.Wears(2));

            RematchCrown.Won(0);
            RematchCrown.Rematch();
            Assert.IsTrue(RematchCrown.Wears(0), "the next winner takes it");
            Assert.IsFalse(RematchCrown.Wears(2));

            RematchCrown.Won(RematchCrown.NONE); // a tie: the crown stays where it was? No: nobody won, nothing changes hands
            RematchCrown.Rematch();
            Assert.IsTrue(RematchCrown.Wears(0));

            RematchCrown.Clear();
            Assert.IsFalse(RematchCrown.Wears(0));
            Assert.IsFalse(RematchCrown.Wears(-1));
        }

        [Test]
        public void TheCrownsArt_IsThePng()
        {
            Sprite art = RematchCrown.Art();

            Assert.IsNotNull(art);
            Assert.AreEqual("WinnerCrown", art.name, "Assets/Resources/UI/WinnerCrown.png, not the drawn fallback");
        }

        [Test]
        public void MatchReset_ForRematch_CrownsTheWinner()
        {
            RematchCrown.Won(1);
            bool skip = TutorialManager.SkipNextTutorial;
            try
            {
                MatchReset.ForRematch();
                Assert.IsTrue(RematchCrown.Wears(1));
            }
            finally
            {
                TutorialManager.SkipNextTutorial = skip;
            }
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Which stretches of a round make the highlight reel (HighlightRules): how much each moment weighs, how nearby moments
    /// join into clips, and which clips are picked so everyone gets a look-in without passing the reel's length
    /// </summary>
    public class HighlightRulesTests
    {
        static Moment M(MomentKind kind, double time, int seat, int other = -1, float strength = 0f, float timeLeft = 100f)
        {
            return new Moment { Kind = kind, Time = time, Seat = seat, Other = other, Strength = strength, TimeLeft = timeLeft };
        }

        static Clip C(double start, double end, float score, params int[] seats)
        {
            int mask = 0;
            foreach (int s in seats) mask |= 1 << s;
            return new Clip { Start = start, End = end, KeyTime = start, Score = score, SeatsMask = mask };
        }

        static double Total(List<Clip> clips)
        {
            double total = 0;
            foreach (Clip c in clips) total += c.Length; // picked clips never overlap, so the sum is the union
            return total;
        }

        [Test]
        public void Weight_TheTimedModesObjectives()
        {
            Assert.AreEqual(7f, HighlightRules.Weight(M(MomentKind.CrownTaken, 0, 0, 1)));
            Assert.AreEqual(13f, HighlightRules.Weight(M(MomentKind.CrownTaken, 0, 0, 1, 0f, 5f)), "a last-seconds crown swing");
            Assert.AreEqual(5f, HighlightRules.Weight(M(MomentKind.Infected, 0, 0, 1, 0f)));
            Assert.AreEqual(9f, HighlightRules.Weight(M(MomentKind.Infected, 0, 0, 1, 1f)), "the more it has spread, the more a catch weighs");
            Assert.AreEqual(12f, HighlightRules.Weight(M(MomentKind.LastCaught, 0, 0, 1)));
            Assert.AreEqual(2f, HighlightRules.Weight(M(MomentKind.PotatoPassed, 0, 0, 1, 0f)));
            Assert.AreEqual(9f, HighlightRules.Weight(M(MomentKind.PotatoPassed, 0, 0, 1, 1f)), "a pass with the fuse nearly gone");
            Assert.AreEqual(9f, HighlightRules.Weight(M(MomentKind.PotatoExploded, 0, 0)));
            Assert.AreEqual(12f, HighlightRules.Weight(M(MomentKind.LastStanding, 0, 0, 1)));
        }

        [Test]
        public void Weight_GoldRush_ARushHourStealCountsDouble()
        {
            Moment late = M(MomentKind.Steal, 0, 0, 1, 0f, 20f);
            Assert.AreEqual(8f, HighlightRules.Weight(late), "in Standard");
            Assert.AreEqual(12f, HighlightRules.Weight(late, GameMode.GoldRush));
            Assert.AreEqual(8f, HighlightRules.Weight(M(MomentKind.Steal, 0, 0, 1, 0f, 60f), GameMode.GoldRush), "before rush hour");
        }

        [Test]
        public void Clips_TheTimedModesHaveTheirOwnCaptions()
        {
            Assert.AreEqual(Caption.Crown, HighlightRules.Clips(new[] { M(MomentKind.CrownTaken, 1, 0, 1) })[0].Caption);
            Assert.AreEqual(Caption.Infection, HighlightRules.Clips(new[] { M(MomentKind.LastCaught, 1, 0, 1) })[0].Caption);
            Assert.AreEqual(Caption.Explosion, HighlightRules.Clips(new[] { M(MomentKind.PotatoExploded, 1, 0) })[0].Caption);
            Assert.AreEqual(Caption.Survivor, HighlightRules.Clips(new[] { M(MomentKind.LastStanding, 1, 0, 1) })[0].Caption);
        }

        [Test]
        public void Pick_InHotPotato_TheExplosionBeatsAClash()
        {
            Moment[] moments =
            {
                M(MomentKind.Clash, 10, 2, 3),
                M(MomentKind.PotatoExploded, 30, 0),
            };
            List<Clip> clips = HighlightRules.Clips(moments, GameMode.HotPotato);

            Assert.Greater(clips[1].Score, clips[0].Score);
        }

        [Test]
        public void Weight_StartValues()
        {
            Assert.AreEqual(10f, HighlightRules.Weight(M(MomentKind.GoldenSteal, 0, 0, 1)));
            Assert.AreEqual(8f, HighlightRules.Weight(M(MomentKind.Steal, 0, 0, 1)));
            Assert.AreEqual(6f, HighlightRules.Weight(M(MomentKind.Clash, 0, 0, 1)));
            Assert.AreEqual(1f, HighlightRules.Weight(M(MomentKind.Bump, 0, 0, 1, strength: 0f)), 1e-4f);
            Assert.AreEqual(4f, HighlightRules.Weight(M(MomentKind.Bump, 0, 0, 1, strength: 1f)), 1e-4f);
            Assert.AreEqual(5f, HighlightRules.Weight(M(MomentKind.HardDelivery, 0, 0, timeLeft: 100f)));
            Assert.AreEqual(2f, HighlightRules.Weight(M(MomentKind.Delivery, 0, 0, timeLeft: 100f)));
            Assert.AreEqual(8f, HighlightRules.Weight(M(MomentKind.HardDelivery, 0, 0, timeLeft: 8f)), "late bonus");
            Assert.AreEqual(5f, HighlightRules.Weight(M(MomentKind.Delivery, 0, 0, timeLeft: 10f)), "late is 10 s left or fewer");
            Assert.AreEqual(3f, HighlightRules.Weight(M(MomentKind.Fall, 0, 0)));
        }

        [Test]
        public void Fall_AfterBeingBumped_GetsTheKnockedInBonus()
        {
            var moments = new List<Moment>
            {
                M(MomentKind.Bump, 10, 0, 1),
                M(MomentKind.Fall, 12, 1),
                M(MomentKind.Fall, 14, 1),
                M(MomentKind.Fall, 12.5, 0), // not the victim
            };
            Assert.AreEqual(7f, HighlightRules.Weight(moments, 1), 1e-4f, "knocked in two seconds ago");
            Assert.AreEqual(3f, HighlightRules.Weight(moments, 2), 1e-4f, "four seconds is too long ago");
            Assert.AreEqual(3f, HighlightRules.Weight(moments, 3), 1e-4f, "the bumper didn't get knocked in");
            Assert.AreEqual(1f, HighlightRules.Weight(moments, 0), 1e-4f, "other kinds are just their base weight");
        }

        [Test]
        public void Clips_StealThenBumpThenFall_SharingASeat_MergeIntoOne()
        {
            var moments = new List<Moment>
            {
                M(MomentKind.Steal, 10, 0, 1),
                M(MomentKind.Bump, 11, 0, 1),
                M(MomentKind.Fall, 12, 1),
            };
            List<Clip> clips = HighlightRules.Clips(moments);
            Assert.AreEqual(1, clips.Count);
            Assert.AreEqual((8f + 1f + 7f) * 1.5f * 1.5f, clips[0].Score, 1e-3f);
            Assert.AreEqual(3, clips[0].SeatsMask);
            Assert.AreEqual(Caption.Theft, clips[0].Caption, "the steal is the heaviest moment");
            Assert.AreEqual(10.0, clips[0].KeyTime, 1e-6);
        }

        [Test]
        public void Clips_UnsortedMoments_AreHandledInTimeOrder()
        {
            var moments = new List<Moment>
            {
                M(MomentKind.Fall, 12, 1),
                M(MomentKind.Steal, 10, 0, 1),
                M(MomentKind.Bump, 11, 0, 1),
            };
            List<Clip> clips = HighlightRules.Clips(moments);
            Assert.AreEqual(1, clips.Count);
            Assert.AreEqual((8f + 1f + 7f) * 1.5f * 1.5f, clips[0].Score, 1e-3f);
        }

        [Test]
        public void Clips_SameTimeDifferentSeats_StaySeparate()
        {
            var moments = new List<Moment>
            {
                M(MomentKind.Delivery, 10, 0),
                M(MomentKind.Delivery, 10, 1),
            };
            List<Clip> clips = HighlightRules.Clips(moments);
            Assert.AreEqual(2, clips.Count);
            Assert.AreEqual(2f, clips[0].Score, 1e-4f);
        }

        [Test]
        public void Clips_FarApartInTime_StaySeparate()
        {
            var moments = new List<Moment> { M(MomentKind.Delivery, 10, 0), M(MomentKind.Delivery, 15, 0) };
            Assert.AreEqual(2, HighlightRules.Clips(moments).Count);
        }

        [Test]
        public void Clips_Length_ClampedToThreeToSix()
        {
            List<Clip> one = HighlightRules.Clips(new List<Moment> { M(MomentKind.Delivery, 10, 0) });
            Assert.AreEqual(3.0, one[0].Length, 1e-6, "lead + tail is 3 s");
            Assert.AreEqual(8.5, one[0].Start, 1e-6);

            // a chain of deliveries 3 s apart for one seat: 10 to 22 is 15 s raw
            var chain = new List<Moment>();
            for (int i = 0; i < 5; i++) chain.Add(M(MomentKind.Delivery, 10 + 3 * i, 0));
            List<Clip> trimmed = HighlightRules.Clips(chain);
            Assert.AreEqual(1, trimmed.Count);
            Assert.AreEqual(6.0, trimmed[0].Length, 1e-6);
            Assert.AreEqual(23.5, trimmed[0].End, 1e-6, "too long trims the start, keeps the end");
        }

        [Test]
        public void Pick_EveryoneWithAGoodEnoughClip_GetsOne()
        {
            var clips = new List<Clip>
            {
                C(0, 6, 20, 0), C(10, 16, 20, 0), C(20, 26, 20, 0), C(30, 36, 20, 0),
                C(40, 46, 4, 1),
            };
            List<Clip> picked = HighlightRules.Pick(clips, new[] { 0, 1 });
            Assert.IsTrue(picked.Exists(c => (c.SeatsMask & 2) != 0), "seat 1's clip makes it, though it scores under GOOD_ENOUGH");
            Assert.LessOrEqual(Total(picked), HighlightRules.MAX_REEL + 1e-6);
        }

        [Test]
        public void Pick_NeverPassesTwentySeconds()
        {
            var clips = new List<Clip>();
            for (int i = 0; i < 10; i++) clips.Add(C(i * 10, i * 10 + 5, 10, i % 2));
            List<Clip> picked = HighlightRules.Pick(clips, new[] { 0, 1 });
            Assert.AreEqual(4, picked.Count);
            Assert.LessOrEqual(Total(picked), HighlightRules.MAX_REEL + 1e-6);
        }

        [Test]
        public void Pick_OnlyTinyMoments_PicksNothing()
        {
            List<Clip> clips = HighlightRules.Clips(new List<Moment> { M(MomentKind.Bump, 10, 0, 1, strength: 0.1f) });
            Assert.IsEmpty(HighlightRules.Pick(clips, new[] { 0, 1 }));
        }

        [Test]
        public void Pick_ReturnsMatchOrder()
        {
            var clips = new List<Clip> { C(50, 55, 30, 0), C(10, 15, 10, 1), C(30, 35, 20, 0) };
            List<Clip> picked = HighlightRules.Pick(clips, new[] { 0, 1 });
            Assert.AreEqual(3, picked.Count);
            Assert.Less(picked[0].Start, picked[1].Start);
            Assert.Less(picked[1].Start, picked[2].Start);
        }

        [Test]
        public void Pick_OverlappingClips_AreMergedAndCountedOnce()
        {
            var a = new Clip { Start = 10, End = 15, KeyTime = 12, Score = 10, SeatsMask = 1, Caption = Caption.Theft };
            var b = new Clip { Start = 13, End = 18, KeyTime = 14, Score = 6, SeatsMask = 2, Caption = Caption.Water };
            List<Clip> picked = HighlightRules.Pick(new List<Clip> { a, b }, new[] { 0, 1 });
            Assert.AreEqual(1, picked.Count);
            Assert.AreEqual(10.0, picked[0].Start, 1e-6);
            Assert.AreEqual(18.0, picked[0].End, 1e-6);
            Assert.AreEqual(16f, picked[0].Score, 1e-4f);
            Assert.AreEqual(3, picked[0].SeatsMask);
            Assert.AreEqual(Caption.Theft, picked[0].Caption, "keeps the higher-scoring clip's caption");
            Assert.AreEqual(12.0, picked[0].KeyTime, 1e-6);
        }
    }
}

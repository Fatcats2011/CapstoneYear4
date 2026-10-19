using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    public class MatchRulesTests
    {
        private static (ulong lobby, ulong owner, string game, string build, string mm, string open) L(
            ulong lobby, string open, string game = "doa", string build = "b", string mm = "1")
        {
            return (lobby, 100 + lobby, game, build, mm, open);
        }

        [Test]
        public void ParseOpenSeats_Valid()
        {
            Assert.AreEqual(0, MatchRules.ParseOpenSeats("0"));
            Assert.AreEqual(4, MatchRules.ParseOpenSeats("4"));
        }

        [Test]
        public void ParseOpenSeats_Invalid()
        {
            foreach (string tag in new string[] { null, "", "-1", "5", "99", "4abc", " 3", new string('9', 10000) })
                Assert.AreEqual(-1, MatchRules.ParseOpenSeats(tag), tag == null ? "null" : tag.Length > 20 ? "long" : tag);
        }

        [Test]
        public void Candidates_FullestFirst()
        {
            var found = new List<(ulong, ulong, string, string, string, string)> { L(10, "3"), L(11, "1"), L(12, "2") };

            List<MatchRules.Candidate> result = MatchRules.Candidates(found, "b", 99, 1);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(1, result[0].OpenSeats);
            Assert.AreEqual(2, result[1].OpenSeats);
            Assert.AreEqual(3, result[2].OpenSeats);
        }

        [Test]
        public void Candidates_TiesBreakByLobbyId()
        {
            var found = new List<(ulong, ulong, string, string, string, string)> { L(30, "2"), L(20, "2") };

            List<MatchRules.Candidate> result = MatchRules.Candidates(found, "b", 99, 1);

            Assert.AreEqual(20UL, result[0].Lobby);
            Assert.AreEqual(30UL, result[1].Lobby);
        }

        [Test]
        public void Candidates_GarbageTags_Skipped()
        {
            var found = new List<(ulong, ulong, string, string, string, string)>
            {
                L(10, "1", game: "other"), L(11, "1", build: "x"), L(12, "1", mm: ""), L(13, "1", mm: "0"),
                L(14, "-1"), L(15, "99"), L(16, "x"), L(0, "1"), L(17, "1"), L(18, null), L(19, "1", game: null),
            };

            List<MatchRules.Candidate> result = null;
            Assert.DoesNotThrow(() => result = MatchRules.Candidates(found, "b", 17, 1));
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Candidates_NotEnoughSeats_Skipped()
        {
            var found = new List<(ulong, ulong, string, string, string, string)> { L(10, "1") };

            Assert.AreEqual(0, MatchRules.Candidates(found, "b", 99, 2).Count);
        }

        [Test]
        public void Candidates_CapsInput()
        {
            var found = new List<(ulong, ulong, string, string, string, string)>();
            for (ulong i = 1; i <= 200; i++)
                found.Add(L(i, "1"));

            Assert.LessOrEqual(MatchRules.Candidates(found, "b", 999, 1).Count, MatchRules.MAX_RESULTS);
        }

        [Test]
        public void MayYieldTo_HigherOnly()
        {
            Assert.IsTrue(MatchRules.MayYieldTo(5, 3));
            Assert.IsFalse(MatchRules.MayYieldTo(3, 5));
            Assert.IsFalse(MatchRules.MayYieldTo(4, 4));
        }

        [Test]
        public void AutoStartRemaining_Nonsense_Clamped()
        {
            double now = 1000;
            Assert.AreEqual(0f, MatchRules.AutoStartRemaining(double.NaN, now));
            Assert.AreEqual(0f, MatchRules.AutoStartRemaining(double.PositiveInfinity, now));
            Assert.AreEqual(0f, MatchRules.AutoStartRemaining(double.NegativeInfinity, now));
            Assert.AreEqual(0f, MatchRules.AutoStartRemaining(0, now));
            Assert.AreEqual(60f, MatchRules.AutoStartRemaining(now + 1e300, now));
            Assert.AreEqual(0f, MatchRules.AutoStartRemaining(now - 10, now));
            Assert.AreEqual(30f, MatchRules.AutoStartRemaining(now + 30, now));
        }

        [Test]
        public void AutoStartAt_AddsTheDelay()
        {
            Assert.AreEqual(160.0, MatchRules.AutoStartAt(100));
        }

        [Test]
        public void AutoStart_TestOverride_ShortensTheDelayAndTheClamp()
        {
            MatchRules.AutoStartSecondsOverride = 2;
            try
            {
                Assert.AreEqual(102.0, MatchRules.AutoStartAt(100));
                Assert.AreEqual(2f, MatchRules.AutoStartRemaining(1000, 100), "clamped to the shorter delay");
            }
            finally
            {
                MatchRules.AutoStartSecondsOverride = 0;
            }
            Assert.AreEqual(160.0, MatchRules.AutoStartAt(100), "0 = AUTO_START");
        }

        [Test]
        public void StatusLine_Copy()
        {
            Assert.AreEqual("LOOKING FOR A MATCH…", MatchRules.StatusLine(true, true, 10));
            Assert.AreEqual("OPEN TO ANYONE · STARTS IN 30", MatchRules.StatusLine(true, false, 29.2f));
            Assert.AreEqual("OPEN TO ANYONE", MatchRules.StatusLine(true, false, 0));
            Assert.AreEqual("FRIENDS ONLY", MatchRules.StatusLine(false, false, 0));
        }

        [Test]
        public void SearchAllowed_Gap()
        {
            Assert.IsTrue(MatchRules.SearchAllowed(double.NegativeInfinity, 0));
            Assert.IsFalse(MatchRules.SearchAllowed(10, 14.9));
            Assert.IsTrue(MatchRules.SearchAllowed(10, 15));
        }

        [Test]
        public void FillPrompt_WhatPressingDoes()
        {
            Assert.AreEqual("OPEN TO ANYONE", MatchRules.FillPrompt(false));
            Assert.AreEqual("FRIENDS ONLY", MatchRules.FillPrompt(true));
        }

        [Test]
        public void NoneFound_Copy()
        {
            Assert.AreEqual("No open matches right now. Your lobby is open to anyone.", MatchRules.NONE_FOUND);
            Assert.AreEqual("No open matches right now.", MatchRules.NONE_FOUND_CLOSED);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The scoreboard's "+$" and count-up (Scoreboard.MoneyShow: a rise shows a gain that adds up and fades, the number
    /// counts up to its total, a fall or a reset shows nothing) and its lead watch (Scoreboard.WatchLead: a change of
    /// first place is announced, a tie or the first leader is not, and a mode that doesn't call leads starts it over)
    /// </summary>
    public class ScoreboardGainTests
    {
        readonly Dictionary<int, int> points = new Dictionary<int, int>();
        readonly List<int[]> leads = new List<int[]>();
        GameObject holder;
        Scoreboard board;

        int PointsOf(int seat)
        {
            return points[seat];
        }

        void HearLead(int newLeader, int oldLeader)
        {
            leads.Add(new[] { newLeader, oldLeader });
        }

        [SetUp]
        public void SetUp()
        {
            ScoreFeedback.LeadTaken += HearLead;
            holder = new GameObject("Scoreboard Gain Test");
            board = holder.AddComponent<Scoreboard>();
        }

        [TearDown]
        public void TearDown()
        {
            ScoreFeedback.LeadTaken -= HearLead;
            leads.Clear();
            points.Clear();
            Object.DestroyImmediate(holder);
        }

        static Scoreboard.MoneyShow Fresh()
        {
            Scoreboard.MoneyShow show = new Scoreboard.MoneyShow { Total = -1 };
            return show;
        }

        [Test]
        public void MoneyShow_FirstTotal_ShowsNoGain()
        {
            Scoreboard.MoneyShow show = Fresh();

            show.See(0, 1f, true);

            Assert.AreEqual(0, show.Gain);
            Assert.AreEqual("", show.GainText(1f));
            Assert.AreEqual(0, show.Shown(1f));
        }

        [Test]
        public void MoneyShow_FirstTotalAboveZero_ShowsNoGain()
        {
            Scoreboard.MoneyShow show = Fresh();

            show.See(300, 1f, true);

            Assert.AreEqual("", show.GainText(1f), "joining a match with money isn't a gain");
            Assert.AreEqual(300, show.Shown(1f));
        }

        [Test]
        public void MoneyShow_ARise_ShowsTheGain_AndCountsUp()
        {
            Scoreboard.MoneyShow show = Fresh();
            show.See(0, 1f, true);

            show.See(65, 2f, true);

            Assert.AreEqual("+$65", show.GainText(2f));
            Assert.AreEqual(0, show.Shown(2f));
            Assert.That(show.Shown(2.2f), Is.EqualTo(32).Or.EqualTo(33));
            Assert.AreEqual(65, show.Shown(2.4f));
            Assert.AreEqual(65, show.Shown(9f));
        }

        [Test]
        public void MoneyShow_AnotherRiseWhileShowing_AddsUp_AndRestartsTheTime()
        {
            Scoreboard.MoneyShow show = Fresh();
            show.See(0, 1f, true);
            show.See(65, 2f, true);

            show.See(145, 2.5f, true);

            Assert.AreEqual("+$145", show.GainText(2.5f));
            Assert.AreEqual(65, show.Shown(2.5f), "it counts up from what was showing");
            Assert.AreEqual("+$145", show.GainText(3.6f), "the time started again at the second gain");
            Assert.AreEqual(145, show.Shown(3f));
        }

        [Test]
        public void MoneyShow_ARiseAfterTheGainHasFaded_StartsANewOne()
        {
            Scoreboard.MoneyShow show = Fresh();
            show.See(0, 1f, true);
            show.See(145, 2f, true);
            Assert.AreEqual("", show.GainText(4f));

            show.See(155, 5f, true);

            Assert.AreEqual("+$10", show.GainText(5f));
        }

        [Test]
        public void MoneyShow_AFallOrReset_ShowsNothing()
        {
            Scoreboard.MoneyShow show = Fresh();
            show.See(0, 1f, true);
            show.See(145, 2f, true);

            show.See(0, 6f, true);

            Assert.AreEqual(0, show.Gain);
            Assert.AreEqual(0, show.Shown(6f));
            Assert.AreEqual("", show.GainText(6f));
        }

        [Test]
        public void MoneyShow_AResetWhileShowing_ClearsAtOnce()
        {
            Scoreboard.MoneyShow show = Fresh();
            show.See(0, 1f, true);
            show.See(145, 2f, true);

            show.See(0, 2.1f, true);

            Assert.AreEqual("", show.GainText(2.1f));
            Assert.AreEqual(0, show.Shown(2.1f));
        }

        [Test]
        public void MoneyShow_GainsNotShown_NeverAGain_AndTheTotalAtOnce()
        {
            Scoreboard.MoneyShow show = Fresh();
            show.See(0, 1f, false);

            show.See(65, 2f, false);

            Assert.AreEqual("", show.GainText(2f));
            Assert.AreEqual(65, show.Shown(2f));
        }

        [Test]
        public void WatchLead_FirstLeader_IsNotAnnounced()
        {
            points[0] = 0;
            points[1] = 50;

            board.WatchLead(new List<int> { 1, 0 }, PointsOf, GameMode.Standard);

            Assert.AreEqual(1, board.Leader);
            Assert.IsEmpty(leads);
        }

        [Test]
        public void WatchLead_SomeonePasses_IsAnnouncedOnce()
        {
            points[0] = 0;
            points[1] = 50;
            board.WatchLead(new List<int> { 1, 0 }, PointsOf, GameMode.Standard);

            points[0] = 80;
            board.WatchLead(new List<int> { 0, 1 }, PointsOf, GameMode.Standard);
            board.WatchLead(new List<int> { 0, 1 }, PointsOf, GameMode.Standard);

            Assert.AreEqual(1, leads.Count);
            Assert.AreEqual(0, leads[0][0]);
            Assert.AreEqual(1, leads[0][1]);
            Assert.AreEqual(0, board.Leader);
        }

        [Test]
        public void WatchLead_ATie_ChangesNothing()
        {
            points[0] = 0;
            points[1] = 50;
            board.WatchLead(new List<int> { 1, 0 }, PointsOf, GameMode.Standard);

            points[0] = 50;
            board.WatchLead(new List<int> { 0, 1 }, PointsOf, GameMode.Standard);

            Assert.IsEmpty(leads);
            Assert.AreEqual(1, board.Leader);
        }

        [Test]
        public void WatchLead_AModeThatDoesntCallLeads_StartsOver()
        {
            points[0] = 0;
            points[1] = 50;
            board.WatchLead(new List<int> { 1, 0 }, PointsOf, GameMode.Standard);

            board.WatchLead(new List<int> { 1, 0 }, PointsOf, GameMode.Infection);
            Assert.AreEqual(-1, board.Leader);
            Assert.IsEmpty(leads);

            points[0] = 80;
            board.WatchLead(new List<int> { 0, 1 }, PointsOf, GameMode.Standard);

            Assert.IsEmpty(leads, "back in a mode that calls leads, the first leader isn't announced");
            Assert.AreEqual(0, board.Leader);
        }
    }
}

using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// A gain or a change of lead is announced (ScoreFeedback), for the callouts: once each, as it was said. A gain of
    /// nothing or less, or for no seat, is never heard, and no stamp arrives as ""
    /// </summary>
    public class ScoreFeedbackTests
    {
        struct Gain
        {
            public int Seat;
            public GainReason Reason;
            public int Amount;
            public string Stamp;
        }

        readonly List<Gain> gains = new List<Gain>();
        readonly List<int[]> leads = new List<int[]>();

        void HearGain(int seat, GainReason reason, int amount, string stamp)
        {
            gains.Add(new Gain { Seat = seat, Reason = reason, Amount = amount, Stamp = stamp });
        }

        void HearLead(int newLeader, int oldLeader)
        {
            leads.Add(new[] { newLeader, oldLeader });
        }

        [SetUp]
        public void SetUp()
        {
            ScoreFeedback.Gained += HearGain;
            ScoreFeedback.LeadTaken += HearLead;
        }

        [TearDown]
        public void TearDown()
        {
            ScoreFeedback.Gained -= HearGain;
            ScoreFeedback.LeadTaken -= HearLead;
            gains.Clear();
            leads.Clear();
        }

        [Test]
        public void AnnounceGain_IsHeardOnce_AsItWasSaid()
        {
            ScoreFeedback.AnnounceGain(2, GainReason.Golden, 212, "×2");

            Assert.AreEqual(1, gains.Count);
            Assert.AreEqual(2, gains[0].Seat);
            Assert.AreEqual(GainReason.Golden, gains[0].Reason);
            Assert.AreEqual(212, gains[0].Amount);
            Assert.AreEqual("×2", gains[0].Stamp);
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void AnnounceGain_NothingOrLess_IsNeverHeard(int amount)
        {
            ScoreFeedback.AnnounceGain(0, GainReason.Delivered, amount, "");

            Assert.IsEmpty(gains);
        }

        [Test]
        public void AnnounceGain_ForNoSeat_IsNeverHeard()
        {
            ScoreFeedback.AnnounceGain(-1, GainReason.Delivered, 65, "");

            Assert.IsEmpty(gains);
        }

        [Test]
        public void AnnounceGain_NoStamp_ArrivesAsEmpty()
        {
            ScoreFeedback.AnnounceGain(0, GainReason.Delivered, 65, null);

            Assert.AreEqual(1, gains.Count);
            Assert.AreEqual("", gains[0].Stamp);
        }

        [Test]
        public void AnnounceLead_IsHeardOnce_WithBothSeats()
        {
            ScoreFeedback.AnnounceLead(0, 1);

            Assert.AreEqual(1, leads.Count);
            Assert.AreEqual(0, leads[0][0]);
            Assert.AreEqual(1, leads[0][1]);
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Bot personalities (BotPersonality): the first bot seated is the thief, then the hunter, then the courier; and their
    /// weights tell them apart
    /// </summary>
    public class BotPersonalityTests
    {
        [Test]
        public void ForRank_OneThiefThenHunterThenCourier()
        {
            Assert.AreEqual(BotKind.Thief, BotPersonality.ForRank(0));
            Assert.AreEqual(BotKind.Hunter, BotPersonality.ForRank(1));
            Assert.AreEqual(BotKind.Courier, BotPersonality.ForRank(2));
            Assert.AreEqual(BotKind.Courier, BotPersonality.ForRank(3), "never a second thief");
            Assert.AreEqual(BotKind.Courier, BotPersonality.ForRank(-1));
        }

        [Test]
        public void RankOf_LowestBotSeatFirst()
        {
            int[] seats = { 3, 1, 2, 1 };

            Assert.AreEqual(0, BotPersonality.RankOf(1, seats));
            Assert.AreEqual(1, BotPersonality.RankOf(2, seats), "a seat listed twice counts once");
            Assert.AreEqual(2, BotPersonality.RankOf(3, seats));
        }

        [Test]
        public void RankOf_UnknownSeat_IsCourierNotThief()
        {
            Assert.AreEqual(2, BotPersonality.RankOf(-1, new[] { 1, 2 }));
        }

        [Test]
        public void Profiles_AreDistinct()
        {
            BotProfile thief = BotPersonality.For(BotKind.Thief);
            BotProfile hunter = BotPersonality.For(BotKind.Hunter);
            BotProfile courier = BotPersonality.For(BotKind.Courier);

            Assert.Greater(thief.StealAppetite, 0f);
            Assert.AreEqual(0f, hunter.StealAppetite);
            Assert.AreEqual(0f, courier.StealAppetite);
            Assert.Greater(hunter.ValuePerPoint, thief.ValuePerPoint);
            Assert.Greater(hunter.ValuePerPoint, courier.ValuePerPoint);
            Assert.Greater(courier.DistancePerMetre, thief.DistancePerMetre);
            Assert.Greater(courier.DistancePerMetre, hunter.DistancePerMetre);
            Assert.IsTrue(courier.Dodges);
            Assert.IsFalse(thief.Dodges || hunter.Dodges);
            Assert.Less(courier.BoostEvery, thief.BoostEvery);
            Assert.Less(courier.BoostEvery, hunter.BoostEvery);
        }
    }
}

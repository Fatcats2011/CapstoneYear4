using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>A CPU driver's round in a log line (BotStats), to check the personalities really play apart</summary>
    public class BotStatsTests
    {
        [Test]
        public void Line_AveragesAndCounts()
        {
            BotStats stats = new BotStats();
            stats.PickedUp(55, 20f);
            stats.PickedUp(80, 60f);
            stats.Tried();
            stats.Stole();

            Assert.AreEqual("Bot seat 2 Thief: 1 steal tries, 1 steals, 2 pickups, avg value 68, avg pickup 40 m", stats.Line(BotKind.Thief, 2));
        }

        [Test]
        public void Line_NothingYet_ZeroAverages()
        {
            Assert.AreEqual("Bot seat 1 Courier: 0 steal tries, 0 steals, 0 pickups, avg value 0, avg pickup 0 m", new BotStats().Line(BotKind.Courier, 1));
        }
    }
}

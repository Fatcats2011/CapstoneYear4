using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// JoinChurn: a Steam ID that keeps reconnecting is refused for the session. A fake clock, in seconds
    /// </summary>
    public class JoinChurnTests
    {
        double now;
        JoinChurn churn;

        [SetUp]
        public void SetUp()
        {
            now = 0;
            churn = new JoinChurn(Now);
        }

        double Now()
        {
            return now;
        }

        [Test]
        public void FiveInAMinute_Allowed()
        {
            for (int i = 0; i < JoinChurn.CHURN_LIMIT; i++)
            {
                Assert.IsTrue(churn.Allow(7), "connection " + (i + 1));
                now += 5;
            }
        }

        [Test]
        public void SixthInAMinute_Refused()
        {
            for (int i = 0; i < JoinChurn.CHURN_LIMIT; i++)
            {
                churn.Allow(7);
                now += 5;
            }

            Assert.IsFalse(churn.Allow(7));
            Assert.IsTrue(churn.Allow(8), "another ID is unaffected");
        }

        [Test]
        public void Refused_StaysRefused_AfterWindow()
        {
            for (int i = 0; i <= JoinChurn.CHURN_LIMIT; i++)
                churn.Allow(7);

            now += 10 * JoinChurn.CHURN_WINDOW;

            Assert.IsFalse(churn.Allow(7));
        }

        [Test]
        public void SpreadOut_Allowed()
        {
            for (int i = 0; i < 6; i++)
            {
                Assert.IsTrue(churn.Allow(7), "connection " + (i + 1));
                now += 61;
            }
        }

        [Test]
        public void ManyIds_Bounded()
        {
            for (ulong id = 1; id <= 1000; id++)
            {
                churn.Allow(id);
                Assert.LessOrEqual(churn.Tracked, JoinChurn.MAX_TRACKED);
            }

            Assert.AreEqual(JoinChurn.MAX_TRACKED, churn.Tracked);
        }

        [Test]
        public void RefusedIds_AreKept_WhenTheCapDropsOthers()
        {
            for (int i = 0; i <= JoinChurn.CHURN_LIMIT; i++)
                churn.Allow(7);

            for (ulong id = 100; id < 300; id++)
                churn.Allow(id);

            Assert.IsFalse(churn.Allow(7), "still refused");
        }

        [Test]
        public void AllRefused_AtTheCap_StaysBounded()
        {
            for (ulong id = 1; id <= 100; id++)
            {
                for (int i = 0; i <= JoinChurn.CHURN_LIMIT; i++)
                    churn.Allow(id);
                Assert.LessOrEqual(churn.Tracked, JoinChurn.MAX_TRACKED);
            }
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// A CPU driver's chase (BotChase): it gives up on a victim after its time limit, and leaves that victim alone for a
    /// while after
    /// </summary>
    public class BotChaseTests
    {
        [Test]
        public void GivesUpAfterTheLimit_ThenBarsThatVictim()
        {
            BotChase chase = new BotChase();

            Assert.IsFalse(chase.Tick(2, 5f, 4f));
            Assert.IsTrue(chase.Tick(2, 5f, 2f), "6 s of a 5 s chase");
            Assert.IsTrue(chase.Barred(2));
            Assert.IsFalse(chase.Barred(3));
            chase.Advance(7f);
            Assert.IsFalse(chase.Barred(2), "forgiven after FORGET_VICTIM");
        }

        [Test]
        public void NewVictim_FreshTimer()
        {
            BotChase chase = new BotChase();

            chase.Tick(2, 5f, 4f);
            Assert.IsFalse(chase.Tick(3, 5f, 4f));
        }

        [Test]
        public void NoVictim_Resets()
        {
            BotChase chase = new BotChase();

            chase.Tick(2, 5f, 4f);
            chase.Tick(-1, 5f, 0.1f);
            Assert.IsFalse(chase.Tick(2, 5f, 4f), "a fresh chase");
        }
    }
}

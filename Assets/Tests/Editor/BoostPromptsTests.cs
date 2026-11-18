using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>The saved count of online matches played (BoostPrompts): only a match reaching Results online counts</summary>
    public class BoostPromptsTests
    {
        [SetUp]
        public void SetUp()
        {
            BoostPrompts.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            BoostPrompts.Reset();
        }

        [Test]
        public void OnlineMatch_CountsOnlyOnResults()
        {
            int raised = 0;
            BoostPrompts.Changed += count => raised++;

            BoostPrompts.OnResults(false);
            Assert.AreEqual(0, BoostPrompts.OnlineMatches, "an offline match is not counted");
            Assert.AreEqual(0, raised);

            BoostPrompts.OnResults(true);
            Assert.AreEqual(1, BoostPrompts.OnlineMatches);
            Assert.AreEqual(1, raised);
        }
    }
}

using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>The Waves mutator spreads the scene's authored wave rows over the waves played, easy to hard (WaveTable)</summary>
    public class WaveTableTests
    {
        [TestCase(0, 3, 3, 0)]
        [TestCase(1, 3, 3, 1)]
        [TestCase(2, 3, 3, 2)]
        [TestCase(0, 1, 3, 2)]
        [TestCase(0, 2, 3, 0)]
        [TestCase(1, 2, 3, 2)]
        [TestCase(0, 6, 3, 0)]
        [TestCase(2, 6, 3, 1)]
        [TestCase(5, 6, 3, 2)]
        [TestCase(9, 6, 3, 2)]
        [TestCase(3, 6, 1, 0)]
        public void IndexFor(int wave, int waves, int authored, int row)
        {
            Assert.AreEqual(row, WaveTable.IndexFor(wave, waves, authored));
        }
    }
}

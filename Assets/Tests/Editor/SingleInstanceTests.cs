using System.Threading;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// A second copy of the game quits (playtest, 2026-10-24: two copies ran at once)
    /// </summary>
    public class SingleInstanceTests
    {
        [TestCase(false, new string[0], ExpectedResult = true)]                  // a build
        [TestCase(true, new string[0], ExpectedResult = false)]                  // the editor, and ParrelSync clones
        [TestCase(false, new[] { "-MultiInstance" }, ExpectedResult = false)]    // two builds testing online
        public bool Checks(bool editor, string[] args)
        {
            return SingleInstance.Checks(editor, args);
        }

        [Test]
        public void TryClaim_TheSecondTime_GetsNothing_UntilTheFirstLetsGo()
        {
            const string name = "Local\\DeadOnArrival.Tests.SingleInstance";
            Mutex first = SingleInstance.TryClaim(name);
            try
            {
                Assert.IsNotNull(first, "first copy");
                Assert.IsNull(SingleInstance.TryClaim(name), "second copy");
            }
            finally
            {
                first?.ReleaseMutex();
                first?.Dispose();
            }

            Mutex again = SingleInstance.TryClaim(name);
            Assert.IsNotNull(again, "after the first quit");
            again.ReleaseMutex();
            again.Dispose();
        }
    }
}

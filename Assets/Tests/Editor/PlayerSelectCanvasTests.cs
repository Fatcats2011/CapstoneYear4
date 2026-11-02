using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Stopping the ready-up countdown once the lobby is gone: online, a joining machine's countdown was still marked as
    /// running when the match loaded, and stopping it at the opening cutscene reached the menu scene's destroyed
    /// countdown (playtest, 2026-10-31)
    /// </summary>
    public class PlayerSelectCanvasTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void StopCountdown_WithItsAnimatorGone_StillTellsItStopped()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            int stopped = 0;
            canvas.CountdownStopped += () => stopped++;

            Assert.DoesNotThrow(() => canvas.StopCountdown());
            Assert.AreEqual(1, stopped);
        }
    }
}

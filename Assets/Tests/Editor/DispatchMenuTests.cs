using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Dispatch's buttons: X opens the sliders, B closes them before it leaves
    /// </summary>
    public class DispatchMenuTests
    {
        [TestCase(false, DispatchButton.West, false, DispatchAction.OpenSliders, true)]
        [TestCase(true, DispatchButton.West, false, DispatchAction.CloseSliders, false)]
        [TestCase(true, DispatchButton.West, true, DispatchAction.CloseSliders, false)]
        [TestCase(false, DispatchButton.West, true, DispatchAction.None, false)]
        [TestCase(true, DispatchButton.Pad, false, DispatchAction.Scroll, true)]
        [TestCase(false, DispatchButton.Pad, false, DispatchAction.None, false)]
        [TestCase(true, DispatchButton.East, false, DispatchAction.CloseSliders, false)]
        [TestCase(false, DispatchButton.East, false, DispatchAction.Leave, false)]
        [TestCase(false, DispatchButton.East, true, DispatchAction.Unready, false)]
        [TestCase(false, DispatchButton.South, false, DispatchAction.Ready, false)]
        [TestCase(true, DispatchButton.South, false, DispatchAction.CloseAndReady, false)]
        [TestCase(false, DispatchButton.South, true, DispatchAction.None, false)]
        [TestCase(true, DispatchButton.North, false, DispatchAction.Invite, true)]
        [TestCase(false, DispatchButton.North, false, DispatchAction.Invite, false)]
        public void Press_DoesTheAction_AndOpensOrClosesTheSliders(bool openBefore, DispatchButton button, bool readied,
            DispatchAction action, bool openAfter)
        {
            DispatchMenu menu = new DispatchMenu();
            if (openBefore)
                menu.Press(DispatchButton.West, false);
            Assert.AreEqual(openBefore, menu.Open, "setup");

            Assert.AreEqual(action, menu.Press(button, readied));
            Assert.AreEqual(openAfter, menu.Open, "open after");
        }

        [Test]
        public void Reset_ClosesTheSliders()
        {
            DispatchMenu menu = new DispatchMenu();
            menu.Press(DispatchButton.West, false);

            menu.Reset();

            Assert.IsFalse(menu.Open);
        }
    }
}

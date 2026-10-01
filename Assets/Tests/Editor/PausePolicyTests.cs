using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// What the pause menu does online, where pausing doesn't stop the match: it stays open while players drive and closes
    /// for anything else, and pausing says what Main Menu does on this machine
    /// </summary>
    public class PausePolicyTests
    {
        [Test]
        public void ThePauseMenu_StaysWhilePlayersDrive_AndClosesForAnythingElse()
        {
            GameState[] driving = { GameState.Tutorial, GameState.Begin, GameState.MainLoop, GameState.FinalPackage };
            foreach (GameState state in (GameState[])System.Enum.GetValues(typeof(GameState)))
                Assert.AreEqual(System.Array.IndexOf(driving, state) >= 0, PausePolicy.KeepsPause(state), state.ToString());
        }

        [Test]
        public void PausingOnline_SaysWhatMainMenuDoes()
        {
            Assert.IsNull(PausePolicy.HintFor(NetworkRole.Offline), "offline the game stops: nothing to say");
            Assert.AreEqual("Online: the match goes on while you're paused. Main Menu ends it for everyone.", PausePolicy.HintFor(NetworkRole.Host));
            Assert.AreEqual("Online: the match goes on while you're paused. Main Menu leaves it.", PausePolicy.HintFor(NetworkRole.Client));
        }
    }
}

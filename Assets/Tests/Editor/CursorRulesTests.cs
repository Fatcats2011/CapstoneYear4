using NUnit.Framework;

namespace DoA.Tests
{
    public class CursorRulesTests
    {
        [Test]
        public void Build_ShowsWhileTheMouseMoves_HidesOnceItRests()
        {
            Assert.IsTrue(CursorRules.Shows(0f, isEditor: false));
            Assert.IsTrue(CursorRules.Shows(CursorRules.HIDE_AFTER_SECONDS - 0.01f, isEditor: false));
            Assert.IsFalse(CursorRules.Shows(CursorRules.HIDE_AFTER_SECONDS, isEditor: false));
        }

        [Test]
        public void Editor_AlwaysShows()
        {
            Assert.IsTrue(CursorRules.Shows(1000f, isEditor: true));
        }
    }
}

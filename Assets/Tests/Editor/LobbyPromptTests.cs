using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The line along the top of the screen that says what Y and B do in player select online (OnlinePlay)
    /// </summary>
    public class LobbyPromptTests
    {
        [TearDown]
        public void TearDown()
        {
            if (LobbyPrompt.Exists)
                Object.DestroyImmediate(LobbyPrompt.Instance.gameObject);
        }

        [Test]
        public void Show_SomeText_ShowsIt()
        {
            LobbyPrompt.Instance.Show("hello");

            Assert.IsTrue(LobbyPrompt.Instance.IsShown);
            Assert.AreEqual("hello", LobbyPrompt.Instance.Text);
        }

        [Test]
        public void SortsBelowTheHints()
        {
            Assert.AreEqual(LobbyPrompt.SORTING_ORDER, LobbyPrompt.Instance.GetComponent<Canvas>().sortingOrder);
            Assert.Less(LobbyPrompt.SORTING_ORDER, ControllerPrompts.SORTING_ORDER, "a hint about why a session ended stays readable");
        }

        [Test]
        public void Show_Nothing_HidesIt()
        {
            LobbyPrompt.Instance.Show("hello");
            LobbyPrompt.Instance.Show("");

            Assert.IsFalse(LobbyPrompt.Instance.IsShown);
        }
    }
}

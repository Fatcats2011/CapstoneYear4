using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The last 10 seconds' words (NumberHandler.SetFinalCountdown) are drawn twice, a back copy and a Front copy over it:
    /// both read "Time's Up In..." without a golden round. Only the back copy changed, so players saw "Golden Order In..."
    /// (playtest, 2026-11-20)
    /// </summary>
    public class FinalCountdownTextTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            HouseRules.Playing = HouseRules.Normal;
            objects.DestroyAll();
        }

        [Test]
        public void WithoutAGoldenRound_EveryLayerSaysTimesUp()
        {
            NumberHandler numbers = objects.Add<NumberHandler>();
            GameObject words = new GameObject("FinalOrderCountdownText");
            words.transform.SetParent(numbers.transform);
            GameObject back = new GameObject("Golden Order Text", typeof(TextMeshProUGUI));
            back.transform.SetParent(words.transform);
            GameObject front = new GameObject("Golden Order Text Front", typeof(TextMeshProUGUI));
            front.transform.SetParent(back.transform);
            back.GetComponent<TMP_Text>().text = front.GetComponent<TMP_Text>().text = "Golden Order In...";
            GameObject clock = new GameObject("Clock");
            clock.transform.SetParent(numbers.transform);
            GameObject image = new GameObject("Image", typeof(Image));
            image.transform.SetParent(clock.transform);
            numbers.finalOrderTextObject = words;
            numbers.finalOrderCountdownImage = image.GetComponent<Image>();

            HouseRules.Playing = new HouseRules(MatchLength.Normal, true, false);
            numbers.SetFinalCountdown(true);

            string timesUp = Loc.Get(ModeFeedbackRules.CountdownKey(false));
            Assert.AreEqual(timesUp, back.GetComponent<TMP_Text>().text, "the back copy");
            Assert.AreEqual(timesUp, front.GetComponent<TMP_Text>().text, "the front copy, the one players see");
        }
    }
}

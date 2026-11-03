using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The live scoreboard (Scoreboard): who's where, how the rows stack, whose Steam picture shows, when it's up; the
    /// purple flame round a stolen package; bots' names; and the player HUD it replaces
    /// </summary>
    public class ScoreboardTests
    {
        const string DRIVING_CANVAS = "Assets/Prefabs/Player Prefabs/Driving Canvas.prefab";
        const string ORDER = "Assets/Prefabs/Packages/Order.prefab";

        [Test]
        public void Ranking_MostMoneyFirst_ATieToTheLowerSeat()
        {
            Dictionary<int, int> money = new Dictionary<int, int> { { 0, 50 }, { 1, 200 }, { 2, 50 }, { 3, 120 } };

            CollectionAssert.AreEqual(new[] { 1, 3, 0, 2 }, Scoreboard.Ranking(new List<int> { 0, 1, 2, 3 }, s => money[s]));
        }

        [Test]
        public void RowTops_FirstPlaceIsBigger_TheStackCentred()
        {
            float[] tops = Scoreboard.RowTops(4);
            float first = Scoreboard.ROW_HEIGHT * Scoreboard.FIRST_SCALE;
            float rest = Scoreboard.ROW_HEIGHT;

            Assert.AreEqual(first + Scoreboard.ROW_GAP, tops[0] - tops[1], 0.001f, "first place's row is the bigger one");
            Assert.AreEqual(rest + Scoreboard.ROW_GAP, tops[1] - tops[2], 0.001f);
            Assert.AreEqual(rest + Scoreboard.ROW_GAP, tops[2] - tops[3], 0.001f);
            float bottom = tops[3] - rest;
            Assert.AreEqual(0f, tops[0] + bottom, 0.001f, "as much above the middle as below");
            Assert.IsEmpty(Scoreboard.RowTops(0));
        }

        [Test]
        public void FirstWithId_OnePicturePerSteamAccount()
        {
            // Seats 0 and 2 share a machine (and its Steam account); seat 1 is another machine; seat 3 is a bot
            Dictionary<int, ulong> ids = new Dictionary<int, ulong> { { 0, 7 }, { 1, 9 }, { 2, 7 }, { 3, 0 } };

            Assert.IsTrue(Scoreboard.FirstWithId(0, 7, ids), "the machine's first player");
            Assert.IsTrue(Scoreboard.FirstWithId(1, 9, ids));
            Assert.IsFalse(Scoreboard.FirstWithId(2, 7, ids), "sharing seat 0's account: their ghost instead");
            Assert.IsFalse(Scoreboard.FirstWithId(3, 0, ids), "no Steam: their ghost");
        }

        [Test]
        public void ShowsIn_OnlyWhileTheyDrive()
        {
            Assert.IsTrue(Scoreboard.ShowsIn(GameState.Tutorial));
            Assert.IsTrue(Scoreboard.ShowsIn(GameState.Begin));
            Assert.IsTrue(Scoreboard.ShowsIn(GameState.MainLoop));
            Assert.IsTrue(Scoreboard.ShowsIn(GameState.FinalPackage));
            Assert.IsFalse(Scoreboard.ShowsIn(GameState.StartingCutscene));
            Assert.IsFalse(Scoreboard.ShowsIn(GameState.GoldenCutscene));
            Assert.IsFalse(Scoreboard.ShowsIn(GameState.Paused));
            Assert.IsFalse(Scoreboard.ShowsIn(GameState.Results));

            Assert.IsTrue(Scoreboard.EndsMatch(GameState.Results), "the next match makes new portraits");
            Assert.IsFalse(Scoreboard.EndsMatch(GameState.GoldenCutscene), "a cutscene keeps the rows");
        }

        [Test]
        public void PurpleFlame_BurnsRoundThePackage_HottestBehindIt_AndLoops()
        {
            Assert.AreEqual(1f, PurpleFlame.At(0f, 0f, 0.3f).a, 0.001f, "the hot core behind the package, no hole");
            Assert.AreEqual(0f, PurpleFlame.At(-1f, -1f, 0.3f).a, 0.001f, "the bottom corners stay clear");
            float side = PurpleFlame.SIDE / PurpleFlame.SPREAD;
            for (float v = -1f; v <= 1f; v += 0.1f)
                Assert.AreEqual(0f, PurpleFlame.At(side, v, 0.3f).a, 0.001f, "nothing past its side reach");
            Assert.GreaterOrEqual(Scoreboard.PACKAGE_SPACING, Scoreboard.PACKAGE * PurpleFlame.SIDE,
                "two burning packages' flames don't overlap");

            float burning = 0f;
            foreach (Color c in PurpleFlame.Pixels(0.5f))
                burning = Mathf.Max(burning, c.a);
            Assert.Greater(burning, 0.5f, "there is a flame");

            Color[] start = PurpleFlame.Pixels(0f);
            Color[] end = PurpleFlame.Pixels(1f);
            for (int i = 0; i < start.Length; i += 37)
                Assert.AreEqual(start[i].a, end[i].a, 0.02f, "the loop's end meets its start");
        }

        [Test]
        public void BannerAt_WhiteInside_EdgedInBlack_TaperingToTheRight()
        {
            float middle = Scoreboard.ROW_HEIGHT / 2f;
            Color inside = Scoreboard.BannerAt(Scoreboard.ROW_WIDTH / 3f, middle);
            Assert.Greater(inside.a, 0.95f, "almost opaque");
            Assert.Greater(inside.r, 0.95f, "white");
            Assert.Less(Scoreboard.BannerAt(1f, middle).r, 0.1f, "a black edge");

            float nearTop = 2f;
            Assert.Greater(Scoreboard.BannerAt(10f, nearTop).a, 0.9f, "full height at the picture");
            Assert.AreEqual(0f, Scoreboard.BannerAt(Scoreboard.ROW_WIDTH - 40f, nearTop).a, 0.001f, "shorter at the right");
            Assert.AreEqual(0f, Scoreboard.BannerAt(Scoreboard.ROW_WIDTH - 2f, middle + 6f).a, 0.001f, "the right end's cut slants");
        }

        [Test]
        public void BotNames_FitThePersonality_AndAreNeverTaken()
        {
            string thief = BotNames.Pick(BotKind.Thief, new HashSet<string>(), 0);
            CollectionAssert.Contains(BotNames.For(BotKind.Thief), thief);
            CollectionAssert.DoesNotContain(BotNames.For(BotKind.Courier), thief, "each personality has its own names");

            HashSet<string> taken = new HashSet<string>(BotNames.For(BotKind.Hunter));
            taken.Remove(BotNames.For(BotKind.Hunter)[3]);
            Assert.AreEqual(BotNames.For(BotKind.Hunter)[3], BotNames.Pick(BotKind.Hunter, taken, 0), "the one left");

            taken.Add(BotNames.For(BotKind.Hunter)[3]);
            string extra = BotNames.Pick(BotKind.Hunter, taken, 5);
            Assert.IsFalse(taken.Contains(extra), "all taken: still a new name (" + extra + ")");
            Assert.AreEqual(BotNames.Pick(BotKind.Courier, new HashSet<string>(), -3), BotNames.Pick(BotKind.Courier, new HashSet<string>(), 3),
                "any roll picks a name");
        }

        [Test]
        public void TheDrivingHud_NoLongerShowsTheScoreOrPlacement()
        {
            GameObject canvas = AssetDatabase.LoadAssetAtPath<GameObject>(DRIVING_CANVAS);

            Assert.IsFalse(canvas.transform.Find("Score Holder").gameObject.activeSelf, "the scoreboard shows the money");
            Assert.IsFalse(canvas.transform.Find("Placement Image Holder").gameObject.activeSelf, "and the places");
            Assert.IsTrue(canvas.transform.Find("Compass Mask").gameObject.activeSelf, "the rest of the HUD stays");
        }

        [Test]
        public void EveryOrderTier_HasAnIcon_ForTheBoard()
        {
            GameObject prefab = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ORDER));
            try
            {
                Order order = prefab.GetComponentInChildren<Order>(true);
                foreach (Constants.OrderValue value in new[] { Constants.OrderValue.Easy, Constants.OrderValue.Medium, Constants.OrderValue.Hard, Constants.OrderValue.Golden })
                {
                    order.value = value;
                    Assert.IsNotNull(order.Icon, value.ToString());
                }
            }
            finally
            {
                Object.DestroyImmediate(prefab);
            }
        }
    }
}

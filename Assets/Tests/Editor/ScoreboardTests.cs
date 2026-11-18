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
        public void LayoutFor_EachLocalCountAndSplit()
        {
            var one = Scoreboard.LayoutFor(1, SplitLayout.Boxed);
            Assert.IsTrue(one.LeftEdge); Assert.AreEqual(Scoreboard.BoardShape.Column, one.Shape); Assert.IsFalse(one.Compact);
            Assert.AreEqual(one, Scoreboard.LayoutFor(0, SplitLayout.Stacked), "nobody local: the one-player board");

            var side = Scoreboard.LayoutFor(2, SplitLayout.SideBySide);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), side.Anchor); Assert.AreEqual(Scoreboard.BoardShape.Column, side.Shape); Assert.IsTrue(side.Compact);

            var stacked = Scoreboard.LayoutFor(2, SplitLayout.Stacked);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), stacked.Anchor); Assert.AreEqual(Scoreboard.BoardShape.Strip, stacked.Shape); Assert.IsTrue(stacked.Compact);

            var boxed = Scoreboard.LayoutFor(2, SplitLayout.Boxed);
            Assert.AreEqual(new Vector2(0.125f, 0.5f), boxed.Anchor); Assert.IsFalse(boxed.LeftEdge); Assert.IsFalse(boxed.Compact);

            var three = Scoreboard.LayoutFor(3, SplitLayout.SideBySide);
            Assert.AreEqual(new Vector2(0.125f, 0.25f), three.Anchor); Assert.AreEqual(Scoreboard.BoardShape.Column, three.Shape); Assert.IsFalse(three.Compact);

            var four = Scoreboard.LayoutFor(4, SplitLayout.Stacked);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), four.Anchor); Assert.AreEqual(Scoreboard.BoardShape.Grid, four.Shape); Assert.IsTrue(four.Compact);
        }

        [Test]
        public void CellSpots_OneLocal_MatchTodaysColumn()
        {
            Vector2[] spots = Scoreboard.CellSpots(Scoreboard.LayoutFor(1, SplitLayout.Boxed), 4);
            float[] tops = Scoreboard.RowTops(4);
            for (int i = 0; i < 4; i++) { Assert.AreEqual(0f, spots[i].x, 0.001f); Assert.AreEqual(tops[i], spots[i].y, 0.001f); }
        }

        [Test]
        public void CellSpots_CentredColumn_StraddlesTheSeam()
        {
            Vector2[] spots = Scoreboard.CellSpots(Scoreboard.LayoutFor(2, SplitLayout.SideBySide), 3);
            Assert.AreEqual(-Scoreboard.COMPACT_ROW_WIDTH * Scoreboard.FIRST_SCALE / 2f, spots[0].x, 0.001f);
            Assert.AreEqual(-Scoreboard.COMPACT_ROW_WIDTH / 2f, spots[1].x, 0.001f);
            Assert.AreEqual(-Scoreboard.COMPACT_ROW_WIDTH / 2f, spots[2].x, 0.001f);
        }

        [Test]
        public void CellSpots_Strip_SplitRoundAGap_CentredLeftToRight([Values(2, 4)] int count)
        {
            float gap = 300f;
            Vector2[] spots = Scoreboard.CellSpots(Scoreboard.LayoutFor(2, SplitLayout.Stacked), count, gap);
            float w = Scoreboard.COMPACT_ROW_WIDTH;
            int left = (count + 1) / 2;
            float right = spots[count - 1].x + w;
            Assert.AreEqual(0f, spots[0].x + right, 0.001f, "as much left of the seam's middle as right");
            Assert.LessOrEqual(spots[left - 1].x + w * (left == 1 ? Scoreboard.FIRST_SCALE : 1f), -gap / 2f + 0.001f, "the left half ends before the gap");
            Assert.GreaterOrEqual(spots[left].x, gap / 2f - 0.001f, "the right half starts after it");
            if (left > 1)
                Assert.AreEqual(spots[0].x + w * Scoreboard.FIRST_SCALE + Scoreboard.CELL_GAP, spots[1].x, 0.001f);
            for (int rank = 1; rank < count; rank++)
                Assert.Greater(spots[rank].x, spots[rank - 1].x, "ranked left to right");
            Assert.AreEqual(Scoreboard.ROW_HEIGHT * Scoreboard.FIRST_SCALE / 2f, spots[0].y, 0.001f, "centred on the seam");
            Assert.AreEqual(Scoreboard.ROW_HEIGHT / 2f, spots[1].y, 0.001f);
        }

        [Test]
        public void CellSpots_Strip_ClearsTheBottomPlayersCompass([Values(2, 3, 4)] int count)
        {
            Scoreboard.BoardLayout stacked = Scoreboard.LayoutFor(2, SplitLayout.Stacked);
            foreach (float text in new[] { 1f, 1.25f, 1.5f })
            {
                float scale = Scoreboard.BoardScaleFor(stacked, text, 1920f);
                Vector2[] spots = Scoreboard.CellSpots(stacked, count, Scoreboard.StripGap(scale));
                for (int rank = 0; rank < count; rank++)
                {
                    float from = spots[rank].x * scale + 960f;
                    float to = from + Scoreboard.COMPACT_ROW_WIDTH * (rank == 0 ? Scoreboard.FIRST_SCALE : 1f) * scale;
                    Assert.IsTrue(to < 715f || from > 1205f, "rank " + rank + " of " + count + " spans " + from + " to " + to + ", over the compass (715 to 1205)");
                    Assert.GreaterOrEqual(from, 0f, "on the screen");
                    Assert.LessOrEqual(to, 1920f, "on the screen");
                    if (rank > 0)
                        Assert.Greater(spots[rank].x, spots[rank - 1].x, "ranked left to right");
                }
            }
        }

        [Test]
        public void BoardScaleFor_OnePlayer_GrowsWithTextSize()
        {
            Scoreboard.BoardLayout one = Scoreboard.LayoutFor(1, SplitLayout.Boxed);
            Assert.AreEqual(Scoreboard.BOARD_SCALE, Scoreboard.BoardScaleFor(one, 1f, 1920f), 0.0001f);
            Assert.AreEqual(Scoreboard.BOARD_SCALE * 1.5f, Scoreboard.BoardScaleFor(one, 1.5f, 1920f), 0.0001f);
        }

        [Test]
        public void BoardScaleFor_CentreLayouts_NeverGrow([Values(1f, 1.25f, 1.5f)] float text)
        {
            foreach (Scoreboard.BoardLayout centre in new[] { Scoreboard.LayoutFor(2, SplitLayout.SideBySide),
                Scoreboard.LayoutFor(2, SplitLayout.Stacked), Scoreboard.LayoutFor(4, SplitLayout.Boxed) })
                Assert.AreEqual(Scoreboard.BOARD_SCALE, Scoreboard.BoardScaleFor(centre, text, 1920f), 0.0001f, centre.Shape.ToString());
        }

        [Test]
        public void BoardScaleFor_BarLayouts_FirstPlaceFitsTheBar([Values(1920f, 1728f)] float canvasWidth, [Values(1f, 1.25f, 1.5f)] float text)
        {
            foreach (Scoreboard.BoardLayout bar in new[] { Scoreboard.LayoutFor(2, SplitLayout.Boxed), Scoreboard.LayoutFor(3, SplitLayout.Boxed) })
            {
                float scale = Scoreboard.BoardScaleFor(bar, text, canvasWidth);
                Assert.LessOrEqual(Scoreboard.ROW_WIDTH * Scoreboard.FIRST_SCALE * scale, 0.25f * canvasWidth - 2f * Scoreboard.LEFT + 0.001f);
                Assert.LessOrEqual(scale, Scoreboard.BOARD_SCALE * text + 0.0001f, "never bigger than the text size asks");
            }
        }

        [Test]
        public void BoardScaleFor_BarLayouts_NormalSizeWhenItFits()
        {
            Assert.AreEqual(Scoreboard.BOARD_SCALE, Scoreboard.BoardScaleFor(Scoreboard.LayoutFor(2, SplitLayout.Boxed), 1f, 1920f), 0.0001f);
            Assert.AreEqual(Scoreboard.BOARD_SCALE, Scoreboard.BoardScaleFor(Scoreboard.LayoutFor(3, SplitLayout.Boxed), 1f, 1920f), 0.0001f);
            Assert.Less(Scoreboard.BoardScaleFor(Scoreboard.LayoutFor(2, SplitLayout.Boxed), 1.5f, 1920f), Scoreboard.BOARD_SCALE * 1.5f,
                "at 150% first place would leave the bar: held to its width");
        }

        [Test]
        public void CellSpots_Grid_OneCellPerQuarter([Values(3, 4)] int count)
        {
            Vector2[] spots = Scoreboard.CellSpots(Scoreboard.LayoutFor(4, SplitLayout.Boxed), count);
            float g = Scoreboard.CELL_GAP / 2f, w = Scoreboard.COMPACT_ROW_WIDTH, h = Scoreboard.ROW_HEIGHT, s = Scoreboard.FIRST_SCALE;
            AssertSpot(new Vector2(-g - w * s, g + h * s), spots[0]); // first place, top-left
            AssertSpot(new Vector2(g, g + h), spots[1]);
            AssertSpot(new Vector2(-g - w, -g), spots[2]);
            if (count == 4) AssertSpot(new Vector2(g, -g), spots[3]);
        }

        static void AssertSpot(Vector2 expected, Vector2 actual)
        {
            Assert.AreEqual(expected.x, actual.x, 0.001f);
            Assert.AreEqual(expected.y, actual.y, 0.001f);
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
            Assert.AreEqual(0.2f, inside.a, 0.01f, "mostly see through");
            Assert.Greater(inside.r, 0.95f, "white");
            Assert.Less(Scoreboard.BannerAt(1f, middle).r, 0.1f, "a black edge");

            float nearTop = 2f;
            Assert.Greater(Scoreboard.BannerAt(10f, nearTop).a, 0.9f, "full height at the picture");
            Assert.AreEqual(0f, Scoreboard.BannerAt(Scoreboard.ROW_WIDTH - 40f, nearTop).a, 0.001f, "shorter at the right");
            Assert.AreEqual(0f, Scoreboard.BannerAt(Scoreboard.ROW_WIDTH - 2f, middle + 6f).a, 0.001f, "the right end's cut slants");
        }

        [Test]
        public void BannerAt_Compact_IsASquareEndedBox()
        {
            float w = Scoreboard.COMPACT_ROW_WIDTH, h = Scoreboard.ROW_HEIGHT;
            Assert.Greater(Scoreboard.BannerAt(w - 1f, h - 1f, true).a, 0.5f, "bottom-right corner filled: no slanted tip");
            Assert.Greater(Scoreboard.BannerAt(w - 1f, 1f, true).a, 0.5f, "top-right corner filled: no taper");
            Assert.AreEqual(0f, Scoreboard.BannerAt(w + 2f, h / 2f, true).a, 0.001f);
            Assert.AreEqual(Scoreboard.BannerAt(10f, 10f), Scoreboard.BannerAt(10f, 10f, false), "full form unchanged");
        }

        [Test]
        public void Relays_WhenCountOrSplitChanges()
        {
            var one = Scoreboard.LayoutFor(1, SplitLayout.Boxed);
            Assert.IsFalse(Scoreboard.Relays(one, Scoreboard.LayoutFor(1, SplitLayout.Stacked)), "split doesn't matter for one");
            Assert.IsTrue(Scoreboard.Relays(one, Scoreboard.LayoutFor(2, SplitLayout.Boxed)), "a pad joins");
            Assert.IsTrue(Scoreboard.Relays(Scoreboard.LayoutFor(2, SplitLayout.Boxed), Scoreboard.LayoutFor(2, SplitLayout.Stacked)), "option changed");
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

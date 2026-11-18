# Split-screen Scoreboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The live scoreboard moves and changes shape with the number of local players: centre of the split for side-by-side, stacked and four players (compact rows), and the empty black space for boxed two-player and three-player (full rows).

**Architecture:** One pure layout step in `Scoreboard.cs` (`LayoutFor` + `CellSpots`) decides the board's anchor, arrangement and row form. `LateUpdate` reads the local count and `SplitScreenLayout.TwoPlayers` each frame, rebuilds the rows when the layout changes, and slides each row to its rank's spot. Rows gain a compact form: no name, and a straight-ended banner.

**Tech Stack:** Unity 2022.3.62f3, C#, uGUI + TextMeshPro, NUnit EditMode tests run via `bash tools/run-tests.sh <filter>` (the mirror is shared: check no mirror Unity is running first).

**Spec:** `docs/superpowers/specs/2026-11-17-split-screen-scoreboard-design.md`

## Global Constraints

- One local player (including 0, e.g. online spectating): board unchanged, middle-left, full rows.
- 2 Side-by-side: centre (0.5, 0.5), column, compact.
- 2 Stacked: centre (0.5, 0.5), strip ranked left to right, compact.
- 2 Boxed: centred in the left bar, screen point (0.125, 0.5), column, full.
- 3 players: centred in the bottom-left corner, screen point (0.125, 0.25), column, full.
- 4 players: centre (0.5, 0.5), 2×2 grid, rank in reading order (first place top-left), compact.
- Compact row = picture, money (with "+$"), two package slots; no name.
- First place still drawn `FIRST_SCALE` bigger; rows still slide; text size setting still scales the board.
- Count = `PlayerInstantiate.Instance.Roster.LocalCount`.
- No duplicated row code (no second board class).

## Review Focus

1. **A second pad joins or leaves mid-match** (count 1→2 or 2→1): the board re-lays out at once, with no leftover rows in the old place. Covered by the Task 2 rebuild test.
2. **The split option is changed in Options during a match:** the board re-lays out, with the same rebuild path. Covered by the Task 2 rebuild test.
3. **Large text size setting:** a centred board must still straddle the seam. Scaling happens about the board's origin, so the spots must be symmetric about it. Covered by the Task 1 symmetry tests.
4. **Fewer rows than the shape expects** (a strip with 2 rows, a grid while a fourth player is still loading in): the cells stay centred, with no gap for the missing ones. Covered by the Task 1 tests with count 2 and 3.
5. **0 local players** (spectating, highlight reel bots): falls back to the one-player layout instead of throwing. Covered by a Task 1 test.

---

### Task 1: Layout step (pure numbers)

**Files:**
- Modify: `Assets/Scripts/UI/Scoreboard.cs` (new types + statics next to `RowTops`)
- Test: `Assets/Tests/Editor/ScoreboardTests.cs`

**Interfaces:**
- Produces:
  - `internal enum BoardShape { Column, Strip, Grid }`
  - `internal struct BoardLayout : System.IEquatable<BoardLayout> { public Vector2 Anchor; public bool LeftEdge; public BoardShape Shape; public bool Compact; }`. `LeftEdge` = the board hangs off the screen's left edge (today's layout, offset `LEFT`); otherwise the board is centred on `Anchor`.
  - `internal static BoardLayout LayoutFor(int localPlayers, SplitLayout twoPlayers)`
  - `internal const float COMPACT_ROW_WIDTH = ROW_HEIGHT + PAD + MONEY_WIDTH + PACKAGE_SPACING + PACKAGE + 2 * PAD;` (no name, no tip)
  - `internal const float CELL_GAP = ROW_GAP;`
  - `internal static Vector2[] CellSpots(BoardLayout layout, int count)`: each rank's row top-left, in board space (up is +). Rank 0 is sized `FIRST_SCALE`.
    - Column: y = `RowTops(count)`. x = 0 when `LeftEdge`, else `-width * scale / 2`.
    - Strip: cells side by side, `CELL_GAP` apart, the whole strip centred on x = 0. Each cell is centred on y = 0, so y = `ROW_HEIGHT * scale / 2`.
    - Grid: column = rank % 2, row = rank / 2. Left cells end at `-CELL_GAP / 2` and right cells start at `+CELL_GAP / 2`. Top cells' bottoms sit at `+CELL_GAP / 2` and bottom cells' tops at `-CELL_GAP / 2`.
    - Width is `COMPACT_ROW_WIDTH` when `Compact`, else `ROW_WIDTH`.

- [ ] **Step 1: Write the failing tests**

```csharp
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
public void CellSpots_Strip_CentredLeftToRight([Values(2, 4)] int count)
{
    Vector2[] spots = Scoreboard.CellSpots(Scoreboard.LayoutFor(2, SplitLayout.Stacked), count);
    float w = Scoreboard.COMPACT_ROW_WIDTH;
    float right = spots[count - 1].x + w;
    Assert.AreEqual(0f, spots[0].x + right, 0.001f, "as much left of the seam's middle as right");
    Assert.AreEqual(spots[0].x + w * Scoreboard.FIRST_SCALE + Scoreboard.CELL_GAP, spots[1].x, 0.001f);
    Assert.AreEqual(Scoreboard.ROW_HEIGHT * Scoreboard.FIRST_SCALE / 2f, spots[0].y, 0.001f, "centred on the seam");
    Assert.AreEqual(Scoreboard.ROW_HEIGHT / 2f, spots[1].y, 0.001f);
}

[Test]
public void CellSpots_Grid_OneCellPerQuarter([Values(3, 4)] int count)
{
    Vector2[] spots = Scoreboard.CellSpots(Scoreboard.LayoutFor(4, SplitLayout.Boxed), count);
    float g = Scoreboard.CELL_GAP / 2f, w = Scoreboard.COMPACT_ROW_WIDTH, h = Scoreboard.ROW_HEIGHT, s = Scoreboard.FIRST_SCALE;
    Assert.AreEqual(new Vector2(-g - w * s, g + h * s), spots[0]); // first place, top-left
    Assert.AreEqual(new Vector2(g, g + h), spots[1]);
    Assert.AreEqual(new Vector2(-g - w, -g), spots[2]);
    if (count == 4) Assert.AreEqual(new Vector2(g, -g), spots[3]);
}
```

- [ ] **Step 2: Run them and see them fail**

Run: `bash tools/run-tests.sh ScoreboardTests`
Expected: compile errors for `LayoutFor`, `BoardShape`, `CellSpots`, `COMPACT_ROW_WIDTH`, `CELL_GAP`.

- [ ] **Step 3: Implement `BoardShape`, `BoardLayout`, `LayoutFor`, `CellSpots`, `COMPACT_ROW_WIDTH` and `CELL_GAP` in `Scoreboard.cs`**

`LayoutFor` is a switch on the count (≤1 → one-player), then on `twoPlayers` for 2. Make `BoardShape`, `BoardLayout`, `PAD` and `MONEY_WIDTH` `internal` so the tests can reach them. Doc comments follow the file's style.

- [ ] **Step 4: Run and pass**

Run: `bash tools/run-tests.sh ScoreboardTests`
Expected: all ScoreboardTests pass, including the old ones.

- [ ] **Step 5: Commit** (only if the user has asked for commits this session; otherwise leave staged-ready and say so)

```bash
git add Assets/Scripts/UI/Scoreboard.cs Assets/Tests/Editor/ScoreboardTests.cs
git commit -m "Scoreboard layout step for split screen"
```

### Task 2: Board follows the layout, compact rows

**Files:**
- Modify: `Assets/Scripts/UI/Scoreboard.cs` (`LateUpdate`, `KeepRowsFor`, `Banner`/`BannerAt`, `Row`)
- Test: `Assets/Tests/Editor/ScoreboardTests.cs`

**Interfaces:**
- Consumes: `LayoutFor`, `CellSpots`, `BoardLayout`, `COMPACT_ROW_WIDTH` from Task 1.
- Produces:
  - `internal static Color BannerAt(float x, float y, bool compact)`: compact = a plain rectangle `COMPACT_ROW_WIDTH` × `ROW_HEIGHT`, INK border `BORDER`, PAPER inside, no taper and no tip. The full form is today's shape unchanged. Keep a cached sprite per form.
  - `internal static bool Relays(BoardLayout shown, BoardLayout now)` → `!shown.Equals(now)`. Rows are rebuilt when this is true.
  - `Row(RectTransform board, int seat, GameObject player, GhostPortrait ghost, Vector2 spot, bool compact)`: compact skips the name label (`nameText` stays null, and `Show`/`ShowModeState` guard it). Money goes straight after the picture.
  - `Row.MoveTowards(Vector2 spot, float scale, float step)`: lerps both x and y.

- [ ] **Step 1: Write the failing tests**

```csharp
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
```

Keep the existing `BannerAt(x, y)` overload (forwarding with `compact: false`), so its old tests still compile.

- [ ] **Step 2: Run and see them fail**

Run: `bash tools/run-tests.sh ScoreboardTests`. Expected: compile errors for `BannerAt(…, bool)` and `Relays`.

- [ ] **Step 3: Implement**

- `Awake`: the board keeps a zero size. Its anchor and position are set from the layout in `LateUpdate`.
- `LateUpdate`, after the `EndsMatch` check: `BoardLayout layout = LayoutFor(players.Roster.LocalCount, SplitScreenLayout.TwoPlayers);`. If `Relays(shownLayout, layout)`, call `Clear()` and store it. Then set `board.anchorMin = board.anchorMax = layout.Anchor` and `board.anchoredPosition = layout.LeftEdge ? new Vector2(LEFT, 0f) : Vector2.zero`. Replace `RowTops` with `CellSpots(layout, ranked.Count)`, and pass `spots[rank]` to `MoveTowards`.
- `KeepRowsFor` takes the layout. A new row starts at `CellSpots(layout, rows.Count + 1)` last spot, with `layout.Compact`.
- Start `shownLayout` as `LayoutFor(1, SplitLayout.Boxed)`.

- [ ] **Step 4: Run and pass**

Run: `bash tools/run-tests.sh ScoreboardTests`. Expected: all pass.

- [ ] **Step 5: Look at it**

Adapt the screenshot probe from the live-scoreboard work (it renders the overlay through a temp camera; copy it into the mirror as a test). Capture each of the six layouts with 4 rows. Check that the centre boards straddle the seams, the boxed and three-player boards sit inside the black space, and nothing overlaps the steal callout, mode info box or reconnect prompt. Show the images to the user.

- [ ] **Step 6: Run the board's neighbours**

Run: `bash tools/run-tests.sh "Scoreboard|ResultsMenu|ModeInfoBox|StealCallout"`. Expected: all pass.

- [ ] **Step 7: Commit** (same rule as Task 1)

### Task 3: Design docs and playtest entry

**Files:**
- Modify: `docs/design/DoA GDD.docx` (HUD / scoreboard section: the layout table from the spec, in player terms; Revision History line; cover minor version bump)
- Modify: `docs/design/DoA TDD.docx` (Scoreboard section: `LayoutFor`/`CellSpots`, re-layout on count/split change, compact rows; the tests; Revision History; cover bump)
- Modify: `EDITOR-TODO.md` (new "16n. Split-screen scoreboard: playtest (10 minutes)" after 16m: one bullet per split layout + a pad joining mid-match + changing the split option mid-match)

- [ ] **Step 1:** Edit both `.docx` files with the docx skill, keeping their styles and the table of contents in step. Check the values against the code.
- [ ] **Step 2:** Add the EDITOR-TODO entry as short bullets.
- [ ] **Step 3:** Commit (same rule).

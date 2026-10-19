# Dispatch, the Online Lobby Screen: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Online play opens on Dispatch. It keeps the existing 3D podiums, and a corkboard of delivery tickets shows each seat's company, Steam name, ghost colour, hat and ready state live. Each machine has one player, and X opens the colour and hat sliders.

**Architecture:**
- Dispatch is **online player select presented differently**. It is not a new `GameState`. Everything already keyed on `GameState.PlayerSelect` keeps working unchanged: joins, `JoinRules`' "host in the menus", the lobby's life, ready counting, the countdown, the return after results.
- New code adds:
  - a one-player-per-machine rule;
  - a synced player name;
  - plain-C# rules for the tickets and for Dispatch's buttons;
  - two UI MonoBehaviours (`DispatchBoard`, `TicketView`), which the user builds into the menu scene.
- The depot room, board, tickets and banner art are scene work for the user (`EDITOR-TODO.md`).

**Tech Stack:** Unity 2022.3.62f3, C#, Netcode for GameObjects 1.15.1, Steamworks.NET, TextMesh Pro, NUnit (Unity Test Framework).

**Spec:** `docs/superpowers/specs/2026-10-13-dispatch-lobby-design.md`. Mockup: `.superpowers/brainstorm/631-1791924428/content/w3-brighter.html`.

## Global Constraints

- **Edit rules:**
  - No scene, prefab, `.asset`, `.inputactions` or ProjectSettings edits. Those go in `EDITOR-TODO.md`.
  - CRLF files (`PlayerInstantiate.cs`, `MainMenu.cs`, `MenuInteractions.cs`, `SceneManager.cs`, `GameManager.cs`): edit with the Edit tool only, never `sed -i`.
  - Every new file under `Assets/` gets a `.meta`: `bash tools/newmeta.sh <path>`.
  - `SteamManager.cs` and `SteamStartup.cs` are untouched.
- **Git:**
  - Branch `steam-phase1a`. Never check out `main`.
  - Leave everything uncommitted: the user commits. The "Commit" steps below mean "stop and list the changed files".
- **Tests:**
  - Run with `bash tools/run-tests.sh [filter]`, one run at a time. A compile error prints `NO RESULTS`. For a ~30 s compile check: `python tools/compile-check.py`.
  - Network test ports: the next free one is **7817**.
  - Play Mode rules:
    - no lambda captures a test's local;
    - wait by real time;
    - only `yield return null`;
    - `EnterPlayMode` is yielded by the test itself.
- **Players:** `Constants.MAX_PLAYERS` = 4. Online, **one player per machine** on Dispatch.
- **Copy (exact):**
  - Ready stamp `DISPATCHED`
  - Not ready `GETTING READY…`
  - Empty seat `HIRING…`
  - Banner `ALL ORDERS DISPATCHED`
  - Counter `{ready} / {seated} READY`
  - Name fallback `Player {seat+1}`
  - Prompts: `CLOCK IN`, `CUSTOMIZE`, `INVITE`, `LEAVE`
- **Local Play's player select behaves exactly as before.** Every rule below applies only while `GameAuthority.IsOnline`.

## Review Focus

- **Going online with couch players already joined.** Two pads in Local Play, then Online Play or accepting an invite: only the menu player stays. The other leaves cleanly, with no orphan player object and no seat ask to the host. Test in Task 1.
- **The sliders after a match.** Back on Dispatch after results, the sliders are closed, everyone is unready, and A works. Nobody is stuck in "customizing" with the d-pad dead. Test in Task 4.
- **A player leaving mid-countdown.** Their ticket goes to `HIRING…`. The counter drops them, the banner stays only if everyone left is still ready, and the countdown isn't restarted twice. Test in Task 5.
- **A name with emoji, or longer than the field.** It's cut on a character boundary and never throws on any machine. An empty or whitespace name shows `Player N`. Test in Task 2.
- **A client joining while the banner is up.** The countdown stops on every machine and the banner hides (the existing `SetRemoteReady(seat, false)` on join). Test in Task 5.

---

### Task 1: One player per machine online

**Files:**
- Create: `Assets/Scripts/Online/OnlineRules.cs`
- Modify:
  - `Assets/Scripts/Player/PlayerJoiner.cs:63` (`Welcomes`)
  - `Assets/Scripts/Player/PlayerInstantiate.cs:105` (its caller; CRLF)
  - `Assets/Scripts/Online/OnlineGame.cs` (the seat-ask loop near line 335, and where this machine's player is first seated)
- Test:
  - `Assets/Tests/Editor/PlayerJoinerTests.cs`
  - `Assets/Tests/Editor/OnlinePlayNetworkTests.cs`
  - every existing Phase 3J test class: `CouchSeatsNetworkTests`, plus any test that seats two local players online (search for `UnseatedLocalCount` / `SeatAsker` uses in `Assets/Tests`)

**Interfaces:**
- Produces:
  - `public static class OnlineRules { public static bool OnePlayerPerMachine = true; }`. It's a test seam: Phase 3J tests set it `false` in `[SetUp]` and back to `true` in `[TearDown]`.
  - `PlayerJoiner.Welcomes(bool online, bool allowSpawn, int playerCount, int localCount, bool inPlayerSelect, bool onePerMachine)`
  - `public int PlayerInstantiate.DropExtraLocalPlayers()`, which returns how many left.

- [ ] **Step 1: Write the failing tests**
  - In `PlayerJoinerTests`, add the new parameter to the existing `Welcomes` cases, with `onePerMachine: false` so they keep their meaning.
  - Add cases with `onePerMachine: true`:
    - online, localCount 0, any state → `true`;
    - online, localCount 1, in player select → `false`;
    - offline, the same inputs as the existing offline cases → unchanged.
  - Add `OnlinePlayNetworkTests.GoingOnline_WithTwoLocalPlayers_KeepsOnlyTheMenuPlayer` (port 7817):
    - two local players joined in offline player select (as the existing 3J tests set them up);
    - host a session;
    - expect `Roster.LocalCount == 1`, the remaining player's `hostPlayer == true`, the dropped player's GameObject destroyed, and no seat asked of the host.
- [ ] **Step 2: Run `bash tools/run-tests.sh PlayerJoiner` and then `bash tools/run-tests.sh GoingOnline_WithTwoLocalPlayers`. Both should FAIL.** The first fails to compile until the parameter exists.
- [ ] **Step 3: Implement**
  - `Welcomes`: online and `onePerMachine` → `localCount == 0`. Otherwise as now. `PlayerInstantiate.WelcomesJoins` passes `OnlineRules.OnePlayerPerMachine`.
  - `DropExtraLocalPlayers`: every local player except the menu player (`hostPlayer`) leaves through `LeaveLocal`, and each held seat is given back as `RemovePlayerRef` does.
  - In `OnlineGame`, when `OnlineRules.OnePlayerPerMachine`:
    - call `DropExtraLocalPlayers()` as the session starts on this machine, before its player is seated;
    - skip the `asker.ToAsk` loop.
  - Set `OnlineRules.OnePlayerPerMachine = false` in the Phase 3J test classes' `[SetUp]`, and restore it to `true` in `[TearDown]`.
- [ ] **Step 4: Run `bash tools/run-tests.sh` (the full suite: 3J tests must stay green). Expected: all PASS.**
- [ ] **Step 5: Commit**, meaning list the changed files for the user.

---

### Task 2: Each player's name, synced

**Files:**
- Create:
  - `Assets/Scripts/Online/PlayerNames.cs`
  - `Assets/Scripts/Steam/SteamNames.cs`
- Modify:
  - `Assets/Scripts/Online/OnlinePlayer.cs`
  - `Assets/Scripts/Online/OnlineGame.cs:531` (next to `localPlayer.Share`)
- Test:
  - `Assets/Tests/Editor/PlayerNamesTests.cs`
  - `Assets/Tests/Editor/OnlinePlayersNetworkTests.cs`

**Interfaces:**
- Produces:
  - `public static class PlayerNames`:
    - `string Fallback(int seat)` returns `"Player " + (seat + 1)`;
    - `string Clean(string raw)` trims, then cuts to at most `FixedString32Bytes.UTF8MaxLengthInBytes` UTF-8 bytes on a character boundary, never splitting a surrogate pair. `null` gives `""`;
    - `string Shown(string synced, int seat)` gives `Fallback(seat)` when `synced` is null or whitespace, otherwise `synced`.
  - `public static class SteamNames { public static string Mine(); }` returns the persona name, or `""` without Steam. Use the `DISABLESTEAMWORKS` guard and `SteamManager.Initialized`, as `SteamPresence` does.
  - `OnlinePlayer`:
    - `NetworkVariable<FixedString32Bytes> displayName` (owner write);
    - `public string Name`, which is `displayName.Value.ToString()`;
    - `public void ShareName(string cleaned)`, owner only and sent only on a change.

- [ ] **Step 1: Write the failing tests** in `PlayerNamesTests`:
  - `Fallback(0) == "Player 1"` and `Fallback(3) == "Player 4"`.
  - `Clean("  Ryan  ") == "Ryan"`, and `Clean(null) == ""`.
  - `Clean(new string('a', 100)).Length == FixedString32Bytes.UTF8MaxLengthInBytes`.
  - `Clean` of 20 "👻" leaves a string that `Encoding.UTF8.GetByteCount` puts within the limit, and every surrogate in it is paired (`char.IsSurrogatePair` holds at every high surrogate).
  - `Shown("", 2) == "Player 3"`, `Shown("   ", 0) == "Player 1"`, and `Shown("Alex", 1) == "Alex"`.
  - `OnlinePlayersNetworkTests.ClientName_ReachesTheHost` (port 7818): the client calls `ShareName("Sam")`, and the host's copy of that `OnlinePlayer` reads `Name == "Sam"`.
- [ ] **Step 2: Run `bash tools/run-tests.sh PlayerNames` and then `bash tools/run-tests.sh ClientName_ReachesTheHost`. Expected: FAIL.**
- [ ] **Step 3: Implement** the interfaces above. `OnlineGame` calls `localPlayer.ShareName(PlayerNames.Clean(SteamNames.Mine()))` wherever it calls `Share`.
- [ ] **Step 4: Rerun both. Expected: PASS.**
- [ ] **Step 5: Commit**, meaning list the files.

---

### Task 3: Ticket rules

**Files:**
- Create: `Assets/Scripts/UI/Dispatch/DispatchRules.cs`
- Test: `Assets/Tests/Editor/DispatchRulesTests.cs`

**Interfaces:**
- Produces:
  - `public struct TicketState` with:
    - fields `int Seat; bool Taken; string Name; int Colour; int Hat; bool Ready; bool Mine;`
    - value equality: `Equals` and `==` / `!=`.
  - `public static class DispatchRules`:
    - `TicketState[] Tickets(IEnumerable<(int seat, string name, int colour, int hat, bool ready, bool mine)> players)` always returns `Constants.MAX_PLAYERS` entries, indexed by seat. A seat nobody holds is `Taken = false` with `Seat` set. A player with a seat outside 0–3 is ignored. `Name` goes through `PlayerNames.Shown`.
    - `string ReadyLine(TicketState[] tickets)` returns `$"{ready} / {seated} READY"`.
    - `bool Flashes(TicketState before, TicketState after)` is true when anything changed and `after.Taken`. A seat emptying doesn't flash.
    - `bool EveryoneReady(TicketState[] tickets)` means at least one seat is taken and every taken seat is ready.

- [ ] **Step 1: Write the failing tests**
  - `Tickets_EmptyInput_FourFreeSeats`: 4 entries, all `Taken == false`, `Seat` 0..3.
  - `Tickets_PlacesBySeat_AndFillsFallbackName`: the players are `(2, "", 1, 3, false, true)` and `(0, "Ryan", 0, 0, true, false)`.
    - `[0].Name == "Ryan"`;
    - `[2].Name == "Player 3"`;
    - `[2].Mine`;
    - `[1].Taken == false`.
  - `Tickets_SeatOutOfRange_Ignored`: seats -1 and 4 give 4 free seats.
  - `ReadyLine_CountsOnlyTakenSeats`: 3 taken, 2 ready → `"2 / 3 READY"`. None taken → `"0 / 0 READY"`.
  - `Flashes`:
    - colour change → true;
    - ready change → true;
    - name change → true;
    - no change → false;
    - taken → free → false;
    - free → taken → true.
  - `EveryoneReady`:
    - none taken → false;
    - 1 taken and ready → true (the host alone can start);
    - 2 taken, 1 ready → false.
- [ ] **Step 2: Run `bash tools/run-tests.sh DispatchRules`. Expected: FAIL (doesn't compile).**
- [ ] **Step 3: Implement** `DispatchRules.cs` to the interfaces above.
- [ ] **Step 4: Rerun. Expected: PASS.**
- [ ] **Step 5: Commit**, meaning list the files.

---

### Task 4: Dispatch's buttons: X opens the sliders

**Files:**
- Create: `Assets/Scripts/Menu/DispatchMenu.cs`
- Modify: `Assets/Scripts/Menu/MenuInteractions.cs` (CRLF): `ClearMenuInputs`, `CharacterSelectMenuInteractons`, `SwapToPlayerSelect`, and the d-pad and face-button handlers
- Test:
  - `Assets/Tests/Editor/DispatchMenuTests.cs`
  - `Assets/Tests/Editor/MenuButtonsTests.cs`

**Interfaces:**
- Produces:
  - `public enum DispatchButton { South, East, West, North, Pad }`
  - `public enum DispatchAction { None, OpenSliders, CloseSliders, Scroll, Ready, CloseAndReady, Unready, Leave, Invite }`
  - `public class DispatchMenu`:
    - `bool Open { get; }`
    - `DispatchAction Press(DispatchButton button, bool readied)`, which updates `Open`
    - `void Reset()`, which sets `Open = false`
- Consumed by `MenuInteractions`: when `GameAuthority.IsOnline`, the player-select handlers ask `DispatchMenu.Press` and do the returned action. Offline they behave exactly as now.

- [ ] **Step 1: Write the failing tests** in `DispatchMenuTests`, as one `[TestCase]` table of (open before, button, readied) → (action, open after):

  | Open before | Button | Readied | Action | Open after |
  |---|---|---|---|---|
  | closed | West | false | OpenSliders | open |
  | open | West | — | CloseSliders | closed |
  | closed | West | true | None | closed (no customizing while ready, as today) |
  | open | Pad | — | Scroll | open |
  | closed | Pad | — | None | closed |
  | open | East | — | CloseSliders | closed (B closes the sliders first) |
  | closed | East | false | Leave | closed |
  | closed | East | true | Unready | closed |
  | closed | South | false | Ready | closed |
  | open | South | false | CloseAndReady | closed |
  | closed | South | true | None | closed |
  | any | North | — | Invite | unchanged |

  Plus `Reset_ClosesTheSliders`.
- [ ] **Step 2: Run `bash tools/run-tests.sh DispatchMenu`. Expected: FAIL.**
- [ ] **Step 3: Implement `DispatchMenu`.** Then wire `MenuInteractions`:
  - `ClearMenuInputs` also clears `uiHandler.WestFaceEvent`. It doesn't today.
  - `CharacterSelectMenuInteractons` adds `WestFaceEvent` → a new `PlayerCustomize(bool)`.
  - Online, `customizationSelector` is active only while `dispatchMenu.Open`. Offline it's always active, as now.
  - Online, each handler routes through `dispatchMenu.Press`. `Scroll` calls today's `CustomizationScroll*`. `Ready`, `Unready` and `Leave` call today's code paths (`PlayerReady`, the two branches of `PlayerUnreadyDespawn`). `Invite` calls `PlayOnline`.
  - `SwapToPlayerSelect` calls `dispatchMenu.Reset()`. That covers the return after results.
  - Add `public void RefreshDispatchSliders()`, which reapplies the slider visibility rule. Task 5 calls it when the board shows or hides, since the game reaches player select offline and only then goes online (`MainMenu.SwapToOnline`).
- [ ] **Step 4: Add `MenuButtonsTests.Online_PlayerSelect_SlidersHiddenUntilWest`**, an Edit Mode component test in the style of the existing ones in that file:
  - online flag on, sliders inactive;
  - press West → active;
  - East → inactive;
  - `SwapToPlayerSelect()` while open → inactive.
- [ ] **Step 5: Run `bash tools/run-tests.sh DispatchMenu` and `bash tools/run-tests.sh MenuButtons`. Expected: PASS.**
- [ ] **Step 6: Commit**, meaning list the files.

---

### Task 5: The board, the tickets and the banner

**Files:**
- Create:
  - `Assets/Scripts/UI/Dispatch/TicketView.cs`
  - `Assets/Scripts/UI/Dispatch/DispatchBoard.cs`
- Modify:
  - `Assets/Scripts/Menu/PlayerSelectCanvas.cs`, for countdown events
  - `Assets/Scripts/Online/OnlinePlay.cs`, so `HintFor` hides the hint bar on Dispatch
  - `Assets/Scripts/Scriptable Objects/PlayerColorInformationSO.cs` and `PlayerHatInformationSO.cs`, to add `public string displayName;` (code only; the user fills in the assets)
- Test:
  - `Assets/Tests/Editor/DispatchBoardTests.cs`
  - `Assets/Tests/Editor/OnlinePlayTests.cs`

**Interfaces:**
- Consumes:
  - `DispatchRules`, `TicketState` (Task 3)
  - `OnlinePlayer.Name` (Task 2)
  - `MenuInteractions.RefreshDispatchSliders()` (Task 4)
  - `PlayerInstantiate.CompanyForSlot(int)`
  - `CustomizationSelector.Colours` / `.Hats`
- Produces:
  - `PlayerSelectCanvas`: `public event Action CountdownBegan, CountdownStopped`, raised in `BeginCountdown` / `StopCountdown`.
  - `TicketView : MonoBehaviour`:
    - serialized fields:
      - `Image header`;
      - `TMP_Text company, playerName, colourName, hatName`;
      - `Image colourSwatch, hatIcon`;
      - `GameObject filled, hiring, stamp, gettingReady, mineMarker`;
      - `Animator highlight` (trigger `"Flash"`; it may be null);
    - `public void Show(TicketState s, CompanyInformation company, PlayerColorInformationSO colour, PlayerHatInformationSO hat)`
    - `public void Flash()`
  - `DispatchBoard : MonoBehaviour`:
    - serialized fields:
      - `GameObject root`;
      - `TicketView[] tickets` (4, by seat);
      - `TMP_Text readyLine`;
      - `GameObject banner`;
    - `public static bool Showing`
    - `public void Apply(TicketState[] states)`, which shows the tickets, flashes per `DispatchRules.Flashes` against the previous states, and sets `readyLine`
    - `public void SetBanner(bool on)`
  - `OnlinePlay.HintFor(GameState state, bool online, bool inLobby, bool steam, bool hasOnlineEntry, bool dispatch)` returns `""` when `online && dispatch && state == GameState.PlayerSelect`.
- Behaviour:
  - Each `Update`, `DispatchBoard` decides whether it's showing: `GameAuthority.IsOnline && MainState == PlayerSelect`.
  - When that changes, it:
    - sets `root` active and `Showing`;
    - hides or restores `PlayerSelectCanvas.pressButtonTexts` (it may need the canvas's `TogglePressButtonOnAllTexts`, or a new `SetJoinPromptsHidden(bool)`);
    - calls `RefreshDispatchSliders()` on each local `PlayerUIHandler.menuInteractions`.
  - While showing, it builds `DispatchRules.Tickets` from `OnlineGame`'s `Players` (`mine` = `IsOwner`) and calls `Apply`.
  - `CountdownBegan` → `SetBanner(true)`. `CountdownStopped` → `SetBanner(false)`, and leaving player select also hides it.
  - Text fallbacks:
    - the colour or hat name is the SO's `displayName`, or its asset `name` when that's empty;
    - the company line is `company.companyName.ToString().ToUpperInvariant()`.

- [ ] **Step 1: Write the failing tests** in `DispatchBoardTests` (Edit Mode). Build a board with 4 `TicketView`s whose fields are fresh GameObjects and TMP texts, plus test SOs.
  - `Apply_FreeSeat_ShowsHiring`: `hiring` active, `filled` inactive.
  - `Apply_ReadySeat_ShowsStamp_NotGettingReady`.
  - `Apply_Mine_ShowsMarker`, and only on that ticket.
  - `Apply_SetsReadyLine`: `"1 / 2 READY"`.
  - `Apply_SameStatesTwice_FlashesOnce`: count `Flash` calls with a test subclass, or with a counter field exposed `internal`.
  - `Apply_SeatEmpties_NoFlash_ShowsHiring` (Review Focus: a leave mid-countdown).
  - `Banner_FollowsCountdownEvents`: raise `CountdownBegan` → banner active. Raise `CountdownStopped` → inactive. Raise began, began → still one active banner, and nothing throws (Review Focus: a join during the banner raises Stopped, as `SetRemoteReady(seat,false)` does today).
  - `TicketView_EmptyDisplayName_UsesAssetName`.
  - In `OnlinePlayTests`, extend `HintFor` with `dispatch: true` → `""` in online player select, and unchanged elsewhere. The existing cases pass `dispatch: false`.
- [ ] **Step 2: Run `bash tools/run-tests.sh DispatchBoard` and `bash tools/run-tests.sh OnlinePlayTests`. Expected: FAIL.**
- [ ] **Step 3: Implement** the interfaces and behaviour above. `OnlinePlay.Update` passes `DispatchBoard.Showing` as `dispatch`.
- [ ] **Step 4: Rerun, then run the full `bash tools/run-tests.sh`. Expected: all PASS.**
- [ ] **Step 5: Commit**, meaning list the files.

---

### Task 6: Editor checklist and docs

**Files:**
- Modify:
  - `EDITOR-TODO.md`
  - `docs/online.md`
  - `docs/testing.md`
  - `.gitignore`

- [ ] **Step 1: In `EDITOR-TODO.md`, add a "Dispatch lobby" section for the user**, in the file's existing style:
  - Build the depot back room behind the podiums in the menu scene:
    - wall and floor;
    - a full-width corkboard above the podiums, lit at about 75% of the podium spotlights (the user asked for W3, brighter);
    - spotlights on the 4 `menuSpawnPositions` podiums, with each rim glowing in the seat's company colour.
  - Add a Screen Space canvas with a `DispatchBoard`:
    - `root`;
    - 4 `TicketView`s laid out over the board, one above each podium, wired per its fields;
    - `readyLine` top-right;
    - a "DISPATCH" wordmark top-left (new art, in the style of `Local.png` / `Multiplayer.png`);
    - `banner` with "ALL ORDERS DISPATCHED";
    - bottom prompts with the green button art: A CLOCK IN · X CUSTOMIZE · Y INVITE · B LEAVE.
  - Ticket art:
    - paper, pin, company header strip, the DISPATCHED stamp, the HIRING… blank;
    - a `Flash` animator trigger (brighten, then fade back over about 0.6 s).
  - Fill in `displayName` on the 6 colour and 7 hat assets.
  - Make sure the menu camera frames the board and all 4 podiums when there's one local player online.
  - Optional: a short celebration animation on the ghost when a player clocks in.
  - **Check:**
    - Online Play opens Dispatch with the invite window;
    - a friend's ticket and ghost appear;
    - X opens the sliders and the ticket follows;
    - A stamps;
    - everyone ready → banner → 3-2-1 → match;
    - after results, back on Dispatch, unstamped;
    - a second pad can't join online;
    - Local Play's player select is unchanged.
- [ ] **Step 2: Update `docs/online.md`:**
  - In Phase 3B, rename "Player select is the online lobby" to Dispatch, and describe the tickets and X customize.
  - In Phase 3J, add the `OnlineRules.OnePlayerPerMachine` note: off for now, and the 3J code is kept.
  - In "How it works", add the name sync (`OnlinePlayer.Name`, `PlayerNames`).
- [ ] **Step 3: Update `docs/testing.md`** with the new test classes and the ports used (7817, 7818). Also update `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md` so the next free port reads 7819.
- [ ] **Step 4: Add `.superpowers/` to `.gitignore`.**
- [ ] **Step 5: Commit**, meaning list the files.

# Local Play and Online Play on the Title Screen: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The title screen gets separate Local Play and Online Play entries. Y in player select only invites friends, and only online.

**Architecture:**
- A plain C# lookup (`TitleEntries`) maps a title-screen position to an entry, from how many entries the scene holds. Today's 4-entry scene keeps the old behaviour; the user's 5-entry scene edit switches the new one on.
- `OnlineLobby` gains a `HostFailed` event.
- `OnlinePlay` gains:
  - `StartOnline` (host, and back to the title screen on failure);
  - `PressY` (one tested rule for Y);
  - one more input to its hint rule.

**Tech Stack:** Unity 2022.3.62f3, C#, NUnit (Unity Test Framework), the existing `FakeLobbyService`/`FakeSessionControl` in `Assets/Tests/Editor/OnlineFakes.cs`.

**Spec:** `docs/superpowers/specs/2026-10-07-local-and-online-menu-design.md`

## Global Constraints

- **Edit rules:**
  - No scene, prefab, `.asset`, `.inputactions` or ProjectSettings edits. The scene change is an `EDITOR-TODO.md` chore.
  - `MainMenu.cs` and `MenuInteractions.cs` are `i/crlf`: edit them with the Edit tool only, never `sed -i`.
  - Every new file under `Assets/` gets a `.meta`: `bash tools/newmeta.sh <path>`.
- **Git:** leave everything uncommitted (the user commits). The "Commit" steps below are replaced by "leave uncommitted".
- **Tests:** `bash tools/run-tests.sh [filter]`, one run at a time. A compile error prints `NO RESULTS`. `python tools/compile-check.py` gives a ~30 s compile check.
- **Entry order with 5 entries:** Local, Online, Options, Credits, Quit. With 4: Local, Options, Credits, Quit.
- **Top line in online player select:** `OnlinePlay.HINT_IN_LOBBY`, unchanged: `"Y / Triangle: invite friends      B / Circle: leave"`.
- **Failure messages:** the existing `OnlineLobby.NO_STEAM`, `LOBBY_FAILED` and `HOST_FAILED`, shown through `Notice` as now.

## Review Focus

- **Online Play pressed twice quickly**, or Y pressed while the lobby is still being made: one lobby only. `Host()` already ignores this; Task 1 adds no second path around it.
- **The player backs out with B while the lobby is being made:** no `HostFailed`, and no jump to the title screen from somewhere else. `OnCreated` sees `wanted == false`, leaves the lobby and raises nothing (Task 1 test `HostFailed_NotRaised_WhenThePlayerMovedOn`).
- **A failure arriving after the player has left player select** (e.g. went to Options): don't yank them to the title screen. `BackToTitle` is false outside PlayerSelect (Task 1 test).
- **A host failure after the session was online:** a later session end is not a host failure, and `HostFailed` fires only from `Host`/`OnCreated`.
- **The old 4-entry scene:** Y in offline player select still hosts, and the old hint still shows (Task 1 `YAction` and `HintFor` tests; Task 2 `TitleEntries` 4-entry test).

---

### Task 1: The lobby's host failure, and OnlinePlay's rules for starting online and Y

**Files:**
- Modify: `Assets/Scripts/Online/OnlineLobby.cs` (LF)
- Modify: `Assets/Scripts/Online/OnlinePlay.cs` (w/crlf in the working tree, i/lf: Edit tool)
- Modify: `Assets/Scripts/Menu/MenuInteractions.cs` (CRLF: Edit tool): `PlayOnline(bool)` calls `PressY`
- Test: `Assets/Tests/Editor/OnlineLobbyTests.cs`, `Assets/Tests/Editor/OnlinePlayTests.cs`

**Interfaces:**
- Produces:
  - `OnlineLobby`: `public event Action<string> HostFailed`.
  - `OnlinePlay`:
    - `public enum YPress { Nothing, Host, Invite }`;
    - `public static YPress YAction(bool hasOnlineEntry, bool online)`;
    - `public static bool BackToTitle(GameState state, bool online)`;
    - `public void StartOnline()`;
    - `public void PressY(bool hasOnlineEntry)`;
    - `public static string HintFor(GameState state, bool online, bool inLobby, bool steam, bool hasOnlineEntry)`;
    - `public static OnlinePlay Instance { get; internal set; }` (the setter becomes internal so tests can set it).
  - `PlayOnline()` is removed: `PressY` replaces it.

- [ ] **Step 1: Write the failing tests**

In `OnlineLobbyTests`, record failures with a `List<string> failures` and a `void Failed(string reason)` subscribed in `SetUp` (method group, like `Noticed`):

```csharp
[Test]
public void HostFailed_WithoutSteam_GivesTheReason()
{
    lobbies.Available = false;
    lobby.Host();
    CollectionAssert.AreEqual(new[] { OnlineLobby.NO_STEAM }, failures);
}

[Test]
public void HostFailed_LobbyCantBeMade_GivesTheReason()
{
    lobby.Host();
    lobbies.RaiseCreated(0);
    CollectionAssert.AreEqual(new[] { OnlineLobby.LOBBY_FAILED }, failures);
}

[Test]
public void HostFailed_SessionWontStart_GivesTheReason()
{
    session.HostWorks = false;
    Hosting();
    CollectionAssert.AreEqual(new[] { OnlineLobby.HOST_FAILED }, failures);
}

[Test]
public void HostFailed_NotRaised_OnSuccess_OrWhenThePlayerMovedOn()
{
    Hosting();
    session.Stop(null);                      // a session end isn't a host failure
    lobby.Host();
    lobby.ShowState(GameState.Menu);         // B before the lobby is made
    lobbies.RaiseCreated(43);
    CollectionAssert.IsEmpty(failures);
}
```

In `OnlinePlayTests`:
- Update the existing `HintFor` calls with a fifth argument `false`. Their expected values are unchanged.
- Add these tests:

```csharp
[Test]
public void Hint_WithTheOnlineEntry_OfflinePlayerSelect_OffersNothing()
{
    Assert.AreEqual("", OnlinePlay.HintFor(GameState.PlayerSelect, false, false, true, true));
    Assert.AreEqual(OnlinePlay.HINT_IN_LOBBY, OnlinePlay.HintFor(GameState.PlayerSelect, true, true, true, true));
}

[TestCase(true, false, OnlinePlay.YPress.Nothing)]   // Local Play
[TestCase(true, true, OnlinePlay.YPress.Invite)]     // Online Play
[TestCase(false, false, OnlinePlay.YPress.Host)]     // the old 4-entry scene
[TestCase(false, true, OnlinePlay.YPress.Invite)]
public void YAction_FollowsTheMenuAndTheRole(bool hasOnlineEntry, bool online, OnlinePlay.YPress expected)
{
    Assert.AreEqual(expected, OnlinePlay.YAction(hasOnlineEntry, online));
}

[TestCase(GameState.PlayerSelect, false, true)]
[TestCase(GameState.PlayerSelect, true, false)]   // online already: the session's own flow handles it
[TestCase(GameState.Options, false, false)]       // moved on: left alone
[TestCase(GameState.Menu, false, false)]
public void BackToTitle_OnlyFromOfflinePlayerSelect(GameState state, bool online, bool expected)
{
    Assert.AreEqual(expected, OnlinePlay.BackToTitle(state, online));
}
```

Add a component test, `StartOnline_Fails_GoesBackToTheTitleScreen`, in the style of `UseDirect_Twice_…`:
- Setup:
  - a `GameManager` from `TestObjects`, with `GameManager.instance` set;
  - `SetGameState(GameState.PlayerSelect)`;
  - an `OnlinePlay` with `UseDirect(new FakeLobbyService { Available = false }, "127.0.0.1", 7777)`.
- Act: `play.StartOnline()`.
- Assert: `GameState.Menu == game.MainState`.
- TearDown: clear `GameManager.instance`.

Add `StartOnline_AsksSteamForALobby`:
- Setup as above, but `Available = true`.
- Act: `StartOnline()`.
- Assert:
  - `lobbies.Creates.Count == 1`;
  - the state is still `PlayerSelect`.
- Don't raise `Created`: that would start a real direct session in Edit Mode. `OnlineLobbyTests` already covers a made lobby hosting and opening the invite window.

- [ ] **Step 2: Run the tests to see them fail**

Run: `bash tools/run-tests.sh "OnlineLobbyTests|OnlinePlayTests"`
Expected: `NO RESULTS` (compile errors: `HostFailed`, `YAction`, `BackToTitle`, `StartOnline`, the 5-argument `HintFor` don't exist).

- [ ] **Step 3: Implement**

**`OnlineLobby`:**
- Raise `HostFailed(reason)` beside each `Tell(...)` for `NO_STEAM`, `LOBBY_FAILED` (only when `wanted`) and `HOST_FAILED`.
- Use a private `FailHost(string reason)` that calls `Tell` and raises the event.

**`OnlinePlay`:**
- `YAction`:
  - online → `Invite`;
  - offline with the entry → `Nothing`;
  - offline without it → `Host`.
- `BackToTitle`: `state == GameState.PlayerSelect && !online`.
- `StartOnline()`: `Lobby.Host()`.
- `PressY(bool hasOnlineEntry)`: switch on `YAction(hasOnlineEntry, GameAuthority.IsOnline)`. `Host` calls `Lobby.Host()`, `Invite` calls `Lobby.Invite()`, `Nothing` returns.
- **Failure handling:** in `Use()`, subscribe `Lobby.HostFailed += OnHostFailed` (and unsubscribe from the replaced lobby, beside `Notice`). `OnHostFailed` calls `GameManager.Instance.SetGameState(GameState.Menu)` when `GameManager.Instance != null && BackToTitle(MainState, GameAuthority.IsOnline)`. That is what `MainMenu.SwapToMainMenu` does, without needing the menu.
- **`HintFor`:** the new last parameter `hasOnlineEntry`. Offline in player select, return `steam && !hasOnlineEntry ? HINT_PLAY_ONLINE : ""`.
- **`Update`:**
  - passes `MainMenu.Instance != null && MainMenu.Instance.HasOnlineEntry`;
  - Task 2 adds `HasOnlineEntry`. Until then, pass `false` with a `// Task 2` note, so this task compiles alone.
- **Docs and setter:**
  - Update the class summary: Online Play on the title screen starts online, and Y invites friends.
  - `Instance` setter becomes `internal`.

**`MenuInteractions.PlayOnline(bool)`:**
- calls `OnlinePlay.Instance.PressY(MainMenu.Instance != null && MainMenu.Instance.HasOnlineEntry)`;
- passes `false` until Task 2;
- its summary becomes "Y in player select: invites friends online (Steam's invite window); in the old title screen, offline, it goes online".

- [ ] **Step 4: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "OnlineLobbyTests|OnlinePlayTests"`
Expected: all pass.

- [ ] **Step 5: Leave uncommitted**

---

### Task 2: The title screen's entries, and the docs

**Files:**
- Create: `Assets/Scripts/Menu/TitleEntries.cs` (+ `.meta` via `bash tools/newmeta.sh`)
- Modify: `Assets/Scripts/Menu/MainMenu.cs` (CRLF: Edit tool)
- Modify: `Assets/Scripts/Online/OnlinePlay.cs`, `Assets/Scripts/Menu/MenuInteractions.cs`: replace Task 1's `false` placeholders with `MainMenu.Instance != null && MainMenu.Instance.HasOnlineEntry`.
- Create: `Assets/Tests/Editor/TitleEntriesTests.cs` (+ `.meta`)
- Modify: `Assets/Tests/Editor/MainMenuTests.cs` (w/crlf: Edit tool)
- Docs: `docs/online.md`, `docs/testing.md`, `EDITOR-TODO.md`, the handoff (`docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`).

**Interfaces:**
- Consumes: `OnlinePlay.Instance`, `OnlinePlay.StartOnline()` (Task 1).
- Produces:
  - `public enum TitleEntry { None, Local, Online, Options, Credits, Quit }`;
  - `public static class TitleEntries` with `static TitleEntry At(int index, int count)` and `static bool HasOnline(int count)`;
  - `MainMenu`:
    - `public bool HasOnlineEntry`;
    - `internal void Choose(TitleEntry entry)`;
    - `public void SwapToOnline()`;
    - `selectorObjects` becomes `[SerializeField] internal GameObject[]`. Same name, still serialized, so the scene keeps its references.

- [ ] **Step 1: Write the failing tests**

`TitleEntriesTests`:

```csharp
[Test]
public void FiveEntries_LocalOnlineOptionsCreditsQuit()
{
    CollectionAssert.AreEqual(
        new[] { TitleEntry.Local, TitleEntry.Online, TitleEntry.Options, TitleEntry.Credits, TitleEntry.Quit },
        new[] { 0, 1, 2, 3, 4 }.Select(i => TitleEntries.At(i, 5)));
    Assert.IsTrue(TitleEntries.HasOnline(5));
}

[Test]
public void FourEntries_TheOldTitleScreen_HasNoOnline()
{
    CollectionAssert.AreEqual(
        new[] { TitleEntry.Local, TitleEntry.Options, TitleEntry.Credits, TitleEntry.Quit },
        new[] { 0, 1, 2, 3 }.Select(i => TitleEntries.At(i, 4)));
    Assert.IsFalse(TitleEntries.HasOnline(4));
}

[TestCase(-1, 5)]
[TestCase(5, 5)]
[TestCase(4, 4)]
[TestCase(0, 3)]   // a count the code doesn't know: nothing
public void OutsideTheKnownEntries_IsNothing(int index, int count)
{
    Assert.AreEqual(TitleEntry.None, TitleEntries.At(index, count));
}
```

`MainMenuTests`:
- `Choose_Local_GoesToPlayerSelect`: `menu.Choose(TitleEntry.Local)`, then the state is `PlayerSelect`.
- `Choose_Online_StartsOnline_AndAFailureComesBackToTheTitleScreen`:
  - Setup:
    - an `OnlinePlay` on a `TestObjects` object, with `UseDirect(new FakeLobbyService { Available = false }, "127.0.0.1", 7777)`;
    - `OnlinePlay.Instance` set to it;
    - a recorder subscribed to `play.Lobby.Notice`.
  - Act: `menu.Choose(TitleEntry.Online)`.
  - Assert: the state is `Menu`, and the notice is `OnlineLobby.NO_STEAM`.
  - TearDown: `OnlinePlay.Instance = null`.
- `HasOnlineEntry_FollowsTheScenesEntries`: `menu.selectorObjects = new GameObject[5]` gives true, `new GameObject[4]` gives false.

- [ ] **Step 2: Run the tests to see them fail**

Run: `bash tools/run-tests.sh "TitleEntriesTests|MainMenuTests"`
Expected: `NO RESULTS` (`TitleEntry`, `TitleEntries`, `Choose`, `HasOnlineEntry` don't exist).

- [ ] **Step 3: Implement**

- **`TitleEntries.At`:**
  - 5 → `{Local, Online, Options, Credits, Quit}`;
  - 4 → `{Local, Options, Credits, Quit}`;
  - any other count or an index out of range → `None`.
  - Summary: why the count decides it (the user's scene edit switches Online Play on).
- **`MainMenu`:**
  - `ConfirmMenu()` becomes `Choose(TitleEntries.At(selectorPos, selectorObjects.Length))`.
  - `Choose` switches:
    - `Local` → `SwapToPlayerSelect()`;
    - `Online` → `SwapToOnline()`;
    - `Options`, `Credits` → their swaps;
    - `Quit` → the existing quit code;
    - `None` → nothing.
  - `SwapToOnline()`: `SwapToPlayerSelect()`, then `OnlinePlay.Instance?.StartOnline()`. Player select must come first: a lobby still being made is kept only in player select.
  - `HasOnlineEntry => selectorObjects != null && TitleEntries.HasOnline(selectorObjects.Length)`.
- Replace Task 1's two `false` placeholders.
- Fix `MainMenu.Start`'s comment: "where Y and B work" becomes "where Y invites and B leaves".

- [ ] **Step 4: Run the tests to see them pass**

Run: `bash tools/run-tests.sh "TitleEntriesTests|MainMenuTests|OnlinePlayTests|OnlineLobbyTests"`
Expected: all pass.

- [ ] **Step 5: Docs**

- **`docs/online.md`:**
  - Line 3: nothing online runs unless a player chooses Online Play, or presses Y in the old 4-entry title screen, or accepts an invite.
  - The two-PC walk-through (~line 272): Online Play → player select, Steam's invite window opens.
  - Replace the "no Play Online row until there's art" limit (~line 311) with how the entry count switches it on.
  - Add a short "Local Play and Online Play" section.
- **`docs/testing.md`:** `TitleEntriesTests` and the new tests.
- **`EDITOR-TODO.md`:** a new section 1, "Local Play and Online Play on the title screen". Renumber the rest.
  - Duplicate the Play entry under itself, and draw or label it "Online Play".
  - In MainMenu, put it in Selector Objects at position 1, and its ghost sprite in Selector Ghost Sprites at position 1. Both lists then hold 5.
  - Optional: relabel Play as "Local Play".
  - Check:
    - Local Play: Y does nothing, and no line along the top.
    - Online Play: Steam's invite window opens, Y reopens it, B goes back to the title screen.
    - With Steam closed: Online Play shows "Start Steam to play online." and goes back to the title screen.
- **Handoff:** the status line, and this plan done.

- [ ] **Step 6: The full suite**

Run: `bash tools/run-tests.sh` (in the background, about 40 minutes)
Expected: all pass (691 plus the new tests).

- [ ] **Step 7: Leave uncommitted**

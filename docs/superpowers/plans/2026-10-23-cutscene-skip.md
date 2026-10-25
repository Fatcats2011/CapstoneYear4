# Cutscene Skip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** every player holds A for 0.5 s to vote to skip the opening or golden cutscene. Once everyone has voted, it ends.

**Architecture:**
- **The rules:** pure `SkipVote` (who's voted) and `SkipHold` (one player's hold, armed only by a fresh press after the prompt shows).
- **The screen:** a `CutsceneSkip` component, added by `CutsceneManager` at runtime, times each local player's hold, shows the prompt, and calls `CutsceneManager.Skip()` where states are decided.
- **Online:** it copies the rematch vote's pattern: a static `SkipSync` seam, an `OnlineCutsceneSkip` relay, two `OnlineMatch` RPCs, and a `SkipBoard` sent to clients.

**Tech Stack:** Unity 2022.3, C#, Netcode for GameObjects 1.15.1, Input System 1.14, TextMesh Pro, NUnit.

**Spec:** `docs/superpowers/specs/2026-10-23-cutscene-skip-design.md`

## Global Constraints

**What changes, and how files are edited**
- No scene, prefab, `.inputactions` or ProjectSettings edits. The prompt is built by code.
- New `Assets/` files get a `.meta` (`bash tools/newmeta.sh`).
- CRLF files: edit them with CRLF-preserving edits.

**Branch and testing**
- Branch `steam-phase1a`. Leave everything uncommitted: the user commits.
- `bash tools/run-tests.sh <filter>`, one run at a time. Compile check: `python tools/compile-check.py`.

**Values and copy**
- `SkipHold.HOLD = 0.5f` and `SkipHold.SHOW_AFTER = 1f`.
- Copy: `HOLD` + glyph + `TO SKIP` + `  {n}/{m}`, in the scores' font (Bangers). The confirm glyph comes from `ButtonArtSet.Results()`.
- The UI South action is `"UI/ReadyUp"` (the prefab's PlayerInput sends it to `PlayerUIHandler.SouthFaceTrigger`).

## Review Focus

1. **A still held from the loading screen as the prompt appears.** It must not count: the player has to let go and press again. Test in Task 1.
2. **Quick taps.** A tap of under 0.5 s never votes, however many there are. Test in Task 1.
3. **A machine leaves mid-cutscene online.** The others don't wait on it. Test in Task 3.
4. **The cutscene ends by itself before everyone votes.** The vote closes with no skip and no stray prompt. Test in Task 3.
5. **The golden cutscene after an opening skip.** A fresh vote, with nothing carried over. Test in Task 3.

---

### Task 1: The rules (`SkipVote`, `SkipHold`)

**Files:**
- Create: `Assets/Scripts/Menu/SkipVote.cs` (both classes)
- Test: `Assets/Tests/Editor/SkipVoteTests.cs`

**Interfaces:**
- Produces:
  ```csharp
  public class SkipVote {
      public SkipVote(IEnumerable<int> seats);
      public bool Vote(int seat);   // false: not in, or already voted
      public void Leave(int seat);
      public bool IsIn(int seat); public bool HasVoted(int seat);
      public int Voted { get; } public int Total { get; }
      public bool Everyone { get; } // Total > 0 and every seat in has voted
  }
  public class SkipHold {
      public const float HOLD = 0.5f, SHOW_AFTER = 1f;
      public bool Tick(bool pressed, float deltaTime, bool promptShown); // true once, the frame the hold completes
  }
  ```
  **SkipHold rules:**
  - It's armed only after it has seen the button up while the prompt shows.
  - Holding adds up time while it's armed and pressed. Letting go resets the time.
  - After it returns true, it never returns true again.

- [ ] **Step 1: Write the failing tests:**
  - **SkipVote:**
    - `Everyone_AllSeatsVoted_IsTrue`
    - `Everyone_SomeToVote_IsFalse`
    - `Vote_NotIn_OrTwice_IsRefused`
    - `Leave_TheLastHoldout_MakesItEveryone`: seats {0,1}, 0 votes, 1 leaves → true.
    - `Everyone_NoSeats_IsFalse`
  - **SkipHold:**
    - `Hold_HalfASecondAfterThePrompt_Votes`: shown, up, then pressed at 0.1 s × 5 → true on the 5th tick.
    - `Hold_HeldSinceBeforeThePrompt_DoesNotCount`: pressed throughout, shown from the start, 2 s → never true. Then up once, then 0.5 s of pressing → true.
    - `Tap_ShorterThanTheHold_DoesNothing`: 0.3 s down, up, 0.3 s down → false.
    - `Hold_BeforeThePromptShows_DoesNotCount`
    - `Hold_OnceVoted_NeverAgain`

- [ ] **Step 2: Watch them fail.** Run `python tools/compile-check.py`. Expected: it doesn't compile (the types are missing).

- [ ] **Step 3: Implement both classes.** Run `newmeta.sh` on the new files.

- [ ] **Step 4: Watch them pass.** Run `bash tools/run-tests.sh SkipVoteTests`. Expected: all pass.

---

### Task 2: The skip vote's messages (`SkipBoard`, `SkipSync`, RPCs)

**Files:**
- Create:
  - `Assets/Scripts/Online/SkipBoard.cs`
  - `Assets/Scripts/Online/SkipSync.cs`
- Modify:
  - `RateGate.cs`: `RpcKind.Skip`, with the default allowance.
  - `NetChecks.cs`: `Sane(SkipBoard)`.
  - `OnlineMatch.cs`
- Test: `Assets/Tests/Editor/SkipBoardTests.cs`

**Interfaces:**
- Consumes: `SkipVote` (Task 1).
- Produces:
  - **`SkipBoard`:** `public struct SkipBoard : INetworkSerializable, IEquatable<SkipBoard>`.
    - Fields: `byte InMask, VotedMask`.
    - `static From(SkipVote)`, `IsIn(int)`, `HasVoted(int)`, plus `int Voted`, `int Total`.
  - **`SkipSync`** (static, shaped like `RematchSync`), each with a raising method and a `Reset()`:
    - `VoteSent(int seat)`, on a client;
    - `VoteArrived(int seat)`, on the host, already checked to be the sender's own seat;
    - `BoardShared(SkipBoard)`, on the host;
    - `BoardArrived(SkipBoard)`, on a client.
  - **`OnlineMatch`:**
    - `SendSkipVote(int seat)`, from a client → `SkipServerRpc`, through `Allowed(sender, RpcKind.Skip)` → `event Action<ulong,int> SkipVoteReceived`.
    - `ShareSkipBoard(SkipBoard)`, from the host → `SkipBoardClientRpc` → `event Action<SkipBoard> SkipBoardReceived` when `NetChecks.Sane(board)`.
  - **`NetChecks.Sane(SkipBoard)`:** the masks are within the low 4 bits, and `VotedMask` is a subset of `InMask`.

- [ ] **Step 1: Write the failing tests:**
  - `From_ReflectsSeatsAndVotes`
  - `Sane_RefusesAFifthSeat_AndAVoteOutsideTheSeats`
  - `Sane_AcceptsAnHonestBoard`
  - `SkipSync_ArriveVote_RaisesVoteArrived`

- [ ] **Step 2: Watch them fail.** Run `python tools/compile-check.py`. Expected: it doesn't compile.

- [ ] **Step 3: Implement.** Mirror `RematchBoard` and `RematchSync`, and the `VoteServerRpc` / `BoardClientRpc` pair.

- [ ] **Step 4: Watch them pass.** Run `bash tools/run-tests.sh "SkipBoardTests|RateGateTests|NetChecks"`. Expected: all pass.

---

### Task 3: The cutscene's skip (`CutsceneSkip`), its prompt, and the online relay

**Files:**
- Create:
  - `Assets/Scripts/Menu/CutsceneSkip.cs`: the controller and its code-built prompt.
  - `Assets/Scripts/Online/OnlineCutsceneSkip.cs`
- Modify:
  - `Assets/Scripts/Menu/CutsceneManager.cs`: add the component in `Start` (or `OnEnable` if there's no `Start`); open it in `BeginCutsceneCoroutine`; close it in `CloseView`.
  - `Assets/Scripts/Online/OnlineGame.cs`: add `OnlineCutsceneSkip` next to `OnlineRematch`.
- Test:
  - `Assets/Tests/Editor/CutsceneSkipTests.cs`
  - `Assets/Tests/Editor/OnlineCutsceneSkipNetworkTests.cs` (port 7832)

**Interfaces:**
- Consumes:
  - `SkipVote` and `SkipHold` (Task 1)
  - `SkipBoard`, `SkipSync` and the `OnlineMatch` members (Task 2)
  - `ButtonArtSet.Results()`
  - `CutsceneManager.Skip()`
- Produces **`CutsceneSkip : MonoBehaviour`:**
  - `public void Open(IEnumerable<int> seats, bool decides)`
  - `public void Close()`
  - `internal void Tick(float deltaTime)`
  - **Test hooks:**
    - `internal Func<int, bool> Pressed`, defaulting to the seat's local `PlayerInput.actions.FindAction("UI/ReadyUp")?.IsPressed()`, and false for a seat that isn't local;
    - `internal Action Skip`, defaulting to `CutsceneManager.Instance.Skip()`;
    - `internal Func<int, bool> SeatStillThere`, with the roster check as `ResultsVote`'s;
    - `internal IEnumerable<int> LocalSeats`, defaulting to `Roster.LocalPlayers`' indices.
- Produces **`OnlineCutsceneSkip`**, shaped like `OnlineRematch`:
  - client: `VoteSent` → `SendSkipVote`, and `SkipBoardReceived` → `ArriveBoard`;
  - host: `SkipVoteReceived` → `ArriveVote` when `session.Owns(machine, seat)`, and `BoardShared` → `ShareSkipBoard`.

**Behaviour:**
- **While open, `Tick` does this:**
  1. Adds up the time since `Open`. The prompt shows once it passes `SHOW_AFTER`.
  2. For each local seat: if `SkipHold.Tick(Pressed(seat), dt, shown)` is true, then if this machine decides, `vote.Vote(seat)`; otherwise `SkipSync.SendVote(seat)`.
  3. When it decides:
     - seats gone from the roster leave the vote;
     - the board is shared on any change;
     - once `vote.Everyone`, it calls `Skip()` once and closes.
- **`Close`:** it hides the prompt and unhooks. Any later presses or votes do nothing.
- **An online client:** its prompt count follows `SkipSync.BoardArrived`.

- [ ] **Step 1: Write the failing Edit Mode tests**, with the hooks injected (static press table, no captured locals needed in Edit Mode):
  - `EveryoneHolds_SkipsOnce`
  - `OnePlayerHolds_OfTwo_DoesNotSkip`
  - `ASeatGoneFromTheRoster_DoesNotHoldTheOthersUp`
  - `HeldFromBeforeThePrompt_DoesNotSkip` (Review Focus 1)
  - `Closed_ThenHeld_DoesNothing` (Review Focus 4)
  - `OpenAgain_StartsFresh` (Review Focus 5)
  - `OnAClient_ACompletedHold_SendsTheVote`: hook `SkipSync.VoteSent`; `Skip` is never called.

- [ ] **Step 2: Watch them fail.** Run `python tools/compile-check.py`. Expected: it doesn't compile.

- [ ] **Step 3: Implement `CutsceneSkip`, `OnlineCutsceneSkip`, and the `CutsceneManager` / `OnlineGame` wiring.**
  - **Prompt:** under `cutsceneCanvas`, anchored bottom right (pivot (1,0), at (-40, 40)). It's a row with a `HOLD` label, a 56 px glyph `Image` with a `ButtonGlyph`, and a `TO SKIP  {n}/{m}` label. It's hidden until `SHOW_AFTER`.
  - **Opening:** `CutsceneManager` opens it with every roster seat and `GameAuthority.IsAuthority`.

- [ ] **Step 4: Watch them pass.** Run `bash tools/run-tests.sh "CutsceneSkipTests|SkipVoteTests|SkipBoardTests"`. Expected: all pass.

- [ ] **Step 5: Write the network test** `OnlineCutsceneSkipNetworkTests`, with the game as host and a bare client, ended the way `OnlineRematchNetworkTests.EndSession` ends it:
  - `Hosting_AClientsSkipCountsOnlyForItsOwnSeat_AndEveryonesSkips`:
    - Open a `CutsceneSkip` by hand, with seats {0,1} and decides.
    - `other.Match.SendSkipVote(0)` → not counted.
    - `SendSkipVote(1)` → counted, and the other machine gets a board showing seat 1.
    - Seat 0's hold (`Pressed` hooked to a static) → the injected `Skip` is called once.
  - Run `bash tools/run-tests.sh OnlineCutsceneSkipNetworkTests`. Expected: it passes.

---

### Task 4: Docs and the full suite

- [ ] **Step 1: Update the docs:**
  - `docs/controls.md`: a row, "Cutscenes: hold A (Space) to vote to skip".
  - `docs/online.md`: a short "Cutscene skip" bullet list.
  - `docs/online-safety.md`: a row for `RpcKind.Skip`, the own-seat check, and `Sane(SkipBoard)`.
  - `docs/testing.md`: the new tests.
- [ ] **Step 2: Run the full suite.** Run `bash tools/run-tests.sh`. Expected: 0 failed.

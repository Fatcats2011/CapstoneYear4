# Local Play and Online Play on the title screen: design

**Date:** 2026-10-07  
**Branch:** `steam-phase1a`  
**Status:** approved in chat, waiting for the user to review this written spec

## Goal

The title screen offers local split screen and online play as separate entries. That replaces Y in player select as the way to go online.

**From the user:**
- Two entries: "Local Play" and "Online Play".
- In Local Play, Y does nothing.
- In Online Play, Y opens Steam's invite window, the overlay that lists the player's Steam friends.
- The user adds the new entry's art in the scene. The code handles everything else.

**Assumptions (agreed):**
- Accepting a friend's invite still works from anywhere in the menus, including Local Play's player select. The local players come along, as in Phase 3J.
- The editor's Online test menu is unchanged.

## Design

### 1. The title screen entries

- `MainMenu.ConfirmMenu`'s hard-coded `case 0..3` becomes a lookup in a new plain C# class, `TitleEntries`.
- `TitleEntries.At(int index, int count)` returns an enum: `Local`, `Online`, `Options`, `Credits`, `Quit`.
  - **5 entries:** Local, Online, Options, Credits, Quit.
  - **4 entries** (today's scene): Local, Options, Credits, Quit.
  - Any other count: entries past the known ones do nothing.
- `TitleEntries.HasOnline(int count)` is true for 5.
- `MainMenu.HasOnlineEntry` exposes it for the scene's `selectorObjects.Length`.
- **Until the user's scene edit lands (4 entries), the game behaves exactly as it does now, Y included.** The scene edit is what switches the new behaviour on.

### 2. Local Play

- Goes to player select (`MainMenu.SwapToPlayerSelect`), as Play does now.
- With the online entry present:
  - Y in player select does nothing while this machine is offline.
  - The line along the top (`LobbyPrompt`) shows nothing.

### 3. Online Play

- `MainMenu.SwapToOnline()` switches to player select, then calls `OnlinePlay.StartOnline()`. The order matters: `OnlineLobby` keeps a lobby still being made only while the state is PlayerSelect.
- `StartOnline` calls `Lobby.Host()`, which creates the lobby, hosts the session in it, and opens Steam's invite window (as Y does today).
- **Failure goes back to the title screen.**
  - `OnlineLobby` raises a new event, `HostFailed`, with its reason. It covers all three failures: `NO_STEAM`, `LOBBY_FAILED` and `HOST_FAILED`.
  - The message still shows in the hint bar, through `Notice`, as now.
  - OnlinePlay answers `HostFailed` with `MainMenu.SwapToMainMenu()` when the game is in player select and offline.
  - So a failed Online Play never leaves the player in a local lobby.
- **In Online Play's player select:**
  - Y (`MenuInteractions.PlayOnline`) calls `OnlinePlay.Invite()`, which reopens Steam's invite window. It does nothing without a lobby. Without the overlay, the existing "Invite friends from your Steam friends list." still shows.
  - The top line reads `HINT_IN_LOBBY`: "Y / Triangle: invite friends      B / Circle: leave".
  - B is unchanged: the menu player leaves to the title screen, and another player on this PC gives their seat back.
- **What Y does, in one rule** (`OnlinePlay.YAction(bool hasOnlineEntry, bool online)`, tested):
  - online: Invite;
  - offline with the online entry: nothing;
  - offline without it (old scene): Host, as today.
- **The hint, extended:** `OnlinePlay.HintFor(state, online, inLobby, steam, hasOnlineEntry)`. With the online entry, `HINT_PLAY_ONLINE` is never shown. Otherwise the hint is as now.

### 4. Joining a friend

Unchanged: `OnlinePlay.AnswerRequest` and `LobbyRules.Busy`.

### 5. Files

- **New:** `Assets/Scripts/Menu/TitleEntries.cs` (+ `.meta`).
- **Changed:**
  - `MainMenu.cs` (CRLF: Edit tool only): `ConfirmMenu` via `TitleEntries`, `HasOnlineEntry`, `SwapToOnline`.
  - `MenuInteractions.cs` (CRLF): `PlayOnline` follows `YAction`.
  - `OnlinePlay.cs`: `StartOnline`, `Invite`, `YAction`, `HintFor` (extra parameter), and the `HostFailed` handler.
  - `OnlineLobby.cs`: the `HostFailed` event.
- **No scene, prefab or asset edits.**
- **`EDITOR-TODO.md`, a new section for the user:**
  - Duplicate the Play entry in the title screen and draw/label it "Online Play".
  - Put it in `MainMenu.selectorObjects` at index 1, and its ghost sprite in `selectorGhostSprites` at index 1. Both lists then hold 5.
  - Optionally relabel Play as "Local Play".
  - Then check: Local Play, Y does nothing; Online Play, invite window opens; Online Play with Steam closed goes back to the title with "Start Steam to play online.".

### 6. Testing

- **Edit Mode:**
  - `TitleEntriesTests`: 4 and 5 entries, and an index out of range.
  - `OnlinePlayTests`: `YAction`, and `HintFor` with and without the entry.
  - `OnlineLobbyTests`: `HostFailed` is raised for no Steam, a lobby that fails, and a host that fails, and not on success.
- **Component tests** (Edit Mode, with the fake lobby service, as `OnlinePlayTests.UseDirect_Twice_…` does):
  - a host failure in player select returns the game to the title screen;
  - choosing Online Play hosts and opens the invite window.
- **Docs:** `docs/online.md` (how to go online) and `docs/testing.md` (the new tests).

## Out of scope

- An in-game friends list.
- New art.
- Refusing invites while in Local Play.

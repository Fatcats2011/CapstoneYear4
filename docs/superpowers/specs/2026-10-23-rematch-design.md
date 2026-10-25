# Rematch from the results screen: design

**Date:** 2026-10-23
**Branch:** `steam-phase1a`
**Status:** waiting for the user to review this written spec
**Source:** feature 1 of `2026-10-23-modern-features-handoff.md` (pack C)

## Why

Today the results screen only goes back to player select, or to Dispatch online. Every new round costs a walk back through the lobby and the loading screen's "press A". Party games run on "one more round": a rematch puts the same players straight back into a match.

## Goals (decided with the user)

- **Two choices on the results screen:** REMATCH and BACK TO LOBBY.
- **Everyone has to agree, like Mario Kart.**
  - Each player votes for themselves.
  - A vote for Rematch shows a tick under that player's colour.
  - The round starts once every player still in has voted Rematch.
- **Back to lobby takes only that player out.**
  - **Online:** they leave the session, as leaving works today (back to the menu). The others carry on voting and rematch without them.
  - **On the couch (my call):** their seat is dropped and the split screen re-lays out for whoever is left. They can join again later from player select.
  - **If everyone picks Back to lobby**, everyone goes to the lobby, as today (`SceneFlow.Current.ReturnToMenu()`).
- **A countdown.** Anyone who hasn't voted when it runs out is taken out, the same as picking Back to lobby.

## The calls I made (the user said "looks right")

- **Countdown:** 15 seconds, starting when the choices appear (today's `QuitDelay`, 2 s after the scores). It shows on the results screen.
- **When it runs out:**
  - Whoever voted Rematch plays again.
  - Everyone else is taken out.
  - If nobody voted Rematch, everyone goes to the lobby.
- **Early start:** once every player still in has voted, the round starts at once, without waiting out the clock.
- **Changing your mind:** B after a Rematch vote takes the tick back, while there's time left.
- **Loading screen:** a rematch keeps it, since it hides the scene swap. It skips the "everyone press A" wait, because everyone just voted.
- **No "Title screen" button.** Back to lobby is enough.
- **The new round:**
  - Same players, seats, colours and hats.
  - The tutorial is skipped.
  - Scores, orders, boost, compass and match stats are reset.
  - The starting cutscene and main theme play as for any match.

## Approaches

**A. A "new match" reset, and load the match straight from the results (picked by the user)**
- One "new match" event carries the per-match resets.
- The menu path raises it as today; Rematch raises it and loads the match scene directly.
- The menu-only steps stay on the menu event, so a rematch never fires them.
- **Trade-off:** it touches about seven reset listeners, each needing a test on both paths. It's the base that the pause menu's Restart (pack B) reuses.

Not taken:
- **B. Rematch through the menu, behind the loading screen:** go back through `ReturnToMenu`, then auto-confirm everyone in player select or Dispatch. It means the least reset work. But each rematch loads the menu scene and the Dispatch room, fires the menu music and podium moves under cover, and re-runs the online ready-up. It's slower, has more ways to flicker, and Restart can't reuse it mid-match.
- **Reloading the results scene into a fresh match:** A with more scene juggling.

## How it fits the game (findings)

### The results screen
- `ResultsMenu` lives in the `Results Canvas` prefab in `FinalAreaScene`.
- `UpdateResults` shows the scores after `timeBeforeUI`, then `QuitDelay` shows `quitInfo` (the "press A" prompt) 2 s later.
- `ConfirmMenu` is the only exit. It's authority-only, and `MenuInteractions.ResultsMenuConfirm` calls it only for the host player (`hostPlayer`).
- **No prefab edits:** the choices, ticks and countdown are built by code at runtime under the results canvas, like `ResolutionRow`. They take the place of `quitInfo`, which is hidden.
- **Look:** the game's font (Bangers), the player colours, and the button prompts with `ButtonGlyph` art (A: Rematch, B: Back to lobby / take back).

### Input
- Every player's `MenuInteractions` is in `MenuType.ResultsMenu` (`SwapForResults`), so each player's own presses reach the results.
- **Today only the host's South press counts.** Every player now votes:
  - South = Rematch.
  - East = Back to lobby, or taking back a Rematch vote.
- **Up/down selection or two buttons:** two buttons (my call), since a selector shared by four players doesn't work and each player votes for themselves.

### The per-match resets today
On the menu path they listen to:
- `GameManager.OnSwapMenu`:
  - `ScoreManager.ResetScore`
  - `BallDriving.ResetBoost`
  - `Compass.ResetCompass`
  - `SoundPool.TurnOffPlayerSounds`
- `SceneFlow.OnReturnToMenu`: `OrderHandler.ResetHandler`.
- `SteamFeatures.OnStateApplied` on Menu / PlayerSelect: `MatchFeats.NewMatch`.

`OnSwapMenu` also has menu-only listeners, which must **not** run on a rematch:
- `SoundManager.PlayMenuTheme`
- `MainMenu.ShowTitleScreen`
- `SceneManager.HideLoadingScreen`
- `PlayerInstantiate.SwapForMainMenu`

`OrderManager.ResetForNextGame` (destroy the OrderManager, tutorial back on) listens to `OnSwapMenu` too, and also runs in `SceneManager.StartGameLoad`. It's safe to run twice, but the builder keeps it to one call per path. The lists above are a starting point, not complete: the builder audits every `OnSwapMenu` and `OnReturnToMenu` subscriber and sorts each into "new match" or "menu only".

### Loading the match from the results
- **Locally:** `SceneManager.StartGameLoad` checks only that the match scene isn't the active one, so it works from `FinalAreaScene`.
- **Online:** `OnlineSceneFlow.LoadGameScene` → `Begin(MatchScene.Game)` loads it on every machine and shows it once all have it.
- **The tutorial:** `ResetForNextGame` sets `TutorialManager.ShouldTutorialize = true`, so a rematch sets it false after that call.
- **Skipping the press-A wait:** locally the loading screen waits for every player's `LoadingScreenManager.ConfirmButton`. A rematch skips that wait. Online the host shows the scene already, with no wait.

### Online
- **Who decides:** the host (`GameAuthority.IsAuthority`) holds the vote and starts the rematch or the return.
- **Clients:** each client sends its player's vote to the host (Rematch, or taking a vote back) as a new `OnlineMatch` message.
  - It gets a `RateGate` allowance and the host's checks: only the sender's own seat, and only during the vote.
  - It's added to `docs/online-safety.md`.
- **The host sends:**
  - the ticks and the time left, so every machine shows the same screen;
  - the outcome, as a rematch (the existing scene load) or a return (the existing `ReturnClientRpc`).
- **Back to lobby on a client:** that machine leaves the session (`OnlineGame.LeaveOnline`, as the existing leave does). The host sees the machine go and drops its seat from the vote.
- **Back to lobby on the host:** the host can't leave alone without ending the session. So the host's Back to lobby (or the host timing out) sends everyone to Dispatch, as today. *(my call: the only option that doesn't strand the clients; a host migration is out of scope)*
- **Taken-out players and seats:** the rematch has fewer seats, the same as a player leaving mid-match today. `JoinRules` already lets nobody in once a match has started, so no new player joins between rounds.
- **Only the host left:** Rematch works as a solo match.

### Locally
- **Dropping a couch player** reuses what player select does when one leaves: `PlayerInstantiate.RemovePlayerRef` / `PlayerRoster.Leave`. The split screen re-lays out for the rest.
- `RemovePlayerRef` is gated by `allowPlayerSpawn`, so dropping a seat from the results needs a path that isn't.
- **Player 1 leaving:** Player 1 is usually the "host player" for menus (`hostPlayer`). If Player 1 is taken out and the others rematch, the host role passes to the lowest remaining seat. *(my call; the builder checks how `hostPlayer` is assigned and keeps it consistent)*

### Steam
- **Match stats:** `MatchFeats.NewMatch` today runs when the menus show. It must also run on a rematch, through the new event, or achievements like "last to first" would carry over.
- **Rich Presence:** it follows the game state, as for any match.

## Details settled after review

- **Online, one player per machine** (already decided for Dispatch). A vote is a machine's vote, for its one seat, and the host checks the seat is the sender's. Back to lobby on a client leaves the session with its one player.
- **The players carry over.** The local players (`PlayerInput`) and the online objects (`OnlinePlayer`, `OnlineScooter`, `OnlineMatch`, all `DontDestroyOnLoad`) already live through the menu → match → golden scene swaps. A rematch loads the match the same way, so nothing is rebuilt. The builder confirms this with the Play Mode test.
- **Couch, one player left (my call):** they rematch solo, as online. One player is a full-screen view, as today.
- **Dropped connections mid-vote (my call):**
  - A client that disconnects counts as taken out, through the existing disconnect path.
  - If the host goes, clients end up in the lobby through the existing session-end path. There's no host migration.
  - A slow loader is handled as any match load today: `OnlineSceneFlow.Begin` waits for every machine, with its existing timeout.
- **`hostPlayer` (my call):** the results no longer use it, since every player votes. The builder audits its other uses (e.g. `MenuInteractions`' online check) and passes the role to the lowest remaining seat only if a later menu needs it.

## Non-goals

- A Title screen button on the results.
- New players joining between rounds (`JoinRules` unchanged).
- Rematch from the pause menu (that's Restart, pack B, which will reuse the reset).
- Rematch with bots, or changing colours or hats between rounds.
- Host migration, when the online host leaves.

## Anti-goals

- **A stale round:** any score, order, boost, compass, held order or stat carried over from the last match.
- **A menu step firing on a rematch:** the menu music, the title screen, podiums, or a flash of player select or Dispatch.
- **A results screen nobody can leave:** the countdown always ends it, and Back to lobby always works.
- **Online, machines seeing different results screens** (ticks, time) or ending in different places.
- **A client voting for another seat,** or flooding the host with votes.

## Constraints

- Unity 2022.3, Netcode for GameObjects 1.15.1, Steamworks.NET.
- **No scene, prefab, `.inputactions` or ProjectSettings edits.** The results UI is built by code. Anything else is an `EDITOR-TODO.md` chore.
- **Rules go in a small pure class with Edit Mode tests** (`RematchVote` or similar), as in `JoinRules` and `StealRules`. It covers:
  - the ticks, taking a vote back, and the countdown;
  - who's out, and the outcome (rematch with these seats, everyone to the lobby, or still waiting);
  - early start, and the host's choices online.
- CRLF files are edited with the Edit tool only. New `Assets/` files get a `.meta` (`tools/newmeta.sh`).
- Short, plain player-facing text in the game's style: `REMATCH`, `BACK TO LOBBY`, `WAITING FOR PLAYERS… 12`.

## Tests (expected)

- **Edit Mode:**
  - the vote rules (above);
  - the new-match event resets scores, orders, boost and match stats on both paths;
  - the menu-only listeners don't run on a rematch.
- **Play Mode, online (like `OnlinePlayNetworkTests`):**
  - the host and a client both vote, and both machines load the match with the tutorial off and scores at zero;
  - a client picks Back to lobby, and the host rematches alone;
  - a client's vote for a seat that isn't its own is refused.

## Left to the builder

- The results UI's exact layout, sizes and position (below the scores, clear of the podium animation).
- The new event's name and home (`GameManager`, `SceneFlow`, or a small `MatchReset`), and the order its listeners run in.
- How the vote reaches each player's `MenuInteractions` and the host's `OnlineMatch`.
- How a couch player is dropped from the results without `allowPlayerSpawn`.
- Docs: `docs/controls.md` (the results buttons), `docs/online.md` and `docs/online-safety.md` (the vote message), `docs/testing.md`.

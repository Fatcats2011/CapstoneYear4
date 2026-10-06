# Steam features in the game (Phase 4A)

The game uses three Steam features while it runs: achievements, Rich Presence, and opt-in session logs. The code is in `Assets/Scripts/Steam/`, plus `Assets/Scripts/Online/OnlineAchievements.cs`. Without Steam (the editor with Steam closed, batch-mode tests, a copy launched outside Steam), none of it calls Steamworks.

## Achievements

These are set up in Steamworks, under Stats & Achievements, with these exact API names (Phase 4B):

| API name | Display name | Description |
|---|---|---|
| `FIRST_DELIVERY` | First Delivery | Deliver your first order in a match. |
| `GOLDEN_WIN` | Golden Finish | Deliver the golden order and finish first. |

- Each achievement needs two 256×256 JPG icons, one for unlocked and one for locked.
- The test app (480, Spacewar) has neither achievement. Until the game's own App ID exists, the first unlock of each logs one warning, `Steam: this app has no achievement …`, and nothing unlocks.

**Who earns them** (`MatchFeats`):
- **First Delivery:** any delivery in a match. The tutorial's deliveries don't count.
- **Golden Finish:** the player who delivered the golden order, if nobody scored more by the results. A tie for first counts.

**Who decides** (`SteamFeatures`): the machine that runs the rules, so the PC in a local match, or the host online.
- Deliveries reach it through `FeatSync`, raised by `OrderHandler.DeliverOrder` on that machine only. The results reach it through `GameManager.StateApplied`.
- **Offline:** every seat is this PC's Steam user, so it unlocks there.
- **Online** (`OnlineAchievements`):
  - the host unlocks its own player's achievements;
  - it sends every other player's to every client, with the seat (`OnlineMatch.SendAchievement`);
  - each client unlocks only its own seat's.

## Rich Presence

Every game state sets what friends see in the Steam friends list (`PresenceRules`):

| States | Token | Shows |
|---|---|---|
| Menu, Options, Credits | `#Menus` | In the menus |
| PlayerSelect | `#GettingReady` | Getting ready |
| Tutorial | `#Tutorial` | Learning the ropes |
| StartingCutscene, Begin, MainLoop | `#Delivering`, or `#DeliveringSolo` for one player | Delivering — 3 players / Delivering solo |
| GoldenCutscene, FinalPackage | `#Golden`, or `#GoldenSolo` for one player | Chasing the golden order — 3 players / … solo |
| Results | `#Results` | Counting the takings |

- Loading and Paused change nothing.
- The player count is everyone in the match, so online it includes the other machines' players.
- The English texts are in `docs/steam/rich-presence-english.vdf`. Upload it in Steamworks (Community → Rich Presence localization) when the App ID exists. Until then Steam shows nothing, or the raw token.
- Keep the file and `PresenceRules` in step. A token the file lacks shows nothing.

## Session logs (opt-in)

Session logs help with bug reports: crashes in the game's code, disconnects, and slow loads.

**How a player turns them on:** in Steam's library, right-click Dead on Arrival → Properties → General → Launch Options, and type `-sessionlog`. Delete it to turn them off.

**Where they are:**
- `%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\logs\`
- There's one file per launch, `session-YYYYMMDD-HHMMSS.log`, and the newest 10 are kept.
- Steam Cloud syncs only `settings.cfg`, so the logs stay on the PC.

**What's in them:**
- A header with the version and the OS.
- Every error and exception, with its stack.
- Every `Online:` line:
  - hosting and joining;
  - players joining and leaving;
  - refused players;
  - each session's end and its reason;
  - each match load's time and longest frame (Phase 3I).

Each line is written as it happens, so a crash in the game's code still leaves the lines before it. A native crash can cut the file short; Unity's `Player.log`, in the folder above, covers that.

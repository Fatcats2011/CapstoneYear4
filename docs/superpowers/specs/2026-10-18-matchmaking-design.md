# Matchmaking design

Date: 2026-10-18. Branch: `steam-phase1a` (main stays Steam API only).

## Why

Online play is friends-only today: Online Play hosts a friends-only Steam lobby, and people get in only through an invite (`OnlineLobby`, `SteamLobbyService`). A player who bought the game alone has no way to play online. Matchmaking lets strangers find each other.

"Safely" (the user's word) means: **protect the host and every joined player's machine from a malicious player** (crash, freeze, memory/disk blow-up, IP leak, file access, code execution). Today's protections (`docs/online-safety.md`) assume only friends get in; matchmaking breaks that assumption, so every protection has to hold against strangers.

## Goals

- In Dispatch (online player select) the host can press a button to **Fill**: the lobby becomes findable by strangers, who drop into empty seats.
- Any player in Dispatch can press a button to **Find**: the game searches for another public lobby with free seats on the same build and joins it.
- **No match found:** "Looking for a match…" for a few seconds, then "No open matches right now", and the player's own lobby turns on Fill by itself, so the next searcher lands there. The player stays in Dispatch (can still invite or leave).
- **Auto-start:** in a lobby with Fill on, a countdown (default 60 s, restarting each time someone joins) starts the match with whoever is there, ready or not. If the host is still alone when it ends, nothing starts: it keeps waiting (decided by Claude, user approved "looks good").
- Friend invites (Y) keep working exactly as now, in any lobby, Fill on or off.
- Every new path is covered by safety measures and tests, and `docs/online-safety.md` is updated.

## Non-goals

- Skill-based matching, ranks, regions picked by the player, server browser list UI.
- Dedicated servers. It stays peer to peer through Steam's relay.
- Text or voice chat.
- Anti-cheat inside the rules (still out of scope, as in online-safety.md).
- A host kick button (not asked for; the existing flood kick/ban stays).

## Anti-goals (a failure even if it works)

- Any stranger learning a player's IP address (relay-only must hold).
- Anything from a stranger's lobby tags or session used as a path, deserialised into objects, run, or shown/logged unbounded or with control characters.
- A lobby being public when its host didn't choose Fill or Find (Local Play and friends-only sessions must never show up in search).
- Breaking the friends flow, Local Play, or the existing tests.

## Behaviour

### Fill (host)

- A new Dispatch button toggles Fill. On: the Steam lobby type becomes **public** and the lobby is tagged with a matchmaking tag and its free seat count. Off: back to **friends-only**; nobody already in is removed.
- Free seats = 4 minus seated players (couch players on one machine each take a seat, Phase 3J). The host updates the tag whenever seats change, and Steam's member limit is not the seat count (one machine can bring several players).
- The lobby is findable only while the host is in the menus (`LobbyRules.InTheMenus`, as `SetJoinable` does today) and has a free seat.
- After the match, back in Dispatch, Fill keeps its setting for that session. Leaving the lobby (B) or the session ending ends it. A new Online Play starts friends-only.
- A ticket line or the Dispatch prompt shows Fill's state ("Open to anyone" / "Friends only") and the countdown.

### Find (any player in Dispatch, not mid-join)

- Requests Steam's lobby list filtered by: game tag `doa`, exact build tag (`LobbyRules.BuildTag`), matchmaking tag on, at least as many free seats as this machine's players. Steam's distance filter: default (Claude's call: `Default`, Steam's nearby-first).
- Picks the fullest lobby (fewest free seats that still fit), skipping this machine's own lobby.
- Joining one goes through the existing `OnlineLobby.Answer` path: leave own lobby/session, enter, check tags (`LobbyRules.Refusal`), join owner's session, host's `JoinRules` still apply. If the join fails (lobby full, ended), try the next candidate, at most 3 tries, then fall back to "no match found".
- A player who is alone hosting may Find; a host with others already in their lobby can't (Claude's call: the button does nothing / is hidden for a host with joined players, so nobody is dragged out). A client in someone's lobby can't Find without leaving first.
- Find can be cancelled with B while searching.

### Auto-start

- Runs on the host only, and only while Fill is on and there's at least one other machine seated.
- 60 s countdown (a constant), restarted whenever a new player is seated; shown on every machine (it's part of the host's state, sent like other match state).
- When it ends, the host starts the match through the same path the all-ready start uses (`DispatchRules.EveryoneReady` → 3-2-1 → load), treating everyone as ready.
- If everyone readies before it ends, the normal start happens.

## Safety (the core of "safely")

Existing measures that already cover strangers, to be re-checked as if every peer is hostile: relay-only Steam sessions (`SteamRelay`), lobby-membership gate and one connection per Steam ID (`SteamPeerRules`, `ConnectionGate`), message size limits, `RateGate` flood kick + session ban, `NetChecks` and client-side checks of everything the host sends, 64-byte join payload cap, name cleaning (`PlayerNames.Clean`), version text cleaning (`LobbyRules.Shown`), network-object caps on clients.

New measures:

1. **Lobby list results are untrusted.** Read only the game's own keys. Every tag read from a found lobby is length-capped and parsed strictly (free seats parsed as a small int in 0–4, anything else drops the lobby). Never show or log a found lobby's text except through `LobbyRules.Shown`.
2. **Cap the search.** Ask Steam for a bounded result count (e.g. 50) and look at no more than that. One search at a time; a new Find while one runs is ignored. A minimum gap between searches (e.g. 5 s) so a held button can't hammer Steam.
3. **Public only by choice.** The lobby is created friends-only exactly as today, made public only by Fill (or Find's fallback), and set back to friends-only on Fill off, leaving, or the session ending. A test asserts a lobby that never had Fill stays friends-only.
4. **Join churn on the host.** Track connection attempts per Steam ID in the session; one that connects and drops repeatedly (e.g. more than 5 times in 60 s) is banned for the session using the existing ban (`ConnectionGate.Ban`). The countdown restart is only for a player newly seated, so join/leave spam can't keep the match from ever auto-starting forever beyond the cap.
5. **A hostile host.** Re-audit every client-side check (`OnlineMatch` client RPCs, `OnlineSession.AddPlayer/SetMatch/AddScooter`, `OnlineSteals`, `OnlineAchievements`, the auto-start countdown value: finite, clamped to 0–60 s) for the case where the host is a stranger. Countdown and Fill state sent by the host are validated like other host state.
6. **Names.** Strangers' names show on tickets; they already go through `PlayerNames.Clean`/`Shown`. Confirm nothing else shows a raw Steam name.
7. **Docs and tests.** Update `docs/online-safety.md` (in-scope line "no strangers joining" becomes "strangers join only through a public lobby the host opened"; new rows per measure) and `docs/online.md` (new matchmaking section). Every new measure gets a unit test in `Assets/Tests/Editor`, following `SteamPeerRulesTests`, `LobbyRulesTests`, `RateGateTests`.

## How (decided)

- Built on what exists: Steam lobbies through `ILobbyService` (extend it with set-lobby-type and lobby search, with the fake in tests), the flow in `OnlineLobby` (or a new plain-C# `Matchmaker` class next to it, same style: pure logic, events, tested without Steam), Steam calls only in `SteamLobbyService`.
- Pure rules in a static class (like `LobbyRules`): parsing tags, picking the best lobby, the countdown.
- Dispatch input through `DispatchMenu`/`DispatchAction` (new actions for Fill and Find).

## Calls made after the builder check (Claude's, for the user to check)

- **Find keeps your own lobby until a match is confirmed.** It leaves its own lobby and session only after it has entered the found lobby and the tags pass `LobbyRules.Refusal`. If the session join then fails, it re-hosts a fresh friends-only lobby, which turns Fill on as the no-match fallback. Find is only allowed for a host who is alone, so no joined friend is stranded. A pending invite to the old lobby can go stale, which is accepted. The Find path calls the join directly instead of through `Answer`'s `Busy` check.
- **Two solo searchers meet.** While a Fill-on host is alone, it searches again every ~15 s. If it finds another lobby, it joins only if its own Steam ID is the higher one, so the two never both leave.
- **Seat races.** No seat reservation. The host's `JoinRules` refusal ("no free seat") counts as "try the next candidate", while other failures count against the 3 tries.
- **New replicated state** (Fill on, countdown end time) changes the Netcode setup hash and so the build tag. Every test build must be rebuilt together. This is fine because the matchmaking build isn't live yet.
- **Buttons:** the builder checks `MenuInteractions`/`PlayerSelectCanvas` for any existing use of the shoulder buttons before picking them, and extends `DispatchButton`.

## Left to the builder

- Which buttons. Suggestion (Claude's call): left shoulder = Find, right shoulder = Fill; keyboard keys to match the `docs/controls.md` pattern. Update `docs/controls.md`.
- Exact wording of prompts (keep the existing tone, e.g. "LOOKING FOR DRIVERS…").
- Whether Find lives in `OnlineLobby` or a new class.
- Exact constants (search gap, result cap, churn limit) within the ranges above.
- How the countdown shows on the corkboard.

## Constraints

- Unity 2022.3.62f3, Netcode for GameObjects 1.15.1, Steamworks.NET, the patched SteamNetworkingSockets transport.
- Steam's test app (480) is shared by every game using it: the `doa` game tag and build tag filters are what keep other games' lobbies out. Search results on 480 will include junk; that's the case the safety checks are for.
- The working tree has a large uncommitted set of changes on `steam-phase1a`; implementation must not mix with them unknowingly (commit or confirm with the user first).
- Editor tests must keep passing; new logic is tested as plain C#.
- Testing for real needs two Steam accounts on two machines (or ParrelSync can't do Steam); the plan should list the manual check steps as short bullets.

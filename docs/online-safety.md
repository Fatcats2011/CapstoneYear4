# Online safety (Phase 4C)

Online matches are peer to peer: one player's game hosts, and their friends' games connect to it. A player could run a modified game that sends anything the network lets through. This page lists what such a game could try, and what stops it.

**In scope:** the safety of every player's machine and session: no crash, freeze, memory or disk blow-up, IP leak or file access, and strangers join only through a lobby its host opened to anyone (Fill).

**Out of scope:** cheating inside the rules. Each player's game drives its own scooter (Phase 3C), so a modified game could still teleport or speed up its own scooter. That needs host-side movement checks: a later anti-cheat phase.

## What an attacker could try, and what stops it

| Threat | Measure | Code |
|---|---|---|
| A stranger who knows the host's Steam ID connects directly, not through the lobby | The Steam transport asks the game before accepting a connection; the host accepts only Steam users in its lobby right now. Someone not in the lobby yet gets 3 s for the host's copy of the lobby to catch up, then is closed before Netcode sees them. A Steam ID that's already connected can't open a second connection (and take a second seat). | `SteamPeerRules`, `OnlineLobby.LetsIn`, `OnlineSession.AcceptsPeer`, the patched transport's `ConnectionGate` |
| Learning another player's IP address | Steam sessions are relay-only: direct (ICE) connections are off, so every connection goes through Valve's relay. | `SteamRelay.Options()` |
| An empty or huge message crashing the receiver | The patched transport drops messages of 1 byte or less, or over 256 KB, and always releases Steam's message. It used to throw and leak on an empty one. | the patched transport, `SteamPeerRules.Deliverable` |
| Flooding the host with requests (one-shots, drops, steals, bumps…) | Each machine has an allowance per kind of request (a burst, refilled each second). The excess is dropped; 200 drops within 10 s disconnects that machine ("Disconnected: too many messages."). The disconnect closes with linger on, so the reason reaches them first. | `RateGate`, `OnlineMatch.Allowed`, `OnlineSession.Kick` |
| A kicked player reconnecting to flood again | Over Steam the kicked Steam ID is banned for the rest of the session: the lobby has no kick, so they stay a member, but every new connection from them is refused. A new hosting session starts with no bans. | the patched transport's `BanOnNextDisconnect`, `ConnectionGate.Ban` |
| A lobby open to strangers by accident | A lobby is made friends-only. Only `SetFill(true)` (Fill, or Find's fallback) makes it public. A host's open lobby is set back to friends only before it's left (Steam may hand it to a member who stays). Turning Fill off stops any search, so Find's fallback can't open the lobby again and no switch carries the host into a stranger's; a Fill host's quiet search again never opens its lobby, and one that had to leave it and found nothing hosts it again open, as the host chose (Fill off would have stopped the search). B between sessions hosts the player's lobby again only as it was before the switch: friends only stays friends only. A client can't open one. | `OnlineLobby.SetFill`, `OnlineLobby.LeaveLobby`, `Matchmaker` |
| A stranger connecting and dropping over and over | refused for the session after 5 connections in 60 s | `JoinChurn`, `OnlineLobby.LetsIn` |
| A stranger joining and leaving to put the automatic start off for ever | The count restarts only when a machine sits down for the first time in the hosted session (by Steam ID), not when it asks for or gives back a seat, or leaves and comes back. Once the host has made everyone ready, a seat such a machine takes then sits down ready, so it can't stop the countdown either. | `AutoStartClock`, `OnlineSession.MachineOf`, `OnlineGame.SeatWaitingPlayers` |
| A found lobby lying in its tags (another game, a huge or nonsense seat count) | Lobby search results are untrusted. Only the game's own keys are read, from the first 50 lobbies whatever Steam sends. A tag over 64 characters, or a seat count that isn't a plain number 0–4, drops the lobby. A found lobby's text is never shown or logged. Its tags are checked again once entered. | `MatchRules.Candidates`, `MatchRules.ParseOpenSeats`, `Matchmaker`, `OnlineLobby.Switch` |
| Find hammering Steam's lobby search | One search at a time, at least 5 s apart, for at most 50 lobbies; one Steam doesn't answer within 10 s counts as none found. At most 3 lobbies that fail are tried, and no more than 10 in all (hosts turning it away for want of a seat included). A found lobby Steam doesn't answer within 10 s is one that failed. | `MatchRules.SEARCH_GAP`, `MAX_RESULTS`, `SEARCH_TIMEOUT`, `MAX_TRIES`, `MAX_ATTEMPTS` |
| Lobby squatters (members who never connect) filling the lobby | The Steam member cap is 16, above the 4 seats, so members who never connect can't fill it, hide it from search or block invites. Seats are checked when a machine connects (`JoinRules`) and published in the `open` tag, so a full table reads 0 and searchers skip it. Residual risk: a squatter stays a lobby member until they leave, and the host can't kick them. | `MatchRules.LOBBY_MEMBERS`, `JoinRules.Refusal`, `MatchRules.Candidates` |
| A malicious host sending a nonsense automatic-start time (NaN, infinity, far future or past) | The time is read only through a clamp: 0–60 s, and nonsense counts as no time left. Only a start this machine saw counting down readies its players, so nonsense never does. | `MatchRules.AutoStartRemaining`, `OnlineGame.AutoStart` |
| A malicious host ending the session with a huge or forged reason | Its reason is shown and logged without control characters, cut to 160 characters. The hint bar shows it as plain text: tags in it (a huge size, say) show as written. | `OnlineSession.EndReason`, `MAX_REASON`, `ControllerPrompts` |
| A malicious host holding a client's scene change for good while it keeps sending | At most 1024 of its messages wait; the rest are dropped, with one warning until the wait drains. | `HostQueue.MAX_WAITING` |
| Another machine's name with control characters (a modified game skips the cleaning) | Names are cleaned again where they're shown, and tickets show them as plain text, not markup. | `PlayerNames.Shown`, `TicketView` |
| Bumps that push another player across the map | The host passes on a bump only for the sender's own seat, between players within reach, neither respawning, one per pair per half second, and never harder than a clash, whatever speed was sent. Clients take only seats 0–3 and a finite push. | `BumpRules`, `OnlineBumps`, `OnlineMatch.BumpClientRpc` |
| A scooter pose that isn't numbers (NaN, infinity), making Unity log an error every frame | Another machine's scooter proxy stops following poses that aren't numbers, and holds its last good one until a second of good poses has come. | `PoseHold`, `OwnerNetworkTransform` |
| A huge or tricky join request | Join data over 64 bytes is refused unread. Version text shown or logged is cut to 32 characters, without control characters, so it can't forge log lines. | `OnlineSession.MAX_PAYLOAD`, `LobbyRules.Shown` |
| Nonsense values (NaN or far-away places, undefined states, scenes, cues or achievements, a clock that never ends) | The host refuses requests with places outside the world (±5000 m). Clients check everything the host sends where it arrives, and skip what no honest game sends. | `NetChecks`, `OnlineMatch`'s client messages, `OnlineOrders.AcceptsDrop`, `MatchClock.Remaining` |
| A malicious host spawning extra objects | A client takes one match, at most 4 players and scooters, and no two players in one seat. Anything more is ignored, with one warning. | `OnlineSession.AddPlayer`, `SetMatch`, `AddScooter` |
| A malicious host locking a player in bounces | A client shows a pair's hits no closer together than half the host's steal cooldown. | `OnlineSteals.Show` |
| A malicious host faking other players' achievements | A client unlocks only its own seat's, and only the game's own achievements. | `OnlineAchievements`, `AchievementNames` |
| Filling the disk through the opt-in session log | The log stops at 5 MB. | `SessionLog.MAX_BYTES` |
| Reaching a player's files or running code | Nothing that arrives over the network is used as a path, deserialised into objects, or run. The audit found no such use. | n/a |
| A release build listening on an IP address | Direct (IP address) sessions exist only in the editor and development builds. | `#if UNITY_EDITOR \|\| DEVELOPMENT_BUILD` in `OnlineSession`, `OnlinePlay` |

Known limit: a forged *teleport* pose isn't held back. Netcode applies a teleport the moment it arrives, before any hook the game can override (its state is internal to the package), so `PoseHold` can't skip it. Unity refuses a position that isn't numbers, so nothing moves: the cost is one error line per message, within the per-machine message limits above.

Netcode's own settings (`OnlineSession.NewConfig`):
- Network-variable lengths are checked as they're read.
- A connection that isn't let in within 10 s is dropped (5 s until Phase 5A: a host hitch of a few seconds in the menus could drop a joining friend).
- Netcode's logs are off in release builds.

## Already in place before this phase

- The host checks that every request comes from the machine whose seat it names.
- Seats and indexes are range-checked.
- Only the host spawns, and only the game's three network prefabs.
- Lobbies are friends-only unless the host turns Fill on.
- Scene management is off.

## The transport copy

`Packages/com.community.netcode.transport.steamnetworkingsockets` is an embedded copy of the community SteamNetworkingSockets transport (MIT, commit `d862504b`), patched for the gate, message sizes and message release. `CHANGES-DOA.md` in that folder lists each change. To update it, copy the new upstream package over the folder, then re-apply the changes marked `// DoA`.

## The Unity runtime advisory (CVE-2025-59489)

Unity disclosed a flaw in how the Unity player handles command-line arguments in October 2025. Per Unity's advisory, it's fixed in 2022.3.62f2 and later, and the project is on 2022.3.62f3. Ship only builds made with a patched editor, and rebuild any build made before.

## Tests

- `SteamPeerRulesTests`: lobby membership, message sizes and the relay options. `ConnectionGateTests`: accept, wait, refuse, and one connection per Steam ID.
- `RateGateTests` and `MessageLimitsNetworkTests`: allowances, and a flood capped then disconnected.
- `NetChecksTests` and `NonsenseNetworkTests`: a client ignores nonsense from the host.
- The `OnlineSessionTests` approval and end-reason tests, the `LobbyRulesTests` version-text test, the `PlayerNamesTests` shown-name tests, the `HostQueueTests` cap test and the `SessionLogTests` size test.
- `MatchRulesTests`: tag parsing, the candidates (garbage and the result cap), the search gap and the automatic-start clamp. `MatchmakerTests`: Find over a fake Steam, a huge or repeated answer included, the `MAX_ATTEMPTS` cap, a found lobby that never answers, and Fill turned off stopping a search (the player's or a quiet one) without the lobby opening again; B between sessions hosting the lobby again only as it was. `OnlinePlayersNetworkTests.AutoStart_SeatAfterTheForce_JoinsTheStart`: a seat taken after the host's force joins the start.
- `JoinChurnTests` and the `OnlineLobbyTests` churn and friends-only tests: reconnects refused, a lobby is public only through Fill, and an open lobby goes back to friends only as it's left.
- `AutoStartClockTests`: a machine's seat moves, or its leaving and coming back, don't put the automatic start off.
- `ControllerPromptsTests.Hint_ShowsTagsAsWritten`: the hint bar shows tags as text.

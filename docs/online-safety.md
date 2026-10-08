# Online safety (Phase 4C)

Online matches are peer to peer: one player's game hosts, and their friends' games connect to it. A player could run a modified game that sends anything the network lets through. This page lists what such a game could try, and what stops it.

**In scope:** the safety of every player's machine and session: no crash, freeze, memory or disk blow-up, IP leak or file access, and no strangers joining.

**Out of scope:** cheating inside the rules. Each player's game drives its own scooter (Phase 3C), so a modified game could still teleport or speed up its own scooter. That needs host-side movement checks: a later anti-cheat phase.

## What an attacker could try, and what stops it

| Threat | Measure | Code |
|---|---|---|
| A stranger who knows the host's Steam ID connects directly, not through the lobby | The Steam transport asks the game before accepting a connection; the host accepts only Steam users in its lobby right now. Someone not in the lobby yet gets 3 s for the host's copy of the lobby to catch up, then is closed before Netcode sees them. A Steam ID that's already connected can't open a second connection (and take a second seat). | `SteamPeerRules`, `OnlineLobby.LetsIn`, `OnlineSession.AcceptsPeer`, the patched transport's `ConnectionGate` |
| Learning another player's IP address | Steam sessions are relay-only: direct (ICE) connections are off, so every connection goes through Valve's relay. | `SteamRelay.Options()` |
| An empty or huge message crashing the receiver | The patched transport drops messages of 1 byte or less, or over 256 KB, and always releases Steam's message. It used to throw and leak on an empty one. | the patched transport, `SteamPeerRules.Deliverable` |
| Flooding the host with requests (one-shots, drops, steals, bumps…) | Each machine has an allowance per kind of request (a burst, refilled each second). The excess is dropped; 200 drops within 10 s disconnects that machine ("Disconnected: too many messages."). The disconnect closes with linger on, so the reason reaches them first. | `RateGate`, `OnlineMatch.Allowed`, `OnlineSession.Kick` |
| A kicked player reconnecting to flood again | Over Steam the kicked Steam ID is banned for the rest of the session: the lobby has no kick, so they stay a member, but every new connection from them is refused. A new hosting session starts with no bans. | the patched transport's `BanOnNextDisconnect`, `ConnectionGate.Ban` |
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
- Lobbies are friends-only.
- Scene management is off.

## The transport copy

`Packages/com.community.netcode.transport.steamnetworkingsockets` is an embedded copy of the community SteamNetworkingSockets transport (MIT, commit `d862504b`), patched for the gate, message sizes and message release. `CHANGES-DOA.md` in that folder lists each change. To update it, copy the new upstream package over the folder, then re-apply the changes marked `// DoA`.

## The Unity runtime advisory (CVE-2025-59489)

Unity disclosed a flaw in how the Unity player handles command-line arguments in October 2025. Per Unity's advisory, it's fixed in 2022.3.62f2 and later, and the project is on 2022.3.62f3. Ship only builds made with a patched editor, and rebuild any build made before.

## Tests

- `SteamPeerRulesTests`: lobby membership, message sizes and the relay options. `ConnectionGateTests`: accept, wait, refuse, and one connection per Steam ID.
- `RateGateTests` and `MessageLimitsNetworkTests`: allowances, and a flood capped then disconnected.
- `NetChecksTests` and `NonsenseNetworkTests`: a client ignores nonsense from the host.
- The `OnlineSessionTests` approval tests, the `LobbyRulesTests` version-text test and the `SessionLogTests` size test.

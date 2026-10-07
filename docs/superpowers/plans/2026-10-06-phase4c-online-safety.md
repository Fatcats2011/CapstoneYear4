# Phase 4C — Online Safety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** a player online is safe from another player's modified game.
- Only the friends in the lobby can connect.
- Nobody's IP address is shown to the others.
- No message can crash, freeze or flood another machine, or reach its files.
- Values that make no sense are refused on every machine.

**Architecture:**
- **Who connects:** the Steam transport is copied into the project (`Packages/`) so it can be patched.
  - Before it accepts a connection, it asks the session (`OnlineSession.AcceptsPeer`), which on a Steam host means "is this Steam ID in my lobby?" (`ILobbyService.IsMember`). Everyone else is closed before Netcode sees them.
  - The patched transport also drops empty and oversized messages, and always releases Steam's message.
- **Hidden IPs:** Steam sessions pass relay-only options to the transport (`SteamRelay.Options()`), so every connection goes through Valve's relay and no peer learns another's IP.
- **Message limits:** every client-to-host RPC goes through a per-sender rate gate (`RateGate`). Excess messages are dropped, and a sender that keeps flooding is disconnected. The approval payload is capped and never echoed.
- **Value checks:** a small `NetChecks` class: finite vectors inside the world's bounds, defined enum values, and a sane clock.
  - The host applies them to client requests; clients apply them to what the host sends.
  - Clients also refuse a second match object, more than 4 players or scooters, and duplicate seats.
- **Release builds:** the direct-IP transport (editor and tests only) is compiled out. Netcode's network-variable length checks are on, its pending-connection timeout is 5 s, and its own logs are off. The opt-in session log stops at 5 MB.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 · Steamworks.NET 2025.164.1 · the community SteamNetworkingSockets transport (commit `d862504b`, embedded in this phase) · Unity Test Framework 1.1.33.

**Spec:**
- The user, 2026-10-06: "since the game uses peer-peer connections this opens security concerns for hacking/ pack injections on user computers. I want measures to ensure a safe experience".
- The audits behind this plan (two read-only Sonnet agents, then spot-checked):
  - **Transport:**
    - It accepts every connection ("blindly accept", `SteamNetworkingSocketsTransport.cs:138-160`).
    - `new byte[data.m_cbSize - 1]` throws on an empty message and leaks it (`:250`).
    - Its `options` are empty, so direct (ICE) connections, which reveal IPs, are allowed.
  - **Messages:**
    - There's no rate limit on any `ServerRpc`.
    - A flood of rise cues makes every machine instantiate a gravestone per cue (`Respawn.ShowRise`).
    - Vectors aren't checked (`DropServerRpc`, `RespawnServerRpc`, `OrderChange`, `ScooterPose`).
    - `MatchClock.Remaining` passes NaN through.
    - Enums from the host aren't range-checked (`GameState`, `MatchScene`, `Achievement`).
    - Clients accept any number of spawned network objects.
  - **Already good:**
    - Every `ServerRpc` handler checks that the sender owns the seat it names.
    - Seats and indexes are range-checked.
    - Only the host spawns, and there are only three network prefabs.
    - The lobby is friends-only.
    - There's no unsafe deserialiser, file path or process launch anywhere on network data.

## Global Constraints

- **Stay on Unity 2022.3 LTS** and Netcode for GameObjects **1.x**. Steamworks.NET stays **2025.164.1**.
- **The transport copy:**
  - Copy the package from `Library/PackageCache/com.community.netcode.transport.steamnetworkingsockets@d862504b14/` into `Packages/com.community.netcode.transport.steamnetworkingsockets/`.
  - Remove its line from `Packages/manifest.json`, and its entry from `Packages/packages-lock.json`.
  - Keep its `LICENSE` and `package.json`, and add `CHANGES-DOA.md` listing every change.
  - It's MIT-licensed; add a line to `LICENSES.md`.
- **Never edit a scene, prefab, `.inputactions` asset or ProjectSettings file.** Everything is code, plus the package copy.
- **`SteamManager.cs` and `SteamStartup.cs` stay untouched.**
- **Local play must behave exactly as before.** `LocalMatchSmokeTest` and every online test pass after every task.
- **Branch:** `steam-phase1a`. **No git commits:** the user commits.
- **Meta files:** run `bash tools/newmeta.sh` for every new file under `Assets/`. The package copy under `Packages/` keeps its own `.meta` files.
- **Line endings:** check with `git ls-files --eol`. Edit `i/crlf` files with the Edit tool only.
- **Tests:**
  - Use `bash tools/run-tests.sh [filter]`. Run long runs in the background, and only one test run at a time, since there's one mirror.
  - Play Mode rules: recorder classes; wait by real time; `EnterPlayMode` yielded by the test itself; `log.MachinesLeave()` before a machine leaves.
  - Network test ports in use run to 7810. This phase uses **7811** and **7812**.
- **Subagents:** the user allows Sonnet agents "as you see fit". Independent tasks may go to a Sonnet agent, but never two agents running tests at once.

## Rulings (decided while planning)

1. **"Safe" means the machine and the session, not fair play.**
   - In scope: crashes, freezes, memory and disk blow-ups, IP leaks, strangers joining, and values no honest game sends.
   - Out of scope: cheating inside the rules (a client teleporting its own scooter, or driving faster). Scooter physics are client-owned by design (Phase 3C).
   - Only values no honest game could send are refused: non-finite numbers, positions outside the world's bounds, and undefined enum values.
   - Cost if wrong: a later anti-cheat phase (host-side speed checks).
2. **The world's bounds** are a box of ±5000 m on each axis (`NetChecks.WORLD_EXTENT`). The maps are a few hundred metres across, so a real game never comes near it.
3. **Lobby membership is the only key.**
   - A Steam host accepts a connection only from a Steam ID that's in its lobby right now. Friends join the lobby first, then connect, which is the existing flow.
   - Direct (Unity Transport) sessions skip the check: they exist only in the editor and tests (Ruling 7).
4. **Rate limits** are per sender, per RPC, as a token bucket (`RateGate`):

   | RPC | Burst | Refill per second |
   |---|---|---|
   | `CueServerRpc` | 20 | 10 |
   | `DropServerRpc`, `StealServerRpc`, `RespawnServerRpc` | 10 | 5 |
   | `LearntServerRpc`, `CutoutServerRpc`, `LoadedServerRpc` | 5 | 1 |

   - An honest game sends far below these.
   - Excess messages are dropped silently.
   - A sender with 200 drops within 10 s is disconnected, with the reason "Disconnected: too many messages." That's a modified game, not lag.
   - Cost if wrong: an honest burst loses a cosmetic cue.
5. **Relay-only:** `k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable` is set to `k_nSteamNetworkingConfig_P2P_Transport_ICE_Enable_Disable`.
   - Every connection goes through Valve's relay (SDR), which adds a few milliseconds.
   - Cost if wrong: a little latency. The two-PC check confirms the connection still works.
6. **Messages in the patched transport:**
   - A message of 1 byte or less (only the channel byte, or nothing) is dropped.
   - So is one over 256 KB. Netcode's own messages are far smaller.
   - Steam's message is released whatever happens.
7. **The direct transport isn't in release builds.**
   - `OnlineSession.HostDirect`, `JoinDirect` and `OnlinePlay.UseDirect` are wrapped in `#if UNITY_EDITOR || DEVELOPMENT_BUILD`. Tests and the editor menu still have them.
   - The `UnityTransport` component is still added, so `Direct` stays non-null for code that reads it, but nothing in a release build starts it.
8. **The approval payload:**
   - Over 64 bytes, the joiner is refused with "That game isn't Dead on Arrival.", without reading it.
   - The host's log line and the refusal never contain the joiner's text, only the reason.
   - The lobby's version, shown to a joiner in a hint (`LobbyRules.OtherBuild`), is cut to 32 characters with control characters removed.
9. **On clients:**
   - A second `OnlineMatch` is ignored.
   - So are more than `Constants.MAX_PLAYERS` players or scooters, and a duplicate seat. A warning is logged once.
   - Host-sent hits (bounces) to this machine's player get the same pair cooldown the host uses (`StealRules`), so a malicious host can't lock a client in bounces.
10. **The Unity runtime advisory (CVE-2025-59489)** is fixed in 2022.3.62f2 and later, per Unity's advisory, and the project is on 62f3.
    - There's no code change.
    - `EDITOR-TODO.md` gets a check: confirm the editor version in Unity Hub, and only ship builds made with it.
11. **The session log** stops at 5 MB, with a final line: "Session log: 5 MB reached, logging stopped."

## Review Focus

- **A stranger who knows the host's Steam ID connects directly** (not through the lobby) → closed before Netcode sees them, and logged once. (Task 1 `SteamPeerRules_OnlyLobbyMembers`; the two-PC check.)
- **An approved friend floods rise cues** → each machine shows at most the rate gate's worth, and the sender is disconnected. (Task 2 `Hosting_AFloodOfCues_IsCapped_AndTheSenderLeaves`.)
- **A drop request at NaN, or at 1e30** → refused; nothing moves. (Task 3 `Hosting_ADropAtNoPlace_IsRefused`.)
- **A malicious host sends an undefined game state, a NaN clock or a second match** → the client ignores each, with no exception loop. (Task 3 `Joining_NonsenseFromTheHost_IsIgnored`.)
- **A 500 KB approval payload** → refused without being read or echoed. (Task 2 `Approve_AHugePayload_IsRefusedUnread`.)

---

### Task 1: Who connects, and the patched transport

**Files:**
- Create:
  - `Packages/com.community.netcode.transport.steamnetworkingsockets/` (copied, then patched), with `CHANGES-DOA.md`;
  - `Assets/Scripts/Online/SteamPeerRules.cs`, `Assets/Scripts/Online/SteamRelay.cs`.
- Modify:
  - `Packages/manifest.json`, `Packages/packages-lock.json`, `LICENSES.md`;
  - `Assets/Scripts/Online/ILobbyService.cs` and `SteamLobbyService.cs` (`IsMember`), `OnlineFakes.cs` (the fake's `IsMember`);
  - `Assets/Scripts/Online/OnlineSession.cs` (`AcceptsPeer`; relay options in `UseSteam`), `OnlineLobby.cs` or `OnlinePlay.cs` (setting the gate while hosting a lobby).
- Test: `Assets/Tests/Editor/SteamPeerRulesTests.cs`, plus `OnlineLobbyTests` (one test).

**Interfaces:**
- Produces:
  - `public static class SteamPeerRules`:
    - `public static bool Accepts(ulong steamId, ulong lobby, ILobbyService lobbies)`: false for 0, a null `lobbies`, or a `lobby` of 0; otherwise `lobbies.IsMember(lobby, steamId)`.
    - `public static bool Deliverable(int size)`: `size > 1 && size <= MAX_MESSAGE`.
    - `public const int MAX_MESSAGE = 256 * 1024;`
  - `ILobbyService.bool IsMember(ulong lobby, ulong steamId)`. Steam's implementation loops over `SteamMatchmaking.GetNumLobbyMembers` / `GetLobbyMemberByIndex`; without Steam it's false. The fake has a settable member list.
  - `OnlineSession.Func<ulong, bool> AcceptsPeer { get; set; }`. It's null by default, which accepts everyone. `OnlineLobby` sets it while hosting a Steam lobby to `id => SteamPeerRules.Accepts(id, lobby, lobbies)`, and clears it when the lobby closes.
  - `public static class SteamRelay { public static SteamNetworkingConfigValue_t[] Options(); }`: one entry, ICE disabled (Ruling 5). It's under `#if !DISABLESTEAMWORKS`, like `SteamLobbyService`.
- **The transport patch** (`SteamNetworkingSocketsTransport.cs`):
  - It gains `public Func<ulong, bool> AcceptPeer;`. In the `k_ESteamNetworkingConnectionState_Connecting` branch, when `AcceptPeer` is set and returns false for `param.m_info.m_identityRemote.GetSteamID64()`, it calls `CloseConnection(conn, 0, "Not in this lobby", false)` and logs once per Steam ID: `"Online: refused a connection from someone outside the lobby"`. It never logs the Steam ID.
  - `PollEvent` reads the message size first. When `!SteamPeerRules.Deliverable(size)` it releases and skips the message. Copy that one pure check into the package as a private method: a package can't reference `Assets/`. The test pins the rule in `SteamPeerRules`, and `CHANGES-DOA.md` says the two must match.
  - The message is released in a `finally`.
  - `OnlineSession.UseSteam` sets `transport.AcceptPeer = AcceptsPeer` (through a lambda, so a later change applies) and `transport.options = SteamRelay.Options()`.

- [ ] **Step 1: Write the failing tests.**

  `SteamPeerRulesTests`:
  - `SteamPeerRules_OnlyLobbyMembers`: with the fake's members `{111, 222}`, lobby 9 accepts 222 and refuses 333.
  - `SteamPeerRules_NoLobbyOrNoSteam_RefusesEveryone`: a lobby of 0, a null service, or a Steam ID of 0 refuses.
  - `Deliverable_DropsEmptyAndHugeMessages`: 0 and 1 are false; 2 is true; `MAX_MESSAGE` is true; `MAX_MESSAGE + 1` is false.
  - `SteamRelay_TurnsOffDirectConnections`: one option, `m_eValue == k_ESteamNetworkingConfig_P2P_Transport_ICE_Enable`, int value 0.
  - `SteamLobbyService_WithoutSteam_HasNoMembers`.

  `OnlineLobbyTests`: `Hosting_ASteamLobby_LetsOnlyItsMembersConnect`. Hosting through the fakes sets `session.AcceptsPeer`; a member returns true and a stranger false. Leaving clears it.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`).

- [ ] **Step 3: Implement.** Copy, then patch, the package. Add the license line and `CHANGES-DOA.md`.

- [ ] **Step 4: Run the tests.** Run `bash tools/run-tests.sh "SteamPeerRulesTests|OnlineLobbyTests|OnlineSessionTests|OnlinePlayNetworkTests"`; expect all to pass. A package compile error also prints `NO RESULTS`.

### Task 2: Message limits

**Files:**
- Create: `Assets/Scripts/Online/RateGate.cs`
- Modify:
  - `Assets/Scripts/Online/OnlineMatch.cs`: every `ServerRpc` body starts with `if (!Allowed(rpc.Receive.SenderClientId, RpcKind.X)) return;`.
  - `Assets/Scripts/Online/OnlineSession.cs`: approval payload cap and the log line; `Kick(ulong clientId, string reason)`.
  - `Assets/Scripts/Online/LobbyRules.cs`: `OtherBuild` cuts the version.
  - `Assets/Scripts/Steam/SessionLogWriter.cs`: the 5 MB cap.
- Test: `Assets/Tests/Editor/RateGateTests.cs`, `Assets/Tests/Editor/MessageLimitsNetworkTests.cs` (port **7811**), plus `LobbyRulesTests`, `OnlineSessionTests` and `SessionLogTests` (one test each).

**Interfaces:**
- Produces:
  - `public enum RpcKind { Cue, Drop, Steal, Respawn, Learnt, Cutout, Loaded }`
  - `public class RateGate`, with these members:
    - `public RateGate(Func<double> clock)`
    - `public bool Allow(ulong sender, RpcKind kind)`, with Ruling 4's buckets.
    - `public bool ShouldKick(ulong sender)`: 200 drops within 10 s.
    - `public void Forget(ulong sender)`
  - `OnlineMatch`:
    - holds one `RateGate` on the host, made in `OnNetworkSpawn` with `Time.realtimeSinceStartupAsDouble`;
    - `Allowed` calls `session.Kick(sender, KICK_REASON)` once `ShouldKick`;
    - `public const string KICK_REASON = "Disconnected: too many messages.";`
    - a client that leaves is `Forget`-ten.
  - `OnlineSession`:
    - `public const int MAX_PAYLOAD = 64;`
    - `Approve` refuses longer payloads with `JoinRules.NOT_THIS_GAME`, the existing "That game isn't Dead on Arrival." text; add the constant if it's missing;
    - the log line is `"Online: turned a player away: " + reasonOnly`.
  - `LobbyRules`: `public const int SHOWN_VERSION = 32;` and `static string Shown(string version)`, which strips control characters and cuts to 32. `OtherBuild` and the `JoinRules` wording use it.
  - `SessionLogWriter`: `public const long MAX_BYTES = 5 * 1024 * 1024;`. The pure check is `SessionLog.Full(long written)`.

- [ ] **Step 1: Write the failing tests.**

  `RateGateTests` (EditMode, with a fake clock):
  - `Allow_UpToTheBurst_ThenRefills`: 20 cues are allowed and the 21st refused; 0.1 s later, one more is allowed.
  - `Allow_EachSenderAndKindHasItsOwnBucket`.
  - `ShouldKick_After200DropsIn10Seconds_NotBefore`.
  - `Forget_StartsTheSenderAfresh`.

  `MessageLimitsNetworkTests` (empty scene, a direct host and client, as `CueMessagesNetworkTests` does):
  - `Hosting_AFloodOfCues_IsCapped_AndTheSenderLeaves`:
    - the client calls `ReportCue(1, Of(Boost))` 500 times in one frame;
    - the host's `CueReported` recorder hears ≤ 20;
    - within 5 s the client's session ends;
    - `log.Problems` is empty, after `log.MachinesLeave()`.
  - `Hosting_HonestCues_AllArrive`: 8 cues spaced 0.2 s apart all arrive.

  Others:
  - `OnlineSessionTests.Approve_AHugePayload_IsRefusedUnread`: a 500 000-byte payload is refused with the not-this-game reason, and the reason doesn't contain the payload.
  - `LobbyRulesTests.OtherBuild_ShowsAtMost32Characters_WithoutControlCharacters`.
  - `SessionLogTests.Full_At5MB`.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`).

- [ ] **Step 3: Implement.**

- [ ] **Step 4: Run the tests.**

  Run: `bash tools/run-tests.sh "RateGateTests|MessageLimitsNetworkTests|OnlineSessionTests|LobbyRulesTests|SessionLogTests|CueMessagesNetworkTests|OrderMessagesNetworkTests|RespawnMessagesNetworkTests|TutorialMessagesNetworkTests"`

  Expected: all pass.

### Task 3: Value checks on every machine

**Files:**
- Create: `Assets/Scripts/Online/NetChecks.cs`
- Modify:
  - **host side:** `OnlineOrders.cs` (`DropFor`), `OnlineRespawns.cs` (the respawn request);
  - **client side:** `OnlineOrders.cs` (`Show`, for an `OrderChange`'s spots and heights), `OnlineDriving.cs` (`Show`: the pose), `MatchClock.cs` (`Remaining`), `OnlineGame.cs` (`FollowHost` state, load and show scenes), `OnlineAchievements.cs`, `OnlineCues.cs` (`ShowClock`, `Show`), `OnlineSteals.cs` (the hit cooldown), `OnlineSession.cs` (`SetMatch`, `AddPlayer`, the scooter count);
  - `SteamAchievementStore.cs` (undefined values).
- Test: `Assets/Tests/Editor/NetChecksTests.cs`; `Assets/Tests/Editor/NonsenseNetworkTests.cs` (port **7812**); `MatchClockTests` (one test).

**Interfaces:**
- Produces: `public static class NetChecks`, with these members:
  - `public const float WORLD_EXTENT = 5000f;`
  - `public static bool Finite(float f)`
  - `public static bool InWorld(Vector3 v)`: each axis finite and `|x| <= WORLD_EXTENT`.
  - `public static bool Defined<T>(T value) where T : Enum`: `Enum.IsDefined`.
  - `public static bool Sane(ScooterPose pose)`: positions in the world, and the rotation finite with a length near 1. `OnlineDriving.Show` normalises the rotation and resets the model's `localScale` to `Vector3.one` after applying.
- **Uses:**
  - **Host:** a drop or respawn request with a spot outside the world is refused.
  - **Clients:**
    - an `OrderChange` with a spot or height outside the world is skipped;
    - a pose that isn't sane is skipped (the last good one stays);
    - `MatchClock.Remaining` returns 0 for a non-finite end, and at most 3600;
    - an undefined `GameState`, `MatchScene`, `ClockCue`, `CueKind` or `Achievement` is skipped. `AchievementNames.ApiName` returns null for an undefined value, and the store skips null.
  - **Spawns** (Ruling 9): a second `OnlineMatch` is ignored; more than `Constants.MAX_PLAYERS` players or scooters, or a duplicate seat, is ignored, with one warning: `"Online: the host sent more than the game allows; ignored."`.
  - **Hits:** a host-sent hit to this machine's player within `StealRules.PAIR_COOLDOWN` of the last one from the same pair is skipped. Use the constant `StealRules` already has; if its name differs, use what's there and ledger it.

- [ ] **Step 1: Write the failing tests.**

  `NetChecksTests`:
  - `InWorld_RefusesNaNInfinityAndFarAway`;
  - `Defined_RefusesUndefinedEnums`;
  - `Sane_RefusesABadRotation`.

  `MatchClockTests.Remaining_NonsenseEnds_AreZeroOrCapped`: NaN gives 0; +∞ gives 3600; a negative remainder gives 0.

  `NonsenseNetworkTests` (empty scene, direct sessions; the client side is wired as `OnlineGameNetworkTests` does):
  - `Hosting_ADropAtNoPlace_IsRefused`: `AskDrop(1, NaN vector, (1e30,0,0), false)` from the client → the host's `DropAsked` handler (via `OnlineOrders`) makes no `OrderChange.Drop`, checked with an order-change recorder over 1 s.
  - `Joining_NonsenseFromTheHost_IsIgnored`:
    - the host sends `SendState((GameState)99)`, `ShareClock(double.NaN, true, false)` and `SendAchievement(1, (Achievement)12345)`;
    - the client's `GameManager.MainState` is unchanged, its clock shows 0, and a fake store unlocked nothing;
    - `log.Problems` is empty.

- [ ] **Step 2: Run them, and see them fail.**

- [ ] **Step 3: Implement.**

- [ ] **Step 4: Run the tests.**

  Run: `bash tools/run-tests.sh "NetChecksTests|MatchClockTests|NonsenseNetworkTests|OnlineOrdersNetworkTests|OnlineScootersNetworkTests|OnlineDrivingNetworkTests|OnlineStealsNetworkTests|AchievementMessagesNetworkTests"`

  Run it in the background. Expected: all pass.

### Task 4: Release hardening

**Files:**
- Modify:
  - `Assets/Scripts/Online/OnlineSession.cs`: `NewConfig`, plus `#if` around `HostDirect` and `JoinDirect`;
  - `Assets/Scripts/Online/OnlinePlay.cs`: `UseDirect`.
- Test: `OnlineSessionTests` (two tests).

**Interfaces:**
- `NewConfig` sets these:
  - `EnsureNetworkVariableLengthSafety = true`
  - `ClientConnectionBufferTimeout = 5`
  - `EnableNetworkLogs = Debug.isDebugBuild`
- These change the Netcode setup hash, and with it the build tag. That's fine: `NetcodeSetup` is computed, and its test compares it with what Netcode computes.

- [ ] **Step 1: Write the failing tests.**
  - `OnlineSessionTests.NewSessions_CheckNetworkVariableLengths_AndDropSlowJoinsAfter5Seconds`: a created session's `Network.NetworkConfig` has `EnsureNetworkVariableLengthSafety` set and `ClientConnectionBufferTimeout` equal to 5.
  - `OnlineSessionTests.TheDirectTransport_IsEditorAndDevelopmentOnly`: read the source text of `OnlineSession.cs` and `OnlinePlay.cs` from `Application.dataPath`. Each of `HostDirect(`, `JoinDirect(` and `UseDirect(`'s declarations must sit inside a `#if UNITY_EDITOR || DEVELOPMENT_BUILD` block. A source check, like `DevToolsTests` does for `DevTools`.

- [ ] **Step 2: Run them, and see them fail.**

- [ ] **Step 3: Implement.**

- [ ] **Step 4: Run the tests.** Run `bash tools/run-tests.sh "OnlineSessionTests|OnlinePlayNetworkTests|OnlineSessionNetworkTests|DisconnectsNetworkTests"` in the background; expect all to pass.

### Task 5: Docs, editor chores, full suite

- [ ] **Step 1: Write `docs/online-safety.md`:**
  - the threats, and what each measure does;
  - what's out of scope (in-game cheating, Ruling 1);
  - the transport copy, and how to update it (re-copy from upstream, then re-apply `CHANGES-DOA.md`);
  - the CVE note.

  Link it from `docs/online.md`.
- [ ] **Step 2: Update `EDITOR-TODO.md`.** A new section 1 covers:
  - letting both editors import Phase 4C: the Package Manager re-resolves, since the transport is now embedded;
  - Unity Hub: confirm the editor is 2022.3.62f3 or later (CVE-2025-59489), and rebuild any build made before;
  - two PCs over Steam:
    - a friend joins as before;
    - Steam's connection info shows a relay ("Relayed"/SDR) in the overlay's network view, or the game's log;
    - a third account that isn't in the lobby can't connect.
- [ ] **Step 3: Update `docs/testing.md`** (the new test classes, and ports 7811–7812; the next free one is 7813) **and the handoff:** a Phase 4C entry.
- [ ] **Step 4: Run the full suite in the background,** with `bash tools/run-tests.sh`. Expected: everything passes; 575 plus about 25 new tests. Then check `git status` for files without a `.meta` file under `Assets/`.

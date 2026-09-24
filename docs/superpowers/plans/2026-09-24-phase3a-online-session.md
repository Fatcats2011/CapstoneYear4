# Phase 3A — Online Session Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Install Netcode for GameObjects 1.x with its transports and ParrelSync, and add the online session core: host or join a match (Unity Transport or Steam), let players in by the join rules, keep `GameAuthority.Role` in step, and say why a session ended. Local split-screen stays identical.

**Architecture:** One new MonoBehaviour, `OnlineSession`, builds its own `NetworkManager` and transport at runtime (no scene or prefab edits) and wraps start, stop, connection approval and the role. The rules for letting a player in are a pure static class, `JoinRules`. Nothing creates a session in a local match: the Steam lobby (roadmap Task 3.2) will, and until then an editor menu (**Tools → Dead on Arrival → Online**) does, so two editors (ParrelSync) can connect. Netcode's scene management stays off until Task 3.4.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 · Unity Transport 1.5.0 · community SteamNetworkingSockets transport @ `d862504b148f6c3a31763797900eb5a54d4625a5` · Steamworks.NET 2025.164.1 (already installed) · ParrelSync 1.5.3 · Unity Test Framework 1.1.33.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap):
- Task 3.1, all three bullets.
- Task 3.2, bullet 3: start the host, and start clients against the host's Steam ID. The Steam lobby and the menus stay in 3.2.
- Task 3.8, bullets 2–3: when the host leaves, the client's session ends with "The host left the match." (returning to the menu stays in 3.8). Reject mismatched builds, the approval-payload half (the lobby metadata half is 3.2).
- Global constraints: no mid-match joining.

## Global Constraints

- Stay on Unity 2022.3 LTS until launch (no Unity 6 / Cinemachine 3 / URP 17 migration): Netcode for GameObjects **1.x** only (2.x needs Unity 6).
- 1–4 players, gamepad-first (`Constants.MAX_PLAYERS = 4`). Online, one player per machine until Task 3.9.
- Local split-screen must pass the §R checklist after every phase: nothing online may run unless a session is started.
- Online: no mid-match joining; if the host leaves, the match ends.
- The Unity editor is open on the real project. Don't edit scenes, prefabs or ProjectSettings. This plan edits none.
  - `Packages/manifest.json` is fine: Unity reloads it on its own. The exact change in Task 1 was tried in the mirror first (compiles clean, 12/12 quick tests).
  - The open editor imports the packages and compiles the new scripts the next time it gets focus.
- No git commits without explicit approval from your human partner.
- Every new file under `Assets/` gets a `.meta` with a fresh GUID: `bash tools/newmeta.sh <path>`, or `bash tools/newmeta.sh <path> folder` for a folder.
  - `DefaultNetworkPrefabs.asset.meta` is the exception: Netcode writes it.
- Line endings:
  - Existing files: edit with the Edit tool, never `sed -i`.
  - New files may be LF.
  - No `i/crlf` file is touched here (check with `git ls-files --eol`).
- Tests: `bash tools/run-tests.sh [filter]`. It runs in the mirror (`../_doa_test_mirror/CapstoneYear4`) and takes 2–6 minutes, so run it in the background.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
- Branch `steam-phase1a` (memory `branch-policy.md`: only the Steam API goes to `main`).

## Findings (from a throwaway spike in the mirror, 2026-09-24)

Netcode:
- `NetworkManager` added from code has `NetworkConfig == null`. Assign `new NetworkConfig { … }` yourself.
- The host passes through the approval callback as it starts, with `ClientNetworkId == NetworkManager.ServerClientId` (0) and its own `ConnectionData`. Declining it only logs a warning (Netcode lets the host in anyway), so approve it.
- Approval runs when a join request arrives. Netcode adds the player to `ConnectedClientsIds` later in the frame, so count seats yourself when you approve.
- Joining on the same computer takes about 0.06 s.
- On a client, `OnClientConnectedCallback`'s id isn't the local id (it passed 0). Use `IsServer` to tell host from client.
- A refused client stops in about 0.02 s. At `OnClientStopped`, `DisconnectReason` holds the host's `response.Reason`.
- When the host shuts down, each client stops within a frame, with `DisconnectReason = "Disconnected due to host shutting down."`.
- A client disconnect makes Netcode shut the client down itself, so `OnClientStopped(false)` follows.
- Host shutdown raises `OnServerStopped(true)` and also `OnClientStopped(true)` for the host's own client.
- A stopped `NetworkManager` can `StartHost()` again.
- Netcode checks its own config hash before approval. A client whose `NetworkConfig` differs (tick rate, prefab list…) is dropped with no reason, so it looks like an unreachable host.

Unity Transport:
- Unity Transport defaults: `MaxConnectAttempts` 60 × `ConnectTimeoutMS` 1000 = 60 s before a join gives up; `DisconnectTimeoutMS` 30000.
- With nobody listening on 127.0.0.1, a client with 3 attempts stopped after 3.0 s with an empty `DisconnectReason`. Unity Transport also logged `All socket receive requests were marked as failed, likely because socket itself has failed.` as an **error**, once per attempt (the Windows loopback port-unreachable reply). A test of that case needs `LogAssert.ignoreFailingMessages = true`.
- With Unity Transport 1.5 (not 2.x), `UnityTransport.SetDebugSimulatorParameters(delayMs, jitterMs, dropPercent)` and `DebugSimulator` work. Call it before the session starts.

Tests that enter Play Mode:
- After `yield return new EnterPlayMode()` the test method resumes in a reloaded script domain, and its state machine restarts at the resume point.
- A lambda that captures the test's locals throws `NullReferenceException` there: its closure object was created before the reload. Subscribe with methods of a small recorder class, and wait with explicit `while` loops.
- `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)` before `EnterPlayMode` gives an empty scene (the test runner backs up and restores the open scenes). Entering Play Mode took about 2.8 s.
- Exiting Play Mode with a session still running logs Netcode's *warning* "Directly calling `UnityTransport.Shutdown()`…". Leave sessions before the test ends.

Steam transport:
- It lives in namespace `Netcode.Transports`, class `SteamNetworkingSocketsTransport`, with the field `public ulong ConnectToSteamID`. It has no `Awake`; Steam is only touched when it starts. Its newer commit (2026-09-08) references Unity 6's transport interop and drops the Netcode define, so this plan pins `d862504b` (May 2023), the last one built for Netcode 1.x.

## Review Focus

1. Two players ask to join in the same frame with one seat left → exactly one gets in (seats counted at approval, before Netcode lists them) — Task 3, `Approve_PlayersOnTheSameBuild_TakeTheFreeSeats_ThenTheMatchIsFull`.
2. The host can't be reached (wrong address, host closed, firewall) → the join ends by itself with "Couldn't reach the host." and the machine stays Offline — Task 4, `Join_NobodyHosting_EndsWithHostUnreachable` (10 attempts in the game, 2 in the test).
3. The host quits mid-session → every client's session ends with "The host left the match.", and it is Offline again, so gameplay gates and pausing work locally — Task 4, `HostLeaving_EndsTheClientsSession`.
4. Hosting or joining over Steam with Steam closed (editor, launched outside Steam, Steam crashed) → returns false with a warning, no exception, still set up for direct sessions — Task 5, `HostSteam_WithoutSteam_StaysOffline`, `JoinSteam_WithoutSteam_StaysOffline`.
5. Host pressed while already hosting, or Leave with no session → nothing breaks: the running session keeps its players, and a later session's end is still reported — Task 4, `Join_SameBuild_ConnectsAsAClient` (hosts twice), `Join_NobodyHosting_EndsWithHostUnreachable` (leaves first).

## File map

| File | Task | Responsibility |
|---|---|---|
| `Packages/manifest.json`, `Packages/packages-lock.json` | 1 | Netcode 1.15.1, the Steam transport (pinned), ParrelSync 1.5.3 |
| `Assets/DefaultNetworkPrefabs.asset` (+ `.meta`) | 1 | Netcode's generated prefab list (empty until Task 3.3) |
| `Assets/Tests/Editor/NetworkPackagesTests.cs` | 1 | The packages are there |
| `LICENSES.md` | 1 | Licence rows for the new packages |
| `Assets/Scripts/Online/` (+ folder `.meta`) | 2 | Online play's scripts |
| `Assets/Scripts/Online/JoinRules.cs` | 2 | Who the host lets in (pure) |
| `Assets/Tests/Editor/JoinRulesTests.cs` | 2 | |
| `Assets/Scripts/Online/OnlineSession.cs` | 3–5 | Session: Netcode setup, approval, host / join / leave, role, end reasons, Steam |
| `Assets/Tests/Editor/OnlineSessionTests.cs` | 3–5 | EditMode tests (no networking) |
| `Assets/Tests/Editor/OnlineSessionNetworkTests.cs` | 4 | Host and clients on 127.0.0.1 in Play Mode |
| `docs/testing.md` | 4 | Mentions the Play Mode network tests |
| `Assets/Scripts/Editor/OnlineTestMenu.cs` | 6 | Tools → Dead on Arrival → Online (two editors, bad connection) |
| `Assets/Tests/Editor/OnlineTestMenuTests.cs` | 6 | |
| `docs/online.md`, roadmap | 6 | How to test online; progress |

Test count: 167 now → 170 (Task 1) → 188 (2) → 193 (3) → 204 (4) → 206 (5) → 208 (6).

---

### Task 1: Networking packages

**Files:**
- Modify: `Packages/manifest.json`, `Packages/packages-lock.json` (copied from the mirror), `LICENSES.md`
- Create: `Assets/DefaultNetworkPrefabs.asset` + `.meta` (copied from the mirror, where Netcode writes them)
- Test: `Assets/Tests/Editor/NetworkPackagesTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: nothing.
- Produces: namespaces `Unity.Netcode` (`NetworkManager`, `NetworkConfig`), `Unity.Netcode.Transports.UTP` (`UnityTransport`), `Netcode.Transports` (`SteamNetworkingSocketsTransport`). All packages are auto-referenced, so `Assembly-CSharp` and the tests (`Assembly-CSharp-Editor`) can use them.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/Editor/NetworkPackagesTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/NetworkPackagesTests.cs`):

```csharp
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace DoA.Tests
{
    /// <summary>
    /// The online packages are installed (docs/online.md): Netcode for GameObjects on the 1.x line (2.x needs Unity 6),
    /// the community Steam transport and ParrelSync. Types are looked up by name so these tests compile without them
    /// </summary>
    public class NetworkPackagesTests
    {
        [Test]
        public void Netcode_IsInstalledOnThe1xLine()
        {
            Type manager = FindType("Unity.Netcode.NetworkManager");
            Assert.IsNotNull(manager, "Netcode for GameObjects");

            StringAssert.StartsWith("1.", PackageInfo.FindForAssembly(manager.Assembly).version);
        }

        [Test]
        public void SteamTransport_IsInstalled()
        {
            // Its class only compiles when both Netcode and Steamworks.NET are present
            Assert.IsNotNull(FindType("Netcode.Transports.SteamNetworkingSocketsTransport"));
        }

        [Test]
        public void ParrelSync_IsInstalled()
        {
            Assert.IsNotNull(FindType("ParrelSync.ClonesManager"));
        }

        static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);
        }
    }
}
```

- [ ] **Step 2: Run it to watch it fail**

Run: `bash tools/run-tests.sh NetworkPackagesTests` (the sync puts the project's manifest back in the mirror, so Unity removes the packages the spike added: a slower run).
Expected: `tests: 3 total, 0 passed, 3 failed` — `Netcode for GameObjects` / `Expected: not null`.

- [ ] **Step 3: Add the packages**

`Packages/manifest.json`, with the Edit tool (Unity keeps the non-module packages sorted, then `com.unity.modules.*`):
- Before `"com.jimmycushnie.noisynodes": …` add:
  `"com.community.netcode.transport.steamnetworkingsockets": "https://github.com/Unity-Technologies/multiplayer-community-contributions.git?path=/Transports/com.community.netcode.transport.steamnetworkingsockets#d862504b148f6c3a31763797900eb5a54d4625a5",`
- After `"com.unity.inputsystem": "1.14.0",` add:
  `"com.unity.netcode.gameobjects": "1.15.1",`
- After `"com.unity.visualscripting": "1.9.4",` add:
  `"com.veriorpies.parrelsync": "https://github.com/VeriorPies/ParrelSync.git?path=/ParrelSync#1.5.3",`

- [ ] **Step 4: Run it to watch it pass**

Run: `bash tools/run-tests.sh NetworkPackagesTests`
Expected: `tests: 3 total, 3 passed, 0 failed`. `Logs/test-unity.log` registers `com.unity.netcode.gameobjects@1.15.1`, `com.unity.transport@1.5.0`, `com.unity.collections@1.2.4`, `com.unity.nuget.mono-cecil@1.11.6`, the transport `@d862504b14` and ParrelSync `@8072a60940`.

- [ ] **Step 5: Bring back what Unity wrote in the mirror**

```bash
M=../_doa_test_mirror/CapstoneYear4
cp "$M/Packages/packages-lock.json" Packages/packages-lock.json
cp "$M/Assets/DefaultNetworkPrefabs.asset" "$M/Assets/DefaultNetworkPrefabs.asset.meta" Assets/
git diff --stat -- Packages; git status --short
```

Expected:
- `packages-lock.json` gains only the six entries above: collections at depth 2, `burst` stays 1.8.21, `mathematics` stays 1.2.6.
- `DefaultNetworkPrefabs.asset` holds `List: []`.
- The next sync then leaves the asset alone instead of deleting it from the mirror (and Netcode making a new GUID).

- [ ] **Step 6: Licences**

`LICENSES.md` → *Code, packages and tools*, after the `Steamworks.NET` row, add:

```markdown
| Netcode for GameObjects 1.15.1 | `Packages` (Unity registry) | MIT (© Unity Technologies) | ✅ | Online play (Phase 3). |
| Unity Transport 1.5.0, Collections 1.2.4, Mono Cecil 1.11.6 | `Packages` (installed with Netcode) | Unity Companion License | ✅ | |
| SteamNetworkingSockets transport (community) | `Packages` (git: Unity-Technologies/multiplayer-community-contributions, pinned `d862504b`) | MIT | ✅ | Keep the copyright notice. |
| ParrelSync 1.5.3 | `Packages` (git: VeriorPies/ParrelSync) | MIT | ✅ | Editor only; not in builds. |
```

- [ ] **Step 7: Full suite (local play unchanged with the packages in)**

Run: `bash tools/run-tests.sh`
Expected: `tests: 170 total, 170 passed, 0 failed` (the smoke match included).

Files: `Packages/manifest.json`, `Packages/packages-lock.json`, `Assets/DefaultNetworkPrefabs.asset` + `.meta`, `Assets/Tests/Editor/NetworkPackagesTests.cs` + `.meta`, `LICENSES.md`. No commit.

---

### Task 2: Join rules

**Files:**
- Create: `Assets/Scripts/Online/` (folder + `.meta`), `Assets/Scripts/Online/JoinRules.cs` (+ `.meta`)
- Test: `Assets/Tests/Editor/JoinRulesTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `GameState` (`Assets/Scripts/Management/GameManager.cs`), `Constants.MAX_PLAYERS`.
- Produces:
  - `public static class JoinRules`
  - `public const string FULL = "That match is full.";`
  - `public const string STARTED = "That match has already started.";`
  - `public static string Refusal(string hostVersion, string joinerVersion, int seatsTaken, GameState hostState)`: null = let them in.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/JoinRulesTests.cs`:

```csharp
using NUnit.Framework;

namespace DoA.Tests
{
    public class JoinRulesTests
    {
        [Test]
        public void Refusal_SameBuildWithASeatFreeBeforeTheMatch_LetsThePlayerIn()
        {
            Assert.IsNull(JoinRules.Refusal("1.0.0", "1.0.0", Constants.MAX_PLAYERS - 1, GameState.PlayerSelect));
        }

        [Test]
        public void Refusal_AnotherBuild_NamesBothVersions()
        {
            string refusal = JoinRules.Refusal("1.0.0", "1.0.1", 1, GameState.PlayerSelect);

            StringAssert.Contains("1.0.0", refusal);
            StringAssert.Contains("1.0.1", refusal);
        }

        [Test]
        public void Refusal_NoBuildSent_CallsTheVersionUnknown()
        {
            StringAssert.Contains("an unknown version", JoinRules.Refusal("1.0.0", "", 1, GameState.PlayerSelect));
        }

        [Test]
        public void Refusal_EverySeatTaken_SaysTheMatchIsFull()
        {
            Assert.AreEqual(JoinRules.FULL, JoinRules.Refusal("1.0.0", "1.0.0", Constants.MAX_PLAYERS, GameState.PlayerSelect));
        }

        [TestCase(GameState.Default, false)]
        [TestCase(GameState.Menu, false)]
        [TestCase(GameState.Options, false)]
        [TestCase(GameState.Credits, false)]
        [TestCase(GameState.PlayerSelect, false)]
        [TestCase(GameState.Loading, true)]
        [TestCase(GameState.StartingCutscene, true)]
        [TestCase(GameState.Tutorial, true)]
        [TestCase(GameState.Begin, true)]
        [TestCase(GameState.MainLoop, true)]
        [TestCase(GameState.GoldenCutscene, true)]
        [TestCase(GameState.FinalPackage, true)]
        [TestCase(GameState.Results, true)]
        [TestCase(GameState.Paused, true)]
        public void Refusal_OnceTheHostsMatchHasStarted_SaysSo(GameState hostState, bool started)
        {
            Assert.AreEqual(started ? JoinRules.STARTED : null, JoinRules.Refusal("1.0.0", "1.0.0", 1, hostState));
        }
    }
}
```

Then run:

```bash
mkdir -p Assets/Scripts/Online
bash tools/newmeta.sh Assets/Scripts/Online folder
bash tools/newmeta.sh Assets/Tests/Editor/JoinRulesTests.cs
```

- [ ] **Step 2: Run them to watch them fail**

Run: `bash tools/run-tests.sh JoinRulesTests`
Expected: `NO RESULTS` with `error CS0103: The name 'JoinRules' does not exist in the current context`.

- [ ] **Step 3: Write the rules**

`Assets/Scripts/Online/JoinRules.cs` (then `bash tools/newmeta.sh Assets/Scripts/Online/JoinRules.cs`):

```csharp
/// <summary>
/// Who the host lets into an online match: a player on the same build, while a seat is free and the host is still in
/// the menus. Nobody joins a match that has started
/// </summary>
public static class JoinRules
{
    public const string FULL = "That match is full.";
    public const string STARTED = "That match has already started.";

    /// <summary>
    /// Why the host turns a joining player away, or null to let them in
    /// </summary>
    /// <param name="hostVersion">The host's build version</param>
    /// <param name="joinerVersion">The build version the player sent (empty if they sent none)</param>
    /// <param name="seatsTaken">Players already let in, the host included</param>
    /// <param name="hostState">What the host's game is doing</param>
    public static string Refusal(string hostVersion, string joinerVersion, int seatsTaken, GameState hostState)
    {
        if (joinerVersion != hostVersion)
            return "That match is on version " + hostVersion + " and you have "
                + (string.IsNullOrEmpty(joinerVersion) ? "an unknown version" : joinerVersion) + ". You both need the same version.";

        if (seatsTaken >= Constants.MAX_PLAYERS)
            return FULL;

        if (HasStarted(hostState))
            return STARTED;

        return null;
    }

    // Players may join while the host is on the title screen, in options or credits, or in player select
    static bool HasStarted(GameState hostState)
    {
        switch (hostState)
        {
            case GameState.Default:
            case GameState.Menu:
            case GameState.Options:
            case GameState.Credits:
            case GameState.PlayerSelect:
                return false;
            default:
                return true;
        }
    }
}
```

- [ ] **Step 4: Run them to watch them pass**

Run: `bash tools/run-tests.sh JoinRulesTests`
Expected: `tests: 18 total, 18 passed, 0 failed`.

Files: `Assets/Scripts/Online.meta`, `Assets/Scripts/Online/JoinRules.cs` + `.meta`, `Assets/Tests/Editor/JoinRulesTests.cs` + `.meta`. No commit.

---

### Task 3: Session setup and approval

**Files:**
- Create: `Assets/Scripts/Online/OnlineSession.cs` (+ `.meta`)
- Test: `Assets/Tests/Editor/OnlineSessionTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: `JoinRules.Refusal`, `JoinRules.FULL`, `JoinRules.STARTED` (Task 2); `GameManager.Instance.MainState`; test helpers `TestObjects`, `Reflect.SetSingleton` (`Assets/Tests/Editor/TestSupport.cs`).
- Produces (`public class OnlineSession : MonoBehaviour`):
  - `public static OnlineSession Create(string version)`
  - `public NetworkManager Network { get; }`
  - `public UnityTransport Direct { get; }`
  - `public string Version { get; }`
  - `public int PlayersIn { get; }` (seats taken, the host included)
  - The approval handler, registered as `Network.ConnectionApprovalCallback`: tests call it through that property.

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/OnlineSessionTests.cs`:

```csharp
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// OnlineSession without networking: its Netcode setup and who it lets in. OnlineSessionNetworkTests connect for real
    /// </summary>
    public class OnlineSessionTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<OnlineSession> sessions = new List<OnlineSession>();

        [TearDown]
        public void TearDown()
        {
            foreach (OnlineSession session in sessions)
            {
                if (session != null)
                    Object.DestroyImmediate(session.gameObject);
            }
            sessions.Clear();
            objects.DestroyAll();
            Reflect.SetSingleton<GameManager>(null);
            GameAuthority.Role = NetworkRole.Offline;
        }

        OnlineSession NewSession(string version = "1.0.0")
        {
            OnlineSession session = OnlineSession.Create(version);
            sessions.Add(session);
            return session;
        }

        // What the host decides about a player asking to join
        static NetworkManager.ConnectionApprovalResponse Approve(OnlineSession host, ulong clientId, string version)
        {
            NetworkManager.ConnectionApprovalResponse response = new NetworkManager.ConnectionApprovalResponse();
            NetworkManager.ConnectionApprovalRequest request = new NetworkManager.ConnectionApprovalRequest
            {
                ClientNetworkId = clientId,
                Payload = Encoding.UTF8.GetBytes(version),
            };
            host.Network.ConnectionApprovalCallback(request, response);
            return response;
        }

        [Test]
        public void Create_SetsUpNetcodeToApproveJoinsOverUnityTransport()
        {
            OnlineSession session = NewSession();

            NetworkConfig config = session.Network.NetworkConfig;
            Assert.AreSame(session.Direct, config.NetworkTransport, "starts on Unity Transport");
            Assert.IsTrue(config.ConnectionApproval, "the host approves every join");
            Assert.IsFalse(config.EnableSceneManagement, "scene loads stay local until roadmap Task 3.4");
            Assert.AreEqual("1.0.0", session.Version);
        }

        [Test]
        public void Approve_TheHostItself_IsLetInWhateverItSends()
        {
            OnlineSession session = NewSession();

            Assert.IsTrue(Approve(session, NetworkManager.ServerClientId, "").Approved);
            Assert.AreEqual(1, session.PlayersIn);
        }

        [Test]
        public void Approve_PlayersOnTheSameBuild_TakeTheFreeSeats_ThenTheMatchIsFull()
        {
            OnlineSession session = NewSession();
            Approve(session, NetworkManager.ServerClientId, "1.0.0");
            for (ulong player = 1; player < Constants.MAX_PLAYERS; player++)
                Assert.IsTrue(Approve(session, player, "1.0.0").Approved, "player " + player);

            // Netcode lists approved players only later in the frame: the session counts them as it approves
            NetworkManager.ConnectionApprovalResponse late = Approve(session, Constants.MAX_PLAYERS, "1.0.0");

            Assert.IsFalse(late.Approved);
            Assert.AreEqual(JoinRules.FULL, late.Reason);
            Assert.AreEqual(Constants.MAX_PLAYERS, session.PlayersIn);
        }

        [Test]
        public void Approve_APlayerOnAnotherBuild_IsTurnedAwayWithTheReason()
        {
            OnlineSession session = NewSession("1.0.0");
            Approve(session, NetworkManager.ServerClientId, "1.0.0");

            NetworkManager.ConnectionApprovalResponse response = Approve(session, 1, "0.9.0");

            Assert.IsFalse(response.Approved);
            Assert.AreEqual(JoinRules.Refusal("1.0.0", "0.9.0", 1, GameState.Default), response.Reason);
            Assert.AreEqual(1, session.PlayersIn);
        }

        [Test]
        public void Approve_WhileTheHostsMatchIsUnderWay_TurnsPlayersAway()
        {
            GameManager game = objects.Add<GameManager>();
            Reflect.SetSingleton(game);
            game.SetGameState(GameState.MainLoop);
            OnlineSession session = NewSession();
            Approve(session, NetworkManager.ServerClientId, "1.0.0");

            NetworkManager.ConnectionApprovalResponse response = Approve(session, 1, "1.0.0");

            Assert.IsFalse(response.Approved);
            Assert.AreEqual(JoinRules.STARTED, response.Reason);
        }
    }
}
```

Then `bash tools/newmeta.sh Assets/Tests/Editor/OnlineSessionTests.cs`.

- [ ] **Step 2: Run them to watch them fail**

Run: `bash tools/run-tests.sh OnlineSessionTests`
Expected: `NO RESULTS` with `error CS0246: The type or namespace name 'OnlineSession' could not be found`.

- [ ] **Step 3: Write the session's setup and approval**

`Assets/Scripts/Online/OnlineSession.cs` (then `bash tools/newmeta.sh Assets/Scripts/Online/OnlineSession.cs`):

```csharp
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// This machine's online match. Builds its own Netcode NetworkManager and transport, so no scene holds it, and lets
/// players in by JoinRules. Nothing creates a session in a local match. See docs/online.md
/// </summary>
public class OnlineSession : MonoBehaviour
{
    readonly HashSet<ulong> seats = new HashSet<ulong>(); // host: the players it let in, itself included

    /// <summary>Netcode's manager for this session</summary>
    public NetworkManager Network { get; private set; }

    /// <summary>The Unity Transport that direct (IP address) sessions use</summary>
    public UnityTransport Direct { get; private set; }

    /// <summary>This machine's build: sent when joining, and required of players who join this host</summary>
    public string Version { get; private set; }

    /// <summary>How many players the host has let in, the host included</summary>
    public int PlayersIn { get { return seats.Count; } }

    /// <summary>
    /// Builds a session on its own object (Netcode keeps it across scene loads)
    /// </summary>
    /// <param name="version">This build's version: the game passes Application.version</param>
    public static OnlineSession Create(string version)
    {
        GameObject holder = new GameObject("Online Session");
        OnlineSession session = holder.AddComponent<OnlineSession>();
        session.Version = version;
        session.Direct = holder.AddComponent<UnityTransport>();

        NetworkManager network = holder.AddComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig // a NetworkManager added from code has no config
        {
            NetworkTransport = session.Direct,
            ConnectionApproval = true,
            EnableSceneManagement = false, // scene loads stay local until the networked scene flow (roadmap Task 3.4)
        };
        network.ConnectionApprovalCallback = session.Approve;
        session.Network = network;
        return session;
    }

    // Host: Netcode asks about every joining player, and about the host itself as it starts
    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string refusal = request.ClientNetworkId == NetworkManager.ServerClientId ? null
            : JoinRules.Refusal(Version, ReadVersion(request.Payload), seats.Count, HostState());

        response.Approved = refusal == null;
        response.Reason = refusal;
        response.CreatePlayerObject = false; // the match spawns the scooters (roadmap Task 3.3)

        if (response.Approved)
            seats.Add(request.ClientNetworkId); // counted now: Netcode lists the player later in the frame
        else
            Debug.Log("Online: turned a player away: " + refusal);
    }

    static string ReadVersion(byte[] payload)
    {
        return payload == null ? "" : Encoding.UTF8.GetString(payload);
    }

    // What the host's game is doing (without a GameManager, as in tests, it isn't in a match)
    static GameState HostState()
    {
        return GameManager.Instance != null ? GameManager.Instance.MainState : GameState.Default;
    }
}
```

- [ ] **Step 4: Run them to watch them pass**

Run: `bash tools/run-tests.sh OnlineSessionTests`
Expected: `tests: 5 total, 5 passed, 0 failed`.

Files: `Assets/Scripts/Online/OnlineSession.cs` + `.meta`, `Assets/Tests/Editor/OnlineSessionTests.cs` + `.meta`. No commit.

---

### Task 4: Hosting, joining and leaving

**Files:**
- Modify: `Assets/Scripts/Online/OnlineSession.cs` (whole file below), `Assets/Tests/Editor/OnlineSessionTests.cs` (add one test), `docs/testing.md`
- Test: `Assets/Tests/Editor/OnlineSessionNetworkTests.cs` (+ `.meta`)

**Interfaces:**
- Consumes: Task 3's `OnlineSession` members; `GameAuthority.Role`, `NetworkRole` (`Assets/Scripts/Management/GameAuthority.cs`); `JoinRules.Refusal`.
- Produces (added to `OnlineSession`):
  - `public const ushort DIRECT_PORT = 7777;`
  - `public const int DIRECT_CONNECT_ATTEMPTS = 10;`
  - `public const string HOST_LEFT = "The host left the match.";`
  - `public const string HOST_UNREACHABLE = "Couldn't reach the host.";`
  - `public const string CONNECTION_LOST = "The connection was lost.";`
  - `public NetworkRole Role { get; }`
  - `public bool IsRunning { get; }`
  - `public event Action<string> Ended;`
  - `public bool HostDirect(string listenAddress, ushort port)`
  - `public bool JoinDirect(string address, ushort port)`
  - `public void Leave()`
  - `public static string EndReason(bool reachedHost, string netcodeReason)`
  - Private `StartHost()` / `StartClient()` (Task 5 calls them).

- [ ] **Step 1: Write the failing tests**

Add to `OnlineSessionTests` (below `Approve_WhileTheHostsMatchIsUnderWay_TurnsPlayersAway`):

```csharp
        [TestCase(true, "Disconnected due to host shutting down.", OnlineSession.HOST_LEFT)]
        [TestCase(true, "", OnlineSession.HOST_LEFT)]
        [TestCase(false, JoinRules.FULL, JoinRules.FULL)]
        [TestCase(false, "", OnlineSession.HOST_UNREACHABLE)]
        [TestCase(false, null, OnlineSession.HOST_UNREACHABLE)]
        public void EndReason_TellsAClientWhatHappened(bool reachedHost, string netcodeReason, string expected)
        {
            Assert.AreEqual(expected, OnlineSession.EndReason(reachedHost, netcodeReason));
        }
```

`Assets/Tests/Editor/OnlineSessionNetworkTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/OnlineSessionNetworkTests.cs`):

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Hosts and clients on this computer (127.0.0.1), in one empty Play Mode scene: joining, being turned away, the
    /// host or a player leaving, a host nobody can reach, Netcode stopping on its own. Each test enters Play Mode (a few
    /// seconds).
    /// After EnterPlayMode a test resumes in a reloaded script domain, where a lambda that captures the test's locals
    /// throws (its closure was made before the reload): these tests record events with EndedRecorder and wait in loops
    /// </summary>
    public class OnlineSessionNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7791; // not the game's 7777, in case an editor is hosting
        const float WAIT = 10f;

        /// <summary>
        /// Remembers why a session ended
        /// </summary>
        class EndedRecorder
        {
            public string Reason;

            public EndedRecorder(OnlineSession session)
            {
                session.Ended += OnEnded;
            }

            void OnEnded(string reason)
            {
                Reason = reason;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            GameAuthority.Role = NetworkRole.Offline;
        }

        [UnityTest]
        public IEnumerator Join_SameBuild_ConnectsAsAClient()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "host started");
            OnlineSession client = OnlineSession.Create("1.0.0");
            Assert.IsTrue(client.JoinDirect(THIS_COMPUTER, PORT), "client started");

            float deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Role != NetworkRole.Client && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(NetworkRole.Client, client.Role, "the client's part");
            Assert.AreEqual(NetworkRole.Host, host.Role, "the host's part");
            Assert.AreEqual(NetworkRole.Client, GameAuthority.Role, "this machine's part (the client set it last)");
            Assert.AreEqual(2, host.PlayersIn, "players the host let in");
            Assert.IsFalse(host.HostDirect(THIS_COMPUTER, PORT), "hosting again while hosting");
            Assert.AreEqual(2, host.PlayersIn, "players after hosting again");

            client.Leave();
            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Join_AnotherBuild_IsTurnedAwayWithBothVersions()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("0.9.0");
            EndedRecorder ended = new EndedRecorder(client);
            client.JoinDirect(THIS_COMPUTER, PORT);

            float deadline = Time.realtimeSinceStartup + WAIT;
            while (ended.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(JoinRules.Refusal("1.0.0", "0.9.0", 1, GameState.Default), ended.Reason);
            Assert.IsFalse(client.IsRunning, "the client's session ended");
            Assert.AreEqual(NetworkRole.Offline, client.Role);
            Assert.AreEqual(1, host.PlayersIn, "only the host is in");

            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator HostLeaving_EndsTheClientsSession()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            EndedRecorder hostEnded = new EndedRecorder(host);
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            EndedRecorder clientEnded = new EndedRecorder(client);
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Role != NetworkRole.Client && Time.realtimeSinceStartup < deadline)
                yield return null;

            host.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (clientEnded.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(OnlineSession.HOST_LEFT, clientEnded.Reason);
            Assert.AreEqual(NetworkRole.Offline, client.Role, "the client is local again");
            Assert.AreEqual(NetworkRole.Offline, host.Role, "the host is local again");
            Assert.IsNull(hostEnded.Reason, "the host left on purpose");
        }

        [UnityTest]
        public IEnumerator PlayerLeaving_FreesTheirSeat()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            EndedRecorder clientEnded = new EndedRecorder(client);
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (client.Role != NetworkRole.Client && Time.realtimeSinceStartup < deadline)
                yield return null;

            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, host.PlayersIn, "the player's seat is free again");
            Assert.AreEqual(NetworkRole.Host, host.Role, "the host keeps hosting");
            Assert.AreEqual(NetworkRole.Offline, client.Role);
            Assert.IsNull(clientEnded.Reason, "the player left on purpose");

            host.Leave();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Join_NobodyHosting_EndsWithHostUnreachable()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true; // Unity Transport logs a socket error per attempt when nobody answers on this computer

            OnlineSession client = OnlineSession.Create("1.0.0");
            client.Leave(); // nothing to leave yet: must not hide the end of the next session
            client.Direct.MaxConnectAttempts = 2; // give up after about 2 s (the game waits 10)
            EndedRecorder ended = new EndedRecorder(client);
            Assert.IsTrue(client.JoinDirect(THIS_COMPUTER, PORT), "client started");

            float deadline = Time.realtimeSinceStartup + WAIT;
            while (ended.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(OnlineSession.HOST_UNREACHABLE, ended.Reason);
            Assert.IsFalse(client.IsRunning);
            Assert.AreEqual(NetworkRole.Offline, client.Role);
        }

        [UnityTest]
        public IEnumerator NetcodeStoppingOnItsOwn_EndsTheHostsSession()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();

            OnlineSession host = OnlineSession.Create("1.0.0");
            EndedRecorder ended = new EndedRecorder(host);
            host.HostDirect(THIS_COMPUTER, PORT);

            host.Network.Shutdown(); // what a transport failure does
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (ended.Reason == null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(OnlineSession.CONNECTION_LOST, ended.Reason);
            Assert.AreEqual(NetworkRole.Offline, host.Role);
            Assert.AreEqual(NetworkRole.Offline, GameAuthority.Role, "this machine is local again");
        }
    }
}
```

- [ ] **Step 2: Run them to watch them fail**

Run: `bash tools/run-tests.sh OnlineSession`
Expected: `NO RESULTS` with `error CS0117: 'OnlineSession' does not contain a definition for 'HOST_LEFT'` (and `HostDirect`, `Role`, `Ended`…).

- [ ] **Step 3: Write hosting, joining and leaving**

Replace `Assets/Scripts/Online/OnlineSession.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// This machine's online match: hosts one or joins one, lets players in by JoinRules, and keeps GameAuthority.Role in
/// step (Host or Client while the session runs, Offline once it ends). Builds its own Netcode NetworkManager and
/// transport, so no scene holds it. Nothing creates a session in a local match. See docs/online.md
/// </summary>
public class OnlineSession : MonoBehaviour
{
    /// <summary>The port direct (Unity Transport) sessions use</summary>
    public const ushort DIRECT_PORT = 7777;

    /// <summary>A direct join tries to reach the host once a second, this many times</summary>
    public const int DIRECT_CONNECT_ATTEMPTS = 10;

    /// <summary>Why a session ended, when the host gave no reason of its own</summary>
    public const string HOST_LEFT = "The host left the match.";
    public const string HOST_UNREACHABLE = "Couldn't reach the host.";
    public const string CONNECTION_LOST = "The connection was lost.";

    readonly HashSet<ulong> seats = new HashSet<ulong>(); // host: the players it let in, itself included
    bool reachedHost; // client: the host let this machine in
    bool leaving;     // Leave was called, so the end that follows is expected

    /// <summary>Netcode's manager for this session</summary>
    public NetworkManager Network { get; private set; }

    /// <summary>The Unity Transport that direct (IP address) sessions use</summary>
    public UnityTransport Direct { get; private set; }

    /// <summary>This machine's build: sent when joining, and required of players who join this host</summary>
    public string Version { get; private set; }

    /// <summary>This session's part: Host or Client while it runs (a client once the host lets it in), else Offline</summary>
    public NetworkRole Role { get; private set; }

    /// <summary>Hosting or joining: from a successful start until the session ends</summary>
    public bool IsRunning { get; private set; }

    /// <summary>How many players the host has let in, the host included</summary>
    public int PlayersIn { get { return seats.Count; } }

    /// <summary>
    /// Raised when a session ends without Leave, with the reason to show: the host left, turned this player away or
    /// couldn't be reached; on the host, the connection was lost
    /// </summary>
    public event Action<string> Ended;

    /// <summary>
    /// Builds a session on its own object (Netcode keeps it across scene loads)
    /// </summary>
    /// <param name="version">This build's version: the game passes Application.version</param>
    public static OnlineSession Create(string version)
    {
        GameObject holder = new GameObject("Online Session");
        OnlineSession session = holder.AddComponent<OnlineSession>();
        session.Version = version;
        session.Direct = holder.AddComponent<UnityTransport>();
        session.Direct.MaxConnectAttempts = DIRECT_CONNECT_ATTEMPTS;

        NetworkManager network = holder.AddComponent<NetworkManager>();
        network.NetworkConfig = new NetworkConfig // a NetworkManager added from code has no config
        {
            NetworkTransport = session.Direct,
            ConnectionApproval = true,
            EnableSceneManagement = false, // scene loads stay local until the networked scene flow (roadmap Task 3.4)
        };
        network.ConnectionApprovalCallback = session.Approve;
        network.OnClientConnectedCallback += session.OnConnected;
        network.OnClientDisconnectCallback += session.OnDisconnected;
        network.OnClientStopped += session.OnClientStopped;
        network.OnServerStopped += session.OnServerStopped;
        session.Network = network;
        return session;
    }

    /// <summary>
    /// Hosts a match that players join by IP address (Unity Transport): the editor and LAN tests
    /// </summary>
    /// <param name="listenAddress">"127.0.0.1" = this computer only; "0.0.0.0" = the local network too (Windows asks once to allow it)</param>
    /// <returns>False when a session is already running, or Netcode can't start (the port is in use)</returns>
    public bool HostDirect(string listenAddress, ushort port)
    {
        if (IsRunning)
            return false;

        Direct.SetConnectionData(listenAddress, port, listenAddress);
        Network.NetworkConfig.NetworkTransport = Direct;
        return StartHost();
    }

    /// <summary>
    /// Joins a match hosted with HostDirect. The session becomes a client once the host lets it in; Ended says why if it doesn't
    /// </summary>
    /// <returns>False when a session is already running, or Netcode can't start</returns>
    public bool JoinDirect(string address, ushort port)
    {
        if (IsRunning)
            return false;

        Direct.SetConnectionData(address, port);
        Network.NetworkConfig.NetworkTransport = Direct;
        return StartClient();
    }

    /// <summary>
    /// Ends the session on purpose (Ended isn't raised). The host leaving ends the match for everyone
    /// </summary>
    public void Leave()
    {
        if (!IsRunning)
            return;

        leaving = true;
        Network.Shutdown(); // Netcode finishes stopping at the end of the frame, then End tidies up
        SetRole(NetworkRole.Offline);
    }

    /// <summary>
    /// What to tell a client whose session ended: the host left (after letting it in), the host's own reason (it turned
    /// the player away), or that the host couldn't be reached
    /// </summary>
    public static string EndReason(bool reachedHost, string netcodeReason)
    {
        if (reachedHost)
            return HOST_LEFT;

        return string.IsNullOrEmpty(netcodeReason) ? HOST_UNREACHABLE : netcodeReason;
    }

    bool StartHost()
    {
        seats.Clear(); // the host takes the first seat while Netcode starts
        if (!Network.StartHost())
            return false;

        IsRunning = true;
        SetRole(NetworkRole.Host);
        Debug.Log("Online: hosting version " + Version);
        return true;
    }

    bool StartClient()
    {
        reachedHost = false;
        Network.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(Version);
        if (!Network.StartClient())
            return false;

        IsRunning = true;
        Debug.Log("Online: joining the host");
        return true;
    }

    // Host: Netcode asks about every joining player, and about the host itself as it starts
    void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        string refusal = request.ClientNetworkId == NetworkManager.ServerClientId ? null
            : JoinRules.Refusal(Version, ReadVersion(request.Payload), seats.Count, HostState());

        response.Approved = refusal == null;
        response.Reason = refusal;
        response.CreatePlayerObject = false; // the match spawns the scooters (roadmap Task 3.3)

        if (response.Approved)
            seats.Add(request.ClientNetworkId); // counted now: Netcode lists the player later in the frame
        else
            Debug.Log("Online: turned a player away: " + refusal);
    }

    static string ReadVersion(byte[] payload)
    {
        return payload == null ? "" : Encoding.UTF8.GetString(payload);
    }

    // What the host's game is doing (without a GameManager, as in tests, it isn't in a match)
    static GameState HostState()
    {
        return GameManager.Instance != null ? GameManager.Instance.MainState : GameState.Default;
    }

    // Host: a player is in. Client: the host let this machine in (the id Netcode passes a client isn't its own)
    void OnConnected(ulong clientId)
    {
        if (Network.IsServer)
        {
            if (clientId != NetworkManager.ServerClientId)
                Debug.Log("Online: player " + clientId + " joined (" + seats.Count + " in)");
            return;
        }

        reachedHost = true;
        SetRole(NetworkRole.Client);
        Debug.Log("Online: joined the host");
    }

    // Host: a player left, so their seat is free (the host's own client only goes when the host stops: End clears it)
    void OnDisconnected(ulong clientId)
    {
        if (Network.IsServer && clientId != NetworkManager.ServerClientId && seats.Remove(clientId))
            Debug.Log("Online: player " + clientId + " left (" + seats.Count + " in)");
    }

    // A client's Netcode stops when it leaves, is turned away, loses the host or never reaches it
    void OnClientStopped(bool wasHost)
    {
        if (!wasHost) // the host's own client stops with the host: OnServerStopped covers it
            End(EndReason(reachedHost, Network.DisconnectReason));
    }

    // The host's Netcode stops when it leaves, or on its own (a transport failure)
    void OnServerStopped(bool wasHost)
    {
        End(CONNECTION_LOST);
    }

    // Back to a local machine. Ended says why, unless Leave ended it or the session never started
    void End(string reason)
    {
        bool expected = leaving || !IsRunning;
        IsRunning = false;
        leaving = false;
        reachedHost = false;
        seats.Clear();
        SetRole(NetworkRole.Offline);

        if (expected)
            return;

        Debug.Log("Online: session ended: " + reason);
        Ended?.Invoke(reason);
    }

    void SetRole(NetworkRole role)
    {
        Role = role;
        GameAuthority.Role = role;
    }
}
```

- [ ] **Step 4: Run them to watch them pass**

Run: `bash tools/run-tests.sh OnlineSession`
Expected: `tests: 16 total, 16 passed, 0 failed` (10 in `OnlineSessionTests`, 6 in `OnlineSessionNetworkTests`).

- [ ] **Step 5: Mention the network tests in `docs/testing.md`**

After the LocalMatchSmokeTest bullet list (below `- It fails on any error or exception on the way.`), add:

```markdown
- `OnlineSessionNetworkTests` start online hosts and clients on this computer (127.0.0.1, port 7791) in an empty Play Mode scene, a few seconds per test. Nothing needs to be running for them.
```

- [ ] **Step 6: Full suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 204 total, 204 passed, 0 failed`.

Files: `Assets/Scripts/Online/OnlineSession.cs`, `Assets/Tests/Editor/OnlineSessionTests.cs`, `Assets/Tests/Editor/OnlineSessionNetworkTests.cs` + `.meta`, `docs/testing.md`. No commit.

---

### Task 5: Steam sessions

**Files:**
- Modify: `Assets/Scripts/Online/OnlineSession.cs`, `Assets/Tests/Editor/OnlineSessionTests.cs`

**Interfaces:**
- Consumes: `SteamManager.Initialized` (`Assets/Scripts/Management/SteamManager.cs`); Task 4's private `StartHost()` / `StartClient()` and `IsRunning`; `Netcode.Transports.SteamNetworkingSocketsTransport` (`ConnectToSteamID`).
- Produces (in `OnlineSession`, Windows / Linux / macOS standalone and the editor; the same guard as `SteamManager`):
  - `public const string NO_STEAM = "Online: Steam isn't running, so this machine can't host or join over Steam.";`
  - `public bool HostSteam()`
  - `public bool JoinSteam(ulong hostSteamId)` — Task 3.2's lobby passes the lobby owner's Steam ID.

- [ ] **Step 1: Write the failing tests**

Add `using UnityEngine.TestTools;` to `OnlineSessionTests.cs`, and these tests at the end of the class:

```csharp
        [Test]
        public void HostSteam_WithoutSteam_StaysOffline()
        {
            OnlineSession session = NewSession();
            LogAssert.Expect(LogType.Warning, OnlineSession.NO_STEAM);

            Assert.IsFalse(session.HostSteam());
            Assert.IsFalse(session.IsRunning);
            Assert.AreSame(session.Direct, session.Network.NetworkConfig.NetworkTransport, "still set up for direct sessions");
        }

        [Test]
        public void JoinSteam_WithoutSteam_StaysOffline()
        {
            OnlineSession session = NewSession();
            LogAssert.Expect(LogType.Warning, OnlineSession.NO_STEAM);

            Assert.IsFalse(session.JoinSteam(76561198000000000UL));
            Assert.IsFalse(session.IsRunning);
            Assert.AreSame(session.Direct, session.Network.NetworkConfig.NetworkTransport, "still set up for direct sessions");
        }
```

(EditMode tests never have Steam: `SteamManager` creates itself only when Play Mode starts.)

- [ ] **Step 2: Run them to watch them fail**

Run: `bash tools/run-tests.sh OnlineSessionTests`
Expected: `NO RESULTS` with `error CS0117: 'OnlineSession' does not contain a definition for 'NO_STEAM'` (and `HostSteam`, `JoinSteam`).

- [ ] **Step 3: Add Steam sessions**

In `Assets/Scripts/Online/OnlineSession.cs`:

At the very top of the file, before `using System;` (the same guard `SteamManager.cs` uses):

```csharp
#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

```

After `using System.Text;`:

```csharp
#if !DISABLESTEAMWORKS
using Netcode.Transports;
#endif
```

After `public const string CONNECTION_LOST = "The connection was lost.";`:

```csharp

    /// <summary>Logged when a Steam session can't start</summary>
    public const string NO_STEAM = "Online: Steam isn't running, so this machine can't host or join over Steam.";
```

After `bool leaving;     // Leave was called, so the end that follows is expected`:

```csharp
#if !DISABLESTEAMWORKS
    SteamNetworkingSocketsTransport steam; // added the first time a Steam session starts
#endif
```

After the `JoinDirect` method:

```csharp

#if !DISABLESTEAMWORKS
    /// <summary>
    /// Hosts a match that players join over Steam (peer to peer through Steam's relay), with the host's Steam ID
    /// </summary>
    /// <returns>False without Steam, when a session is already running, or when Netcode can't start</returns>
    public bool HostSteam()
    {
        return UseSteam() && StartHost();
    }

    /// <summary>
    /// Joins a match hosted with HostSteam. The session becomes a client once the host lets it in; Ended says why if it doesn't
    /// </summary>
    /// <param name="hostSteamId">The host's Steam ID (the Steam lobby's owner)</param>
    /// <returns>False without Steam, when a session is already running, or when Netcode can't start</returns>
    public bool JoinSteam(ulong hostSteamId)
    {
        if (!UseSteam())
            return false;

        steam.ConnectToSteamID = hostSteamId;
        return StartClient();
    }

    // Switches Netcode to the Steam transport. Without Steam, Steamworks throws as soon as the transport starts
    bool UseSteam()
    {
        if (IsRunning)
            return false;

        if (!SteamManager.Initialized)
        {
            Debug.LogWarning(NO_STEAM);
            return false;
        }

        if (steam == null)
            steam = gameObject.AddComponent<SteamNetworkingSocketsTransport>();
        Network.NetworkConfig.NetworkTransport = steam;
        return true;
    }
#endif
```

- [ ] **Step 4: Run them to watch them pass**

Run: `bash tools/run-tests.sh OnlineSession`
Expected: `tests: 18 total, 18 passed, 0 failed`.

Files: `Assets/Scripts/Online/OnlineSession.cs`, `Assets/Tests/Editor/OnlineSessionTests.cs`. No commit.

---

### Task 6: Two-editor testing menu and docs

**Files:**
- Create: `Assets/Scripts/Editor/OnlineTestMenu.cs` (+ `.meta`), `docs/online.md`
- Test: `Assets/Tests/Editor/OnlineTestMenuTests.cs` (+ `.meta`)
- Modify: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`

**Interfaces:**
- Consumes: `OnlineSession.Create`, `HostDirect`, `JoinDirect`, `Leave`, `IsRunning`, `Direct`, `DIRECT_PORT`.
- Produces: `public static class OnlineTestMenu` (`Assembly-CSharp-Editor`, like the tests), with:
  - `internal static bool BadConnection { get; set; }` (`SessionState`: remembered until the editor closes)
  - `internal static OnlineSession Prepare()` (the menu's session, set up for its next start)
  - `internal const int BAD_DELAY_MS = 150;`
  - `internal const int BAD_LOSS_PERCENT = 1;`

- [ ] **Step 1: Write the failing tests**

`Assets/Tests/Editor/OnlineTestMenuTests.cs` (then `bash tools/newmeta.sh Assets/Tests/Editor/OnlineTestMenuTests.cs`):

```csharp
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    public class OnlineTestMenuTests
    {
        bool badConnectionBefore;
        OnlineSession session;

        [SetUp]
        public void SetUp()
        {
            badConnectionBefore = OnlineTestMenu.BadConnection;
        }

        [TearDown]
        public void TearDown()
        {
            OnlineTestMenu.BadConnection = badConnectionBefore;
            if (session != null)
                Object.DestroyImmediate(session.gameObject);
        }

        [Test]
        public void Prepare_WithBadConnectionTicked_Adds150msAnd1PercentLoss()
        {
            OnlineTestMenu.BadConnection = true;

            session = OnlineTestMenu.Prepare();

            Assert.AreEqual(150, session.Direct.DebugSimulator.PacketDelayMS);
            Assert.AreEqual(1, session.Direct.DebugSimulator.PacketDropRate);
        }

        [Test]
        public void Prepare_AfterBadConnectionIsUnticked_AddsNothing()
        {
            OnlineTestMenu.BadConnection = true;
            session = OnlineTestMenu.Prepare();
            OnlineTestMenu.BadConnection = false;

            OnlineSession again = OnlineTestMenu.Prepare();

            Assert.AreSame(session, again, "the menu keeps one session");
            Assert.AreEqual(0, again.Direct.DebugSimulator.PacketDelayMS);
            Assert.AreEqual(0, again.Direct.DebugSimulator.PacketDropRate);
        }
    }
}
```

- [ ] **Step 2: Run them to watch them fail**

Run: `bash tools/run-tests.sh OnlineTestMenuTests`
Expected: `NO RESULTS` with `error CS0103: The name 'OnlineTestMenu' does not exist in the current context`.

- [ ] **Step 3: Write the menu**

`Assets/Scripts/Editor/OnlineTestMenu.cs` (then `bash tools/newmeta.sh Assets/Scripts/Editor/OnlineTestMenu.cs`):

```csharp
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools → Dead on Arrival → Online: hosts or joins a session between two editors on this computer (ParrelSync) before
/// the online menus exist, and can make the connection bad on purpose. Play Mode only. See docs/online.md
/// </summary>
public static class OnlineTestMenu
{
    const string MENU = "Tools/Dead on Arrival/Online/";
    const string BAD_CONNECTION_ITEM = MENU + "Bad Connection (150 ms, 1% Loss)";
    const string BAD_CONNECTION_KEY = "DoA.Online.BadConnection";
    const string THIS_COMPUTER = "127.0.0.1";

    /// <summary>Delay and packet loss the Bad Connection toggle adds (the roadmap's lag test)</summary>
    internal const int BAD_DELAY_MS = 150;
    internal const int BAD_LOSS_PERCENT = 1;

    static OnlineSession session;

    /// <summary>Whether the next Host or Join simulates a bad connection (remembered until the editor closes)</summary>
    internal static bool BadConnection
    {
        get { return SessionState.GetBool(BAD_CONNECTION_KEY, false); }
        set { SessionState.SetBool(BAD_CONNECTION_KEY, value); }
    }

    [MenuItem(MENU + "Host", false, 1)]
    static void Host()
    {
        if (!Prepare().HostDirect(THIS_COMPUTER, OnlineSession.DIRECT_PORT))
            Debug.LogWarning("Online: couldn't host. Is another editor hosting on port " + OnlineSession.DIRECT_PORT + "?");
    }

    [MenuItem(MENU + "Join This Computer", false, 2)]
    static void Join()
    {
        Prepare().JoinDirect(THIS_COMPUTER, OnlineSession.DIRECT_PORT);
    }

    [MenuItem(MENU + "Leave", false, 3)]
    static void Leave()
    {
        session.Leave();
    }

    [MenuItem(MENU + "Host", true)]
    [MenuItem(MENU + "Join This Computer", true)]
    static bool CanStart()
    {
        return Application.isPlaying && (session == null || !session.IsRunning);
    }

    [MenuItem(MENU + "Leave", true)]
    static bool CanLeave()
    {
        return Application.isPlaying && session != null && session.IsRunning;
    }

    [MenuItem(BAD_CONNECTION_ITEM, false, 20)]
    static void ToggleBadConnection()
    {
        BadConnection = !BadConnection;
    }

    [MenuItem(BAD_CONNECTION_ITEM, true)]
    static bool ShowBadConnection()
    {
        Menu.SetChecked(BAD_CONNECTION_ITEM, BadConnection);
        return true;
    }

    /// <summary>
    /// The session the menu uses (made on first use), set up with or without a bad connection for its next start
    /// </summary>
    internal static OnlineSession Prepare()
    {
        if (session == null)
            session = OnlineSession.Create(Application.version);

        bool bad = BadConnection;
        session.Direct.SetDebugSimulatorParameters(bad ? BAD_DELAY_MS : 0, 0, bad ? BAD_LOSS_PERCENT : 0);
        return session;
    }
}
```

- [ ] **Step 4: Run them to watch them pass**

Run: `bash tools/run-tests.sh OnlineTestMenuTests`
Expected: `tests: 2 total, 2 passed, 0 failed`.

- [ ] **Step 5: Write `docs/online.md`**

```markdown
# Online play

Online multiplayer is being built in steps (roadmap Phase 3: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`). Local split-screen doesn't touch any of it: nothing online runs unless a session starts.

## What's there so far (Phase 3A)

- Packages:
  - Netcode for GameObjects **1.15.1**, with Unity Transport 1.5.0. 1.15 is the newest line for Unity 2022.3; 2.x needs Unity 6.
  - The community **SteamNetworkingSockets** transport, pinned to commit `d862504b`. Its newer commit targets Unity 6's transport.
  - **ParrelSync 1.5.3** (editor only).
- `OnlineSession` (`Assets/Scripts/Online/OnlineSession.cs`) hosts or joins a match and keeps `GameAuthority.Role` in step: Host or Client while a session runs, Offline otherwise.
  - Direct, by IP address (Unity Transport): `HostDirect` / `JoinDirect`. For the editor and LAN.
  - Steam, peer to peer through Steam's relay: `HostSteam` / `JoinSteam(hostSteamId)`. For builds with Steam running. The Steam lobby that hands out the host's Steam ID is roadmap Task 3.2.
  - `Leave` ends a session.
  - `Ended` says why a session ended by itself: "The host left the match.", "Couldn't reach the host.", the host's reason for turning a player away, or (on the host) "The connection was lost."
- `JoinRules` (`Assets/Scripts/Online/JoinRules.cs`): the host lets a player in only if all three hold:
  - They're on the same build (`Application.version`).
  - There's a free seat (4 players).
  - The host is still on the title screen, in options or credits, or in player select.
- There are no scooters online yet: after connecting, each machine still plays its own local game (roadmap Tasks 3.3–3.7).

## Two editors on one computer (ParrelSync)

- **ParrelSync → Clones Manager → Add new clone** (once). The clone shares this project's Assets, Packages and ProjectSettings. Unity imports the project the first time the clone opens, which takes a while.
- **Open in New Editor**.
- Press Play in both editors (from `SplashScreen`, as usual).
- Editor 1: **Tools → Dead on Arrival → Online → Host**. The Console shows `Online: hosting version 1.0`.
- Editor 2: **Tools → Dead on Arrival → Online → Join This Computer**. It shows `Online: joined the host`; editor 1 shows `Online: player 1 joined (2 in)`.
- **Leave** ends a session. When the host leaves, editor 2 shows `Online: session ended: The host left the match.`
- Only change files in the original editor: clones share them.

## Bad connection

- **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)**: while ticked, the next Host or Join in that editor adds 150 ms of delay and 1% packet loss (Unity Transport's own simulator; editor only). The tick lasts until the editor closes.
- For builds, use a tool like *clumsy* (Windows).

## Known limits

- A player whose build has a different Netcode setup (tick rate, network prefabs…) is dropped by Netcode before the version check, and sees "Couldn't reach the host." The Steam lobby's build filter (roadmap Task 3.2) keeps such players apart.
- Hosting fails if another program uses port 7777.
- Direct joins give up after 10 seconds.
```

- [ ] **Step 6: Update the roadmap**

In `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`:
- Under `## Phase 3 — Online multiplayer (Netcode for GameObjects + Steam)`, add a progress line:
  `Progress (2026-09-24): Phase 3A (`2026-09-24-phase3a-online-session.md`) — Task 3.1, plus the online session: host / join / leave, join rules, roles and end reasons (`OnlineSession`, `JoinRules`, `docs/online.md`).`
- Task 3.1: tick all three and append:
  - bullet 1: ` *(1.15.1, with Unity Transport 1.5.0.)*`
  - bullet 2: ` *(`OnlineSession.HostDirect` / `JoinDirect` use Unity Transport; `HostSteam` / `JoinSteam` use the Steam transport, pinned at `d862504b`, the last commit built for Netcode 1.x.)*`
  - bullet 3: ` *(ParrelSync 1.5.3. Unity Transport 1.5's own simulator instead of Multiplayer Tools: **Tools → Dead on Arrival → Online → Bad Connection** adds 150 ms / 1%. See `docs/online.md`.)*`
- Task 3.2, bullet 3: append ` *(`OnlineSession.HostSteam` / `JoinSteam(hostSteamId)` exist, Phase 3A.)*`
- Task 3.8, bullet 2: append ` *(The client's session ends with `OnlineSession.HOST_LEFT`; returning to the menu with the message is still to do.)*`
- Task 3.8, bullet 3: append ` *(Approval half done: `JoinRules` compares `Application.version`. A build whose Netcode setup differs is dropped by Netcode before approval, with no reason, so the lobby filter matters.)*`
- Partner steps → Checks: `Run All: 167 passed` → `Run All: 208 passed`.

- [ ] **Step 7: Full suite**

Run: `bash tools/run-tests.sh`
Expected: `tests: 208 total, 208 passed, 0 failed`.

Files: `Assets/Scripts/Editor/OnlineTestMenu.cs` + `.meta`, `Assets/Tests/Editor/OnlineTestMenuTests.cs` + `.meta`, `docs/online.md`, the roadmap. No commit.

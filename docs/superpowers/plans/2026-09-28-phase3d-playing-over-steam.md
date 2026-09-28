# Phase 3D — Playing Over Steam Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Friends play online over Steam. In player select, a player presses Y to host a friends-only Steam lobby and invite friends. Friends join by accepting the invite, or with "Join Game" in Steam's friends list. B leaves. Players land back in player select after each online match. Local split-screen stays identical.

**Architecture:**
- **`OnlineLobby`** (plain C#) runs the lobby flow over two interfaces, so it's tested without Steam:
  - `ILobbyService`: Steam's lobbies (`SteamLobbyService` in the game, a fake in tests).
  - `ISessionControl`: starting the game's `OnlineSession` (over Steam in the game; by IP address in tests and editor tools).
- **The host:** makes a lobby, tags it `game=doa` and `build=<version>`, and hosts the session in it.
- **A joiner:** enters the lobby, checks the tags, and joins the session of the lobby's owner.
- **The lobby is left** whenever the session stops.
- **`OnlinePlay`** (a MonoBehaviour made at launch) is the game's glue:
  - It owns the Steam session and `OnlineLobby`.
  - It answers Steam's join requests and the `+connect_lobby` launch argument once the title screen is up.
  - It keeps the lobby's joinability in step with the game state, and shows a hint line in player select (`LobbyPrompt`).
- **Buttons:**
  - A new "North Face" action in the UI map brings the Y button to the menus.
  - In player select, Y calls `OnlinePlay.PlayOnline` (host, or invite once online).
  - B while online and not ready leaves the session (`OnlineGame.LeaveOnline`).
- **Messages** use `ControllerPrompts`' existing bottom hint bar.

**Tech Stack:** Unity 2022.3.62f3 · Netcode for GameObjects 1.15.1 · Steamworks.NET 2025.164.1 (`SteamMatchmaking`, `SteamFriends`) · community SteamNetworkingSockets transport (`d862504b`) · Input System 1.14.0 · Unity Test Framework 1.1.33.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap), **Task 3.2**:
- Bullet 1:
  - Main menu → Play Online → *Host*: a friends-only or public lobby, 4 slots, metadata `game=doa` and `build=<version>`.
  - Or *Join*: an overlay invite via `GameLobbyJoinRequested_t`, or a lobby list filtered by `game` + `build`.
  - Ruling 1 picks friends-only lobbies and Steam's invites. Ruling 2 moves "Play Online" into player select.
- Bullet 2: the lobby screen reuses player select. Phase 3B did it: seats give companies, and colour, hat and ready sync.
- Bullet 3: the host starts the session; clients connect to the host's SteamID.
  - `OnlineSession.HostSteam` and `JoinSteam(hostSteamId)` exist (Phase 3A).
  - The match start is Phase 3C's host-coordinated load.
- Also Task 3.8 bullet 2, in part: when the host leaves, clients see "The host left the match."

## Global Constraints

- **Stay on Unity 2022.3 LTS:** Netcode for GameObjects **1.x**; Steamworks.NET stays **2025.164.1** (a version bump is a Steam API change: it goes to `main`, after asking).
- **Players:** online is one player per machine until Task 3.9 (`OnlineGame.CanGoOnline`).
- **Local split-screen must behave exactly as before:**
  - `LocalMatchSmokeTest` passes after every task.
  - Nothing online runs unless a player presses Y or accepts an invite.
- **Online rules:** no mid-match joining (`JoinRules`, Phase 3A). Lobby tags: key `game` = `doa`, key `build` = `Application.version`. Lobbies are friends-only with room for 4 (`Constants.MAX_PLAYERS`).
- **Steam API files stay untouched:** `SteamManager.cs` and `SteamStartup.cs` live on `main` too (memory `branch-policy.md`). Steam lobby code goes in new files on `steam-phase1a`.
- **Two Unity editors are open on the real project:** the user's editor, plus a ParrelSync clone that shares `Assets/`.
  - Never edit an existing scene, prefab or ProjectSettings file.
  - `Assets/Resources/CapstoneYear4.inputactions` is a JSON asset, not one of those. Edit it as text, keeping its CRLF endings and 4-space JSON style.
- **No git commits:** your human partner commits and pushes.
- **Meta files:** every new file under `Assets/` gets a `.meta` with a fresh GUID: `bash tools/newmeta.sh <path>`.
- **Line endings:**
  - Files marked `i/crlf` in `git ls-files --eol` must stay CRLF, so edit them with the Edit tool only. In this plan: `MenuInteractions.cs` and `MainMenu.cs`.
  - Never use `sed -i` on scripts.
  - New files may be LF.
- **Tests:**
  - Run them with `bash tools/run-tests.sh [filter]`. They run in the mirror (`../_doa_test_mirror/CapstoneYear4`). Unity takes 2–3 minutes to start, and the full suite about 25 minutes, so run long ones in the background.
  - A compile error prints `NO RESULTS` plus the `error CS…` lines.
  - Before a long run, check the other local Claude session ("Controller connection issue", `ListAgents`): it shares the mirror.
- **Play Mode tests:**
  - A test resumes in a reloaded script domain after `yield return new EnterPlayMode()`.
  - Never write a lambda that captures a local of the test method: after the reload, **even assigning** such a local throws a bare `NullReferenceException`. Use static helper methods, and recorder classes whose methods are subscribed as method groups.
  - Wait by time, never by frame count.
  - Two-machine tests call `log.MachinesLeave()` before the first machine leaves (`LogCollector.CLOSED_PORT`).
- **The tests can't see `internal` members** (no asmdefs: game code is `Assembly-CSharp`, tests are `Assembly-CSharp-Editor`). What tests call is `public`, documented as such; private state is read through `Reflect`.
- **Branch:** `steam-phase1a`.
- **The starting working tree:** it holds the uncommitted 2026-09-28 online start fixes (city spawns, the loading-screen leave). Leave them; they ship with this phase.

## Rulings (decided while planning)

1. **Friends-only lobbies; friends join through Steam** (an accepted invite, or "Join Game" in the friends list: both raise `GameLobbyJoinRequested_t`, or start the game with `+connect_lobby <id>`).
   - No public lobbies or in-game lobby list yet.
   - Cost if wrong: strangers can't find matches until a lobby-list task.
2. **"Play Online" is Y in player select, not a new title-screen row.**
   - The title's rows (Play, Options, Credits, Quit) are hand-lettered sprites, so a new row needs new art.
   - Player select is already the online lobby (Phase 3B). Y there hosts, then invites. A line along the top says so.
   - Cost if wrong: players must spot the line. Once there's art, a title row can go to player select and call `OnlinePlay.PlayOnline`.
3. **Y comes to the menus through a new UI action, "North Face" (`<Gamepad>/buttonNorth`), wired in code** (`PlayerUIHandler.Start`).
   - `PlayerUIHandler.NorthFaceTrigger` exists, but nothing calls it.
   - The player prefab's event list can't be edited while the editors are open.
   - Cost if wrong: none. The prefab can list the action later.
4. **Messages reuse `ControllerPrompts`' bottom hint bar.** The lobby line is a new runtime-built `LobbyPrompt` along the top, in the same plain style (TMP's default font).
   - Cost if wrong: a plain look until there's art.
5. **B in the online player select (not ready) leaves the session.**
   - The host's B ends it for everyone. The others see "The host left the match." and stay in their player select, offline.
   - After an online match, the menu scene opens on player select, not the title. So between matches everyone is in the lobby, where Y and B work.
   - Cost if wrong: a host can't sit on the title screen and keep its friends.
6. **The Steam lobby follows the session:**
   - It's left whenever the session stops (left, ended, or never let in).
   - A lobby that's made or entered after the player has moved on is left at once, never hosted or joined.
   - The host's lobby is joinable only while the host is in the menus.
   - Cost if wrong: none.
7. **`OnlinePlay` owns the game's Steam session.** The editor's **Tools → Dead on Arrival → Online** keeps its own direct session.
   - `OnlineGame.LeaveOnline` leaves whichever one this machine plays, so B works for both.
   - Cost if wrong: two session objects in the editor, one idle.
8. **Tests use direct sessions under a fake lobby service.**
   - A Steam account can't connect to itself, so two editors on one PC can't test Steam.
   - The Steam path is checked by hand on two PCs with two Steam accounts (`EDITOR-TODO.md`).
   - Cost if wrong: a Steamworks detail (callback timing, overlay) only shows up in that manual check.

## Review Focus

- **An invite accepted during a local match** → a notice ("finish your match"); the match isn't touched. (Task 1 `Busy_InAMatch_…`, Task 2 `Answer_WhileBusy_…`.)
- **Steam answering late:** a lobby made after the player backed out, or entered after a local match began → left at once, never hosted or joined. (Task 2 `Host_PlayerSelectClosesBeforeTheLobbyIsMade_…`, `Answer_ALocalMatchStartsBeforeTheLobbyAnswers_…`.)
- **A lobby from another build or another game:** App 480 is shared by every Spacewar developer. → Left, with a message naming both versions. (Task 2 `Answer_ALobbyOfAnotherBuild_…`, `Answer_AnotherGamesLobby_…`.)
- **Two invites in a row** → the latest one wins; the other lobby is left. (Task 2 `Answer_TwoRequests_…`.)
- **Two players here pressing Y** → "one player per machine", and no lobby. (Task 5 `Hosting_…`.)

## File Structure

- Create `Assets/Scripts/Online/LobbyRules.cs`: the lobby's tags and messages, whether a lobby can be joined, the `+connect_lobby` argument, whether this machine can answer an invite now.
- Create `Assets/Scripts/Online/ILobbyService.cs`: a lobby provider (Steam in the game).
- Create `Assets/Scripts/Online/ISessionControl.cs`: how the lobby flow starts the game's session.
- Create `Assets/Scripts/Online/OnlineLobby.cs`: the lobby flow.
- Create `Assets/Scripts/Online/SteamLobbyService.cs`: `ILobbyService` over Steamworks.NET.
- Create `Assets/Scripts/Online/OnlinePlay.cs`: the game's glue (made at launch).
- Create `Assets/Scripts/UI/LobbyPrompt.cs`: the runtime-built line along the top of the screen.
- Modify `Assets/Resources/CapstoneYear4.inputactions`: the UI map's "North Face" action.
- Modify `Assets/Scripts/Menu/PlayerUIHandler.cs`: wire "North Face" to `NorthFaceTrigger`.
- Modify `Assets/Scripts/Menu/MenuInteractions.cs` (`i/crlf`): Y and B in player select.
- Modify `Assets/Scripts/Menu/MainMenu.cs` (`i/crlf`): online, the menu scene opens on player select.
- Modify `Assets/Scripts/Online/OnlineGame.cs`: `LeaveOnline`, and session-end notices.
- Tests:
  - Create `LobbyRulesTests`, `OnlineFakes` (the two fakes), `OnlineLobbyTests`, `SteamLobbyServiceTests`, `MenuButtonsTests`, `LobbyPromptTests`, `OnlinePlayTests` and `OnlinePlayNetworkTests` (port 7797).
  - Modify `MainMenuTests` and `OnlineMatchNetworkTests`.
  - All under `Assets/Tests/Editor/`.
- Docs: `docs/online.md`, `docs/testing.md`, the roadmap, `EDITOR-TODO.md`.

---

### Task 1: Lobby rules

**Files:**
- Create: `Assets/Scripts/Online/LobbyRules.cs`
- Test: `Assets/Tests/Editor/LobbyRulesTests.cs`

**Interfaces:**
- Consumes: `JoinRules.Refusal(string hostVersion, string joinerVersion, int seatsTaken, GameState hostState)` (Phase 3A), for the version wording.
- Produces: `public static class LobbyRules` with
  - `const string GAME_KEY = "game"`, `GAME = "doa"`, `BUILD_KEY = "build"`, `CONNECT_LOBBY = "+connect_lobby"`
  - `const string NOT_THIS_GAME = "That lobby isn't a Dead on Arrival match."`
  - `const string BUSY = "Finish your match before joining a friend's."`
  - `const string ALREADY_ONLINE = "Leave your online match before joining another."`
  - `const string ONE_PLAYER = "Online play is one player per machine: the other players here need to leave first."`
  - `static string Refusal(string lobbyGame, string lobbyBuild, string myBuild)`: null = join.
  - `static ulong LobbyToJoin(string[] args)`: 0 = none.
  - `static bool InTheMenus(GameState state)`: `Default`, `Menu`, `Options`, `Credits`, `PlayerSelect`.
  - `static string Busy(GameState state, int localPlayers, bool online)`: null = free. Online beats a match, which beats the player count.

- [ ] **Step 1: Write the failing tests** (`namespace DoA.Tests`, `public class LobbyRulesTests`)

```csharp
[Test] public void Refusal_ThisGameAndBuild_LetsThePlayerIn()
{ Assert.IsNull(LobbyRules.Refusal("doa", "1.2", "1.2")); }

[Test] public void Refusal_AnotherGamesLobby_IsTurnedDown()
{ Assert.AreEqual(LobbyRules.NOT_THIS_GAME, LobbyRules.Refusal("spacewar", "1.2", "1.2")); }

[Test] public void Refusal_AnotherBuild_NamesBothVersions_LikeADirectJoin()
{
    string refusal = LobbyRules.Refusal("doa", "1.1", "1.2");
    StringAssert.Contains("1.1", refusal);
    StringAssert.Contains("1.2", refusal);
    Assert.AreEqual(JoinRules.Refusal("1.1", "1.2", 0, GameState.Menu), refusal);
}

[Test] public void LobbyToJoin_SteamsConnectArgument_GivesTheLobby()
{ Assert.AreEqual(109775241021923456UL, LobbyRules.LobbyToJoin(new[] { "DoA.exe", "+connect_lobby", "109775241021923456" })); }

[Test] public void LobbyToJoin_WithoutTheArgument_IsZero()
{
    Assert.AreEqual(0UL, LobbyRules.LobbyToJoin(new[] { "DoA.exe" }));
    Assert.AreEqual(0UL, LobbyRules.LobbyToJoin(null));
}

[Test] public void LobbyToJoin_NoNumberAfterIt_IsZero()
{
    Assert.AreEqual(0UL, LobbyRules.LobbyToJoin(new[] { "DoA.exe", "+connect_lobby", "abc" }));
    Assert.AreEqual(0UL, LobbyRules.LobbyToJoin(new[] { "DoA.exe", "+connect_lobby" }));
}

[TestCase(GameState.Menu)] [TestCase(GameState.PlayerSelect)] [TestCase(GameState.Options)] [TestCase(GameState.Credits)]
public void Busy_InTheMenus_WithOnePlayer_IsFree(GameState state)
{ Assert.IsNull(LobbyRules.Busy(state, 1, false)); }

[TestCase(GameState.Loading)] [TestCase(GameState.MainLoop)] [TestCase(GameState.Results)] [TestCase(GameState.Paused)]
public void Busy_InAMatch_SaysFinishItFirst(GameState state)
{ Assert.AreEqual(LobbyRules.BUSY, LobbyRules.Busy(state, 1, false)); }

[Test] public void Busy_NobodyHereYet_IsFree()
{ Assert.IsNull(LobbyRules.Busy(GameState.Menu, 0, false)); }

[Test] public void Busy_TwoPlayersHere_SaysOnePlayerPerMachine()
{ Assert.AreEqual(LobbyRules.ONE_PLAYER, LobbyRules.Busy(GameState.PlayerSelect, 2, false)); }

[Test] public void Busy_AlreadyOnline_SaysLeaveFirst()
{ Assert.AreEqual(LobbyRules.ALREADY_ONLINE, LobbyRules.Busy(GameState.MainLoop, 1, true)); }
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh LobbyRulesTests`
Expected: `NO RESULTS` with `error CS0103: The name 'LobbyRules' does not exist`.

- [ ] **Step 3: Implement `LobbyRules`** with the signatures and values above.
  - `Refusal`: the game tag first, then the build (return `JoinRules.Refusal(lobbyBuild, myBuild, 0, GameState.Menu)`: the host's build is the lobby's).
  - `LobbyToJoin`: the number right after `CONNECT_LOBBY` (`ulong.TryParse`).
  - Add `bash tools/newmeta.sh Assets/Scripts/Online/LobbyRules.cs` and a meta for the test file.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh LobbyRulesTests`
Expected: `tests: 17 total, 17 passed, 0 failed, 0 skipped`.

- [ ] **Step 5: No commit** (your human partner commits).

---

### Task 2: The lobby flow

**Files:**
- Create: `Assets/Scripts/Online/ILobbyService.cs`, `Assets/Scripts/Online/ISessionControl.cs`, `Assets/Scripts/Online/OnlineLobby.cs`
- Test: `Assets/Tests/Editor/OnlineFakes.cs`, `Assets/Tests/Editor/OnlineLobbyTests.cs`

**Interfaces:**
- Consumes: Task 1's `LobbyRules` (tags, `Refusal`, `InTheMenus`); `NetworkRole` (Phase 2B).
- Produces:

```csharp
public interface ILobbyService
{
    bool Available { get; }                        // lobbies work here (Steam is running)
    void Create(int maxMembers);                   // a friends-only lobby; Created follows (0 = it failed)
    event Action<ulong> Created;
    void Join(ulong lobby);                        // Entered follows
    event Action<ulong, bool> Entered;             // lobby, whether this machine got in
    event Action<ulong> JoinRequested;             // the player accepted an invite or picked "Join Game"
    void Leave(ulong lobby);
    void SetData(ulong lobby, string key, string value);
    string GetData(ulong lobby, string key);       // "" when unknown
    ulong Owner(ulong lobby);                      // 0 when unknown
    void SetJoinable(ulong lobby, bool joinable);
    bool Invite(ulong lobby);                      // false when the invite dialog can't open (Steam's overlay is off)
}

public interface ISessionControl
{
    bool IsRunning { get; }
    bool Host();                                   // false when the session can't start
    bool Join(ulong hostId);                       // the lobby owner's ID (a Steam ID over Steam)
    event Action<NetworkRole> RoleChanged;         // as OnlineSession.RoleChanged
    event Action<string> Ended;                    // as OnlineSession.Ended
}

public class OnlineLobby
{
    public const string NO_STEAM = "Start Steam to play online.";
    public const string LOBBY_FAILED = "Steam couldn't open a lobby. Try again.";
    public const string HOST_FAILED = "Couldn't start the online match. Try again.";
    public const string JOIN_FAILED = "Couldn't join that match. It may have ended.";
    public const string INVITE_FROM_STEAM = "Invite friends from your Steam friends list.";

    public OnlineLobby(ILobbyService lobbies, ISessionControl session, string version);
    public ulong Current { get; }                  // the lobby this machine is in (0 = none)
    public bool IsHost { get; }
    public event Action<string> Notice;            // a message for the player
    public void Host();                            // make a lobby, host in it, open the invite dialog
    public void Answer(ulong lobby, string busy);  // a lobby to join; busy = LobbyRules.Busy's reason (null = free)
    public void Invite();                          // the invite dialog for Current (nothing without a lobby)
    public void ShowState(GameState state);        // the game's state, every time it changes
}
```

- Behaviour the tests pin:
  - `Host` does nothing while a lobby is being made, or while `Current != 0` or the session runs.
  - `ShowState` cancels a lobby still being made once the state leaves `PlayerSelect`, and a join once the state leaves the menus (`!InTheMenus`).
  - On the host, it sets the lobby joinable exactly when `InTheMenus(state)`.
  - A lobby that answers after being cancelled or overtaken is left.
  - The lobby is left on `RoleChanged(Offline)` and on `Ended`, and `Current` goes back to 0.
  - `Notice` isn't raised for session ends: `OnlineGame` shows those (Task 5).

- [ ] **Step 1: Write the fakes** in `OnlineFakes.cs` (`namespace DoA.Tests`). `FakeLobbyService : ILobbyService`:
  - Recorded calls: `List<int> Creates`, `List<ulong> Joins`, `Left`, `Invites`.
  - Lobby state: `Dictionary<ulong, Dictionary<string, string>> Data`, `Dictionary<ulong, ulong> Owners`, `Dictionary<ulong, bool> Joinable`.
  - Switches: `bool Available = true`, `bool InviteWorks = true`, `ulong NextLobby = 42`, and `bool AutoAnswer`: `Create` raises `Created(NextLobby)` and `Join` raises `Entered(lobby, Data.ContainsKey(lobby))` at once.
  - Helpers: `RaiseCreated(ulong)`, `RaiseEntered(ulong, bool)`, `RaiseJoinRequested(ulong)`, `AddLobby(ulong lobby, string game, string build, ulong owner)`.
  - `SetData` makes the lobby's dictionary if it's missing.

  `FakeSessionControl : ISessionControl`:
  - `bool HostWorks = true`, `JoinWorks = true`; `int Hosts`; `List<ulong> JoinedHosts`.
  - `Host()` counts, and when it works sets `IsRunning` and raises `RoleChanged(Host)`.
  - `Join(id)` records, and sets `IsRunning = JoinWorks`.
  - `Stop(string reason)`: `IsRunning = false`; raises `RoleChanged(Offline)`, then `Ended(reason)` when the reason isn't null.
  - `TurnAway(string reason)`: `IsRunning = false`, then `Ended` only (a joiner never let in).

- [ ] **Step 2: Write the failing tests** (`OnlineLobbyTests`).
  - Fixture: `lobbies = new FakeLobbyService()`, `session = new FakeSessionControl()`, `lobby = new OnlineLobby(lobbies, session, "1.2")`.
  - A `List<string> notices` filled by a `Notice` handler method.
  - SetUp ends with `lobby.ShowState(GameState.PlayerSelect)`: the player is in player select.
  - `Hosting()` helper: `lobby.Host(); lobbies.RaiseCreated(42);`.

```csharp
Host_MakesAFriendsOnlyLobbyForFour_TagsItWithTheGameAndBuild_AndHostsInIt
    Hosting();
    CollectionAssert.AreEqual(new[] { 4 }, lobbies.Creates);
    Assert.AreEqual("doa", lobbies.Data[42]["game"]);  Assert.AreEqual("1.2", lobbies.Data[42]["build"]);
    Assert.AreEqual(1, session.Hosts);  Assert.AreEqual(42UL, lobby.Current);  Assert.IsTrue(lobby.IsHost);
    CollectionAssert.AreEqual(new[] { 42UL }, lobbies.Invites, "the invite dialog opens");
Host_WithoutSteam_SaysSo_AndMakesNoLobby            // Available = false → notices == [NO_STEAM], Creates empty
Host_LobbyCantBeMade_SaysSo_AndDoesntHost           // RaiseCreated(0) → notices == [LOBBY_FAILED], Hosts == 0, Current == 0
Host_SessionWontStart_LeavesTheLobby_AndSaysSo      // HostWorks = false → Left == [42], notices == [HOST_FAILED], Current == 0
Host_PressedAgainWhileTheLobbyIsMade_MakesOnlyOne   // Host(); Host() → Creates.Count == 1
Host_PlayerSelectClosesBeforeTheLobbyIsMade_LeavesIt_AndDoesntHost
                                                    // Host(); ShowState(Menu); RaiseCreated(42) → Left == [42], Hosts == 0, Current == 0
Invite_WithoutSteamsOverlay_PointsToTheFriendsList  // InviteWorks = false; Hosting() → notices == [INVITE_FROM_STEAM]
Answer_JoinsTheLobby_ThenItsOwnersSession           // AddLobby(7, "doa", "1.2", 99); Answer(7, null) → Joins == [7];
                                                    // RaiseEntered(7, true) → JoinedHosts == [99], Current == 7, !IsHost
Answer_WhileBusy_SaysWhy_AndJoinsNothing            // Answer(7, LobbyRules.BUSY) → notices == [BUSY], Joins empty
Answer_ALobbyOfAnotherBuild_LeavesIt_AndSaysWhy     // AddLobby(7, "doa", "1.1", 99) → Left == [7], JoinedHosts empty,
                                                    // notices == [LobbyRules.Refusal("doa", "1.1", "1.2")], Current == 0
Answer_AnotherGamesLobby_LeavesIt                   // game "spacewar" → Left == [7], notices == [NOT_THIS_GAME]
Answer_LobbyCantBeEntered_SaysSo                    // RaiseEntered(7, false) → notices == [JOIN_FAILED], Current == 0, Left empty
Answer_TwoRequests_FollowsTheLatest_AndLeavesTheFirst
                                                    // lobbies 7 (owner 99) and 8 (owner 98); Answer(7); Answer(8);
                                                    // RaiseEntered(7, true) → Left == [7], JoinedHosts empty;
                                                    // RaiseEntered(8, true) → JoinedHosts == [98], Current == 8
Answer_ALocalMatchStartsBeforeTheLobbyAnswers_LeavesIt
                                                    // Answer(7); ShowState(Loading); RaiseEntered(7, true) → Left == [7], JoinedHosts empty
Answer_SessionWontStart_LeavesTheLobby              // JoinWorks = false → Left == [7], notices == [JOIN_FAILED]
Answer_TheLobbyThisMachineIsIn_IsIgnored            // Hosting(); Answer(42, null) → Joins empty, notices empty
SessionStops_LeavesTheLobby                         // Hosting(); session.Stop(null) → Left == [42], Current == 0
SessionTurnsTheJoinerAway_LeavesTheLobby            // joined 7; session.TurnAway(JoinRules.FULL) → Left == [7], Current == 0, notices empty
ShowState_TheMatchStarts_ClosesTheHostsLobby_TheMenusReopenIt
                                                    // Hosting(); ShowState(Loading) → Joinable[42] false; ShowState(PlayerSelect) → true
ShowState_OnAClient_LeavesJoinabilityAlone          // joined 7; ShowState(Loading) → Joinable has no key 7
```

- [ ] **Step 3: Run to verify they fail**

Run: `bash tools/run-tests.sh OnlineLobbyTests`
Expected: `NO RESULTS` with `error CS0246: The type or namespace name 'ILobbyService' could not be found`.

- [ ] **Step 4: Implement** the two interfaces and `OnlineLobby` (with metas).
  - `OnlineLobby` keeps the lobby being made (`creating`), the lobby being joined (`joining`, 0 = none), the last state it was shown, and `Current` / `IsHost`.
  - After `Created`: tag the lobby (`SetData` ×2), then `session.Host()`, then `Invite()`.
  - After `Entered`: check `LobbyRules.Refusal(GetData(game), GetData(build), version)` before `session.Join(Owner(lobby))`.

- [ ] **Step 5: Run to verify they pass**

Run: `bash tools/run-tests.sh OnlineLobbyTests`
Expected: `tests: 20 total, 20 passed, 0 failed, 0 skipped`.

- [ ] **Step 6: No commit.**

---

### Task 3: Steam's lobbies

**Files:**
- Create: `Assets/Scripts/Online/SteamLobbyService.cs`
- Test: `Assets/Tests/Editor/SteamLobbyServiceTests.cs`

**Interfaces:**
- Consumes: Task 2's `ILobbyService`; `SteamManager.Initialized`.
- Produces: `public class SteamLobbyService : ILobbyService` with a public parameterless constructor.
  - `Available => SteamManager.Initialized`.
  - Without Steam (or built without Steamworks):
    - `Create` raises `Created(0)` and `Join` raises `Entered(lobby, false)`, both at once.
    - `Invite` returns false, `GetData` returns "" and `Owner` returns 0.
    - `Leave`, `SetData` and `SetJoinable` do nothing. No Steamworks call is made: they throw without Steam.

- [ ] **Step 1: Write the failing tests** (EditMode: `SteamManager` only exists in Play Mode, so Steam is never running here).

```csharp
[SetUp] public void NoSteam() { Assume.That(!SteamManager.Initialized); }

WithoutSteam_IsUnavailable                          // Assert.IsFalse(new SteamLobbyService().Available)
WithoutSteam_CreateAnswersAtOnce_WithNoLobby        // a recorder on Created; Create(4) → recorded == [0]
WithoutSteam_JoinAnswersAtOnce_NotEntered           // a recorder on Entered; Join(7) → recorded == [(7, false)]
WithoutSteam_TheRestIsSafe                          // GetData(7, "game") == "", Owner(7) == 0, Invite(7) == false;
                                                    // Assert.DoesNotThrow for Leave(7), SetData(7, "game", "doa"), SetJoinable(7, true)
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh SteamLobbyServiceTests`
Expected: `NO RESULTS` with `error CS0246: … 'SteamLobbyService' could not be found`.

- [ ] **Step 3: Implement `SteamLobbyService`.**
  - Copy `SteamManager.cs`'s `DISABLESTEAMWORKS` define block to the top, and wrap the Steamworks parts in `#if !DISABLESTEAMWORKS`.
  - Values the Steamworks API pins:
    - `SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, maxMembers)`, through a `CallResult<LobbyCreated_t>`. The lobby is `m_ulSteamIDLobby` when `!ioFailure && m_eResult == EResult.k_EResultOK`, else 0.
    - `SteamMatchmaking.JoinLobby(new CSteamID(lobby))`, through a `CallResult<LobbyEnter_t>`. It entered when `!ioFailure && m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess`.
    - Using a `CallResult` for the join means the `LobbyEnter_t` Steam also sends to a lobby's creator never looks like a join.
    - `Callback<GameLobbyJoinRequested_t>` raises `JoinRequested(m_steamIDLobby.m_SteamID)`. Create it in the constructor, only when `Available`.
    - Keep every `Callback` and `CallResult` in a field, so they aren't collected.
    - `Leave`, `SetData`, `GetData`, `Owner` and `SetJoinable` map to `SteamMatchmaking.LeaveLobby`, `SetLobbyData`, `GetLobbyData`, `GetLobbyOwner(…).m_SteamID` and `SetLobbyJoinable`.
    - `Invite`: false unless `SteamUtils.IsOverlayEnabled()`. Then `SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobby))`, and true.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh SteamLobbyServiceTests`
Expected: `tests: 4 total, 4 passed, 0 failed, 0 skipped`.

- [ ] **Step 5: No commit.**

---

### Task 4: The Y button in the menus

**Files:**
- Modify: `Assets/Resources/CapstoneYear4.inputactions` (UI map), `Assets/Scripts/Menu/PlayerUIHandler.cs`
- Test: `Assets/Tests/Editor/MenuButtonsTests.cs`

**Interfaces:**
- Consumes: `PlayerUIHandler.NorthFaceTrigger(CallbackContext)` and `NorthFaceEvent` (existing, unused).
- Produces:
  - `PlayerUIHandler.NORTH_ACTION = "UI/North Face"`.
  - `NorthFaceEvent` fires when a player presses Y (△) in any menu, gated by `canInput` like the other buttons.

- [ ] **Step 1: Write the failing tests** (`MenuButtonsTests`).

```csharp
[Test] public void UIMap_HasNorthFace_OnTheGamepadsNorthButton()
{
    InputActionAsset actions = Resources.Load<InputActionAsset>("CapstoneYear4");
    InputAction north = actions.FindAction(PlayerUIHandler.NORTH_ACTION);
    Assert.IsNotNull(north);
    Assert.AreEqual(InputActionType.Button, north.type);
    Assert.IsTrue(north.bindings.Any(b => b.path == "<Gamepad>/buttonNorth" && b.groups == "Gamepad"));
}

[UnityTest] public IEnumerator NorthButton_InPlayerSelect_ReachesTheMenus()
// Play Mode in "Assets/Scenes/Alex Player Testing.unity" (RemoteAvatarTests' pattern):
//   wait for the title → TestPlayers.Add() → wait for 1 player → SetGameState(PlayerSelect);
//   a NorthRecorder (int Presses; void Press(bool)) added with handler.NorthFaceEvent.AddListener(recorder.Press),
//   the handler being the player's PlayerUIHandler;
//   wait 0.5 s (the menus ignore buttons for uiDelayTime after opening) → press buttonNorth on TestPlayers.Pads[0]
//   (QueueStateEvent with the button, then without) → wait 0.3 s → Assert.AreEqual(1, recorder.Presses).
// TearDown: ExitPlayMode, playModeStartScene = null, TestPlayers.RemoveAll().
```

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh MenuButtonsTests`
Expected: `NO RESULTS` with `error CS0117: 'PlayerUIHandler' does not contain a definition for 'NORTH_ACTION'`.
- Then add the constant alone and run again. Expected: both tests FAIL: "Expected: not null" and "Expected: 1 But was: 0".

- [ ] **Step 3: Add the action and wire it.**
  - In the UI map, after the `UnPause` action, add an action: `name` "North Face", `type` "Button", a new GUID `id`, `expectedControlType` "Button", empty `processors` and `interactions`, `initialStateCheck` false.
  - After the `UnPause` binding, add a binding: new GUID `id`, `path` "<Gamepad>/buttonNorth", `groups` "Gamepad", `action` "North Face", `isComposite` / `isPartOfComposite` false.
  - Copy the neighbouring entries' layout, and keep CRLF (edit through Python with `newline=""`).
  - `PlayerUIHandler.Start`: find `GetComponentInParent<PlayerInput>().actions.FindAction(NORTH_ACTION)` and subscribe `performed += NorthFaceTrigger`. `OnDestroy` unsubscribes.
    - Why `Start`: every player gets its own copy of the actions by then.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh MenuButtonsTests`
Expected: `tests: 2 total, 2 passed, 0 failed, 0 skipped`.

- [ ] **Step 5: No commit.**

---

### Task 5: Play online from player select

**Files:**
- Create: `Assets/Scripts/Online/OnlinePlay.cs`, `Assets/Scripts/UI/LobbyPrompt.cs`
- Modify:
  - `Assets/Scripts/Menu/MenuInteractions.cs` (`i/crlf`: Edit tool only)
  - `Assets/Scripts/Online/OnlineGame.cs`
  - `docs/online.md` and `docs/testing.md`
- Test: `Assets/Tests/Editor/LobbyPromptTests.cs`, `Assets/Tests/Editor/OnlinePlayTests.cs`, `Assets/Tests/Editor/OnlinePlayNetworkTests.cs` (port 7797)

**Interfaces:**
- Consumes:
  - Tasks 1–4: `LobbyRules`, `OnlineLobby`, `ILobbyService`, `ISessionControl`, `SteamLobbyService`, `NorthFaceEvent`.
  - `OnlineSession.Create/HostDirect/JoinDirect/HostSteam/JoinSteam/IsRunning/RoleChanged/Ended`, `OnlineGame.Attach/CanGoOnline`.
  - `ControllerPrompts.Instance.ShowHint(string, float)`.
- Produces:

```csharp
public class LobbyPrompt : MonoBehaviour               // built on first use, like ControllerPrompts; a line along the top
{
    public static bool Exists { get; }
    public static LobbyPrompt Instance { get; }
    public void Show(string text);                     // "" or null hides it
    public bool IsShown { get; }
    public string Text { get; }
}

public class OnlinePlay : MonoBehaviour, ISessionControl   // made at launch (RuntimeInitializeOnLoadMethod AfterSceneLoad), DontDestroyOnLoad
{
    public const string HINT_PLAY_ONLINE = "Y / Triangle: play online with Steam friends";
    public const string HINT_IN_LOBBY = "Y / Triangle: invite friends      B / Circle: leave";
    public const string HINT_LEAVE = "B / Circle: leave online";
    public static OnlinePlay Instance { get; }
    public OnlineLobby Lobby { get; }
    public OnlineSession Session { get; }              // made on first use, played by OnlineGame
    public void PlayOnline();                          // Y in player select
    public void RequestJoin(ulong lobby);              // Steam's request or the launch argument; answered once the title is up
    public void UseDirect(ILobbyService lobbies, string address, ushort port); // tests and editor tools: IP sessions, these lobbies
    public static string HintFor(GameState state, bool online, bool inLobby, bool steam, int localPlayers);
}

// OnlineGame additions
public const float NOTICE_SECONDS = 5f;
public static void LeaveOnline();                      // leaves the session this machine plays online (nothing offline)
```

- **`OnlinePlay` behaviour:**
  - At launch it answers `LobbyRules.LobbyToJoin(Environment.GetCommandLineArgs())` through `RequestJoin`.
  - Steam's `JoinRequested` also goes to `RequestJoin`.
  - A request waits until `GameManager.Instance` exists and its state isn't `Default` (the title has come up). From options or credits it first goes back to the title (`MainMenu.Instance.SwapToMainMenu()`). Then it calls `Lobby.Answer(lobby, LobbyRules.Busy(state, LocalCount, GameAuthority.IsOnline))`.
  - Each frame it shows the lobby a changed state (`Lobby.ShowState`), and sets the line: `LobbyPrompt.Instance.Show(HintFor(...))`, built only once there's text.
  - `PlayOnline`:
    - `!OnlineGame.CanGoOnline()` → the notice `LobbyRules.ONE_PLAYER`.
    - Online → `Lobby.Invite()`.
    - Else → `Lobby.Host()`.
  - Notices go to `ControllerPrompts.Instance.ShowHint(text, OnlineGame.NOTICE_SECONDS)`.
  - `ISessionControl` is implemented explicitly (so nothing hosts around the lobby): `Session` over Steam (`HostSteam` / `JoinSteam`), or direct after `UseDirect`. `RoleChanged` and `Ended` are forwarded from `Session`.
- **`HintFor`:**
  - Outside `PlayerSelect` → "".
  - Online in a lobby → `HINT_IN_LOBBY`.
  - Online without a lobby (the editor's session) → `HINT_LEAVE`.
  - Offline with Steam and at most one player here → `HINT_PLAY_ONLINE`.
  - Else "".
- **`MenuInteractions`:**
  - Player select adds `uiHandler.NorthFaceEvent.AddListener(PlayOnline)`. `PlayOnline(bool)` calls `OnlinePlay.Instance.PlayOnline()`.
  - `ClearMenuInputs` also clears `NorthFaceEvent`.
  - `PlayerUnreadyDespawn`, not ready, online → `OnlineGame.LeaveOnline(); MainMenu.Instance.SwapToMainMenu();` and return.
- **`OnlineGame`:**
  - `LeaveOnline` leaves the session of the `OnlineGame` whose role this machine took. It's set in `OnRoleChanged` and cleared at Offline and `OnDestroy`.
  - Its session's `Ended` shows the reason through `ControllerPrompts` for `NOTICE_SECONDS`.

- [ ] **Step 1: Write the failing EditMode tests.**

```csharp
// LobbyPromptTests (TearDown destroys the prompt's object if LobbyPrompt.Exists)
Show_SomeText_ShowsIt                  // Show("hello") → IsShown, Text == "hello"
Show_Nothing_HidesIt                   // Show("hello"); Show("") → !IsShown

// OnlinePlayTests: HintFor
Hint_PlayerSelect_OfflineWithSteam_OnePlayer_OffersOnlinePlay    // (PlayerSelect, false, false, true, 1) == HINT_PLAY_ONLINE
Hint_PlayerSelect_OfflineWithSteam_TwoPlayers_OffersNothing      // (PlayerSelect, false, false, true, 2) == ""
Hint_PlayerSelect_WithoutSteam_OffersNothing                     // (PlayerSelect, false, false, false, 1) == ""
Hint_PlayerSelect_InALobby_OffersInvitesAndLeaving               // (PlayerSelect, true, true, true, 1) == HINT_IN_LOBBY
Hint_PlayerSelect_OnlineWithoutALobby_OffersLeaving              // (PlayerSelect, true, false, false, 1) == HINT_LEAVE
[TestCase(GameState.Menu)] [TestCase(GameState.MainLoop)]
Hint_OutsidePlayerSelect_OffersNothing(GameState state)          // (state, true, true, true, 1) == ""
```

- [ ] **Step 2: Write the failing Play Mode tests** (`OnlinePlayNetworkTests`: menu scene, `THIS_COMPUTER = "127.0.0.1"`, `PORT = 7797`, the version `Application.version`; TearDown as `OnlineMatchNetworkTests`).

```text
Hosting_YHostsALobby_FriendsJoin_AndBEndsIt
  - title up; TestPlayers.Add() twice (the second joins in player select): SetGameState(PlayerSelect) after the first.
  - fake = new FakeLobbyService { AutoAnswer = true }; OnlinePlay.Instance.UseDirect(fake, THIS_COMPUTER, PORT).
  - wait 0.5 s; press North on pad 0 → ControllerPrompts.Instance.HintText == LobbyRules.ONE_PLAYER, fake.Creates empty.
  - press East on pad 1 (player 2 leaves) → wait until LocalCount == 1; wait 0.5 s.
  - press North on pad 0 → wait until GameAuthority.Role == Host:
      fake.Data[42]["game"] == "doa", ["build"] == Application.version; fake.Invites == [42];
      wait a frame: LobbyPrompt.Instance.Text == OnlinePlay.HINT_IN_LOBBY.
  - other = OnlineSession.Create(Application.version); other.JoinDirect(THIS_COMPUTER, PORT); an EndRecorder on other.Ended
    → wait until Slot(1) != null.
  - log.MachinesLeave(); press East on pad 0 → wait until Role == Offline and State == Menu:
      fake.Left contains 42; wait until !other.IsRunning; recorder.Reasons == [OnlineSession.HOST_LEFT].
  - no errors (LogCollector).

Joining_AFriendsLobbyFromTheTitle_LandsInTheirPlayerSelect_AndTheirLeavingSaysSo
  - right after EnterPlayMode: fake = new FakeLobbyService { AutoAnswer = true }; fake.AddLobby(42, "doa", Application.version, 99);
    OnlinePlay.Instance.UseDirect(fake, THIS_COMPUTER, PORT);
    host = OnlineSession.Create(Application.version) (a session without the game); host.HostDirect(THIS_COMPUTER, PORT);
    OnlinePlay.Instance.RequestJoin(42)  (the title isn't up yet: it waits).
  - wait until GameAuthority.Role == Client: fake.Joins == [42], OnlinePlay.Instance.Lobby.Current == 42.
  - host.Match.SendState(GameState.PlayerSelect) → wait until State == PlayerSelect.
  - log.MachinesLeave(); host.Leave() → wait until Role == Offline:
      ControllerPrompts.Instance.HintText == OnlineSession.HOST_LEFT; fake.Left contains 42; State stays PlayerSelect.
  - no errors.
```

- [ ] **Step 3: Run to verify they fail**

Run: `bash tools/run-tests.sh "LobbyPromptTests|OnlinePlayTests|OnlinePlayNetworkTests"`
Expected: `NO RESULTS` with `error CS0246: … 'OnlinePlay' could not be found` (and `'LobbyPrompt'`).

- [ ] **Step 4: Implement** `LobbyPrompt` and `OnlinePlay` (with metas), then the `MenuInteractions` and `OnlineGame` changes above.
  - `LobbyPrompt` copies `ControllerPrompts.Create`/`CreatePanel`: sorting order 1000, 1920×1080 reference, 0.6 background alpha, 36 pt. Its anchors are (0, 0.9)–(1, 0.97).
  - `OnlinePlay` gets its `RuntimeInitializeOnLoadMethod` pair as `SteamManager`'s: `SubsystemRegistration` resets `Instance`; `AfterSceneLoad` creates it.

- [ ] **Step 5: Run to verify they pass**

Run: `bash tools/run-tests.sh "LobbyPromptTests|OnlinePlayTests|OnlinePlayNetworkTests|MenuButtonsTests|RemoteAvatarTests|OnlineMatchNetworkTests"`
Expected: `tests: 19 total, 19 passed, 0 failed, 0 skipped` (11 new; `MenuButtonsTests` 2, `RemoteAvatarTests` 2 and `OnlineMatchNetworkTests` 4 check that Y and B didn't break the menus or a match).

- [ ] **Step 6: Docs.**
  - `docs/online.md`:
    - A "Playing over Steam" section: Y hosts and invites, friends join through Steam, B leaves, the line along the top, one player per machine.
    - A two-PC check: two Steam accounts that are friends, each PC running Steam.
    - Known limits: friends-only, no lobby list, a Steam account can't join itself (so two editors on one PC use **Tools → Online**), and no title-screen row until there's art.
  - `docs/testing.md`: the new test classes, and `OnlinePlayNetworkTests` on port 7797.

- [ ] **Step 7: No commit.**

---

### Task 6: Back in the lobby after an online match

**Files:**
- Modify: `Assets/Scripts/Menu/MainMenu.cs` (`i/crlf`: Edit tool only), `docs/online.md`
- Test: `Assets/Tests/Editor/MainMenuTests.cs`, `Assets/Tests/Editor/OnlineMatchNetworkTests.cs`

**Interfaces:**
- Consumes: `GameAuthority.IsOnline`, `GameManager.SetGameState`.
- Produces: `MainMenu.Start` opens on `GameState.PlayerSelect` online, and `GameState.Menu` offline. On a client, `SetGameState` is ignored and the host's state follows.

- [ ] **Step 1: Write the failing tests.**

```csharp
// MainMenuTests (TearDown also resets GameAuthority.Role = NetworkRole.Offline)
[Test] public void Start_Offline_OpensOnTheTitleScreen()
{ menu.Start(); Assert.AreEqual(GameState.Menu, game.MainState); }

[Test] public void Start_Online_OpensOnPlayerSelect_TheLobby()
{ GameAuthority.Role = NetworkRole.Host; menu.Start(); Assert.AreEqual(GameState.PlayerSelect, game.MainState); }
```

- In `OnlineMatchNetworkTests.Hosting_TheMatchStarts_…`, after `SceneFlow.Current.ReturnToMenu()`:
  - Wait until `ActiveScene() == MENU && State() == GameState.PlayerSelect`.
  - Keep `Assert.AreEqual(MENU, ActiveScene(), "back in the menu")`.
  - Add `Assert.AreEqual(GameState.PlayerSelect, State(), "back in the lobby, player select")`.

- [ ] **Step 2: Run to verify they fail**

Run: `bash tools/run-tests.sh "MainMenuTests|OnlineMatchNetworkTests.Hosting_TheMatchStarts"`
Expected: `Start_Online_OpensOnPlayerSelect_TheLobby` FAILS ("Expected: PlayerSelect But was: Menu"). The hosting test FAILS with "back in the lobby, player select".

- [ ] **Step 3: Implement:** `MainMenu.Start` sets `GameAuthority.IsOnline ? GameState.PlayerSelect : GameState.Menu`.
  - Update `docs/online.md`'s "Back to the menu" bullets: back to player select, the lobby.

- [ ] **Step 4: Run to verify they pass**

Run: `bash tools/run-tests.sh "MainMenuTests|OnlineMatchNetworkTests"`
Expected: no failures.

- [ ] **Step 5: No commit.**

---

### Task 7: Whole suite, roadmap and the editor list

**Files:**
- Modify: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`, `EDITOR-TODO.md`, `docs/testing.md` (the count)

- [ ] **Step 1: Full suite**

Run (background): `bash tools/run-tests.sh`
Expected: `tests: 372 total, 372 passed, 0 failed, 0 skipped` (316 + 56 new: 17 + 20 + 4 + 2 + 2 + 7 + 2 + 2).

- [ ] **Step 2: Roadmap.**
  - Tick Task 3.2 bullets 1 and 3, with a *(Phase 3D: …)* note of what was done and Rulings 1–2.
  - Add Phase 3D to the Phase 3 progress list.
  - Next: orders (3.5).
  - Set the "Run All" count to the number printed.

- [ ] **Step 3: `EDITOR-TODO.md`** (short bullets). Check it on one PC, then on two.
  1. Let both editors import: scripts, the UI map's new "North Face" action, and the new tests.
  2. **One PC, Steam running:** Play → A → player select.
     - The top line says "Y / Triangle: play online with Steam friends". Y hosts.
     - The notice says to invite friends from the Steam friends list (the editor has no overlay).
     - B returns to the title.
  3. **Two PCs, two Steam accounts that are friends:**
     - PC 1: Y in player select, then invite the friend from Steam.
     - PC 2 accepts. It lands in PC 1's player select, seat 2.
     - Ready up both: the match starts.
     - After the match both are back in player select.
     - PC 1's B: PC 2 shows "The host left the match."
  4. Commit (your call).

- [ ] **Step 4: No commit.**

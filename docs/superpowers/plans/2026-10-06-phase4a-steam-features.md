# Phase 4A — Steam Features in the Game Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Use the Steam features a player sees inside the game:
- **Achievements:** two of them, decided by whoever runs the match's rules (this PC offline, the host online).
- **Rich Presence:** friends see what the player is doing, for example "Delivering — 3 players".
- **Session logs:** opt-in log files of errors, disconnects and load times, for bug reports.

**Architecture:**
- **Achievements:**
  - `MatchFeats` is a plain class. It runs on the authority and turns deliveries and the results into earned achievements, each for a seat.
  - `SteamFeatures` is a launch-made `DontDestroyOnLoad` object, like `OnlinePlay`. It feeds `MatchFeats` from:
    - `FeatSync.Delivered`, a static hub that `OrderHandler.DeliverOrder` raises on the authority;
    - `GameManager.StateApplied`.
  - **Offline**, every seat is this PC's Steam user, so `SteamFeatures` unlocks each earned achievement itself.
  - **Online**, `SteamFeatures` raises the static `Earned` event and `OnlineAchievements` (added by `OnlineGame`, as `OnlineCues` is) handles it:
    - the host unlocks its own seat's achievements;
    - the host sends every other seat's to every client (`OnlineMatch.SendAchievement`);
    - each client unlocks only its own seat's.
  - Steam is reached through a seam, `IAchievementStore`. `SteamAchievementStore` is the real one; tests use a fake. This copies `ILobbyService`.
- **Rich Presence:**
  - `PresenceRules.For(GameState, players)` picks a localization token.
  - `SteamFeatures` sets it on every applied state, through an `IPresence` seam (`SteamPresence` is the real one).
  - The token strings live in `docs/steam/rich-presence-english.vdf`, which the user uploads in Steamworks (Phase 4B).
- **Session logs:**
  - A `-sessionlog` launch option turns them on. `SessionLogWriter` is then made at launch.
  - It writes errors, exceptions and every `Online:` line to `persistentDataPath/logs/session-<time>.log`, one file per launch. Only the newest 10 files are kept.
  - The `Online:` lines include session ends with their reasons, joins and leaves, and the Phase 3I load lines.
  - The rules (`SessionLog`) are a plain class.

**Tech Stack:** Unity 2022.3.62f3 · Steamworks.NET 2025.164.1 (`SteamUserStats`, `SteamFriends`) · Netcode for GameObjects 1.15.1 · Unity Test Framework 1.1.33.

**Spec:** `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md` (the roadmap):
- Task 1.5: "Optional: achievements (first delivery, win holding the golden order), Rich Presence ("Delivering — 3 players")."
- Phase 4: "Achievements/stats unlocked by the host (authoritative)." and "Opt-in crash/disconnect logs in `Application.persistentDataPath` (never `StreamingAssets`)."
- The handoff (`docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`), section 2:
  - "the host decides, then tells each machine to unlock for its own player";
  - "Offline, each local match unlocks for the PC's Steam user";
  - "Put them behind a small seam with a fake for tests, as `ILobbyService` does".
- The user, 2026-10-06: Phase 4A goes on `steam-phase1a`, all of it.

## Global Constraints

- **Stay on Unity 2022.3 LTS** and Netcode for GameObjects **1.x**. Steamworks.NET stays **2025.164.1**.
- **Players:** online is one player per machine until Task 3.9.
- **Branch:** everything goes on `steam-phase1a`, including the Steam API calls (the user, 2026-10-06). Phase 3I is committed (`693799f0`), and the tree is clean.
- **Local split-screen must behave exactly as before.** `LocalMatchSmokeTest` passes after every task that changes game code.
- **Steam API files stay untouched:** `SteamManager.cs` and `SteamStartup.cs`. New code reads `SteamManager.Initialized` and never calls Steamworks when it's false (no Steam, batch-mode tests).
- **Steamworks calls go behind `#if !DISABLESTEAMWORKS`**, with the same `#define` block as `SteamLobbyService.cs`.
- **The user may have Unity editors open on the real project:** never edit a scene, prefab, `.inputactions` asset or ProjectSettings file. Everything here is code, plus docs.
- **No git commits:** your human partner commits.
- **Meta files:** every new file or folder under `Assets/` gets a `.meta`:
  - a file: `bash tools/newmeta.sh <path>`;
  - a folder: `bash tools/newmeta.sh <path> folder`.
- **Line endings:** `Order.cs` and `SceneManager.cs` are `i/crlf`, and this plan touches neither. Check any other file with `git ls-files --eol`. Edit scripts with the Edit tool only, never `sed -i`.
- **Tests:**
  - `bash tools/run-tests.sh [filter]`, which runs on the mirror. The mirror copies only `Assets`, `Packages` and `ProjectSettings`, so tests can't read `docs/`.
  - Run long runs in the background, and check `ListAgents` first.
  - A compile error prints `NO RESULTS`.
- **Play Mode test rules:**
  - Recorder classes subscribed as method groups; no lambda captures a test's local.
  - Wait by real time.
  - Call `log.MachinesLeave()` before a machine leaves.
  - The next free network test port is **7810**.
- **Tests can't see `internal` members.** Make what tests call `public`.

## Rulings (decided while planning)

1. **Two achievements, no stats.**
   - `FIRST_DELIVERY` ("First Delivery"): deliver any order in a match. Tutorial deliveries don't count: everyone makes one there.
   - `GOLDEN_WIN` ("Golden Finish"): deliver the golden order, then finish first. A tie for first counts, since the results screen shows ties as shared places.
   - Stats need dashboard definitions and a design for what to count. They can be added later through the same seam.
   - Cost if wrong: one more enum value and rule.
2. **"Win" is read at the results, from the scores.**
   - The golden delivery ends the match (`OrderManager.GoldOrderDelivered` → Results).
   - The scores are read when Results is applied, not from `Placement`: `StateApplied` fires before `ScoreManager`'s `OnSwapResults`.
3. **API names are fixed now:** `FIRST_DELIVERY` and `GOLDEN_WIN`.
   - The user creates them in the Steamworks dashboard with these names (Phase 4B).
   - Until then the test app (480, Spacewar) has neither. `SetAchievement` returns false for them, and the store logs one warning per name.
4. **Online, the host decides and sends; each client unlocks only its own seat's.**
   - The host unlocks its own seat's achievements. It sends every other seat's to every client (one ClientRpc), with the seat, and a client unlocks it only if the seat is its own (`session.SeatOf(LocalClientId)`).
   - Why not target one client: online has one player per machine, and these are rare messages, so a broadcast is simpler.
   - A client never decides: `FeatSync` is raised only on the authority, and a client's replays (`OrderSync.Showing`) run with `IsAuthority` false.
   - Cost if wrong: none to players. A cheater could fake their own unlock anyway, since Steam unlocks are client-side.
5. **Each machine's earned list starts fresh at Menu and PlayerSelect.** It doesn't reset at Loading, because the golden round is its own load. Steam ignores an achievement that's already unlocked, and the store checks `GetAchievement` first.
6. **Rich Presence tokens:**

   | States | Token | English text |
   |---|---|---|
   | Menu, Options, Credits | `#Menus` | In the menus |
   | PlayerSelect | `#GettingReady` | Getting ready |
   | Tutorial | `#Tutorial` | Learning the ropes |
   | StartingCutscene, Begin, MainLoop | `#Delivering` / `#DeliveringSolo` | Delivering — %players% players / Delivering solo |
   | GoldenCutscene, FinalPackage | `#Golden` / `#GoldenSolo` | Chasing the golden order — %players% players / Chasing the golden order solo |
   | Results | `#Results` | Counting the takings |

   - Loading, Paused and Default change nothing: the last presence stays.
   - "Solo" means 1 player or fewer.
   - `players` is the roster count (`PlayerInstantiate.Instance.PlayerCount`), which online includes the other machines' players.
   - Not in this phase: `steam_player_group` (grouping friends in one lobby in the friends list), and a `connect` string.
7. **Session logs are opt-in through the `-sessionlog` launch option.**
   - A menu row needs hand-lettered art. Steam's launch options (Properties → General → Launch Options) need none, and the user can set them for testers.
   - The logs live in `persistentDataPath/logs/` (`%USERPROFILE%\AppData\LocalLow\The Boo Crew\Dead on Arrival\logs`). Steam Cloud syncs only `settings.cfg`, so they stay on the PC.
   - Kept: `Error`, `Assert` and `Exception` (with the stack), plus `Log` and `Warning` lines that start with `Online:`.
   - The file is flushed on every line, so it survives a crash of the game's code. A native crash ends it early; Unity's `Player.log` covers that.
   - Cost if wrong: players who want logs have to set a launch option.
8. **Not in this phase:** stats, achievement icons (art: the user uploads 256×256 JPGs, in two states each), the dashboard set-up, and crash-dump uploading.

## Review Focus

- **Steam isn't running** (the editor without Steam, batch-mode tests, a DRM-free copy) → nothing calls Steamworks, and nothing logs. (Task 1 `SteamAchievementStore_WithoutSteam_IsUnavailable_AndUnlockDoesNothing`; Task 2 `SteamPresence_WithoutSteam_IsUnavailable_AndSetDoesNothing`.)
- **A client delivers its first order** → that client's machine unlocks `FIRST_DELIVERY`, and the host's doesn't. (Task 4 `Hosting_AnotherSeatEarnsAnAchievement_OnlyItsMachineUnlocksIt`.)
- **The golden deliverer ties for first** → `GOLDEN_WIN` still unlocks. (Task 1 `Results_TheGoldenDelivererTiesForFirst_EarnsGoldenWin`.)
- **The log folder can't be written** (a file in the way, a read-only drive) → the game runs on without a log, with one warning. (Task 5 `Open_WhenTheFolderCantBeMade_ReturnsNull`.)
- **A client's replay of the host's delivery** → it never counts as the client's own delivery. (Task 3 `ReplayedDelivery_RaisesNoFeat`.)

## Facts (read 2026-10-06)

- **`OrderHandler.DeliverOrder(Order rightOrder)`:**
  - It runs when `OrderSync.MayChange`, that is on the authority, or on a client while it shows the host's change (`OrderSync.Showing`).
  - It scores only `if (GameAuthority.IsAuthority)`.
  - For the golden order, `order.DeliverOrder()` → `EraseOrder` adds the golden bonus. In FinalPackage it then calls `OrderManager.GoldOrderDelivered`, which sets Results after `postGameLinger / 2`.
- **`OrderSync.SeatOf(Component)`** returns the roster seat, or −1. Offline, seats are the split-screen slots.
- **`GameManager.StateApplied(GameState)`** fires for every applied state, on every machine, before that state's own event. `GameManager` persists across scenes; `OnlineGame` subscribes once (`GameManager.Instance` may be null at first).
- **`PlayerInstantiate.Instance.Roster.Players`** holds `PlayerSlot`s (`Index`, `Player`). Online they include the other machines' players (`AddRemotePlayer`).
- **`OnlineSession`:**
  - `SeatOf(ulong clientId)`;
  - `Network.LocalClientId`;
  - `Match` (`OnlineMatch`) and `MatchSpawned`.
- **`OnlineMatch`'s RPC pattern** is a public `SendX`, guarded by `IsServer`, plus a `[ClientRpc] XClientRpc` that raises a public event. See `SendCue` / `CueReceived`. Enums go through RPCs as they are (`StateClientRpc(GameState)`).
- **`OnlinePlay.CreateOnLaunch`** is the launch-made-object pattern:
  - `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`, plus a `ResetStatics` at `SubsystemRegistration`;
  - a new `GameObject` with `DontDestroyOnLoad`.
- **Steamworks.NET 2025.164.1** (SDK 1.62+): the user's stats load on their own, with no `RequestCurrentStats`.
  - Achievements: `SteamUserStats.GetAchievement(string, out bool)`, `SetAchievement(string)` (false for an unknown name), and `StoreStats()`.
  - Presence: `SteamFriends.SetRichPresence(string key, string value)`. Steam clears it at shutdown.
- **The `Online:` log lines today:** hosting, joining, a refused player, joins and leaves with the count, "session ended: <reason>", and the Phase 3I load line.

---

### Task 1: Achievements and their rules

**Files:**
- Create: `Assets/Scripts/Steam/` (folder), `Assets/Scripts/Steam/Achievement.cs`, `Assets/Scripts/Steam/IAchievementStore.cs`, `Assets/Scripts/Steam/SteamAchievementStore.cs`, `Assets/Scripts/Steam/MatchFeats.cs`
- Test: `Assets/Tests/Editor/AchievementsTests.cs`

**Interfaces:**
- Produces:
  - `public enum Achievement { FirstDelivery, GoldenWin }`
  - `public static class AchievementNames { public static string ApiName(Achievement a); }`, which returns `"FIRST_DELIVERY"` and `"GOLDEN_WIN"`.
  - `public interface IAchievementStore { bool Available { get; } void Unlock(Achievement a); }`
  - `public class SteamAchievementStore : IAchievementStore`.
    - `Available` is `SteamManager.Initialized`.
    - `Unlock` skips an achievement that's already unlocked, otherwise calls `SetAchievement` then `StoreStats`.
    - When `SetAchievement` returns false, it logs one warning per name: `"Steam: this app has no achievement " + name`.
  - `public readonly struct Feat { public readonly int Seat; public readonly Achievement Achievement; public Feat(int seat, Achievement achievement); }`, with value equality.
  - `public class MatchFeats`, with these methods:
    - `List<Feat> Delivered(int seat, bool golden, GameState state)`
    - `List<Feat> Results(IReadOnlyDictionary<int, int> scoresBySeat)`
    - `void NewMatch()`
    - `public static bool UnlocksHere(bool online, int seat, int ownSeat)`

- [ ] **Step 1: Write the failing tests** (`AchievementsTests`, EditMode):
  - `ApiName_IsWhatTheDashboardUses`: `"FIRST_DELIVERY"` and `"GOLDEN_WIN"`.
  - `Delivered_InTheMatch_EarnsFirstDelivery_OncePerSeat`:
    - `Delivered(1, false, MainLoop)` returns `[Feat(1, FirstDelivery)]`, and a second call returns `[]`.
    - `Delivered(2, false, MainLoop)` returns `[Feat(2, FirstDelivery)]`.
  - `Delivered_InTheTutorial_EarnsNothing`: `Delivered(0, false, Tutorial)` returns `[]`, and a later `Delivered(0, false, MainLoop)` still earns.
  - `Results_TheGoldenDelivererLeads_EarnsGoldenWin`: `Delivered(1, true, FinalPackage)` returns `[Feat(1, FirstDelivery)]`; then `Results({0:40, 1:90})` returns `[Feat(1, GoldenWin)]`.
  - `Results_TheGoldenDelivererTiesForFirst_EarnsGoldenWin`: the same with `{0:90, 1:90}`.
  - `Results_TheGoldenDelivererTrails_EarnsNothing`: `{0:120, 1:90}` returns `[]`.
  - `Results_WithoutAGoldenDelivery_EarnsNothing`.
  - `Delivered_TheGoldenOrderOutsideTheGoldenRound_IsNoGoldenDelivery`: `Delivered(1, true, MainLoop)`, then `Results({1:90})`, returns `[]`.
  - `NewMatch_ForgetsTheLastMatch`: after `NewMatch()`, seat 1 earns `FirstDelivery` again, and an earlier golden delivery is forgotten.
  - `Results_TwiceInAMatch_EarnsGoldenWinOnce`.
  - `UnlocksHere_OfflineEverySeat_OnlineOnlyItsOwn`:
    - `(false, 3, -1)` is true;
    - `(true, 1, 1)` is true;
    - `(true, 0, 1)` is false.
  - `SteamAchievementStore_WithoutSteam_IsUnavailable_AndUnlockDoesNothing`: in batch mode Steam isn't running. `Available` is false; `Unlock(FirstDelivery)` throws nothing and logs nothing (`LogAssert.NoUnexpectedReceived()`).

- [ ] **Step 2: Run them, and see them fail to compile**

  Run: `bash tools/run-tests.sh AchievementsTests`. Expected: `NO RESULTS` with CS0246 (`MatchFeats`, `Achievement`).

- [ ] **Step 3: Implement the files.**
  - Make the folder's meta first.
  - `SteamAchievementStore` copies `SteamLobbyService.cs`'s `DISABLESTEAMWORKS` header, and keeps a `HashSet<string>` of names it has already warned about.
  - Every type gets a doc comment in the codebase's style: what it is, and who uses it.

- [ ] **Step 4: Run them, and see them pass**

  Run: `bash tools/run-tests.sh AchievementsTests`. Expected: 12/12 pass.

### Task 2: Rich Presence rules

**Files:**
- Create: `Assets/Scripts/Steam/IPresence.cs`, `Assets/Scripts/Steam/SteamPresence.cs`, `Assets/Scripts/Steam/PresenceRules.cs`, `docs/steam/rich-presence-english.vdf`
- Test: `Assets/Tests/Editor/PresenceRulesTests.cs`

**Interfaces:**
- Produces:
  - `public interface IPresence { bool Available { get; } void Set(string key, string value); }`
  - `public class SteamPresence : IPresence`.
    - `Available` is `SteamManager.Initialized`.
    - `Set` calls `SteamFriends.SetRichPresence`.
  - `public static class PresenceRules` with these members:
    - `public const string DISPLAY = "steam_display";`
    - `public const string PLAYERS = "players";`
    - `public static string For(GameState state, int players)`, which returns a token from Ruling 6, or null for no change.

- [ ] **Step 1: Write the failing tests** (`PresenceRulesTests`):
  - `For_TheMenus_IsMenus`: Menu, Options and Credits give `"#Menus"`.
  - `For_PlayerSelect_IsGettingReady`.
  - `For_TheTutorial_IsTutorial`.
  - `For_TheMatch_IsDelivering_OrSoloForOne`:
    - StartingCutscene, Begin and MainLoop with 3 players give `"#Delivering"`;
    - MainLoop with 1 gives `"#DeliveringSolo"`, and with 0 also `"#DeliveringSolo"`.
  - `For_TheGoldenRound_IsGolden_OrSoloForOne`: GoldenCutscene and FinalPackage, with 2 players and with 1.
  - `For_TheResults_IsResults`.
  - `For_LoadingPausedOrDefault_ChangesNothing`: each returns null.
  - `For_EveryState_IsCovered`: loop over `Enum.GetValues(typeof(GameState))`. Each state is either in the expected-null set {Loading, Paused, Default} or returns a token starting with `#`. This catches a state added later.
  - `SteamPresence_WithoutSteam_IsUnavailable_AndSetDoesNothing`.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`, CS0103/CS0246).

- [ ] **Step 3: Implement the files, and write `docs/steam/rich-presence-english.vdf`.**
  - Use Steam's localization format: `"lang" { "Language" "english" "Tokens" { … } }`.
  - It holds the seven tokens and their texts from Ruling 6, word for word, with `%players%` kept as is.
  - The mirror can't read `docs/`, so no test checks this file. Its header comment says that it must match `PresenceRules`.

- [ ] **Step 4: Run them, and see them pass** (9/9).

### Task 3: SteamFeatures, offline

**Files:**
- Create: `Assets/Scripts/Steam/FeatSync.cs`, `Assets/Scripts/Steam/SteamFeatures.cs`
- Modify: `Assets/Scripts/Player/OrderHandler.cs`, in `DeliverOrder`'s two branches (around lines 155–176)
- Test: `Assets/Tests/Editor/SteamFeaturesTests.cs`; `Assets/Tests/Editor/OnlineOrdersNetworkTests.cs` (one assertion)

**Interfaces:**
- Consumes: Task 1's `MatchFeats`, `IAchievementStore` and `SteamAchievementStore`; Task 2's `IPresence`, `SteamPresence` and `PresenceRules`.
- Produces:
  - `public static class FeatSync`, with these members:
    - `public static event Action<int, bool> Delivered;` (a seat, and whether it was golden)
    - `public static void Deliver(int seat, bool golden)`
    - `public static void Reset()`, called by `ResetStatics`
  - `public class SteamFeatures : MonoBehaviour`, with these members:
    - `public static SteamFeatures Instance { get; }`
    - `public static event Action<int, Achievement> Earned;`
    - `public void Use(IAchievementStore achievements, IPresence presence)`
    - `public IAchievementStore Achievements { get; }`
    - `public void Unlock(Achievement a)`
- Behaviour:
  - `CreateOnLaunch` (AfterSceneLoad) makes it once, with `DontDestroyOnLoad`. `Awake` calls `Use(new SteamAchievementStore(), new SteamPresence())` and subscribes to `FeatSync.Delivered`.
  - `Update` subscribes to `StateApplied` whenever `GameManager.Instance` isn't the instance it's watching. It unsubscribes from the old one first, when that one is still alive.
  - **On a delivery,** only `if (GameAuthority.IsAuthority)`, it runs `MatchFeats.Delivered(seat, golden, GameManager.Instance.MainState)`, then `Earn`s each result.
  - **On a state:**
    - Menu or PlayerSelect → `NewMatch()`.
    - Results, on the authority → `Results(scores)`, then `Earn` each. `scores` maps each roster slot's `Index` to its `OrderHandler`'s `Score`, and is empty without a `PlayerInstantiate`.
    - Then presence: when `PresenceRules.For(state, players)` isn't null, and `presence.Available`, set `PLAYERS` and then `DISPLAY`.
  - **`Earn(Feat f)`:**
    - raise `Earned(f.Seat, f.Achievement)`;
    - then `if (!GameAuthority.IsOnline) Unlock(f.Achievement)`. Offline that's every seat. Online, `OnlineAchievements` (Task 4) unlocks. Don't call `UnlocksHere` with a made-up own seat: a seat of −1, from a part that's nobody's, would match it.
  - `Unlock` calls `achievements.Unlock(a)` when `achievements.Available`.
  - **`OrderHandler.DeliverOrder`**, in each branch, right after the `score +=` line and inside the same `if (GameAuthority.IsAuthority)`: `FeatSync.Deliver(OrderSync.SeatOf(this), rightOrder.Value == Constants.OrderValue.Golden);`. Give that `if` braces.

- [ ] **Step 1: Write the failing tests** (`SteamFeaturesTests`). They're Play Mode tests in an empty scene, with a `GameManager` on a new object. They use these fakes:
  - `FakeAchievements : IAchievementStore`, with `Available = true` and a `List<Achievement> Unlocked`;
  - `FakePresence : IPresence`, with `Available = true` and a `Dictionary<string,string> Values`.

  The tests:
  - `OfflineDelivery_InTheMatch_UnlocksFirstDelivery`:
    - set `SteamFeatures.Instance.Use(fakes)`;
    - `GameManager.Instance.ApplyGameState(MainLoop)`;
    - wait one frame (the `Update` subscription);
    - `FeatSync.Deliver(0, false)`;
    - expect `Unlocked == [FirstDelivery]`.
  - `ReplayedDelivery_RaisesNoFeat`: the same with `GameAuthority.Role = Client`. The fake has nothing unlocked, and an `Earned` recorder heard nothing. Reset `Role` in teardown.
  - `OnlineHostDelivery_IsEarnedButNotUnlockedHere`: with `Role = Host`, the recorder hears `(2, FirstDelivery)`, and the fake has nothing unlocked.
  - `States_SetRichPresence`:
    - `ApplyGameState(Menu)` gives `Values["steam_display"] == "#Menus"`;
    - `MainLoop` gives `"#DeliveringSolo"`, because there's no roster, so 0 players;
    - `Loading` leaves `"#DeliveringSolo"` in place.
  - `ANewGameManager_IsWatchedToo`:
    - destroy the `GameManager` object, wait a frame, then make a new one and wait a frame;
    - `ApplyGameState(Results)` on it sets `"#Results"`.

  Also add to `OnlineOrdersNetworkTests.Hosting_TheHostDecidesEveryScootersOrders_AndSharesThemWithTheScores`:
  - a `FeatRecorder`, with a `Heard(int seat, bool golden)` method group, subscribed to `FeatSync.Delivered` before the delivery;
  - assert that it contains `(1, false)`.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`).

- [ ] **Step 3: Implement `FeatSync`, `SteamFeatures` and the `OrderHandler` hook.**

- [ ] **Step 4: Run the tests.**
  - Run `bash tools/run-tests.sh SteamFeaturesTests`: expect 5/5.
  - Run `bash tools/run-tests.sh "OnlineOrdersNetworkTests|LocalMatchSmokeTest"` in the background: expect all to pass.

### Task 4: Achievements online

**Files:**
- Create: `Assets/Scripts/Online/OnlineAchievements.cs`
- Modify:
  - `Assets/Scripts/Online/OnlineMatch.cs`: add `SendAchievement`, `AchievementClientRpc` and `AchievementReceived`, next to the cue members.
  - `Assets/Scripts/Online/OnlineGame.cs`: add the component next to `OnlineCues`, around line 133.
- Test: `Assets/Tests/Editor/AchievementMessagesNetworkTests.cs` (port **7810**)

**Interfaces:**
- Consumes: Task 3's `SteamFeatures.Earned`, `SteamFeatures.Instance.Achievements` and `SteamFeatures.Unlock`; Task 1's `MatchFeats.UnlocksHere`.
- Produces:
  - In `OnlineMatch`:
    - `public event Action<int, Achievement> AchievementReceived;`
    - `public void SendAchievement(int seat, Achievement a)`, which sends to every client, host only.
  - `public class OnlineAchievements : MonoBehaviour`, with `public void Begin(OnlineSession onlineSession, IAchievementStore store)`.
    - It unsubscribes in `OnDestroy`, and follows `MatchSpawned` as `OnlineCues` does.
    - **Host:** on `SteamFeatures.Earned(seat, a)`, when `session.Match.IsServer`:
      - unlock in `store` if `UnlocksHere(true, seat, OwnSeat)`;
      - otherwise `SendAchievement(seat, a)`.
    - **Client:** on `AchievementReceived(seat, a)`, unlock in `store` if `UnlocksHere(true, seat, OwnSeat)`.
    - `OwnSeat` is `session.SeatOf(session.Network.LocalClientId)`.
    - `store.Unlock` only `if (store.Available)`.
  - `OnlineGame` adds it with `.Begin(session, SteamFeatures.Instance != null ? SteamFeatures.Instance.Achievements : new SteamAchievementStore())`.

- [ ] **Step 1: Write the failing tests** (`AchievementMessagesNetworkTests`). Copy `CueMessagesNetworkTests`' set-up: an empty scene, a direct host and client, `Sees`, and `log.MachinesLeave()` before leaving. The fake store is a recorder class.
  - `TheHostsAchievements_ReachEveryClient_WithTheirSeat`:
    - `host.Match.SendAchievement(1, GoldenWin)`;
    - the client's recorder on `AchievementReceived` hears `(1, GoldenWin)`.
  - `AClientNeverSends`:
    - `client.Match.SendAchievement(0, FirstDelivery)`;
    - after 1 s, the host's recorder heard nothing.
  - `Hosting_AnotherSeatEarnsAnAchievement_OnlyItsMachineUnlocksIt`:
    - add an `OnlineAchievements` to a new object for each session, each with its own fake store;
    - raise `SteamFeatures.Earned` through a `public static void RaiseEarned(int seat, Achievement a)` test hook on `SteamFeatures` (add it in this task; its doc says "tests");
    - the client's seat (1) earning `FirstDelivery` → the client's fake has `[FirstDelivery]`, and the host's is empty;
    - the host's seat (0) earning `GoldenWin` → the host's fake has `[GoldenWin]`, and the client's still has only `[FirstDelivery]`.
    - In one process the static `Earned` reaches both components. The client's ignores it because its match isn't the server.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`).

- [ ] **Step 3: Implement the RPC, `OnlineAchievements` and the `OnlineGame` line.**

- [ ] **Step 4: Run the tests.**
  - Run `bash tools/run-tests.sh AchievementMessagesNetworkTests`: expect 3/3.
  - Run `bash tools/run-tests.sh "OnlinePrefabsTests|OnlineSessionTests|CueMessagesNetworkTests"`: expect all to pass. The RPC changes `OnlineMatch`, but not its prefab id.

### Task 5: Opt-in session logs

**Files:**
- Create: `Assets/Scripts/Steam/SessionLog.cs`, `Assets/Scripts/Steam/SessionLogWriter.cs`
- Test: `Assets/Tests/Editor/SessionLogTests.cs`

**Interfaces:**
- Produces:
  - `public static class SessionLog`, with these members:
    - `public const string OPT_IN = "-sessionlog";`
    - `public const int KEEP = 10;`
    - `public static bool OptedIn(string[] args)`: the exact argument, ignoring case.
    - `public static bool Keeps(LogType type, string message)`
    - `public static string FileName(DateTime start)`, which gives `"session-" + start.ToString("yyyyMMdd-HHmmss", InvariantCulture) + ".log"`.
    - `public static string Line(DateTime time, LogType type, string message, string stack)`. It gives `"[HH:mm:ss] <Type>: <message>"`. For Error, Assert and Exception it adds each non-empty stack line, indented by two spaces, all ending in `\n`.
    - `public static List<string> ToDelete(IEnumerable<string> fileNames, int keep)`. It sorts the `session-*.log` names by name, keeps the newest `keep - 1` to leave room for the new file, and ignores other files.
    - `public static StreamWriter Open(string folder, DateTime start)`. It creates the folder, prunes it, and opens the new file with `AutoFlush = true`. On an `IOException` or `UnauthorizedAccessException` it returns null.
    - `public static string Header(string version, string os, DateTime start)`, which gives `"Dead on Arrival <version> on <os>, started <yyyy-MM-dd HH:mm:ss>\n"`.
  - `public class SessionLogWriter : MonoBehaviour`:
    - `CreateOnLaunch` (AfterSceneLoad) makes it only `if (SessionLog.OptedIn(Environment.GetCommandLineArgs()))`.
    - It opens `Path.Combine(Application.persistentDataPath, "logs")`, writes the header, and subscribes to `Application.logMessageReceivedThreaded`, writing under a lock.
    - On a null writer it logs one warning, `"Session log: couldn't write to " + folder`, and does nothing else.
    - In `OnDestroy` it unsubscribes, then closes the file.

- [ ] **Step 1: Write the failing tests** (`SessionLogTests`, EditMode):
  - `OptedIn_OnlyWithTheLaunchOption`: `{"DoA.exe", "-sessionlog"}` and `{"x", "-SessionLog"}` are true; `{}`, `{"x"}` and `{"x", "-sessionlogs"}` are false.
  - `Keeps_ProblemsAndOnlineLines`:
    - Error, Exception and Assert with any text are kept;
    - `(Log, "Online: session ended: x")` and `(Warning, "Online: …")` are kept;
    - `(Log, "Loaded")`, `(Warning, "x")` and `(Log, null)` aren't.
  - `FileName_SortsByTime_InAnyCulture`: `new DateTime(2026,10,6,9,5,3)` gives `"session-20261006-090503.log"`, under `de-DE` too.
  - `Line_AddsTheStack_ForProblemsOnly`:
    - an Exception with the stack `"A()\nB()\n"` gives `"[09:05:03] Exception: boom\n  A()\n  B()\n"`;
    - a Log gives one line.
  - `ToDelete_KeepsTheNewest_AndIgnoresOtherFiles`: from 12 session names plus `"Player.log"`, with `keep = 10`, the 3 oldest are deleted.
  - `Open_WritesAFreshFile_AndPrunes`:
    - set up a temp folder with 10 old session files;
    - `Open` it, write `"x"`, and dispose;
    - expect 10 session files, the oldest gone, and the new file's text `"x"`.
  - `Open_WhenTheFolderCantBeMade_ReturnsNull`: a path whose parent is an existing file.
  - `Header_NamesTheBuild`.

- [ ] **Step 2: Run them, and see them fail** (`NO RESULTS`).

- [ ] **Step 3: Implement `SessionLog` and `SessionLogWriter`.**

- [ ] **Step 4: Run them, and see them pass** (8/8).

### Task 6: Docs, editor chores, full suite

**Files:**
- Create: `docs/steam/in-game-features.md`
- Modify: `EDITOR-TODO.md`, `docs/testing.md`, `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`, `docs/superpowers/plans/2026-10-01-remaining-work-handoff.md`

- [ ] **Step 1: Write `docs/steam/in-game-features.md`.** It covers three things:
  - **Achievements:** the two API names, display names and descriptions for the dashboard; who decides; how online unlocking works; and that app 480 has neither.
  - **Rich Presence:** the token table, and uploading the `.vdf`.
  - **Session logs:** how a player turns them on (Steam → the game → Properties → Launch Options: `-sessionlog`), where the files are, and what's in them.

  The achievement texts are:

  | API name | Display name | Description |
  |---|---|---|
  | `FIRST_DELIVERY` | First Delivery | Deliver your first order in a match. |
  | `GOLDEN_WIN` | Golden Finish | Deliver the golden order and finish first. |

- [ ] **Step 2: Update `EDITOR-TODO.md`** with a new section, as short bullets:
  - Optional now: with Steam running, play one offline match, then check the friends list shows "Delivering solo". That works on app 480: Steam shows the raw token until the `.vdf` is uploaded.
  - Optional now: launch a build with `-sessionlog`, then open the `logs` folder.
  - At Phase 4B: create the two achievements, upload the icons and the `.vdf`.

- [ ] **Step 3: Update the docs.**
  - **Roadmap:** tick Task 1.5's optional line, and Phase 4's achievements and logs lines, each with a short *(Phase 4A: …)* note.
  - **Handoff:** set the status, mark section 2 as done, and move its user steps into 4B.
  - **`docs/testing.md`:** the new test files, and port 7810 is now taken (the next free port is 7811).

- [ ] **Step 4: Run the full suite in the background.**

  Run: `bash tools/run-tests.sh`. Expected: 514 + 12 + 9 + 5 + 3 + 8 = **551** pass. Then check `git status` for files without a `.meta`.

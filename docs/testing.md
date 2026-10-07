# Testing

## In the Unity editor

- Window → General → Test Runner → EditMode → **Run All**.
- It includes **LocalMatchSmokeTest**, an automated 4-player match that takes a minute or two:
  - It presses Play in the menu scene and joins 4 virtual controllers.
  - It plays through player select (player 4 leaves with B and joins again), the loading screen, the opening cutscene and a skipped tutorial.
  - It drives, drifts and boosts, checks the match clock runs, pauses, then unplugs player 4's controller, plugs it back in and boosts with it.
  - It fails on any error or exception on the way.
- `OnlineSessionNetworkTests` start online hosts and clients on this computer (127.0.0.1, port 7791) in an empty Play Mode scene, a few seconds per test. Nothing needs to be running for them.
  - `OnlinePlayersNetworkTests` (port 7792) do the same for seats, players' choices and the host's game states.
  - `RemoteAvatarTests`, `OnlineSeatTests` and `OnlineGameNetworkTests` (port 7793) play in the real menu scene, about 10–20 s each. In `OnlineGameNetworkTests`, the other machine is a session without the game.
  - `OnlineScootersNetworkTests` (port 7794, empty scene): every machine's scooter, its pose and flags, and the moves that jump (long ones, and any while it's hidden).
  - `OnlineDrivingNetworkTests` (port 7795, menu scene): another machine's scooter following its owner, and this machine's reaching the others.
  - `OnlineMatchNetworkTests` (port 7796): a whole online start, hosting and joining, and a machine leaving on the loading screen or mid-match. They load the game scene, so about a minute each.
  - `OnlinePlayNetworkTests` (port 7797, menu scene): Y hosting a lobby, a friend joining and B ending it; and joining a friend's lobby from the title screen. Steam is a fake (`OnlineFakes`), and sessions go by IP address.
  - `OrderMessagesNetworkTests` (port 7798, empty scene): the host's order changes reaching clients in order, a client's drop request reaching the host, and the host's scores and golden-order value.
  - `TutorialMessagesNetworkTests` (port 7800, empty scene): a client's tutorial report and cutout request, each reaching the host with who sent it.
  - `OnlineTutorialNetworkTests` (port 7801): the tutorial online. Hosting, the first wave waits for every machine's player (or for one who leaves), and the host hands out the cutouts' orders. Joining, this machine's player does the tutorial and steals from their cutout, and the host hears when they finish. They load the game scene: a minute or two each.
  - `OnlineOrdersNetworkTests` (port 7799): a match's orders, hosting and joining. Hosting, the host decides another machine's pickups, deliveries and drops, and shares the scores. Another machine leaves while its delivery is in the air (it lands as usual), or holding the golden order (back at its start, and the golden round goes on: the one test that plays the golden round). Joining, the host's orders show on whichever scooter holds them, and nothing changes here by itself. They load the game scene: a minute or two each.
  - `StealMessagesNetworkTests` (port 7802, empty scene): a client's steal request reaching the host with who sent it, and the host's hits reaching clients in order.
  - `RespawnMessagesNetworkTests` (port 7803, empty scene): a client's respawn request reaching the host with who sent it and where, and the host's respawn point reaching clients.
  - `OnlineStealsNetworkTests` (port 7804): steals and clashes, hosting and joining. Hosting, two other machines over a 150 ms connection try to steal from each other at once: one steal wins, the same everywhere. Joining, this machine's player's hit goes to the host, and the host's steals and clashes show here. They load the game scene: a minute or two each.
  - `OnlineRespawnsNetworkTests` (port 7805): respawn points, hosting and joining. Hosting, no two players rise on one point. Joining, this machine's player rises where the host says, or on its own point without an answer. They load the game scene: a minute or two each.
  - `CueMessagesNetworkTests` (port 7806, empty scene): a client's one-shot reaching the host with who sent it, and the host's one-shots and clock reaching clients in order.
  - `OnlineCuesNetworkTests` (port 7807): one-shots, hosting and joining. Hosting, this machine's player's one-shots reach every client, another machine's play on its scooter here (silent far away), and the host's clock rings out. Joining, this machine's player's fall and rise reach the host, and the host's player's gravestone and sparkle show here. They load the game scene: a minute or two each.
  - `OnlineMatchFlowNetworkTests` (port 7808): on a client, the host's opening cutscene ending (and only the host skipping), the wave bells, and time up stopping this machine's player. It loads the game scene: a minute or two.
  - `DisconnectsNetworkTests` (port 7809): a machine going silent (another machine while this one hosts, and the host while this one joins), the host's own connection failing, and the host leaving during the opening cutscene (paused) and during both halves of a load. "Silent" means its `UnityTransport` is switched off, with 2 s timeouts. Three of them load the game scene.
  - `AchievementMessagesNetworkTests` (port 7810, empty scene): the host's achievements reaching every client with their seat, a client never sending, and each machine unlocking only its own player's (`OnlineAchievements`, with fake stores).
  - `MessageLimitsNetworkTests` (port 7811, empty scene): a flood of requests capped by the host, the flooding machine disconnected, and an honest pace all arriving (`RateGate`).
  - `NonsenseNetworkTests` (port 7812, empty scene): a client ignoring nonsense from the host (undefined states, scenes, cues and achievements, an order change at no place, a clock that never ends).
  - `BumpsNetworkTests` (port 7815, Phase 5A): another machine's bump pushes the host's player away from them, and a bump for a seat the sender doesn't hold, one that isn't a number and a flood do nothing. It loads the game scene: a minute or two.
  - Port 7813 is kept for Phase 3J's plan. The next free port is 7816.
- Phase 5A, without Play Mode unless said:
  - `BumpRulesTests` (how hard a bump pushes, which count, how often), `PoseHoldTests` (a NaN pose isn't followed), `ConnectionGateTests.Ban_…` (a kicked Steam ID can't reconnect).
  - `PlayerJoinerTests` (a controller joins on any button, a keyboard on Space or Enter only); `KeyboardPlayerTests.PressingOtherKeys_SpawnsNothing` (menu scene) checks no player is made for other keys, even for a frame.
  - `LateCameraTests` (menu scene): a full-screen camera turned on in another script's `LateUpdate` turns the players' views off that frame.
  - `CutoutHandlerTests`, `OrderHandlerResetTests`, `NorthButtonWiringTests`, and new cases in `GraphicsQualityTests`, `FrameStatsTests`, `OnlineSessionTests`, `OnlineLobbyTests`, `SteamLobbyServiceTests`, `OnlinePlayTests`, `OnlineTestMenuTests`, `LobbyPromptTests`, `LobbyRulesTests`, `DisplayRulesTests`, `SessionLogTests` and `PresenceRulesTests`.
  - Network: `DisconnectsNetworkTests.Joining_TheHostReturnsWhileThisMachinesSceneComesUp_…` (no abandoned match state shows), `OnlineOrdersNetworkTests.Joining_AGoldenHolderLeaving_…` (the golden order back at its start here), and `OnlineCuesNetworkTests`' remote death and phase sounds.
- Tests reach the game's private members directly: they're `internal`, and the editor assembly sees them (`Assets/Scripts/AssemblyInfo.cs`). `Reflect.HandlerCount` is the one reflection left, for event subscriber lists.
- Online safety without Play Mode (`docs/online-safety.md`): `SteamPeerRulesTests` (lobby members only, message sizes, relay-only options), `RateGateTests`, `NetChecksTests`.
  - `OnlinePauseTests` (menu scene, no network): pausing online lets the match go on, says what Main Menu does, closes with the host's state, and a controller lost while paused doesn't pause again.
  - The first load of the game scene in a run can take over 40 s when it's cold (the first run after assets changed: it once took 41 s). Direct sessions wait 90 s for a silent machine (Phase 3I), so the other machines stay. Each load's time is in the log: `Online: the Game scene was ready here after …`.
  - When a machine leaves, Windows often reports its closed port to the others, and Unity Transport logs that as an error (`docs/online.md`, Known limits). Tests where machines leave call `LogCollector.MachinesLeave()` first: that one message is let through, and any other error still fails the test.
- The Steam lobby flow (`OnlineLobbyTests`, `LobbyRulesTests`) runs over fakes of Steam and the session (`OnlineFakes`), without Play Mode. `SteamLobbyServiceTests` checks Steam's lobbies without Steam: with Steam they're checked on two PCs (`docs/online.md`).
- `GameSceneSpawnTests` opens the game scene in the editor, without Play Mode. It checks the start points, at the start of each seat's tutorial lane: one per seat, in the open, over ground that isn't water.
  - `OrderBookSceneTests` opens both match scenes the same way: every order in them has its own key, so online an order change always names one order.
- The rules for orders online run without Play Mode:
  - `OrderBookTests`: order keys.
  - `OrderChangeTests`: a change as it travels.
  - `OrderSyncTests`: who may change orders, and which changes go out.
  - `OrderRulesTests`: a client's orders wait for the host.
  - `HostQueueTests`: a client's messages from the host wait while its scene changes.
  - `TutorialManagerTests`: the tutorial ends when every seat's player has finished it (online, only the host ends it).
  - `StealRulesTests`: the host's judge of a steal or clash. `PlayerHitTests`: a hit as it travels.
  - `RespawnPointsTests`: the nearest free respawn point, the host's holds, and where a gravestone stands.
  - `ScooterCueTests`: a one-shot as it travels. `RemoteSoundTests`: how loud another machine's scooter is here.
  - `PausePolicyTests`: when the pause menu closes online, and what pausing says.
  - `LoadWatchTests`: timing a match load for the log.
- The Steam features in the game (Phase 4A, `docs/steam/in-game-features.md`) run without Steam:
  - `AchievementsTests`: who earns which achievement (`MatchFeats`), and Steam's store without Steam.
  - `PresenceRulesTests`: what friends see for each game state, and Steam's presence without Steam.
  - `SessionLogTests`: the opt-in session logs' rules and files (in a temp folder).
  - `SteamFeaturesTests` (empty Play Mode scene, fake stores): a local delivery unlocks, a client's replay counts for nothing, an online host leaves unlocking to `OnlineAchievements`, and each state sets the presence.
- Display settings and the keyboard (Phase 1C, `docs/controls.md`):
  - `GameSettingsTests` also cover the display keys in `settings.cfg`; `DisplayRulesTests`: fullscreen mode, a size the screen can show, VSync and the frame cap.
  - `KeyboardControlsTests` (EditMode, on a copy of the input actions): the keyboard-only scheme, a key for every action, installing twice, uninstalling.
  - `KeyboardPlayerTests` (menu scene, a test keyboard): joining on Space only, readying up, and the online seat move. In batch mode the editor has no focus, so these tests let keyboard input through (`editorInputBehaviorInPlayMode`, `backgroundBehavior`) and put the settings back after.
- Frame rate (Phase 4D, `docs/performance.md`): `FrameStatsTests` (average, 1% low, the `Perf:` line), `GraphicsQualityTests` (each level by player count, SSAO and the shadow map, put back on quit), `CameraBudgetTests` (which cameras need to render), `FrameRateHotPathsTests` (the boost's wide view, the compass order, the timer text). `LocalMatchSmokeTest` also checks that the players' views stop under the cutscene camera and the preview camera stays off in a match.
- Leave the editor alone while it plays. Controllers you touch count as input.
- Run a single test: select it in the Test Runner → **Run Selected**.

## From the command line

- `bash tools/run-tests.sh` runs every EditMode test in a mirror copy of the project, so it works while the editor is open.
- `bash tools/run-tests.sh LocalMatchSmokeTest` runs one test class. The filter is a class or test name, or a regex.
- Results go to `Logs/test-results.xml`, and Unity's log to `Logs/test-unity.log`.
- The mirror sits at `../_doa_test_mirror/CapstoneYear4`. It takes about 7 GB and needs a short path: Windows paths over 260 characters break the import.
- `DOA_MIRROR` and `DOA_UNITY` change the mirror folder and the Unity install.
- `DOA_NO_SYNC=1` tests the mirror without copying the project into it first.
- Unity runs with graphics, because the smoke test renders real scenes. Unity started with `-nographics` crashes in URP.

## A quick compile check

- `python tools/compile-check.py` compiles the game, editor and test scripts in about 30 s, without Unity. It ends with `COMPILES` or the errors. Use it while editing, then run the tests.
- `--warnings` also lists unused locals and private fields.
- It needs the .NET SDK, the `.csproj` files Unity generates (Assets → Open C# Project, once), and a mirror that `run-tests.sh` has built once: package assemblies come from there.
- Its file list is rebuilt from disk each time, so scripts added, moved or deleted outside the editor still count.

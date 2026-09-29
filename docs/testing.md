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
  - `OnlineOrdersNetworkTests` (port 7799): a match's orders, hosting and joining. Hosting, the host decides another machine's pickups, deliveries and drops, and shares the scores. Joining, the host's orders show on whichever scooter holds them, and nothing changes here by itself. They load the game scene: a minute or two each.
  - When a machine leaves, Windows often reports its closed port to the others, and Unity Transport logs that as an error (`docs/online.md`, Known limits). Tests where machines leave call `LogCollector.MachinesLeave()` first: that one message is let through, and any other error still fails the test.
- The Steam lobby flow (`OnlineLobbyTests`, `LobbyRulesTests`) runs over fakes of Steam and the session (`OnlineFakes`), without Play Mode. `SteamLobbyServiceTests` checks Steam's lobbies without Steam: with Steam they're checked on two PCs (`docs/online.md`).
- `GameSceneSpawnTests` opens the game scene in the editor, without Play Mode. It checks the city start points, where players start online: one per seat, in the open, over ground that isn't water or the tutorial area.
  - `OrderBookSceneTests` opens both match scenes the same way: every order in them has its own key, so online an order change always names one order.
- The rules for orders online run without Play Mode:
  - `OrderBookTests`: order keys.
  - `OrderChangeTests`: a change as it travels.
  - `OrderSyncTests`: who may change orders, and which changes go out.
  - `OrderRulesTests`: a client's orders wait for the host.
  - `HostQueueTests`: a client's messages from the host wait while its scene changes.
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

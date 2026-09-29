# Online play

Online multiplayer is being built in steps (roadmap Phase 3: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`). Local split-screen doesn't touch any of it: nothing online runs unless a player presses Y in player select or accepts a Steam friend's invite.

## What's there so far

### Phase 3A: the session

- Packages:
  - Netcode for GameObjects **1.15.1**, with Unity Transport 1.5.0. 1.15 is the newest line for Unity 2022.3; 2.x needs Unity 6.
  - The community **SteamNetworkingSockets** transport, pinned to commit `d862504b`. Its newer commit targets Unity 6's transport.
  - **ParrelSync 1.5.3** (editor only).
- `OnlineSession` (`Assets/Scripts/Online/OnlineSession.cs`) hosts or joins a match. Its role is Host or Client while a session runs, Offline otherwise.
  - Direct, by IP address (Unity Transport): `HostDirect` / `JoinDirect`. For the editor and LAN.
  - Steam, peer to peer through Steam's relay: `HostSteam` / `JoinSteam(hostSteamId)`. For builds with Steam running. The Steam lobby that hands out the host's Steam ID is roadmap Task 3.2.
  - `Leave` ends a session.
  - `Ended` says why a session ended by itself: "The host left the match.", "Couldn't reach the host.", the host's reason for turning a player away, or (on the host) "The connection was lost."
- `JoinRules` (`Assets/Scripts/Online/JoinRules.cs`): the host lets a player in only if all three hold:
  - They're on the same build (`Application.version`).
  - There's a free seat (4 players).
  - The host is still on the title screen, in options or credits, or in player select.

### Phase 3B: online player select

- **Seats.** The host takes the first seat (P1) and each joiner the lowest free one. A seat is the player's slot on every machine: their company, podium, spawn point and name (P1–P4). In code, seats count from 0.
- **One player per machine** (until roadmap Task 3.9).
  - This machine's player moves into its seat, taking their controller with them.
  - Any other controller is turned away.
  - The Online menu won't host or join with more than one player here.
- **Player select is the online lobby.**
  - Every other player's scooter stands on its podium, in its company's colours, with their ghost colour and hat, which update as they change them.
  - Who's ready counts on every machine.
- **Clients follow the host's menus:** title screen and player select. The host's options and credits show as the title screen.
- **When everyone's ready, the match starts on every machine** (Phase 3C, below).
- How it works:
  - **Network objects.** The host spawns two kinds of small network objects: `OnlineMatch` (its game state, sent one state at a time and in order) and an `OnlinePlayer` per machine (seat, colour, hat, ready).
    - The prefabs are in `Assets/Prefabs/Online/`, listed in `Assets/Resources/Online/OnlinePrefabs.asset`.
    - Netcode also lists them in `Assets/DefaultNetworkPrefabs.asset`.
    - Each prefab file saves its Netcode id, and a build uses the saved one. After a script builds or changes an online prefab, use **Tools → Dead on Arrival → Online → Save Network Prefab IDs** (`NetworkPrefabIds`). `OnlinePrefabsTests` checks the saved ids.
  - **`OnlineGame`** is the game's side of a session:
    - It sets `GameAuthority.Role` and the scene flow (`OnlineSceneFlow`).
    - It seats this machine's player, and shows other machines' players.
    - It shares choices and relays the host's states.
    - Whatever starts a session for the game adds one: the Online menu now, the Steam lobby later.
  - **Another machine's scooter** is `PlayerAvatar.prefab` alone (`RemoteAvatar`).
    - Its controls, physics, water respawns and horn gauge are off.
    - It's dressed by `ScooterLook` (see `docs/player-prefab.md`).

### Phase 3C: driving together

- **Starting the match.** When everyone's ready, the host starts the load on every machine:
  - Each machine loads the game behind its loading screen and tells the host when it's done. Online there's no "press A".
  - Once every machine has it (or has left), the host shows it everywhere, and the match starts together.
  - The golden round's scene loads the same way.
- **Each machine drives its own scooter.** Every other machine shows it where its owner has it, with its boost trail, skid marks, drift sparks and rider.
  - A scooter that spawns far away appears there at once. It doesn't slide across the map.
  - A scooter that falls in the water disappears on other machines, and bumps nobody there, until it rises from its grave where its machine shows it.
  - Until its machine sends a first position, it stays where it was (its podium).
- **The host runs the match:** its states (cutscenes, waves, results) and its match clock reach every machine.
  - Online there's no tutorial yet. It teaches orders, which aren't shared between machines yet. The players start in the city instead, on the spawn manager's city start points (`TutorialManager.IsSkipped`).
- **Back to the menu:**
  - The host takes everyone back, from the results or its pause menu. On a client, the results screen waits for the host.
  - Online, the menu opens on player select, the lobby, not the title screen (`MainMenu.Start`). Everyone can ready up again, and Y and B work there.
  - A client picking "main menu" on its own leaves the session.
- **A machine that leaves mid-match:** its scooter goes on every other machine, and the match goes on. One that leaves on the loading screen: the match starts without it.
- How it works:
  - **Scene loads:**
    - `OnlineSceneFlow` and `LoadRound`: the host starts every load, counts who's ready and shows the scene.
    - `SceneManager`'s held loads (`IMatchLoader`) do the loading.
    - A state that reaches a client while its new scene is still coming up waits for it (`OnlineGame`).
    - Netcode's own scene management stays off.
  - **`OnlineScooter`:** one per machine, beside its `OnlinePlayer`.
    - Two proxies carry its pose, each moved by its owner through an `OwnerNetworkTransform`: the ball, and the model's world pose (slope, lean, drift, hop and wheelie).
    - `DriveFlags` say what it's doing, including a rider hidden for a respawn (`Respawn.RiderHidden`). While it's hidden, every move jumps.
    - The proxies wait parked 10 km below the map until the first pose.
  - **`OnlineDriving`** (added by `OnlineGame`) runs every frame: it sends this machine's scooter out, and shows the others on their `RemoteAvatar` (`ScooterPose`, `BallDriving.ShowRemote`).
  - **The match clock** is the host's end time, in Netcode's server time (`MatchClock`, on `OnlineMatch`). With no waves running (the menu, after a match) the host shares a stopped clock.
  - **Another machine's scooter doesn't act on this machine** (`RemoteAvatar.IsRemote`): no water, orders, steals, clashes or end-of-game freeze here. Its own machine does those.

### Phase 3D: playing over Steam

- **Y (triangle) in player select plays online** with Steam friends:
  - It makes a friends-only Steam lobby for four, hosts the match in it, and opens Steam's invite dialog.
  - Once online, Y opens the invite dialog again. Without Steam's overlay (the editor has none), a message says to invite from the Steam friends list.
  - A line along the top of player select says what Y and B do.
  - One player per machine: with two players here, Y says the others need to leave first.
- **Friends join through Steam:** they accept the invite, or pick "Join Game" on the host in their friends list. With the game's own App ID that works whether their game is running or not (Steam starts it with `+connect_lobby`). With the test App ID, start the game first (Known limits).
  - The game answers once its title screen is up, and lands in the host's player select.
  - A lobby from another game or another build is left, with a message. So is one that answers after the player moved on.
  - A player in the middle of a local match is told to finish it first.
- **B (not ready) in player select leaves.** The host's B ends the match for everyone: the others see "The host left the match." and stay in their player select, offline.
- **Why a session ended** shows in the controller hint bar for 5 seconds (`OnlineGame.NOTICE_SECONDS`).
- How it works:
  - **`OnlineLobby`** is the lobby flow, in plain C# over `ILobbyService` (Steam's lobbies: `SteamLobbyService`) and `ISessionControl` (the game's session), so it's tested with fakes.
    - The lobby is tagged `game=doa` and `build=<version>` (`LobbyRules`).
    - It's left whenever the session stops. The host's lobby takes friends only while the host is in the menus.
  - **`OnlinePlay`** (made at launch) owns the lobby flow and the Steam session, which `OnlineGame` plays. It answers invites, keeps the lobby in step with the game's state, and shows the line along the top (`LobbyPrompt`).
  - **Y** is the UI map's "North Face" action, wired in code (`PlayerUIHandler.NORTH_ACTION`).
  - `OnlineGame.LeaveOnline` leaves whichever session this machine plays, so B works for the Steam one and the editor's.

## Two editors on one computer (ParrelSync)

- **ParrelSync → Clones Manager → Create new clone** (once). The clone shares this project's Assets and ProjectSettings. Unity imports the project the first time the clone opens, which takes a while.
- **Open in New Editor**.
- Press Play in both editors (from `SplashScreen`, as usual). Press A on a controller in each: each editor needs its player 1.
  - A controller only drives the editor that has focus, so click into an editor before pressing buttons.
- Editor 1: **Tools → Dead on Arrival → Online → Host**. It goes into player select. The Console shows `Online: hosting version …`.
  - **Host** and **Join** stay greyed out until the title screen is up: the game joins a session through managers that load with it.
- Editor 2: **Tools → Dead on Arrival → Online → Join This Computer**.
  - Editor 2's player moves to the second podium, in the second company's colours.
  - Each editor shows the other's scooter.
  - Editor 1 shows `Online: player 1 joined (2 in)`.
- Change colour or hat in one editor and watch the other.
- Ready up in both. After the countdown both show the loading screen, and the game appears in both at once: the host waits for the other editor.
  - The opening cutscene plays in each, then driving starts in the city (no tutorial online).
  - Drive in one editor and watch the other: the scooter moves there, boosting and drifting.
- Pause in editor 1 and pick **Main Menu**: both go back to player select, still in the session. In editor 2 (a client), **Main Menu** leaves the session.
- **Leave** ends a session. When the host leaves, editor 2 shows `Online: session ended: The host left the match.`
- Only change files in the original editor: clones share them.

## Two PCs over Steam

- Two PCs, each running Steam, signed in to two accounts that are friends. An account can't join itself, so one PC can't test Steam.
- A build on each, with a copy of `steam_appid.txt` next to the `.exe`. Start the game on both before inviting (the test App ID: Known limits).
- PC 1: Play → A → player select. The line along the top says "Y / Triangle: play online with Steam friends". Press Y, then invite the friend in Steam's overlay.
- PC 2: accept the invite (or pick "Join Game" on PC 1's player in the friends list). It lands in PC 1's player select, in the second seat.
- Ready up on both: the match starts. After it, both are back in player select.
- PC 1: B. PC 2 shows "The host left the match." and stays in player select.

## Bad connection

- **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)**: while ticked, the next Host or Join in that editor adds 150 ms of delay and 1% packet loss (Unity Transport's own simulator; editor only). The tick lasts until the editor closes.
- For builds, use a tool like *clumsy* (Windows).

## Known limits

- A player whose build has a different Netcode setup (tick rate, network prefabs…) is dropped by Netcode before the version check, and sees "Couldn't reach the host." Over Steam the lobby's build tag keeps such players apart: a lobby of another build is left, with a message.
- Hosting fails if another program uses port 7777.
- Direct joins give up after 10 seconds.
- On one computer, when one side closes (or both close at once), Unity Transport may log `All socket receive requests were marked as failed…` as an error in the other editor: Windows reporting the closed port. The session was ending anyway.
- **Orders, steals and clashes are the host's alone** until roadmap Tasks 3.5–3.6:
  - Only the host's player collects orders, and other machines don't see them.
  - The results count only the host's deliveries.
  - The golden round ends when the host delivers the golden order.
- There's no tutorial online (it teaches orders): players start in the city.
- **Other machines' scooters are silent**, and a respawn shows no wisp: the scooter is gone until it rises from its grave (roadmap Tasks 3.6–3.7).
- When the main game ends, only the host's scooter stops; the others keep driving until the golden round loads.
- If the host leaves mid-match, clients stay where they are, offline (roadmap Task 3.8).
- An empty seat still says "press A to join" on every machine, though only a machine's first controller can take a seat online.
- When the host leaves, clients see "The host left the match." and stay in player select with their own player, not ready: no countdown starts, and a running one stops. They ready up again to start one.
- **Friends only:** no public lobbies or lobby list yet. Friends join through Steam's invites and "Join Game".
- A Steam account can't join itself, so two editors on one computer play over the Online menu (by IP address), not Steam.
- Until the game has its own App ID, it runs as Valve's test app (480): an invite accepted with the game closed starts Spacewar. Start the game first. A build also needs a copy of `steam_appid.txt` next to its `.exe`.
- The title screen has no "Play Online" row until there's art for one: Y in player select is the way in.

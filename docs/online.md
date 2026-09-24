# Online play

Online multiplayer is being built in steps (roadmap Phase 3: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`). Local split-screen doesn't touch any of it: nothing online runs unless a session starts.

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
- **The match doesn't start online yet** (Phase 3C). When the ready-up countdown ends, the Console says `Online: starting an online match isn't in yet…` and nothing loads. Leave the session to play locally.
- How it works:
  - **Network objects.** The host spawns two kinds of small network objects: `OnlineMatch` (its game state, sent one state at a time and in order) and an `OnlinePlayer` per machine (seat, colour, hat, ready).
    - The prefabs are in `Assets/Prefabs/Online/`, listed in `Assets/Resources/Online/OnlinePrefabs.asset`.
    - Netcode also lists them in `Assets/DefaultNetworkPrefabs.asset`.
  - **`OnlineGame`** is the game's side of a session:
    - It sets `GameAuthority.Role` and the scene flow (`OnlineSceneFlow`).
    - It seats this machine's player, and shows other machines' players.
    - It shares choices and relays the host's states.
    - Whatever starts a session for the game adds one: the Online menu now, the Steam lobby later.
  - **Another machine's scooter** is `PlayerAvatar.prefab` alone (`RemoteAvatar`).
    - Its controls, physics, water respawns and horn gauge are off.
    - It's dressed by `ScooterLook` (see `docs/player-prefab.md`).

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
- Ready up in both. The countdown runs, then ends with the "isn't in yet" message.
- **Leave** ends a session. When the host leaves, editor 2 shows `Online: session ended: The host left the match.`
- Only change files in the original editor: clones share them.

## Bad connection

- **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)**: while ticked, the next Host or Join in that editor adds 150 ms of delay and 1% packet loss (Unity Transport's own simulator; editor only). The tick lasts until the editor closes.
- For builds, use a tool like *clumsy* (Windows).

## Known limits

- A player whose build has a different Netcode setup (tick rate, network prefabs…) is dropped by Netcode before the version check, and sees "Couldn't reach the host." The Steam lobby's build filter (roadmap Task 3.2) keeps such players apart.
- Hosting fails if another program uses port 7777.
- Direct joins give up after 10 seconds.
- On one computer, when one side closes (or both close at once), Unity Transport may log `All socket receive requests were marked as failed…` as an error in the other editor: Windows reporting the closed port. The session was ending anyway.
- The match can't start online yet (Phase 3C).
- An empty seat still says "press A to join" on every machine, though only a machine's first controller can take a seat online.
- When the host leaves, clients stay in player select with their own player. Going back to the menu with the message is roadmap Task 3.8.
- There's no "Play Online" in the game's menus yet: the Online menu above is the way in (roadmap Task 3.2).

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

- **ParrelSync → Clones Manager → Create new clone** (once). The clone shares this project's Assets, Packages and ProjectSettings. Unity imports the project the first time the clone opens, which takes a while.
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
- On one computer, when one side closes (or both close at once), Unity Transport may log `All socket receive requests were marked as failed…` as an error in the other editor: Windows reporting the closed port. The session was ending anyway.

# Online play

Online multiplayer is being built in steps (roadmap Phase 3: `docs/superpowers/plans/2026-09-22-steam-split-screen-and-online.md`). Local split-screen doesn't touch any of it: nothing online runs unless a player picks Online Play on the title screen or accepts a Steam friend's invite (Local Play and Online Play, below).

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
- **This machine's player moves into its seat**, taking their controller with them. Since Phase 3J several players can share one machine: see Phase 3J below.
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
  - Until Phase 3F there was no tutorial online, and players started in the city. Now they start in their tutorial lanes.
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
  - **Another machine's scooter doesn't act on this machine** (`RemoteAvatar.IsRemote`): no water, steals, clashes or end-of-game freeze here. Its own machine does those. (Its orders: Phase 3E.)

### Phase 3D: playing over Steam

- **Y (triangle) in player select plays online** with Steam friends:
  - It makes a friends-only Steam lobby for four, hosts the match in it, and opens Steam's invite dialog.
  - Once online, Y opens the invite dialog again. Without Steam's overlay (the editor has none), a message says to invite from the Steam friends list.
  - A line along the top of player select says what Y and B do.
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

### Phase 3E: orders online

- **Every machine sees the same orders.** The host runs them as a local match does:
  - Its waves spawn them.
  - Its beacons decide every pickup and delivery, for every scooter, another machine's too: that scooter counts where the host sees it.
- **Each change to an order reaches every machine, in order with the host's states** (spawn, pickup, delivery, drop, erase). Each client replays it through the same game code, so everything a pickup does happens everywhere:
  - The order rides on the right scooter. Its beacon, compass marker and arrow show.
  - Its dropoff glows for the holder's camera only, as in split-screen.
  - The rider slows down with the golden order and their boost recharges as in a local match.
- **Falling in the water holding orders:** the host drops them on the spots of the player's respawn point (since Phase 3G the host picks that point too). It picks how high each one flies and drops them for everyone. A machine can only drop its own player's orders.
- **Scores are the host's.** Each delivery scores on the host, and each player's score reaches every machine, with the placings. The golden order's growing value comes from the host too. The results match everywhere.
- **A machine that leaves mid-match:** its player's orders go back to the pool, instead of going with their scooter.
- How it works:
  - **Order keys** (`OrderBook`): every machine knows an order by its scene, value, pickup point and dropoff point, hashed the same way everywhere.
    - `OrderBookSceneTests` checks that no two orders in a match scene share a key.
    - There's no Netcode object per order: orders ride on scooters, and live in scenes Netcode doesn't manage.
  - **`OrderChange`** is one change as it travels, on `OnlineMatch` (`SendOrder`). Scores travel on each `OnlinePlayer` (`Score`), the golden value on `OnlineMatch` (`GoldenValue`), and a client's drop request as `AskDrop`.
  - **`OrderSync` says who may change orders:** the host, and offline as always. A client only changes them while it replays one of the host's changes, so its own spawner, beacons and respawns wait for the host. On the host only the outermost change goes out: what it does on the way (a delivery erasing the golden order) replays with it.
  - **`OnlineOrders`** (added by `OnlineGame`) sends the host's changes, replays them on clients, sends a client's drop requests, and shares the scores and the golden value.
  - **A client's host messages wait while its scene changes** (`HostQueue`): states and order changes stay in the host's order, and the golden order shows once the golden round's scene is up.
  - A client ends an order's fall, throw or pickup cooldown before it replays the next change to it (`Order.FinishMoves`), so late messages don't trip over its animations.
  - The orders a client's player holds when the host takes everyone back go back to their own scene (`Order.ReturnHome`): none rides into the menu.

### Phase 3F: the tutorial online

- **Online matches start with the tutorial, as local ones do.**
  - Each player starts at the start of their seat's lane, with its instructions, tutorial order and cardboard cutout.
  - The tutorial orders are the host's, like every order (Phase 3E).
- **Stealing from the cutout:** boosting into it opens your lane's barrier at once. The host hands you the cutout's order, and every machine shows it on your scooter. Every machine shows the other lanes' steals too.
- **The first wave begins once every player has finished the tutorial**, by driving out of it into the city.
  - Each machine tells the host when its own player has finished.
  - A player who leaves mid-tutorial doesn't hold the others up.
  - If the host leaves, everyone goes back to the title screen (Phase 3I).
- How it works:
  - **`TutorialManager` counts seats:**
    - It records each finished seat (`SeatLearnt`).
    - It checks whether every seat still in the match has finished (`RecheckAlumni`), and checks again after a player leaves.
    - Each match starts the count over. Only the host, or a local match, ends the tutorial.
  - **A client's messages** leave the game code through `TutorialSync` and travel on `OnlineMatch` (`ReportLearnt`, `AskCutout`). The host only takes them for the sender's own seat.
  - **`OnlineTutorial`** (added by `OnlineGame`) is the glue.
  - **`CutoutHandler`** knows its seat's lane (`InSeat`):
    - On the host, or offline, a boost into it steals (`StealFor`).
    - On a client, its own player opens the barrier and asks the host.
    - Every machine opens a cutout once its order is taken.

### Phase 3G: steals, clashes and respawns

- **Steals and clashes work between machines, as in a local match.** Boosting into another player's scooter steals their best order if they aren't boosting, and clashes if they are. Every machine sees the same result:
  - A steal: the order moves to the thief's scooter, and the robbed player drops the rest, spins out and bounces away from the thief.
  - A clash: both players bounce off each other.
- **Two players trying to steal from each other at the same moment make one steal**, the same on every machine.
- **The golden order can be stolen online.** A player robbed of it isn't slowed any more (a 2024 bug, fixed in local play too).
- **Two players who fall in the water never rise on the same point**, and each one's orders land on their own point.
- How it works:
  - **The machine that drives the boosting scooter sees the bump and asks the host.** Its request leaves the game code through `StealSync` and travels on `OnlineMatch` (`AskSteal`). The host judges its own player's bumps at once, and takes a request only for the sender's own seat.
  - **`StealRules` is the host's judge.** It checks its own view of the two players:
    - Both are there, and neither is respawning.
    - Their balls are within 50 m (a request takes a moment to arrive).
    - That pair hasn't been hit in the last 2 s, either way round: when both machines ask about one bump, the first request wins.
    - It's a steal if the host sees the victim not boosting, a clash if it sees them boosting. The asker's own boost isn't checked: its flags can arrive after its request.
  - **A steal's effect on orders** travels as Phase 3E order changes: the stolen order moving (`OrderChange.Steal`), then the victim's drop.
  - **Bounces:** `PlayerHit` tells every machine about a steal or clash (`SendHit`), and each machine bounces only its own player.
  - **`OnlineSteals`** (added by `OnlineGame`) is the glue for steals and clashes.
  - **Respawns:**
    - A client whose player falls in the water asks the host where they rise (`RespawnSync`, `AskRespawn`), and waits, hidden, for up to 2 s. With no answer, it picks its own point as before.
    - The host picks the nearest point nobody is rising from, and holds it for the respawn (`RespawnPoint.HoldFor`). It drops the player's orders there, then answers (`SendRespawn`).
    - **`OnlineRespawns`** is the glue.
  - On a client, hits and respawn points wait with the host's other messages (`HostQueue`).

### Phase 3H: one-shots, cutscenes and pause

- **Other players' scooters are heard here:** a boost, a drift boost, a full horn (their horns flash too), phasing through a building, falling in the water, pickups, deliveries and a steal's whoosh. They fade with distance from this machine's player: full within 10 m, silent from 60 m.
- **A player who falls in the water shows their wisp on every machine,** then their gravestone where they rise, and the reborn sparkle as they lift out of it.
- **The host's cutscenes end everywhere when the host's do.** Only the host's skip counts (the developer key L).
- **The wave bells and the whistle sound everywhere,** and at the whistle every scooter stops, not just the host's.
- **Pausing online doesn't stop the match.** The pause menu is this machine's own:
  - It closes when a cutscene, the results, loading or the lobby comes up.
  - A hint says what "Main Menu" does: the host's ends the match for everyone, a client's leaves it.
  - A controller lost while paused doesn't pause again.
- **Pedestrians, cans and slipstream react to other players' scooters too,** at the speed they're going. Each machine has its own pedestrians and cans.
- How it works:
  - **One-shots travel as cues** (`ScooterCue`). The calls that play a scooter's one-shots on its own machine also send a cue, through `CueSync`: `SoundPool` for the sounds, `Respawn` for a rise. A client reports its cues to the host (`ReportCue`). The host takes a cue only for the sender's own seat, shows it, and sends it to every client (`SendCue`). The host's own player's go straight to every client.
  - **Order sounds aren't cues:** every machine replays the host's order changes on every scooter (Phases 3E and 3G), so another player's scooter plays its pickups, deliveries and whoosh here too.
  - **Another machine's scooter plays one-shots only,** through its sound pool, which stays off (no engine hum): `SoundPool.PlayRemote`. `RemoteSound` sets the volume by distance, because the game's sounds are all 2D.
  - **Respawns:** the wisp shows while the rider is hidden (the flags byte). The owner sends `ScooterCue.Rise` with its point, whoever picked it, and other machines put up the gravestone and play the sparkle there (`Respawn.ShowRise`).
  - **The clock:** the host's `OrderManager` rings its bells and time up (`ClockCue`), and every client plays them (`RingClock`, `OrderManager.FollowHostClockCue`). At time up a client stops its own player, as the host does.
  - **`OnlineCues`** (added by `OnlineGame`) is the glue. One-shots don't wait with the host's states (`HostQueue`): a one-shot is about the moment it happens.
  - **Cutscenes:** a client's cutscene ends when the state it hands over to arrives from the host (the tutorial after the opening, the golden round after its cutscene). `CutsceneManager.Skip` works only where states are decided.
  - **Pause:** `PausePolicy` says when the pause menu closes and what it says online. `PlayerInstantiate.IsPaused` says whether it's open; `ControllerDisconnectPolicy` uses that, not `Time.timeScale`.
  - **Another machine's scooter's speed:** `BallDriving.ShowRemote` sets its `CurrentVelocity`, which pedestrians (`BallCollision`), cans (`CanKicker`) and slipstream read. Bumping something here doesn't drop its drift: its own machine does that.

### Phase 3I: disconnects and versions

- **When the host leaves, or its game stops answering, everyone else goes back to the title screen** and sees "The host left the match.", from anywhere in a match: a cutscene, a paused game, or a loading screen.
  - A load that's under way finishes behind the loading screen first, and nothing in it starts.
  - The message shows at once, and again when the title screen is up.
  - A host whose own connection fails goes back too, with "The connection was lost."
- **A player who leaves holding the golden order** puts it back at its start, at its starting value, and the golden round goes on for everyone else.
- **A player who leaves while their delivery is in the air:** it lands as usual, and the order goes back to the pool.
- **A friend whose game is another build is told before joining**, even with the same version number: "That match is on another build of version …". The lobby's build tag carries the build's Netcode setup.
- **In the editor, a machine that stops answering is dropped after 90 s**: a cold load of the game scene can stall for over 30 s. Over Steam, Steam's own timeouts apply.
- **Every machine logs each match load:** "Online: the Game scene was ready here after … s; its longest frame took … s". In a build it's in `Player.log`.
- How it works:
  - **`OnlineGame.ShowEnd`:** a session that ends by itself away from the menus takes the game back through the local loader (`SceneFlow.Current.ReturnToMenu()`), and says why again once the menus are up.
  - **A held load can't be called off:** Unity keeps every later load waiting behind it. So `SceneManager.LoadMenuScene` shows the held scene as soon as it's loaded, and loads the menu as soon as that scene is up, before its scripts start. `ISceneFlow.LeavingForMenu` tells the scene's `SpawnManager` to start nothing. A second trip to the menu while one is on its way does nothing.
  - **A cutscene stops when a load starts** (`CutsceneManager`). Offline by then, it would otherwise ask for the tutorial during the trip to the menu.
  - **The golden order** goes back through `EraseGoldWithoutDelivering`, then `InitOrder` (`OrderHandler.ReleaseOrders`): two order changes clients already replay (`EraseGold`, `Spawn`).
    - `EraseGoldWithoutDelivering` puts its beacon out without erasing the order through it (`OrderBeacon.PutOut`). Erasing it there would deliver it, which ends the golden round.
    - Putting the golden order out turns the golden round back on (`OrderManager.AddOrder`), so its value grows again for the next holder.
  - **A delivery's throw leaves the scooter** (`Order.DeliverOrder`, `ReturnHome`): a scooter that goes mid-throw doesn't take the order with it.
  - **Builds:** `LobbyRules.BuildTag` is the version plus `OnlineSession.NetcodeSetup`: the hash Netcode compares when a player joins (its protocol, the network prefabs' ids and the session's settings). `LobbyRules.Refusal` names both versions when they differ, and says "another build" when only the setups do.
  - **Timeouts:** direct sessions wait `OnlineSession.DIRECT_DISCONNECT_MS` (90 s). Unity Transport reads it when a session starts.
  - **`LoadWatch`** times each match load on every machine: `OnlineSceneFlow.Tick`, from `OnlineGame.Update`.

### Phase 3J: several players on one machine online

- **Up to 4 players in all, any mix of machines:** two players on one PC and two on another, or three and one, each machine split-screening its own players.
  - Each of a machine's players has their own seat, scooter, company and view there, and every other machine sees them as separate players.
  - Hosting or joining with several players here: the first sits down as the session starts; the others a round trip later (on the host, at once).
  - A controller pressing A in player select once online joins this machine's player select, and sits down the same way.
  - **B in player select:** this machine's menu player leaves the match, as before (the host's ends it for everyone). Any other player here leaves player select and gives their seat back.
  - **A full match:** a machine's players the host has no seat for are told "That match is full." and leave player select. A player still waiting for a seat when the match starts is told it has started.
  - Achievements a machine's players earn unlock for that machine's Steam account, as in local split-screen. Other machines' sounds fade with the distance to the nearest of this machine's players.
- How it works:
  - **Seats belong to machines, several each** (`SeatTable`). The host spawns one `OnlinePlayer` and `OnlineScooter` per seat, owned by that seat's machine, with the seat (`OnlineSession.SpawningSeat`). Every host check that a request names the sender's own seat is `OnlineSession.Owns(machine, seat)`.
  - **A machine joins with one seat and asks for each further one:** `OnlineSession.AskSeat` (a client's goes as `OnlineMatch.AskSeat`, rate-limited as `RpcKind.Seat`). The host applies the join rules (full, started), then seats and spawns, or refuses (`SeatRefused`). `FreeSeat` gives one back; the host ignores another machine's seat or a machine's last.
  - **This machine's players move into the seats it holds** (`PlayerInstantiate.GiveSeat`, `CouchSeats.Plan`): a player already in one stays, the menu player takes the first free one, and a move is a leave and rejoin next frame with the same device (as one player's move always was). A player without a seat waits where they joined (`UnseatedLocalCount`), and `OnlineGame` asks the host for one per waiting player (`SeatAsker`).
  - **Rate limits** grow with the seats a machine holds (`RateGate.Allow`'s `players`): four players on one machine send four players' requests.
  - **A seat given back during a load isn't the machine leaving** (`OnlineGame.MachineGone`).

### Phase 5A: bumps, and the deferred fixes

- **Bumping another player's scooter pushes them, on their machine too.** Another machine's scooter is kinematic here, so before this only the bumper moved.
  - The machine whose player drives into another machine's scooter (not boosting: boosting into someone is a steal or a clash) reports the bump, with how fast they closed.
  - The host checks it (both there, within reach, neither respawning; one bump per pair per half second), then every machine hears it.
  - The bumped player's own machine pushes them away from the bumper: a quarter of a clash at 10 m/s, a whole clash at 40 m/s.
- **A host taking everyone back to the menu while a slow machine's match is still coming up:** that machine no longer shows the match starting on the way out.
- **Other machines' sounds play as on their own machine:** a death on the players' mixer group, and a phase takes over the boost's sound.
- **A double "Join Game" enters the lobby once**, and a lobby Steam refuses at once is reported at once (Y isn't left blocked).
- How it works:
  - **Bumps:** `BumpReporter` (added by `OnlineBumps` to each of this machine's players' balls) reports through `BumpSync`. `OnlineMatch.AskBump` takes it to the host (rate-limited as `RpcKind.Bump`), `BumpRules` decides, and `PlayerBump` (`SendBump`) reaches every machine. `BallDriving.PushFrom` pushes.
  - **The menu return:** `OnlineGame` drops the host messages still waiting for this machine's scene when the host's return reaches it (`OnlineMatch.ReturnRequested`).
  - **`OnlineSession.Ended`** calls each listener on its own: one that throws is logged and the rest still hear it (the Steam lobby's leave).

### Local Play and Online Play on the title screen

- **The title screen** has Local Play, Online Play, Options, Credits and Quit. The scene's entries are images, so `TitleEntries` decides what each place does from how many `MainMenu` holds: with 4 (before the Online Play art), it's the old title screen.
- **Local Play** goes to player select as Play did: split screen, with nothing online. Y does nothing and there's no line along the top.
- **Online Play** (`MainMenu.SwapToOnline`) goes to player select, then `OnlinePlay.StartOnline` hosts a lobby (`OnlineLobby.Host`), which opens Steam's invite window. Player select comes first: a lobby still being made is kept only there.
- **Failure:** no Steam, no lobby, or a session that won't start. `OnlineLobby.HostFailed` fires; the reason shows in the hint bar, and for a host attempt from Online Play, OnlinePlay unreadies this machine's players and goes back to the title screen while the game is still in offline player select (`OnlinePlay.BackToTitle`). A player who moved on is left alone, and Y on the old title screen only says why, as before.
- **Y** (`OnlinePlay.YAction`):
  - online: reopens the invite window;
  - offline: nothing, or, on the old 4-entry title screen, it goes online.
- **B** is unchanged.
- **Accepting a friend's invite** works as before, from Local Play's player select too.

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
- **Two players in one editor (Phase 3J):** in editor 2's player select, press A on a second controller (F1 adds a test pad). It joins, and a moment later sits in the third seat, with its own view; editor 1 shows its scooter on the third podium. B on that second player gives the seat back in both editors.
- Change colour or hat in one editor and watch the other.
- Ready up in both. After the countdown both show the loading screen, and the game appears in both at once: the host waits for the other editor.
  - The opening cutscene plays in each, then each player drives their own lane of the tutorial. The first wave starts once both have driven out of it into the city.
  - Drive in one editor and watch the other: the scooter moves there, boosting and drifting.
  - Pick up and deliver orders in either editor: both show them on the scooter that holds them, with the same scores.
  - Boost into the other editor's scooter: steals and clashes show in both. Drive both into the water at one spot: they rise on different points.
  - Boost next to the other editor's scooter: it hears you. Drive one scooter into the water: the other editor sees its wisp, then its gravestone, and it rises from it.
  - Pause in one editor: the other drives on, and the paused one shows a hint about Main Menu.
- Pause in editor 1 and pick **Main Menu**: both go back to player select, still in the session. In editor 2 (a client), **Main Menu** leaves the session.
- Mid-match, stop Play in editor 1: editor 2 goes back to the title screen and says "The host left the match.". It does the same from the opening cutscene (paused or not) and from the loading screen.
- **Leave** ends a session. When the host leaves, editor 2 shows `Online: session ended: The host left the match.`
- Each editor's Console has an `Online: the Game scene was ready here after …` line for each match load.
- Only change files in the original editor: clones share them.

## Two PCs over Steam

- Two PCs, each running Steam, signed in to two accounts that are friends. An account can't join itself, so one PC can't test Steam.
- A build on each, with a copy of `steam_appid.txt` next to the `.exe`. Start the game on both before inviting (the test App ID: Known limits).
- PC 1: Online Play → player select, and Steam's invite window opens: invite the friend (Y reopens it). Before the Online Play entry is in the scene: Play → player select, then Y.
- PC 2: accept the invite (or pick "Join Game" on PC 1's player in the friends list). It lands in PC 1's player select, in the second seat.
- Ready up on both: the match starts. After it, both are back in player select.
- PC 1: B. PC 2 shows "The host left the match." and stays in player select.

## Bad connection

- **Tools → Dead on Arrival → Online → Bad Connection (150 ms, 1% Loss)**: while ticked, the next Host or Join in that editor adds 150 ms of delay and 1% packet loss (Unity Transport's own simulator; editor only). The tick lasts until the editor closes.
- For builds, use a tool like *clumsy* (Windows).

## Safety

What stops a modified game from harming other players (strangers connecting, IP addresses, floods, nonsense values) is in `docs/online-safety.md` (Phase 4C).

## Known limits

- **Direct joins only** (the editor and LAN tests): a player whose build has a different Netcode setup (tick rate, network prefabs…) is dropped by Netcode before the version check, and sees "Couldn't reach the host." Over Steam the lobby's build tag tells builds apart first: a lobby of another build is left, with a message.
- Hosting fails if another program uses port 7777.
- Direct joins give up after 10 seconds.
- On one computer, when one side closes (or both close at once), Unity Transport may log `All socket receive requests were marked as failed…` as an error in the other editor: Windows reporting the closed port. The session was ending anyway.
- **On a client, a steal or clash takes effect a round trip after the bump**, because the host decides it. If the victim started boosting less than a round trip before the bump, the host may call it a steal where a local match would call a clash.
- A client whose player falls in the water rises a round trip later than offline: it waits, for at most 2 s, for the host's respawn point.
- Another machine's scooter can be bumped during the last 0.7 s of its rise from its grave: its collider comes back when its rider shows.
- **A bump pushes the other player a round trip after it:** it goes through the host. The bumper bounces off at once, as before.
- **A machine the host takes back to the menu while its game scene is still coming up may stay in the game scene** (a slow PC; found in Phase 5A, older than it). The match states don't show there any more, but the menu load can stop. Leaving and joining again gets it back. See the handoff's deferred minors.
- **A pickup shows once the host has seen the scooter in the light:** on a client, about a round trip after it drove in.
- A cutout's order reaches a client's scooter about a round trip after its barrier opens.
- **Other machines' scooters play one-shots only:** no engine, brake or drift-spark sounds. Their horns don't glow with their boost gauge; they flash when it's full.
- Another player's one-shot plays here about a round trip after it played there: it goes through the host.
- The pause menu has no "End match" row (its rows are hand-lettered art). The host's "Main Menu" ends the match for everyone.
- A client whose own connection drops is told "The host left the match." too: Netcode gives no reason either way.
- A machine that crashes or loses its connection is noticed after the timeout: 90 s in the editor (direct sessions), Steam's own over Steam.
- An empty seat says "press A to join" on every machine: pressing A there joins this machine's player select, and they sit down a round trip later (on the host, at once).
- **Nobody joins mid-match.** A machine's extra players sit down only while the host is in the menus.
- **A machine with more players than seats left** gets in with the seats there are; its other players are told "That match is full." and leave player select.
- When the host leaves, clients see "The host left the match." and stay in player select with their own player, not ready: no countdown starts, and a running one stops. They ready up again to start one.
- **Friends only:** no public lobbies or lobby list yet. Friends join through Steam's invites and "Join Game".
- A Steam account can't join itself, so two editors on one computer play over the Online menu (by IP address), not Steam.
- Until the game has its own App ID, it runs as Valve's test app (480): an invite accepted with the game closed starts Spacewar. Start the game first. A build also needs a copy of `steam_appid.txt` next to its `.exe`.
- Online Play on the title screen needs its art in the scene (`EDITOR-TODO.md` section 1). Until then the title screen has 4 entries, and Y in player select is the way in.

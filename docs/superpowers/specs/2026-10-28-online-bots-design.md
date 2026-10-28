# Online bots: empty seats fill with CPU drivers

Status: built 2026-10-28 (phases 1-3); playtest pending. Phase G4 of `2026-10-23-modern-features-handoff.md`, online only.

## What the player sees

- Any online match that starts with fewer than 4 players is topped up to 4 with bots named "CPU", unless the host turned **Bots** off in MATCH RULES.
- MATCH RULES gets a 4th row, **Bots: ON / OFF**, default ON. A public (Find / open) lobby plays normal rules, so it always fills.
- Bots appear at the moment the match starts (when the host drops unready seats). Nobody can join after that, so a bot never has to give its seat up.
- Bots pick up and deliver orders, steal, and score like players. They appear on the results podium and in the awards. They get no achievements and no highlight-reel clips of their own.
- Clients see a bot as any other remote scooter, with a colour and hat no human is using.

## How it fits

The host owns every bot. Nothing new goes over the wire for driving: a bot is a host-owned `OnlinePlayer` + `OnlineScooter`, so clients already show it through `RemoteAvatar` and `OnlineDriving`.

1. **Rule.** `HouseRules` gets `Bots` (default true), saved in `settings.cfg` (`GameSettings.bots`), a row in `RulesPanel`, and a summary entry ("NO BOTS"). `ForMatch(publicLobby)` returns Normal, so Bots is ON there.
2. **How many.** A pure `BotFill.Needed(seated, rulesBots)` returns `4 - seated` (0 when the rule is off, or nobody is seated). Unit-tested without Play Mode.
3. **Seats.** At match start the host asks the session to spawn one host-owned `OnlinePlayer` per bot (seat = the lowest free seat, as `SeatTable` hands out). `OnlinePlayer` gets a host-written `isBot` variable so every machine knows not to expect a human (name "CPU", no Steam avatar, no achievements).
4. **The scooter.** On the host a bot's slot is a full scooter (`PlayerAvatar` prefab) with `BallDriving`, `Respawn` and `OrderHandler` left ON and a `BotDriver` attached, and no camera or menus. It joins the roster through a new `JoinBotAt` (no `PlayerInput`, `OwnerClientId` = host). `PlayerSlot` gets `IsBot`; `IsLocal` stays false so no view, compass UI or split-screen is made for it. Clients build it exactly like another machine's player (`RemoteAvatar`).
5. **Sharing.** `OnlineDriving` shares the pose of a host-owned scooter when the slot is local OR a bot.
6. **Orders, steals, respawns.** These already run on the host for other machines' players by seat. A bot is the host's own seat, decided in-process, so it calls the same host paths a client's request ends in (`OnlineSteals`, `OnlineRespawns`, `OnlineOrders`) without a message.
7. **Match flow.** Bots count as ready, finish the tutorial instantly (`OnlineTutorial`), vote for the rematch (yes), and are skipped by the cutscene skip vote, awards' "your machine" logic, achievements and Rich Presence's "Delivering solo".
8. **Colour and hat.** The host picks ones no human uses.

## Risks (what needs Play Mode and two machines to trust)

- Roster consumers that assume `Player` has cameras if `!IsRemote`: `SpawnManager` (compass), `ScoreManager`, `HighlightReel`, `ResultsMenu`, `OnlinePings`. Each needs a bot-safe check.
- `RemoteAvatar.IsRemote` turns off water, steals and clashes on remote scooters. A bot is physically simulated on the host, so it must not carry `RemoteAvatar` there (clients still do).
- Bots keep driving through the golden round and respawn points; `BotDriver` handles the Scooter NavMesh already (G1). Stealing and the golden order are G3 and not in this build: bots deliver only.
- Leaving a rematch: bots are rebuilt each match, never carried over.

## Phases

1. **Rule + count (no Play Mode):** `HouseRules.Bots`, settings, panel row, strings, `BotFill`, tests.
2. **Host bot seats:** `OnlinePlayer.isBot`, `JoinBotAt`, bot scooter spawn at match start, `OnlineDriving` sharing, tests in the existing network harness (a lone host starts a match and gets 3 bots).
3. **Match flow:** ready / tutorial / rematch / results / awards / achievements handling.
4. **Playtest** with two machines.

## Out of scope

Local (couch) bots, difficulty levels, stealing and golden-order play for bots, rubber-banding, bots joining a match in progress.

## As built

- `HouseRules.Bots` (settings key `bots`), a 4th MATCH RULES row, `BotFill.Needed`. A session fills only when `OnlineSession.FillsWithBots` is set: OnlinePlay and the editor's Online menu set it; sessions made by tests don't, so existing network tests are unchanged.
- The host seats bots in `OnlineGame.OnStateApplied` as the match leaves the menus (`OnlineSession.AddBots`); they go when it returns to them (`RemoveBots`). A bot is a host-owned `OnlinePlayer` (`IsBot`) + `OnlineScooter`; on the host `OnlineGame.SeatHostBots` builds its scooter from the avatar prefab (`PlayerInstantiate.AddBotPlayer`, `PlayerSlot.IsBot`) with a `BotDriver` (`OnlyInRound`) at the wheel. `BallDriving` now tolerates having no camera, rumble or speed-line view.
- Tutorial skips bot seats (`TutorialManager.RecheckAlumni`), achievements ignore them, the results vote counts them as Rematch.
- Tests: `BotFillTests`, `HouseRulesTests`, `RulesPanelTests`, `OnlineBotsNetworkTests` (port 7836: three bots fill a lone host's match and leave with it; Bots off adds none).

## Not checked yet (playtest)

- Two machines: a client seeing a bot drive and deliver, scores and podium with bots, bots in the golden round, rematch with bots.
- Bots' sounds and one-shots (boost, fall) don't play on clients: OnlineCues sends only machines' own players.
- A bot's pedestrian/can bumps are only what the host's physics does.

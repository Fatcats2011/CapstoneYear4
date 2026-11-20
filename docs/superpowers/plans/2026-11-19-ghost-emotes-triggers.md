# Ghost emotes, part 3: playing them in the game

Spec: `docs/superpowers/specs/2026-11-19-ghost-animations-design.md`. Parts 1–2: `2026-11-19-ghost-emotes-core.md` and `-clips.md`. Branch `steam-phase1a`. Nothing is committed.

## Rules
- **Every machine shows every ghost's emotes.** Each machine plays an emote from an event it already sees, so no new network traffic is needed. The two exceptions get one-shot cues: d-pad emotes, which nothing sends online today, and hard hits, which only the host knows about.
- **Emotes are cosmetic.** They never change gameplay, timing or controls, and the picked version is per machine.
- **Spam control** stays with `GhostEmoteRules` (priorities and cooldowns). The trigger thresholds below add to it.
- **Core gameplay code is left alone.** Use static events and polling where possible. Small, clearly marked hooks are allowed only where nothing exists.

## Where the emotes attach
`PlayerEmotes` (new, `Assets/Scripts/Player/Emotes/PlayerEmotes.cs`) sits on the avatar root. It is added at runtime by `PlayerInstantiate` on every registration path: `AddPlayerReference`, `AddRemotePlayer` and bots. It calls `GhostEmotes.On(OrderHandler.PlayerAnimator)`, knows its seat, and runs the triggers. Pure thresholds live in `PlayerEmoteRules`, which is testable.

## Triggers
| Action | Fires when | Source (every machine unless noted) |
|---|---|---|
| Cackle | this seat stole an order | `StealFeedback.Happened`, Attacker = seat, Outcome = a steal |
| FistShake | this seat was robbed | the same event, Victim = seat |
| DeliveryFlourish | this seat delivered | `ScoreFeedback.Gained`, delivery reason |
| HoldYourHat | boost used | `BallDriving.OnBoostStart` |
| Wheee | airborne ≥ 0.4 s | poll `BallDriving.Grounded` |
| SeeingStars | hard bump, strength ≥ 0.6 | offline: `FeatSync.Bumped`; online: the host re-sends it as a new `CueKind.Dizzy` for the victim's seat |
| TreasureHold | picked up the golden parcel, then every ~10 s while carrying it | poll `OrderHandler.HasGoldenOrder` |
| JumpScare | carrying, with a rival within ~4 m behind and closing | poll positions (`RivalWarningRules`-style) |
| SideEye | carrying, with a rival within ~12 m behind | the same poll, longer range |
| Scheme | not carrying, closing on a rival who carries, within ~10 m | the same poll |
| Bored | speed ≈ 0 for 12 s in the match | poll |
| Taunt / BigWave / Raspberry / Cackle | d-pad emote | `EmoteHandler.Emote`, then new `CueKind.Emote0..3` so remotes show it. Map directions to actions by what each bubble sprite shows; the bubble still shows too. |
| Materialise | a player joins the lobby | `AddPlayerReference` / `AddRemotePlayer` |
| WarmUp or KnuckleCrack | readies up | `PlayerReady` / `SetRemoteReady` |
| HatShowOff or MirrorCheck | hat or colour change | `CustomizationSelector.SetCustomizationValue` / `ShowRemoteChoices` |
| Impatient | ready while someone isn't, after 5 s, then every ~8 s | lobby poll |
| RevBounce | match-start countdown (and again at +1.5 s) | `PlayerMoverCountdown` |
| Victory or CrownPolish / Tantrum or FadeAway | results podium, place 1 / last place | after `PodiumPerformer.On`. Podium acts pause while an emote plays. |

"X or Y" = one shared random pick between the two actions.

## Props in hand
- **Parcels:** during a parcel move (1b 1d 7a 7b 7d 20c), the carried order's visible mesh (`Order.orderMeshObject`) follows the hands' midpoint. It keeps its world size, its tweens are paused, and everything is restored afterwards.
- **Crown:** 16a and 25a hold a crown. A small crown prop uses `Resources/UI/WinnerCrown.png` on a camera-facing quad and follows the hands. It is made only while the move plays.

## Tasks (one agent each, one at a time)
1. `PlayerEmotes`, `PlayerEmoteRules`, attaching it, and the match triggers except the d-pad and Dizzy. Tests for the rules and the event wiring, using fake players like `StealFeedbackTests`.
2. Online: `CueKind.Emote0..3` and `Dizzy`, `EmoteHandler` → `GhostEmotes`, and remote playback in `OnlineCues.Show`. Extend `CueMessagesNetworkTests` / `OnlineCuesNetworkTests`.
3. Lobby, countdown and results hooks.
4. Props in hand.
5. Film a bot match (the trailer capture tool) and the lobby/results to check the emotes fire and don't spam. Send the video.
6. GDD (what the player sees, when each plays), TDD, `EDITOR-TODO.md` playtest entry, and memory.

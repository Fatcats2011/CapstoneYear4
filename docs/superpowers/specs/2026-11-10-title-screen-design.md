# Title screen: logo, dark backdrop and a ghost show

## Intent
Replace the title screen's look. The user wants the game logo on a neat purple-black backdrop while scooter ghosts ride on from off screen and act out short funny bits: stealing packages from each other, showing off, being playful. 10–12 different bits cycle, and the ghosts are dressed differently every time. The title screen is the first thing a Steam player sees, so it should show the game's character (cheeky ghosts, scooters, package theft) before a match starts.

The user did not say what is wrong with the current screen; the above is what they asked for, in their words.

## Goals
- The logo is the hero, on a purple-black backdrop.
- A looping show of **12 skits**, each a few seconds, played by real 3D ghosts on scooters that enter from off screen and leave again.
- Package stealing appears in several skits.
- Wardrobe changes every skit, from what the game already has: 7 hats (or none), 6 ghost colours, 4 company scooters.
- Every entry and action on the old title screen still works: Multiplayer, Local, Options, Credits, Quit; pad, keyboard and mouse; the selector; the button prompt; the language flag; the L dev key; returning to the title from player select, Options and Credits.

## Non-goals
- **No new wardrobe** (user, 2026-11-10): no new hats, accessories or models. Blender is not needed.
- No change to what the entries do, their order, their art, or `TitleEntries`.
- No change to player select, Options, Credits or the Dispatch board beyond what they share with the title screen.
- No new sounds or music (my call: the menu music stays; skits are silent). Can be added later.

## Anti-goals
- A skit that covers or distracts from the menu entries so they are hard to read or click.
- The same outfit turning up twice in a row, or the same skit twice in a row.
- The show costing frame rate in player select, where four cameras already run.

## Constraints
- The menu scene is `Assets/Scenes/Alex Player Testing.unity`. The title screen is `Main Menu Canvas` (screen-space overlay, 1920×1080 reference) with `MainMenu` on it.
- Today's ghosts are the `Menu Ghost Animations` rig: four in-scene scooters far off at x ≈ -1120, filmed by `Color Camera Render` / `Black Camera Render` into a render texture, shown by the `Menu Ghost Reveal` image, driven by `MenuGhosts.controller` and `MenuGhostReveal`.
- `PlayerAvatar.prefab` is the scooter and ghost with no player logic. `ScooterLook` dresses it (company, colour, hat) at runtime; `RemoteAvatar` already does this for online players.
- The ghost's animator (`PlayerAnimator.controller`) has: driving, Grab Left (the steal), End Screen Clapping, End Screen Hooray, End Screen Lose.
- Packages: `SmallPackage`, `MediumPackage`, `LargePackage` prefabs.
- DOTween is in the project.
- Unity 2022.3, URP. Work is on `steam-phase1a`.
- Scene changes are made by an editor builder (as `DispatchLobbyBuilder` does), not by hand-editing scene YAML.

## Approaches
- **A. Real 3D ghosts, skits scripted in code — picked (user approved 2026-11-10).** `PlayerAvatar` instances on a small off-world stage, filmed into a transparent render texture that sits in the canvas between the backdrop and the logo. Each skit is a short coroutine/tween sequence. Matches the game exactly, wardrobe comes free through `ScooterLook`, and a skit is a few lines to tune.
- **B. Hand-keyed animation clips** (how today's rig works): 12 clips in the Animation window. Can't be authored or tuned from code, and wardrobe would still need code. Dropped.
- **C. 2D sprites of the ghosts**: cheap to draw on a canvas, but needs new art for every hat and colour, and loses the 3D look. Dropped.

## Design

### Layout (logo top left and "menu untouched" are the user's; the rest is my call)
```
+--------------------------------------------------------------+
|  DEAD ON ARRIVAL                                     [flag]  |
|  (logo, top left,                                            |
|   as today)                                                  |
|                                                              |
|  > MULTIPLAYER                                               |
|    LOCAL             ghosts ride through here, behind        |
|    OPTIONS           the logo and the entries                |
|    CREDITS      ~~o=>          <=o~~                         |
|    QUIT                                                      |
|                                              (A) Select      |
+--------------------------------------------------------------+
```
- **Backdrop:** full-screen, a moonlit graveyard road in purple-blacks drawn as flat silhouettes (moon and stars top right, far hills, chapel, dead trees, gravestones, iron fence, then the road the ghosts ride along), with a thin mist drifting along the road (user asked for a new idea 2026-11-10: the first plain glow and fog looked pixelated; it was block compression, so these textures are imported uncompressed). Replaces `Background Purple` and `Background Pink` on the title canvas only.
- **Logo:** `DoA-Logo_02-08-24` (the sprite used today), in the top left where it is today (user, 2026-11-10). A slow gentle bob. The backdrop's glow sits behind it.
- **Entries, selector, button prompt, P1 controller text, language flag:** same objects, same positions, same wiring.
- **Draw order, back to front:** backdrop → ghost show → logo → entries, selector and prompts. Ghosts never draw over text.
- **Intro:** the old phase into the menu stays (user, 2026-11-11: it had been removed and was put back). The purple left by the boot wiper is a ghost against the camera; it pulls back to the crew in silhouette, the title wipes in behind them, the crew holds a beat and rides off right, then the first skit starts. No fade from black.

### The stage
- A `Title Stage` object where the old rig is (far from the world, same place), holding one camera, a ground-less lane and the cast.
- One camera renders the cast to a transparent render texture at screen resolution; a full-screen `RawImage` on the canvas shows it. The stage is lit by its own light so the ghosts read against the dark backdrop (rim light in the logo's green/purple).
- A cast pool of **4** `PlayerAvatar` instances and a small pool of packages, made once and reused. Skits use 2–4 ghosts.
- The show runs only while the game state is the title screen (`GameState.Menu`). In player select, Options and Credits the stage camera is off and the show is paused; coming back starts a fresh skit.

### How the ghosts move and act (my calls)
- **Movement:** a stage ghost is a `PlayerAvatar` with its driving physics, controls and colliders switched off (as `RemoteAvatar` does), moved by tweening its root along the skit's path. Lean into turns, wheelies, wobble and bounce are tweens on the model under the root. No ball physics on the stage.
- **Acting:** the ghost's animator plays driving by default and the existing Grab Left, Clapping, Hooray and Lose clips where a skit calls for a steal, clap, cheer or slump. The builder checks how each is triggered in `PlayerAnimator.controller` and may play states directly.
- **Bits with no clip** (hat steal, toppling stack, dozing, the scuffle, the golden glow) are faked with tweens, squash-and-stretch and simple effects: a Zzz or dust puff can be a small sprite or particle made in code. A skit may be simplified if a bit can't be made to read, but the count stays at 12.
- **Leaving the title mid-skit:** the skit is killed at once, every ghost and package goes back to the pool (off screen, packages detached, hats restored), and the stage camera turns off. The pool stays alive. Returning to the title from player select, Options or Credits starts a new skit from clean.
- **Compositing:** as today's rig already does, the stage camera clears to transparent black into a render texture with alpha, with no post-processing on that camera. Glow on the golden parcel is an emissive material or a sprite halo, not bloom.

### Wardrobe
- **Exception:** in Hat Thief the hat visibly moves from one ghost to the other; that is the joke. Both are put right off screen afterwards.
- An outfit is hat (8 choices incl. none) + ghost colour (6) + company scooter (4).
- Rule: every ghost in a skit has a different hat **and** a different colour from the others in that skit, and no hat+colour pair is reused until all 48 have been worn (a shuffled deck, reshuffled when empty, never dealing a pair worn in the previous skit). Company is random per ghost.
- The outfit is put on while the ghost is off screen, so a change is never seen.

### The 12 skits
Each lasts roughly 5–9 seconds, with a 1–2 second gap. All motion enters and leaves past the screen edges.
1. **The Snatch** — one ghost cruises across with a package; a second roars up from behind, grabs it and speeds off; the first gives chase.
2. **Pass the Parcel** — three ghosts ride on in a line and slow to a crawl, the package stolen forward and back between them, then all tear off.
3. **Wrong Way** — a convoy of three is met head-on by one ghost riding the other way; they swerve, a package drops, the lone ghost scoops it.
4. **Show-off** — a ghost stops centre-stage, spins a donut and cheers; a second sneaks in and lifts the package off the back; the first slumps.
5. **Hat Thief** — a ghost steals the other's *hat* instead of a package and rides off wearing it; the bare-headed one chases.
6. **Tower** — a ghost wobbles across with a tall stack of packages; a small quick ghost pulls the bottom one and the stack topples.
7. **Tug of War** — two ghosts pull one large package in opposite directions until it pops free and both shoot off screen.
8. **Chain Chase** — a four-ghost chase crosses the screen, the package changing hands; it comes back the other way with the order reversed.
9. **Air Mail** — a ghost arcs high across the screen from one side to the other, drops the package mid-air, and one passing below catches it.
10. **Sneak** — a ghost dozes parked; another creeps in slowly, lifts the package, creeps two steps, then boosts away as the first wakes.
11. **Formation** — four ghosts do synchronised circles and clap; the last arrives late and bowls into them.
12. **Golden Parcel** — one ghost carries a glowing golden package; three mob it in a spinning scuffle; a different ghost leaves with it.

Order: all 12 in a shuffled order, then reshuffled, never the same skit twice in a row.

### What stays and what goes
- **Stays, untouched:** `MainMenu`, `TitleEntries`, `MouseMenus`, the entry images, `Selector`, `Button UI`, `P1 Connected Controller`, the language flag, the Options and Credits canvases (they keep their own purple/pink backgrounds), player select.
- **Stays but unused:** `MainMenu.menuGhostImage` and its `Selector Ghost Image` (already switched off in the scene) keep their references so `MainMenu` needs no change.
- **Stays:** the `Menu Ghost Animations` rig, its two cameras, `Menu Ghost Reveal`, `MenuGhostReveal`, `MenuGhosts.controller`, `MenuTransition.mat` and the MainMenu render textures: they are the phase into the menu. The stage stands well clear of the rig.

### Boot intro engine sound (user, 2026-11-10)
- The boot intro (`SplashScreen.unity`, the 3.25 s `Animate` clip: logo appears, a wiper sweeps across) has no sound today. It gets a scooter engine rev timed to the wiper's sweep.
- Sound: cut from `Assets/Audio/TEST/78651__justinbw__moped-motor.wav` (the user's mp3 was not found on the PC; user said go ahead with this one). A short rev-and-pass, faded at both ends, saved as its own clip outside `TEST`.
- Volume follows the game's saved sound-effects volume if the splash scene can read it; otherwise a fixed modest level.
- Open: the file looks like a Freesound download; its licence needs checking before the Steam release.

### Tests
Editor tests in `Assets/Tests/Editor`, in the style of the existing ones:
- wardrobe: distinct hats and colours within a skit; no pair repeats before the deck is spent; no pair carried from one skit to the next
- skit order: all 12 before any repeats; never the same twice running
- every skit declares its cast size, and each fits the pool of 4 (no skit needs more)
- the menu scene still has `MainMenu` wired with 5 entries, its three canvases and its selector (guards the "everything still works" promise)
- existing `MainMenuTests`, `TitleEntriesTests`, `MouseMenusTests` still pass

Then a manual pass in the editor: every entry by pad, keyboard and mouse; back out of each; watch a full cycle of skits.

### Documents
- GDD: the title screen section (what the player sees), bumped a minor version with a Revision History line.
- TDD: the title stage, show and wardrobe systems, and the builder, bumped likewise.

## Left to the builder
- Exact colours, gradient and fog of the backdrop, within "purple-black, logo is the hero".
- Camera angle and lens, lane positions, speeds and timings of each skit.
- Class names and file layout (under `Assets/Scripts/Menu/Title/` suggested).
- How packages attach to and detach from a scooter on the stage.
- Whether skits are coroutines or DOTween sequences.
- How the editor builder is run (menu item under `Tools/Dead on Arrival/`).

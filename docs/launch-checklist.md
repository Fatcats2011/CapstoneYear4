# Launch checklist

Written 2026-11-16. Steps are in the order to do them; legal goes early because it has the longest wait. Tick items as they're done.

**Open decisions**

- [ ] Pack F (boost speed lines, respawn reason and countdown): build it or cut it from launch. Suggested: cut.
- [ ] Mac and Linux at launch, or Windows plus Steam Deck first. Suggested: Windows plus Deck.
- [ ] Which languages are listed at launch. Suggested: only the reviewed ones.
- [ ] Price and release date.

## 1. Lock the build

- [ ] Commit the uncommitted work (packs D and G, the GDD and TDD updates).
- [ ] Run the full test suite once on that commit (`bash tools/run-tests.sh`).
- [ ] Feature freeze: only fixes from here.

## 2. Legal (blocks release)

Details: `LICENSES.md`, "What to do before release".

- [ ] A written agreement from every contributor (IP, revenue split, credits).
- [ ] Check the school's student-IP policy.
- [ ] Buy the Sobiscuit App / Game license (Khurasan).
- [ ] Buy the Jack's Candlestick commercial license (Iconian: email first, then $20 per seat).
- [ ] Replace the placeholder TEST sounds that play in the game (at least the whistle and the three "Sound Effect" files).
- [ ] Confirm who owns OToon; move its 7 used textures out of the demo folder.
- [ ] Confirm which audio is original, with a written OK.
- [ ] Find the licence of the steal whoosh.
- [ ] Find the origin of `FlowerPot`, `Podium` and `Grass`.
- [ ] Parody brand names and logos: rename them or get a legal opinion. This also blocks screenshots.

## 3. Playtest backlog

Steps: `EDITOR-TODO.md` sections 16 to 16l (about five hours).

- [ ] Standard with the feedback packs (16h, 16i, 16k, 16l).
- [ ] Highlight reel and bot personalities (16, 16b).
- [ ] Mutators and the four new modes (16d, 16e, 16f, 16j).
- [ ] Accessibility rows: rumble, colourblind, text size (16c; they are built, the section is out of date).
- [ ] Online in two editors (sections 2, 9, 11, 12).
- [ ] Two PCs over Steam: invite, relay check, a disconnect, a load time on the slower PC (sections 5, 10, 14).
- [ ] Four real controllers, including a DualSense and a Switch Pro, with Steam Input on and off.
- [ ] Keyboard and display settings in a build (section 7).
- [ ] Tuning notes sent to Claude and fixed.

## 4. Performance

Steps: `docs/performance.md`, `EDITOR-TODO.md` section 4.

- [ ] Measure a 4-player match with F6 at High and at Medium.
- [ ] The editor list, one change at a time, measuring after each.
- [ ] Replace the estimated system requirements in `docs/steam/store-page-draft.md` with measured ones.

## 5. Platforms

- [ ] Steam Deck checks (`docs/steam/steam-deck.md`).
- [ ] If Mac ships: play the build on a Mac; sign it.
- [ ] If Linux ships: play the build on Linux or a Deck; check the executable bit survives the upload.

## 6. Steamworks dashboard

- [ ] Steam Input default config: Gamepad. Check one PlayStation pad doesn't join as two players.
- [x] Steam Cloud (Auto-Cloud: `WinAppDataLocalLow`, `The Boo Crew/Dead on Arrival`, `settings.cfg` and `bindings.cfg`; Mac and Linux overrides). Published 2026-11-17.
- [x] Dev Comp and Beta packages hold all three depots; no common redistributables. Published 2026-11-17.
- [ ] Finish the Game Build checklist (`EDITOR-TODO.md` section 15b).
- [ ] Upload `docs/steam/rich-presence-english.vdf`.
- [ ] Enable Remote Play Together.
- [ ] Publish the 9 achievements; tick Steam Achievements under supported features.
- [ ] Play one solo online match through Steam, then tick Single-player.

## 7. Store page

Text and sizes: `docs/steam/store-page-draft.md`.

- [ ] Remaining capsules, library art, community and client icons (the wide capsule is done).
- [ ] At least 5 screenshots, 1920 × 1080.
- [ ] Trailer, cut from the `tools/trailer` footage.
- [ ] Paste the text, tags and features; fill in the content survey.
- [ ] Set the price and the release date.
- [ ] Submit the page for Valve's review (a few business days).
- [ ] "Coming Soon" page public at least two weeks before release.

## 8. Languages

Steps: `EDITOR-TODO.md`, Localization.

- [ ] Run Tools → Dead on Arrival → Localization → Set Up and commit the result.
- [ ] Look at French and Czech by eye in the menus.
- [ ] A native speaker checks each language that will be listed (add its code to `reviewed` in `strings.csv`).
- [ ] The store page lists only the languages that ship.

## 9. Release build

- [ ] Player Settings: version `1.0.0`, icon, splash.
- [ ] Fresh builds (Tools → Dead on Arrival → Build): the ones in `Builds/` are from 2026-11-08.
- [ ] Upload to a `beta` branch (`docs/steam/steampipe/README.md`).
- [ ] Friends play the beta branch.
- [ ] Submit the build for Valve's review (a few business days).
- [ ] Set the build live on default.

## 10. Tidy-ups

- [ ] Update `EDITOR-TODO.md` section 16c: the accessibility rows exist.
- [ ] Delete the unused art after approving the list in `docs/unused-assets.md`.
- [ ] Title screen: Selector Ghost Sprites holds 5 (`EDITOR-TODO.md` section 1).
- [ ] Button glyphs added to every scene (`EDITOR-TODO.md` section 1e).

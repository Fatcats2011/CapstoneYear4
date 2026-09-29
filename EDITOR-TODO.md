# Editor to-do (master list)

Things only you can do in the Unity editor (and on two PCs), top to bottom. Updated 2026-09-28 (Phase 3D: playing over Steam).

## 1. Save the online prefabs' network IDs (1 minute, main editor only)

- Still to do from the code review: the prefab files still hold the old IDs.
- In the **main editor**, not in Play Mode (the item is greyed out there): **Tools → Dead on Arrival → Online → Save Network Prefab IDs**.
- `git status` then shows 3 changed prefabs in `Assets/Prefabs/Online/`, each with one changed line (`GlobalObjectIdHash`).
- Until then `OnlinePrefabsTests.SavedPrefabIds_AreUnique_AndMatchTheIdNetcodeComputes` fails on purpose. It's the only red test.

## 2. Let both editors import Phase 3D (1 minute)

- Click into the **main editor**, then into the **ParrelSync clone**. No scene or prefab changes:
  - New scripts: `OnlinePlay`, `OnlineLobby`, `LobbyRules`, `SteamLobbyService`, `ILobbyService`, `ISessionControl`, `LobbyPrompt`.
  - Changed: `MenuInteractions`, `PlayerUIHandler`, `MainMenu`, `OnlineGame`, and a new **North Face** action in the UI map (`CapstoneYear4.inputactions`).
  - New tests: `LobbyRulesTests`, `OnlineLobbyTests`, `SteamLobbyServiceTests`, `MenuButtonsTests`, `LobbyPromptTests`, `OnlinePlayTests`, `OnlinePlayNetworkTests`.
- The Console should end with no red errors.

## 3. One PC, Steam running (5 minutes)

- **Play**, press **A**, pick **Play**: player select.
- The line along the top says **"Y / Triangle: play online with Steam friends"**.
- Press **Y**: it hosts.
  - The line changes to "Y / Triangle: invite friends      B / Circle: leave".
  - The bottom hint says **"Invite friends from your Steam friends list."** (the editor has no Steam overlay).
- Press **B**: back to the title screen, offline.
- Offline split screen is unchanged: with two players in player select there's no line, and Y says it's one player per machine.

## 4. Two PCs, two Steam accounts that are friends (15 minutes)

- A build on each PC, with a copy of `steam_appid.txt` (project folder) next to the `.exe`. Steam running on both.
- Start the game on both first. With the test App ID (480), accepting an invite with the game closed starts Spacewar.
- **PC 1:** player select → **Y**, then invite the friend in Steam's overlay (**Shift+Tab**).
- **PC 2:** accept the invite, or **Join Game** on PC 1 in the friends list. It lands in PC 1's player select, in the second seat.
- Ready up on both: the match starts together.
- After the match (or PC 1's pause → **Main Menu**), both are back in **player select**.
- **PC 1: B.** PC 2 shows **"The host left the match."** and stays in player select.
- Anything odd? Note it, plus each PC's log lines starting `Online:` (the build's `Player.log`).

## 5. Review and commit (your call)

- Not committed: Phase 3D on `steam-phase1a`, on top of your "Code Review" commit.
- How it works: `docs/online.md` (Phase 3D: playing over Steam, and Two PCs over Steam).

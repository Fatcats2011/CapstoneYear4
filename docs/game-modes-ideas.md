# Game Modes & Mutators — Ideas

> **Note:** Dead on Arrival was designed and balanced around its standard delivery match.
> The modes and mutators below are optional extras for fun. The game wasn't intended to be
> played this way, so expect odd balance, edge cases and the occasional chaos.

Names are placeholders and still need workshopping.

## Modes

### 2v2 Teams
- Already planned. Two teams share a combined score.

### Kings Crown
- One glowing crown on the map.
- Whoever holds it earns points every second and is marked for everyone (purple flame).
- Steal it to take over. The player with the most points when time runs out wins.

### Infection
- One player starts as the hunter.
- Each thief the hunter catches becomes a hunter too.
- Infected players' ghosts change to the first hunter's ghost color, so the infected team reads as one color at a glance.
- Thieves win if any survive the timer. Hunters win if they catch everyone.

### Gold Rush
- Short timed rounds for a quick match.
- Steal value doubles in the final 30 seconds.
- Far more packages on the map than a normal match, so there's always something to grab.
- Your score slowly ticks down whenever you're not delivering, so standing still or hunting too long costs you.
- Delivering stops the drain (and maybe resets a short grace timer before it starts again).

### Hot Potato
- One cursed package starts with a random player on a hidden fuse timer.
- Pass it on by bumping or stealing from another player.
- Whoever holds it when the fuse runs out "potatoes out": they're eliminated for the rest of the round.
- Eliminated players move to a spectator camera and watch the remaining players.
- Falling in water is **not** an elimination: the player respawns as normal. If they were holding the potato, it stays with them and the fuse keeps ticking.
- The package then respawns on a random surviving player with a fresh fuse.
- The package glows brighter and ticks faster as the fuse runs down.
- Last player standing wins.

**Needs building:**
- **Spectator camera:** doesn't exist in the game yet. Needs to follow the surviving players and let the spectator switch between them, in splitscreen and online.
- **Elimination:** a way to remove a player from the round (no respawn) and bring them back for the next match.

## Mutators
Toggles that stack on top of any mode.

**New**
- **Speed:** a scalable slider (e.g. 0.5x to 2x) for movement speed.
- **Low Gravity:** bigger jumps and floatier bumps.
- **Number of Waves:** pick how many waves a match has (default 3). Match time = waves × wave length, plus the Golden Order.

**Existing house rules (already in the game)**
- **Wave Length:** 45s, 60s or 90s per wave (currently called Match Length: Short, Normal, Long).
- **Steals:** on or off.
- **Golden Order:** on or off.
- **Bots:** on or off.

## Mutators per Mode
The rules menu only shows the mutators that make sense for the selected mode.

| Mutator | Standard | 2v2 Teams | Kings Crown | Infection | Gold Rush | Hot Potato |
|---|---|---|---|---|---|---|
| Speed | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Low Gravity | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| Number of Waves | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| Wave Length | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| Round Timer (new) | ❌ | ❌ | ✅ | ✅ | ❌ | ✅ |
| Steals | ✅ | ✅ | 🔒 on | ❌ | 🔒 on | 🔒 on |
| Golden Order | ✅ | ✅ | ❌ | ❌ | ✅ | ❌ |
| Bots | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

- ✅ shown, ❌ hidden, 🔒 locked (the mode needs it).
- **Round Timer:** modes without deliveries have no waves, so they get a plain timer instead (e.g. 2, 3 or 5 min).
- **Steals locked on:** stealing is how the crown, the steal bonus and the potato change hands.
- **Infection has no steals:** catching (bumping) replaces stealing.
- **Bots:** need new AI for Kings Crown, Infection and Hot Potato before they can play those modes.

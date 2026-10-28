# Bot personalities — design

Date: 2026-10-28. Branch: steam-phase1a.

## Intent

Online matches fill empty seats with up to three CPU drivers (`BotFill`, `OnlineGame.SeatHostBots`). Today they are
three copies of one driver: go to the nearest free order, take it to its dropoff, repeat (`BotDriver.PickTarget`). They
never chase anyone; a steal only happens by accident, when a bot boosts while touching someone.

The user wants the bots to feel like distinct rivals, Pac-Man style: each has its own playstyle, players learn which is
which by how they drive (no labels), and every bot reacts to the scoreboard.

## Goals

- Three personalities, each easy to tell apart by watching:
  - **Thief** — goes after players to steal far more often than the others.
  - **Hunter** — goes after large (high-value) packages.
  - **Courier** (our proposal, the user said "go for it") — safe and efficient: best value for distance, boosts a lot on
    open road, and steers away from boosting rivals while it carries something. The defensive counterpart to the thief.
- Scoreboard-aware, a mix of both readings the user agreed to:
  - **Target the leader:** when a bot weighs stealing, the leading player (human or bot) is the preferred victim.
  - **Desperation:** the further a bot is behind the leader, the more it leans into its risky side; a bot in the lead
    plays safer (nearer orders, quick deliveries, less chasing).
- Exactly one thief at most.

## Non-goals

- No names, labels or colours that reveal a personality. The bots keep the shared "CPU" name and unused colour/hat.
- No offline (local split-screen) bots. Bots stay online-only, as today. The F7 dev autopilot (`BotAutopilot`) may pick
  a personality for testing but that's the builder's call.
- No difficulty levels.
- No new movement: steering, pathing, stuck recovery and boosting mechanics stay as they are.

## Anti-goals

- Bots that drive alike. If a player can't tell the thief from the hunter within a minute or two, it failed even if the
  code works. Weights are deliberately extreme.
- A thief that makes the game miserable: it must not lock onto one victim forever. It gives up a chase after a while
  and goes back to orders.
- Breaking the "Steals" match rule: with steals off (`HouseRules.Playing.Steals` false), no bot chases players to
  steal; the thief plays like an order collector.

## Constraints

- Bots run only on the host (`OnlyInRound`, host-owned scooters). Their steals go through the existing host path
  (`OrderHandler.Hit` → `StealSync.Ask` online). No new network messages.
- A steal happens when a boosting scooter enters another's trigger, or boosts while already touching
  (`OrderHandler.OnTriggerEnter`, `AttemptSteal`). The thief steals by chasing a victim and starting a boost when
  close and lined up.
- A player holds at most two orders (`order1`, `order2`); a steal takes the victim's best order only if the thief has
  room. A bot holding two orders does not chase steals.
- Order value: `Order.Worth` (tier price doubled per steal, `StealValueRules`). Golden orders are worth the most.
- Scores: each seat's `OrderHandler.Score` on the host (`PlayerInstantiate.Roster`).
- Keep the existing pattern: decisions are pure static functions in `BotBrain` with EditMode tests (`BotBrainTests`);
  `BotDriver` gathers the scene and applies them.

## Approaches considered

1. **One scoring rule, three sets of weights (chosen).** Several times a second each bot scores every option — each free
   order, and each rival carrying something — by value, distance, steal doubling and whether the target leads. A
   personality is a small set of weights; the scoreboard shifts them. Fits `BotBrain`'s testable pure functions; easy to
   tune. Risk: weights too close make bots alike — mitigated by extreme values.
2. **A hand-written behaviour (state machine) per personality.** Most scripted and distinct, but three times the code,
   scoreboard logic duplicated in each, tuning means editing code paths.
3. **Option 1 with weights as editor assets (ScriptableObjects).** Adds asset wiring for runtime-created bots; defer
   until tuning by code becomes a chore.

The user picked 1.

## How it works (decided)

### Personality assignment

Bots get personalities in the order they are seated (the first bot added is the first in this list):

| Bots in match | Personalities |
|---|---|
| 1 | Thief |
| 2 | Thief, Hunter |
| 3 | Thief, Hunter, Courier |

### Options a bot scores

- **Each free order** (active, can be picked up, nobody holding it) — only when the bot has room.
- **Each rival carrying an order** (a steal) — only when steals are on, the bot has room, and the rival isn't the bot.
- **Its own dropoff** when it holds an order.

It goes for the highest-scoring option. A bot holding one order delivers it, as today, except that it may take a
second order or a steal first **only when that option is close** (a short detour; the builder tunes the distance).
Desperation does not widen this: the user chose "only if it's close".

### What goes into a score

- **Value** — the order's `Worth` (for a steal, the victim's best order's `Worth`).
- **Distance** — farther is worse; each personality cares about it differently.
- **Leader bonus** (steals only) — extra when the victim is the current leader.
- **Danger** (courier, while carrying) — boosting rivals nearby push its choices away from them.

### Personality baselines

| | Steal appetite | Value weight | Distance weight | Boost | Avoids danger |
|---|---|---|---|---|---|
| Thief | very high | medium | medium | normal, plus boost-to-hit | no |
| Hunter | none, rising when behind (big packages only) | very high (Hard/Golden strongly preferred) | low (will drive far) | normal | no |
| Courier | none | value-for-distance | high | often, on open road | yes, while carrying |

### Scoreboard effects

- **Desperation** (0 when leading or level, rising to 1 as the gap to the leader grows): raises steal appetite, raises
  value weight, lowers distance weight, and makes chases last longer before giving up.
  - Thief: steals even more, chases longer.
  - Hunter: starts stealing big packages off carriers.
  - Courier: takes pricier, farther orders; still never steals.
- **Leading:** the bot plays safe — more weight on distance, quick deliveries, shorter chases.
- **Leader bonus** applies to every bot's steal scores.

### Stealing behaviour

- The bot steers at the victim's scooter (re-pathing as it moves, as today when a target shifts).
- It starts a boost when close and lined up on the victim, so the boost carries it into contact.
- It gives up a chase after a time limit (longer when desperate) or if the victim's lead on it keeps growing, then
  re-scores. It doesn't re-pick the same victim straight away.

## Calls made after the builder check (ours, not the user's)

- **Assignment order:** by the order the host creates the bot players (the order `BotFill`'s count is turned into bot
  `OnlinePlayer`s), not seat number or when a scooter appears. Fixed for the whole match; bots never join or leave
  mid-match.
- **Leader:** the highest `Score` among all seats, the bot itself included. A tie for the top counts as leading.
  "Behind" is an absolute point gap to the leader; desperation reaches 1 at a gap the builder tunes.
- **Clashes:** a steal option on a victim who is boosting scores much lower (it would clash, not steal). Boost-to-hit
  may ignore the bot's own 2.5 s boost spacing (`BOOST_EVERY`), but never the game's own boost rules.
- **Thief with nobody to rob:** collects orders like the others.
- **Robbed mid-chase or mid-delivery:** the bot just re-scores.
- **Checking "clearly distinct":** a dev log of per-bot stats (steal attempts, steals, average order value taken,
  average pickup distance) per match, plus a hand playtest by the user. No headless simulation.

## Left to the builder

- Exact weight numbers, the desperation gap that counts as "fully desperate", chase time limits, the boost-to-hit
  distance and angle, and the danger radius. Tune so the three are clearly distinct in play.
- How often bots re-score (today they re-pick every frame, re-path every 0.5 s), and some stickiness so a bot doesn't
  flip between two near-equal options.
- Whether the F7 autopilot picks a personality.
- Tests: pure scoring and assignment in `BotBrainTests`/new tests; a PlayMode or network test that a thief actually
  steals from a carrier is welcome but the builder judges cost.

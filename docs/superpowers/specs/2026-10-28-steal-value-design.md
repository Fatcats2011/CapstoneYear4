# Steals make orders worth more (design)

## Intent

Players steal from each other too rarely. The 2024 playtest log has 16 of 26 multiplayer matches with no steal at all, and about 46 steals against 591 boosts. A steal only matters today because it denies a rival: the order's worth is fixed by its tier (Easy 40, Medium 60, Hard 80, Golden 50). So give stealing a payoff of its own, and make a much-stolen order a hot potato that everyone wants.

## Goals

- Each steal doubles that order's worth. Whoever finally delivers it collects the built-up value.
- The thief earns a small sum for stealing the package, even if they never get to deliver it.
- Players can see that an order has become more valuable.
- Order prices follow the time an order takes to score, and a strong 4-player winner scores about $1000 ("a very good game").

## Non-goals

- No changes to how a steal is triggered, or to clashes, bounces or drops.
- No pay for a bump that takes nothing (the thief's slots are full and the victim's orders just fall).
- No change to the golden order's own growth over time (50, then +1 per second held).

## Anti-goals

- Two friends trading one order back and forth to farm points.
- Online players disagreeing about an order's worth.
- Orders keeping their steal count after they return to the pool and respawn.
- The golden round deciding the whole match (see "The golden order" below).

## Approaches

1. **A steal count on each order, replayed on every machine.** Chosen. Online, the host decides each steal and every machine already replays it as a Steal change, so each machine counts the same way. Nothing in the network format changes.
2. **The host sends the order's worth in the Steal change.** Clients could never disagree, but it changes the network format, so players on different versions couldn't play together.

Chosen: option 1, because it is the smaller change and fits how steals already work. Its risk is that every machine must count identically, including a player who reconnects mid-match.

## The design

- `Order` keeps a steal count. Its worth is its tier value times 2 for each steal (an Easy order stolen twice is worth 4× its tier). The count goes up inside the existing steal path (`TakeOrderFrom`) on every machine that replays it. It resets when the order returns to the pool.
- Delivery scores the order's current worth (today `OrderHandler` adds the tier value).
- The thief's payment is a small flat amount added to their score on the host, inside `StealFrom` (host and offline only), when a package is actually taken. It is separate from the order's doubling.
- The thief is paid for a given order only once. The host remembers which seats it has already paid for that order. This stops back-and-forth farming; the order itself still doubles on every steal.
- Match Rules: STEALS OFF means no doubling and no payment, because nothing changes hands.

## The golden order

Stealing does not double the golden order. The simulation shows why: with doubling, the golden order alone reaches $400+ on average and over $5000 in some games, so the rest of the match stops mattering. Keeping the tier prices low enough to offset that (33 / 39 / 47) made all tiers nearly equal. The golden order keeps its own growth over time (50, +$1 per second held), and a steal moves it to the thief without changing its worth. The thief's payment still applies to a golden steal.

## Prices (from the simulation)

Prices follow each tier's average time to score (approach, carry and handling), so every tier pays about $3.4 per second:

| Tier | Average time to score | Today | New |
|---|---|---|---|
| Easy | 16.5 s | 40 | 55 |
| Medium | 19.1 s | 60 | 65 |
| Hard | 23.3 s | 80 | 80 |
| Golden | n/a | 50 + $1/s | 50 + $1/s (unchanged) |

Today Easy pays 30% less per second than Hard. The new prices flatten the tiers on purpose; if a Hard order should be a risk with a reward, add a premium later.

With these prices and the doubling rule, simulated 4-player lobbies of strong drivers give a winner median of about 780, a 75th percentile of about 890 and a 90th percentile of about 1010. Steals rise from about 2.6 to 4.6 per game. A solo strong player scores about 1000 every game.

If $1000 should be the 75th percentile, multiply every price by about 1.13; for the median, by about 1.29.

## My calls (tell me if any are wrong)

- **Thief payment:** 7, once per order per thief. The simulation shows it barely changes how often players steal (+0.5 steals per game at 30); the doubling is what drives stealing. It stays as a small reward.
- **No cap** on the doubling for now. The pickup cooldown and the bounce limit how fast an order can change hands, and a 4× or 8× order is the point.
- **"Very good game"** means the 90th percentile of the winner in a 4-player match of strong drivers.
- The prices, multiplier and payment are named constants in one place, to tune in playtests.

## Known limits of the simulation

- It is calibrated on 10 three- and four-player games from 2024, so the steal behaviour is indicative only.
- It models players as picking the best value per second, and chasing when that looks better. The golden round uses a fixed steal rate that real players may raise or lower.
- Weaker drivers get robbed more under the new rules: in a mixed lobby the best driver wins 55% of games (74% today), and the weakest seat averages about 200 points. Watch this in playtests.
- Solo play already reaches $1000, so solo may need its own tuning.

## Left to the builder

- How the extra worth is shown on the order: the existing tier glow and scale could step up per steal, and a number or a sound could mark the thief's payment.
- Whether the tutorial's steal lesson should show the bonus. Check the score is unaffected by the tutorial.
- Checking what a reconnecting player is told about the steal counts (the disconnect and rejoin code), and the rematch reset.
- Tests, following `docs/testing.md`, with the pure rules (worth, payment, once-per-thief) in a plain rules class like the project's other `*Rules.cs`.
- Whether the awards, Steam feats or highlight reel should use the new points.
- The simulation scripts are not in the repo. If you want them kept for retuning, `tools/` is the place.

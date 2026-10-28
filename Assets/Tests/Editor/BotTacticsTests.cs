using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// What a CPU driver goes for (BotTactics): free orders and steals scored by its personality and the scoreboard; a bot
    /// carrying something detours only for something close; and the courier's dodge, the boost-to-hit
    /// </summary>
    public class BotTacticsTests
    {
        static BotSituation Level(BotKind kind, int held = 0, bool stealsOn = true)
        {
            return new BotSituation { Profile = BotPersonality.For(kind), MyScore = 100, LeaderScore = 100, Held = held, StealsOn = stealsOn };
        }

        static BotSituation Desperate(BotKind kind)
        {
            return new BotSituation { Profile = BotPersonality.For(kind), MyScore = 0, LeaderScore = 200, StealsOn = true };
        }

        static BotOption Order(int worth, float distance, bool golden = false)
        {
            return new BotOption { Kind = BotOptionKind.Order, Worth = worth, Distance = distance, Golden = golden };
        }

        static BotOption Steal(int worth, float distance, bool leads = false, bool boosting = false)
        {
            return new BotOption { Kind = BotOptionKind.Steal, Worth = worth, Distance = distance, VictimLeads = leads, VictimBoosting = boosting };
        }

        [Test]
        public void Desperation_ZeroWhenLeadingOrLevel_OneAtFullGap()
        {
            Assert.AreEqual(0f, BotTactics.Desperation(100, 100));
            Assert.AreEqual(0f, BotTactics.Desperation(150, 100));
            Assert.AreEqual(1f, BotTactics.Desperation(0, 200));
            Assert.AreEqual(1f, BotTactics.Desperation(0, 400));
            Assert.AreEqual(0.5f, BotTactics.Desperation(0, 100), 1e-4f);
        }

        [Test]
        public void Leads_TieCountsAsLeading()
        {
            Assert.IsTrue(BotTactics.Leads(100, 100));
            Assert.IsTrue(BotTactics.Leads(120, 100));
            Assert.IsFalse(BotTactics.Leads(99, 100));
        }

        [Test]
        public void Thief_PrefersAStealOverAnEqualOrder()
        {
            List<BotOption> options = new List<BotOption> { Order(65, 30f), Steal(65, 30f) };

            Assert.AreEqual(1, BotTactics.Choose(Level(BotKind.Thief), options, -1));
        }

        [Test]
        public void Courier_NeverSteals()
        {
            Assert.AreEqual(float.NegativeInfinity, BotTactics.Score(Desperate(BotKind.Courier), Steal(500, 5f)));
        }

        [Test]
        public void Hunter_StealsBigPackagesOnlyWhenBehind()
        {
            Assert.AreEqual(float.NegativeInfinity, BotTactics.Score(Level(BotKind.Hunter), Steal(80, 20f)), "level: no appetite");
            Assert.Greater(BotTactics.Score(Desperate(BotKind.Hunter), Steal(80, 20f)), float.NegativeInfinity, "behind: a big one");
            Assert.AreEqual(float.NegativeInfinity, BotTactics.Score(Desperate(BotKind.Hunter), Steal(65, 20f)), "never a small one");
        }

        [Test]
        public void Hunter_DrivesFarForAHardOrder()
        {
            List<BotOption> options = new List<BotOption> { Order(55, 20f), Order(80, 120f) };

            Assert.AreEqual(1, BotTactics.Choose(Level(BotKind.Hunter), options, -1), "the hunter: the hard one, far off");
            Assert.AreEqual(0, BotTactics.Choose(Level(BotKind.Courier), options, -1), "the courier: the easy one, near");
        }

        [Test]
        public void Hunter_PrizesGolden()
        {
            List<BotOption> options = new List<BotOption> { Order(80, 60f), Order(50, 60f, true) };

            Assert.AreEqual(1, BotTactics.Choose(Level(BotKind.Hunter), options, -1));
        }

        [Test]
        public void Steal_LeaderPreferred()
        {
            List<BotOption> options = new List<BotOption> { Steal(65, 30f), Steal(65, 30f, leads: true) };

            Assert.AreEqual(1, BotTactics.Choose(Level(BotKind.Thief), options, -1));
        }

        [Test]
        public void Steal_BoostingVictimScoresLower()
        {
            BotSituation thief = Level(BotKind.Thief);

            Assert.Less(BotTactics.Score(thief, Steal(65, 30f, boosting: true)), BotTactics.Score(thief, Steal(65, 30f)));
        }

        [Test]
        public void Choose_StealsOff_ThiefCollectsOrders()
        {
            List<BotOption> options = new List<BotOption> { Steal(65, 10f), Order(55, 30f) };

            Assert.AreEqual(1, BotTactics.Choose(Level(BotKind.Thief, stealsOn: false), options, -1));
        }

        [Test]
        public void Choose_HoldingTwo_AlwaysDelivers()
        {
            List<BotOption> options = new List<BotOption> { Order(80, 5f), Steal(80, 5f) };

            Assert.AreEqual(-1, BotTactics.Choose(Level(BotKind.Thief, held: 2), options, -1));
        }

        [Test]
        public void Choose_HoldingOne_DetoursOnlyWhenClose()
        {
            Assert.AreEqual(0, BotTactics.Choose(Level(BotKind.Thief, held: 1), new List<BotOption> { Order(65, 20f) }, -1));
            Assert.AreEqual(-1, BotTactics.Choose(Level(BotKind.Thief, held: 1), new List<BotOption> { Order(65, 40f) }, -1));
        }

        [Test]
        public void Choose_NothingHeld_NeverDelivers()
        {
            BotSituation courier = Level(BotKind.Courier);

            Assert.Less(BotTactics.Score(courier, Order(55, 500f)), 0f, "a far order scores below nothing");
            Assert.AreEqual(0, BotTactics.Choose(courier, new List<BotOption> { Order(55, 500f) }, -1), "but it's still the one to go for");
            Assert.AreEqual(-1, BotTactics.Choose(courier, new List<BotOption>(), -1));
        }

        [Test]
        public void Choose_Stickiness_KeepsCurrentOnNearTie()
        {
            // Courier, level (so leading: distance x1.5): 65 - 18 = 47 against 60 - 18 = 42, close enough to stick
            List<BotOption> options = new List<BotOption> { Order(65, 10f), Order(60, 10f) };
            BotSituation courier = Level(BotKind.Courier);

            Assert.AreEqual(0, BotTactics.Choose(courier, options, 0));
            Assert.AreEqual(1, BotTactics.Choose(courier, options, 1));
        }

        [Test]
        public void BoostToHit_CloseAndLinedUp_Only()
        {
            Assert.IsTrue(BotTactics.BoostToHit(5f, 8f));
            Assert.IsTrue(BotTactics.BoostToHit(-5f, 8f));
            Assert.IsFalse(BotTactics.BoostToHit(30f, 8f), "not lined up");
            Assert.IsFalse(BotTactics.BoostToHit(5f, 20f), "too far");
        }

        [Test]
        public void Dodge_NoThreats_AimUnchanged()
        {
            Vector3 aim = new Vector3(0, 0, 20);

            Assert.AreEqual(aim, BotTactics.Dodge(Vector3.zero, aim, new List<Vector3>()));
        }

        [Test]
        public void Dodge_ThreatOnTheRight_AimMovesLeft()
        {
            Vector3 dodged = BotTactics.Dodge(Vector3.zero, new Vector3(0, 0, 20), new List<Vector3> { new Vector3(5, 0, 10) });

            Assert.Less(dodged.x, 0f);
        }

        [Test]
        public void Dodge_FarThreat_Ignored()
        {
            Vector3 aim = new Vector3(0, 0, 20);

            Assert.AreEqual(aim, BotTactics.Dodge(Vector3.zero, aim, new List<Vector3> { new Vector3(40, 0, 0) }));
        }

        [Test]
        public void ChaseSeconds_LongerWhenDesperate_ShorterWhenLeading()
        {
            BotSituation level = Level(BotKind.Thief);
            BotSituation behind = Desperate(BotKind.Thief);
            BotSituation ahead = level;
            ahead.MyScore = 300;
            // Level counts as leading (a tie), so compare with a bot a little behind
            BotSituation trailing = level;
            trailing.MyScore = 90;

            Assert.Greater(BotTactics.ChaseSeconds(behind), BotTactics.ChaseSeconds(trailing));
            Assert.Less(BotTactics.ChaseSeconds(ahead), BotTactics.ChaseSeconds(trailing));
        }
    }
}

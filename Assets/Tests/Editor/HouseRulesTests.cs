using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The match rules the host sets (HouseRules): normal is today's game, the wave length's factor, a public lobby
    /// always playing normal, the summary line, and settings.cfg's keys
    /// </summary>
    public class HouseRulesTests
    {
        [TearDown]
        public void TearDown()
        {
            HouseRules.Current = HouseRules.Normal;
        }

        [Test]
        public void Normal_IsTodaysGame()
        {
            HouseRules normal = HouseRules.Normal;

            Assert.AreEqual(MatchLength.Normal, normal.Length);
            Assert.IsTrue(normal.Steals);
            Assert.IsTrue(normal.GoldenOrder);
            Assert.AreEqual(1f, normal.WaveFactor);
            Assert.AreEqual("", normal.Summary);
        }

        [TestCase(MatchLength.Short, 0.75f)]
        [TestCase(MatchLength.Normal, 1f)]
        [TestCase(MatchLength.Long, 1.5f)]
        public void WaveFactor_ByLength(MatchLength length, float factor)
        {
            Assert.AreEqual(factor, new HouseRules(length, true, true).WaveFactor);
        }

        [Test]
        public void APublicLobby_PlaysNormal()
        {
            HouseRules mine = new HouseRules(MatchLength.Short, false, false);

            Assert.AreEqual(HouseRules.Normal, mine.ForMatch(true));
            Assert.AreEqual(mine, mine.ForMatch(false));
        }

        [Test]
        public void Summary_NamesWhatsChanged()
        {
            Assert.AreEqual("SHORT · NO STEALS · NO GOLDEN ORDER", new HouseRules(MatchLength.Short, false, false).Summary);
            Assert.AreEqual("LONG", new HouseRules(MatchLength.Long, true, true).Summary);
        }

        [Test]
        public void Settings_LoadBack_AndOldFilesPlayNormal()
        {
            GameSettings saved = new GameSettings(3, 4, 1);
            saved.matchLength = (int)MatchLength.Long;
            saved.steals = 0;
            saved.goldenOrder = 0;

            GameSettings loaded = GameSettings.Parse(saved.ToText(), new GameSettings(5, 6, 1));
            Assert.AreEqual(new HouseRules(MatchLength.Long, false, false), HouseRules.From(loaded));

            GameSettings old = GameSettings.Parse("bgm=3", new GameSettings(5, 6, 1));
            Assert.AreEqual(HouseRules.Normal, HouseRules.From(old));
        }

        [Test]
        public void Bots_AreOnInNormal_AndSavedInSettings()
        {
            Assert.IsTrue(HouseRules.Normal.Bots);
            Assert.IsTrue(HouseRules.From(GameSettings.Parse("bgm=3", new GameSettings(5, 6, 1))).Bots);

            GameSettings saved = new GameSettings(3, 4, 1);
            saved.bots = 0;
            HouseRules loaded = HouseRules.From(GameSettings.Parse(saved.ToText(), new GameSettings(5, 6, 1)));

            Assert.IsFalse(loaded.Bots);
            Assert.AreEqual("NO BOTS", loaded.Summary);
        }

        [Test]
        public void BotsOff_AMatchPlaysItAnywayInAPublicLobby()
        {
            HouseRules noBots = new HouseRules(MatchLength.Normal, true, true, false);

            Assert.IsTrue(noBots.ForMatch(true).Bots);
            Assert.IsFalse(noBots.ForMatch(false).Bots);
        }

        [Test]
        public void Settings_AreClamped()
        {
            GameSettings loaded = GameSettings.Parse("length=9\nsteals=4\ngolden=-1", new GameSettings(5, 6, 1));

            Assert.AreEqual((int)MatchLength.Long, loaded.matchLength);
            Assert.AreEqual(1, loaded.steals);
            Assert.AreEqual(0, loaded.goldenOrder);
        }

        [Test]
        public void Normal_IsStandardWithDefaultMutators()
        {
            HouseRules normal = HouseRules.Normal;

            Assert.AreEqual(GameMode.Standard, normal.Mode);
            Assert.AreEqual(3, normal.Waves);
            Assert.AreEqual(100, normal.SpeedPercent);
            Assert.IsFalse(normal.LowGravity);
            Assert.AreEqual(3, normal.RoundMinutes);
            Assert.IsFalse(normal.IsExperimental);
            Assert.AreEqual(1f, normal.SpeedScale);
            Assert.AreEqual(1f, normal.GravityScale);
            Assert.AreEqual(3, normal.WavesPlayed);
        }

        static HouseRules With(GameMode mode = GameMode.Standard, int waves = 3, int speed = 100, bool lowGravity = false,
            int round = 3, bool steals = true, bool golden = true)
        {
            return new HouseRules(MatchLength.Normal, steals, golden, true, mode, waves, speed, lowGravity, round);
        }

        [Test]
        public void Equals_DiffersOnEachNewField()
        {
            HouseRules normal = HouseRules.Normal;

            Assert.AreEqual(normal, With());
            Assert.AreNotEqual(normal, With(mode: GameMode.GoldRush));
            Assert.AreNotEqual(normal, With(waves: 5));
            Assert.AreNotEqual(normal, With(speed: 150));
            Assert.AreNotEqual(normal, With(lowGravity: true));
            Assert.AreNotEqual(normal, With(round: 5));
        }

        [Test]
        public void PackUnpack_RoundTripsEveryField()
        {
            HouseRules odd = new HouseRules(MatchLength.Long, false, false, false, GameMode.HotPotato, 6, 200, true, 5);

            Assert.AreEqual(odd, HouseRules.Unpack(odd.Pack()));
            Assert.AreEqual(HouseRules.Normal, HouseRules.Unpack(HouseRules.Normal.Pack()));
        }

        [Test]
        public void Unpack_NonsenseFromAnotherMachine_BecomesTheNearestChoices()
        {
            HouseRules rules = HouseRules.Unpack(-1);

            Assert.AreEqual(GameMode.Standard, rules.Mode, "mode 7 isn't one");
            Assert.AreEqual(MatchLength.Normal, rules.Length, "length 3 isn't one");
            Assert.AreEqual(6, rules.Waves);
            Assert.AreEqual(200, rules.SpeedPercent);
            Assert.AreEqual(5, rules.RoundMinutes);
            Assert.AreEqual(1, HouseRules.Unpack(0).Waves);
        }

        [Test]
        public void FollowHost_PlaysTheHostsRules()
        {
            HouseRules host = new HouseRules(MatchLength.Normal, true, true, true, GameMode.Standard, 3, 50, true);
            try
            {
                HouseRules.FollowHost(host.Pack());

                Assert.AreEqual(host, HouseRules.Playing);
                Assert.AreEqual(0.5f, MatchPhysics.SpeedScale);
            }
            finally
            {
                HouseRules.Playing = HouseRules.Normal;
                MatchPhysics.Reset();
            }
        }

        [Test]
        public void StealsAndGolden_FollowTheModesTable()
        {
            Assert.IsTrue(With(GameMode.GoldRush, steals: false).StealsOn, "Gold Rush locks steals on");
            Assert.IsFalse(With(GameMode.Infection, steals: true).StealsOn, "Infection has no steals");
            Assert.IsFalse(With(steals: false).StealsOn);
            Assert.IsFalse(With(GameMode.KingsCrown).GoldenOn, "no golden round without deliveries");
            Assert.IsTrue(With(GameMode.GoldRush).GoldenOn);
            Assert.IsFalse(With(golden: false).GoldenOn);
        }

        [Test]
        public void EffectiveMutators()
        {
            Assert.AreEqual(5, With(waves: 5).WavesPlayed);
            Assert.AreEqual(1.5f, With(speed: 150).SpeedScale);
            Assert.AreEqual(0.5f, With(lowGravity: true).GravityScale);
            Assert.IsTrue(With(speed: 75).IsExperimental);
            Assert.IsTrue(With(GameMode.GoldRush).IsExperimental);
            Assert.IsTrue(With(waves: 2).IsExperimental);
            Assert.IsFalse(With(round: 5).IsExperimental, "the round timer only matters in a new mode, which is already experimental");
        }

        [Test]
        public void ForMatch_PublicLobby_IsNormalEvenInAnotherMode()
        {
            Assert.AreEqual(HouseRules.Normal, With(GameMode.HotPotato, speed: 200, lowGravity: true).ForMatch(true));
        }

        [Test]
        public void Summary_ListsTheModeAndMutators()
        {
            Assert.AreEqual("SPEED 150% · LOW GRAVITY · 5 WAVES", With(waves: 5, speed: 150, lowGravity: true).Summary);
            Assert.AreEqual("GOLD RUSH", With(GameMode.GoldRush).Summary);
            Assert.AreEqual("KINGS CROWN · 5 MIN", With(GameMode.KingsCrown, waves: 5, round: 5).Summary,
                "waves don't show in a mode without them; the round timer does");
        }

        [Test]
        public void Settings_KeepTheNewFields_AndOldFilesGetDefaults()
        {
            GameSettings saved = new GameSettings(3, 4, 1);
            With(GameMode.Infection, 4, 125, true, 5).Into(saved);

            HouseRules loaded = HouseRules.From(GameSettings.Parse(saved.ToText(), new GameSettings(5, 6, 1)));
            Assert.AreEqual(With(GameMode.Infection, 4, 125, true, 5), loaded);

            HouseRules old = HouseRules.From(GameSettings.Parse("bgm=3\nlength=2", new GameSettings(5, 6, 1)));
            Assert.AreEqual(100, old.SpeedPercent, "an old file must not freeze everyone at 0%");
            Assert.AreEqual(3, old.Waves);
            Assert.AreEqual(GameMode.Standard, old.Mode);
        }

        [Test]
        public void Settings_NewFieldsAreClamped()
        {
            GameSettings loaded = GameSettings.Parse("mode=99\nwaves=0\nspeed=170\nlowgravity=7\nround=4", new GameSettings(5, 6, 1));
            HouseRules rules = HouseRules.From(loaded);

            Assert.AreEqual(GameMode.HotPotato, rules.Mode);
            Assert.AreEqual(1, rules.Waves);
            Assert.AreEqual(150, rules.SpeedPercent, "nearest speed choice");
            Assert.IsTrue(rules.LowGravity);
            Assert.AreEqual(3, rules.RoundMinutes, "nearest round choice (ties go down)");
        }

        [Test]
        public void ForMatch_TheMatchUsesCurrent()
        {
            HouseRules.Current = new HouseRules(MatchLength.Short, true, false);

            Assert.IsFalse(HouseRules.Current.GoldenOrder);
        }
    }
}

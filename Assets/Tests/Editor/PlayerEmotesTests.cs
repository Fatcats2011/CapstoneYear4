using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// A ghost emoting in a match: when (PlayerEmoteRules, at each edge), and what each player's PlayerEmotes asks for as
    /// steals, deliveries, boosts and bumps are heard and as it is polled (Tick), with players made as StealFeedbackTests
    /// makes them. The asked-for emote is checked; GhostEmotes decides whether it plays (GhostEmotesTests)
    /// </summary>
    public class PlayerEmotesTests
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        readonly TestObjects objects = new TestObjects();
        static bool matchOn;

        static bool Match() { return matchOn; }

        [SetUp]
        public void SetUp()
        {
            matchOn = true;
            PlayerEmotes.MatchOn = Match;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (PlayerEmotes player in Object.FindObjectsOfType<PlayerEmotes>(true))
                player.OnDisable();
            PlayerEmotes.MatchOn = null;
            HouseRules.Playing = HouseRules.Normal;
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        // A player in the next free seat (as StealFeedbackTests), on the ground at a place, facing +z, with their emotes on
        PlayerEmotes NewPlayer(string name, Vector3 at)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            handler.ball.grounded = true;
            control.transform.position = at;
            PlayerEmotes emotes = PlayerEmotes.Attach(player);
            emotes.OnEnable(); // Edit Mode: nothing switches it on
            return emotes;
        }

        static void MoveTo(PlayerEmotes player, Vector3 at)
        {
            player.Handler.transform.position = at;
        }

        static void Hold(PlayerEmotes player, Constants.OrderValue value = Constants.OrderValue.Easy)
        {
            Order order = player.Handler.gameObject.AddComponent<Order>();
            order.value = value;
            player.Handler.order1 = order;
        }

        // Both players driven at a steady velocity for a while (each frame: move both, then tick both)
        static void Drive(PlayerEmotes a, Vector3 aVelocity, PlayerEmotes b, Vector3 bVelocity, int frames, float dt = 0.1f)
        {
            for (int i = 0; i < frames; i++)
            {
                MoveTo(a, a.Handler.transform.position + aVelocity * dt);
                MoveTo(b, b.Handler.transform.position + bVelocity * dt);
                a.Tick(dt);
                b.Tick(dt);
            }
        }

        // ---- PlayerEmoteRules ----

        [Test]
        public void ForSteal_ARealSteal_CacklesTheThief_FistShakesTheVictim()
        {
            Assert.AreEqual(GhostAction.Cackle, PlayerEmoteRules.ForSteal(1, 1, 2, BumpOutcome.Stolen));
            Assert.AreEqual(GhostAction.FistShake, PlayerEmoteRules.ForSteal(2, 1, 2, BumpOutcome.Stolen));
            Assert.IsNull(PlayerEmoteRules.ForSteal(3, 1, 2, BumpOutcome.Stolen), "another seat");
            Assert.IsNull(PlayerEmoteRules.ForSteal(-1, -1, 2, BumpOutcome.Stolen), "no seat");
            Assert.IsNull(PlayerEmoteRules.ForSteal(1, 1, 1, BumpOutcome.Stolen), "both sides");
        }

        [TestCase(BumpOutcome.SlotsFull)]
        [TestCase(BumpOutcome.NothingToSteal)]
        [TestCase(BumpOutcome.StealsOff)]
        [TestCase(BumpOutcome.ModeHit)]
        public void ForSteal_NoSteal_Nothing(BumpOutcome outcome)
        {
            Assert.IsNull(PlayerEmoteRules.ForSteal(1, 1, 2, outcome));
            Assert.IsNull(PlayerEmoteRules.ForSteal(2, 1, 2, outcome));
        }

        [TestCase(GainReason.Delivered)]
        [TestCase(GainReason.Golden)]
        [TestCase(GainReason.Lode)]
        public void ForGain_ItsOwnDelivery_Flourishes(GainReason reason)
        {
            Assert.AreEqual(GhostAction.DeliveryFlourish, PlayerEmoteRules.ForGain(0, 0, reason));
            Assert.IsNull(PlayerEmoteRules.ForGain(0, 1, reason), "another seat's");
            Assert.IsNull(PlayerEmoteRules.ForGain(-1, -1, reason), "no seat");
        }

        [TestCase(0f, 1f, 0)]
        [TestCase(1f, 0f, 1)]
        [TestCase(0f, -1f, 2)]
        [TestCase(-1f, 0f, 3)]
        [TestCase(1f, 1f, 1)]   // sideways wins
        [TestCase(-1f, -1f, 3)]
        [TestCase(0f, 0f, -1)]  // let go
        [TestCase(0.5f, 0f, -1)]
        public void PadIndex_UpRightDownLeft(float x, float y, int index)
        {
            Assert.AreEqual(index, PlayerEmoteRules.PadIndex(new Vector2(x, y)));
        }

        [Test]
        public void ForPad_EachDirection_ItsBubblesAction()
        {
            Assert.AreEqual(GhostAction.BigWave, PlayerEmoteRules.ForPad(0), "up: the grinning bear");
            Assert.AreEqual(GhostAction.Taunt, PlayerEmoteRules.ForPad(1), "right: NUH UH");
            Assert.AreEqual(GhostAction.Cackle, PlayerEmoteRules.ForPad(2), "down: the big laugh");
            Assert.AreEqual(GhostAction.Raspberry, PlayerEmoteRules.ForPad(3), "left: Whaaat?!");
            Assert.IsNull(PlayerEmoteRules.ForPad(-1));
            Assert.IsNull(PlayerEmoteRules.ForPad(PlayerEmoteRules.PAD_EMOTES));
        }

        [Test]
        public void EmoteCues_OnePerDirection_AndOnlyDizzyIsTheHosts()
        {
            for (int i = 0; i < PlayerEmoteRules.PAD_EMOTES; i++)
            {
                ScooterCue cue = ScooterCue.Emote(i);
                Assert.IsTrue(NetChecks.Defined(cue.Kind), "a kind the game knows: " + i);
                Assert.AreEqual(i, ScooterCue.EmoteIndex(cue.Kind));
                Assert.IsFalse(ScooterCue.HostOnly(cue.Kind));
            }
            Assert.AreEqual(CueKind.Emote0, ScooterCue.Emote(0).Kind);
            Assert.AreEqual(CueKind.Emote3, ScooterCue.Emote(3).Kind);
            Assert.AreEqual(-1, ScooterCue.EmoteIndex(CueKind.Boost));
            Assert.AreEqual(-1, ScooterCue.EmoteIndex(CueKind.Dizzy));
            Assert.IsTrue(NetChecks.Defined(CueKind.Dizzy));
            Assert.IsTrue(ScooterCue.HostOnly(CueKind.Dizzy));
            Assert.IsFalse(ScooterCue.HostOnly(CueKind.Boost));
            Assert.IsFalse(NetChecks.Defined((CueKind)200), "an unknown kind");
        }

        [Test]
        public void HardBump_FromPointSix()
        {
            Assert.IsFalse(PlayerEmoteRules.HardBump(0.59f));
            Assert.IsTrue(PlayerEmoteRules.HardBump(0.6f));
            Assert.IsTrue(PlayerEmoteRules.HardBump(1f));
        }

        [Test]
        public void AirFor_CountsUpInTheAir_BackToZeroOnTheGround()
        {
            Assert.AreEqual(0.3f, PlayerEmoteRules.AirFor(0.2f, false, 0.1f), 1e-5f);
            Assert.AreEqual(0f, PlayerEmoteRules.AirFor(0.2f, true, 0.1f));
        }

        [Test]
        public void Wheee_PointFourInTheAir_AfterRising_Once()
        {
            Assert.IsFalse(PlayerEmoteRules.Wheee(0.39f, 1f, false), "not yet");
            Assert.IsTrue(PlayerEmoteRules.Wheee(0.4f, 0.25f, false));
            Assert.IsFalse(PlayerEmoteRules.Wheee(0.5f, 0.24f, false), "a drop: it never rose");
            Assert.IsFalse(PlayerEmoteRules.Wheee(0.5f, 1f, true), "not again in the same time in the air");
            Assert.AreEqual(0.5f, PlayerEmoteRules.Rise(0.2f, 1.5f, 1f), 1e-6f);
            Assert.AreEqual(0.2f, PlayerEmoteRules.Rise(0.2f, 0.5f, 1f), 1e-6f, "falling keeps the highest");
        }

        [Test]
        public void StillFor_CountsUpBelowHalfAMetreASecond_BackToZeroMoving()
        {
            Assert.AreEqual(1.5f, PlayerEmoteRules.StillFor(1f, 0.49f, 0.5f), 1e-5f);
            Assert.AreEqual(0f, PlayerEmoteRules.StillFor(1f, 0.5f, 0.5f));
        }

        [Test]
        public void Bored_OnceAsTheStillTimeReachesTwelve()
        {
            Assert.IsFalse(PlayerEmoteRules.Bored(11f, 11.99f));
            Assert.IsTrue(PlayerEmoteRules.Bored(11.99f, 12f));
            Assert.IsFalse(PlayerEmoteRules.Bored(12f, 20f), "not again until it has moved");
        }

        [Test]
        public void TreasureHold_OnPickup_ThenEveryFifteenSeconds()
        {
            Assert.IsTrue(PlayerEmoteRules.TreasureHold(false, true, 0f), "picked up");
            Assert.IsFalse(PlayerEmoteRules.TreasureHold(true, true, 14.99f));
            Assert.IsTrue(PlayerEmoteRules.TreasureHold(true, true, 15f));
            Assert.IsFalse(PlayerEmoteRules.TreasureHold(true, false, 30f), "not carrying");
        }

        [Test]
        public void BehindAndAhead_TheBackAndFrontHalves_Flat()
        {
            Assert.IsTrue(PlayerEmoteRules.Behind(Vector3.forward, new Vector3(1f, 5f, -0.1f)));
            Assert.IsFalse(PlayerEmoteRules.Behind(Vector3.forward, new Vector3(1f, 0f, 0.1f)));
            Assert.IsFalse(PlayerEmoteRules.Behind(Vector3.forward, Vector3.right), "level with it is neither");
            Assert.IsFalse(PlayerEmoteRules.Ahead(Vector3.forward, Vector3.right));
            Assert.IsTrue(PlayerEmoteRules.Ahead(Vector3.forward, new Vector3(0f, -3f, 2f)));
            Assert.IsFalse(PlayerEmoteRules.Behind(Vector3.up, Vector3.back), "no flat forward");
            Assert.IsFalse(PlayerEmoteRules.Behind(Vector3.forward, Vector3.down), "straight below");
        }

        [Test]
        public void ClosingSpeed_HowFastTheGapShrinks_FromEitherSide()
        {
            Vector3 toOther = new Vector3(0f, 0f, -10f);
            // The other comes up from behind at 5 m/s faster
            Assert.AreEqual(5f, PlayerEmoteRules.ClosingSpeed(toOther, new Vector3(0, 0, 10f), new Vector3(0, 0, 15f)), 1e-5f);
            Assert.AreEqual(5f, PlayerEmoteRules.ClosingSpeed(-toOther, new Vector3(0, 0, 15f), new Vector3(0, 0, 10f)), 1e-5f);
            // Pulling away
            Assert.AreEqual(-3f, PlayerEmoteRules.ClosingSpeed(toOther, Vector3.zero, new Vector3(0, 0, -3f)), 1e-5f);
            // Sideways and up don't close it
            Assert.AreEqual(0f, PlayerEmoteRules.ClosingSpeed(toOther, Vector3.zero, new Vector3(4f, 9f, 0f)), 1e-5f);
            Assert.AreEqual(0f, PlayerEmoteRules.ClosingSpeed(Vector3.up, Vector3.zero, Vector3.down), "on top of each other");
        }

        [Test]
        public void Closing_FasterThanHalfAMetreASecond()
        {
            Assert.IsFalse(PlayerEmoteRules.Closing(0.5f));
            Assert.IsTrue(PlayerEmoteRules.Closing(0.51f));
        }

        [Test]
        public void CarrierLook_JumpScareWithinFourClosing_SideEyeWithinTwelveEveryEight()
        {
            Assert.AreEqual(GhostAction.JumpScare, PlayerEmoteRules.CarrierLook(4f, true, true, 0f));
            Assert.AreEqual(GhostAction.SideEye, PlayerEmoteRules.CarrierLook(4f, true, false, 8f), "close but not closing");
            Assert.IsNull(PlayerEmoteRules.CarrierLook(4f, true, false, 7.99f), "side-eyed too lately");
            Assert.AreEqual(GhostAction.SideEye, PlayerEmoteRules.CarrierLook(4.01f, true, true, 8f));
            Assert.AreEqual(GhostAction.SideEye, PlayerEmoteRules.CarrierLook(12f, true, false, 8f));
            Assert.IsNull(PlayerEmoteRules.CarrierLook(12.01f, true, true, 8f), "too far");
            Assert.IsNull(PlayerEmoteRules.CarrierLook(1f, false, true, 8f), "not behind");
        }

        [Test]
        public void Worse_JumpScareOverSideEyeOverNothing()
        {
            Assert.AreEqual(GhostAction.JumpScare, PlayerEmoteRules.Worse(GhostAction.SideEye, GhostAction.JumpScare));
            Assert.AreEqual(GhostAction.JumpScare, PlayerEmoteRules.Worse(GhostAction.JumpScare, null));
            Assert.AreEqual(GhostAction.SideEye, PlayerEmoteRules.Worse(null, GhostAction.SideEye));
            Assert.IsNull(PlayerEmoteRules.Worse(null, null));
        }

        [Test]
        public void Scheme_HoldingNothing_ClosingOnACarrierAheadWithinTen()
        {
            Assert.IsTrue(PlayerEmoteRules.Scheme(false, true, 10f, true, true));
            Assert.IsFalse(PlayerEmoteRules.Scheme(false, true, 10.01f, true, true), "too far");
            Assert.IsFalse(PlayerEmoteRules.Scheme(true, true, 5f, true, true), "carrying");
            Assert.IsFalse(PlayerEmoteRules.Scheme(false, false, 5f, true, true), "they carry nothing");
            Assert.IsFalse(PlayerEmoteRules.Scheme(false, true, 5f, false, true), "not ahead");
            Assert.IsFalse(PlayerEmoteRules.Scheme(false, true, 5f, true, false), "not closing");
        }

        // ---- PlayerEmotes: the events ----

        [Test]
        public void Attach_IsIdempotent_AndNeedsAnOrderHandler()
        {
            PlayerEmotes first = NewPlayer("Player 1", Vector3.zero);
            Assert.AreSame(first, PlayerEmotes.Attach(first.gameObject));
            Assert.AreEqual(1, first.GetComponents<PlayerEmotes>().Length);
            Assert.IsNull(PlayerEmotes.Attach(objects.NewGameObject("Nobody")));
            Assert.IsNull(PlayerEmotes.Attach(null));
        }

        [Test]
        public void Seat_IsItsRosterSeat()
        {
            PlayerEmotes first = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes second = NewPlayer("Player 2", Vector3.zero);
            Assert.AreEqual(0, first.Seat);
            Assert.AreEqual(1, second.Seat);
        }

        [Test]
        public void StealFeedback_ARealSteal_ThiefCackles_VictimFistShakes_OthersNothing()
        {
            PlayerEmotes thief = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes victim = NewPlayer("Player 2", Vector3.zero);
            PlayerEmotes bystander = NewPlayer("Player 3", Vector3.zero);

            StealFeedback.Announce(new BumpNotice(0, 1, BumpOutcome.Stolen, 5));

            Assert.AreEqual(GhostAction.Cackle, thief.Asked);
            Assert.AreEqual(GhostAction.FistShake, victim.Asked);
            Assert.IsNull(bystander.Asked);
        }

        [TestCase(BumpOutcome.SlotsFull)]
        [TestCase(BumpOutcome.NothingToSteal)]
        [TestCase(BumpOutcome.StealsOff)]
        [TestCase(BumpOutcome.ModeHit)]
        public void StealFeedback_NoSteal_NobodyEmotes(BumpOutcome outcome)
        {
            PlayerEmotes thief = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes victim = NewPlayer("Player 2", Vector3.zero);

            StealFeedback.Announce(new BumpNotice(0, 1, outcome, 0));

            Assert.AreEqual(0, thief.AskCount);
            Assert.AreEqual(0, victim.AskCount);
        }

        [Test]
        public void Events_OutsideAMatch_NobodyEmotes()
        {
            PlayerEmotes thief = NewPlayer("Player 1", Vector3.zero);
            NewPlayer("Player 2", Vector3.zero);
            matchOn = false;

            StealFeedback.Announce(new BumpNotice(0, 1, BumpOutcome.Stolen, 5));
            ScoreFeedback.AnnounceGain(0, GainReason.Delivered, 10, "");
            Reflect.Raise(thief.Handler.ball, nameof(BallDriving.OnBoostStart));
            FeatSync.Bump(1, 0, 1f);

            Assert.AreEqual(0, thief.AskCount);
        }

        [Test]
        public void ScoreFeedback_ItsDelivery_Flourishes_AnotherSeatsDoesnt()
        {
            PlayerEmotes first = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes second = NewPlayer("Player 2", Vector3.zero);

            ScoreFeedback.AnnounceGain(1, GainReason.Golden, 50, "");

            Assert.IsNull(first.Asked);
            Assert.AreEqual(GhostAction.DeliveryFlourish, second.Asked);
        }

        [Test]
        public void OnBoostStart_HoldsItsHat_OnlyItsOwnBall()
        {
            PlayerEmotes first = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes second = NewPlayer("Player 2", Vector3.zero);

            Reflect.Raise(first.Handler.ball, nameof(BallDriving.OnBoostStart));

            Assert.AreEqual(GhostAction.HoldYourHat, first.Asked);
            Assert.IsNull(second.Asked);
        }

        [Test]
        public void FeatSyncBumped_HardOnItsSeat_SeeingStars_SoftOrAnotherSeat_Nothing()
        {
            PlayerEmotes bumper = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes victim = NewPlayer("Player 2", Vector3.zero);

            FeatSync.Bump(0, 1, 0.59f);
            Assert.AreEqual(0, victim.AskCount, "soft");

            FeatSync.Bump(0, 1, 0.6f);
            Assert.AreEqual(GhostAction.SeeingStars, victim.Asked);
            Assert.AreEqual(0, bumper.AskCount, "the bumper");
        }

        // What CueSync sent (tests): the last seat and cue, and how many
        static int sentSeat;
        static ScooterCue sentCue;
        static int sentCount;

        static void Sent(int seat, ScooterCue cue)
        {
            sentSeat = seat;
            sentCue = cue;
            sentCount++;
        }

        [Test]
        public void Pad_Offline_PlaysTheMappedAction_SendsNothing()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            sentCount = 0;
            CueSync.Played += Sent;
            try
            {
                player.Pad(1);
                Assert.AreEqual(GhostAction.Taunt, player.Asked);
                player.Pad(7);
                Assert.AreEqual(1, player.AskCount, "no such direction");
                Assert.AreEqual(0, sentCount, "offline");
            }
            finally
            {
                CueSync.Played -= Sent;
            }
        }

        [Test]
        public void Pad_Online_SendsItsSeatsEmote()
        {
            NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes player = NewPlayer("Player 2", Vector3.zero);
            sentCount = 0;
            CueSync.Played += Sent;
            GameAuthority.Role = NetworkRole.Client;
            try
            {
                player.Pad(3);
                Assert.AreEqual(GhostAction.Raspberry, player.Asked);
                Assert.AreEqual(1, sentCount);
                Assert.AreEqual(1, sentSeat);
                Assert.AreEqual(ScooterCue.Emote(3), sentCue);

                player.Pad(-1);
                Assert.AreEqual(1, sentCount, "nothing for no direction");
            }
            finally
            {
                GameAuthority.Role = NetworkRole.Offline;
                CueSync.Played -= Sent;
            }
        }

        [Test]
        public void ShowPadAndShowDizzy_PlayHere_SendNothing()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            sentCount = 0;
            CueSync.Played += Sent;
            GameAuthority.Role = NetworkRole.Client;
            try
            {
                Assert.IsTrue(player.ShowPad(2));
                Assert.AreEqual(GhostAction.Cackle, player.Asked);
                Assert.IsFalse(player.ShowPad(4));
                player.ShowDizzy();
                Assert.AreEqual(GhostAction.SeeingStars, player.Asked);
                Assert.AreEqual(0, sentCount);
            }
            finally
            {
                GameAuthority.Role = NetworkRole.Offline;
                CueSync.Played -= Sent;
            }
        }

        [Test]
        public void WallStrength_HalfAtTen_FullFromThirty()
        {
            Assert.AreEqual(PlayerEmoteRules.WALL_WEAKEST, PlayerEmoteRules.WallStrength(PlayerEmoteRules.WALL_SOFT), 1e-6f);
            Assert.AreEqual(0.75f, PlayerEmoteRules.WallStrength(20f), 1e-6f);
            Assert.AreEqual(1f, PlayerEmoteRules.WallStrength(PlayerEmoteRules.WALL_HARD), 1e-6f);
            Assert.AreEqual(1f, PlayerEmoteRules.WallStrength(80f), 1e-6f);
            Assert.AreEqual(PlayerEmoteRules.WALL_WEAKEST, PlayerEmoteRules.WallStrength(0f), 1e-6f);
        }

        [Test]
        public void WallBumpCue_CarriesTheSpeed_KeptInRange()
        {
            ScooterCue cue = ScooterCue.WallBump(23.6f);
            Assert.AreEqual(CueKind.WallBump, cue.Kind);
            Assert.AreEqual(24f, ScooterCue.WallSpeed(cue), 1e-6f);
            Assert.AreEqual(ScooterCue.WALL_MAX, ScooterCue.WallBump(500f).Point);
            Assert.AreEqual(ScooterCue.WALL_MAX, ScooterCue.WallSpeed(new ScooterCue { Kind = CueKind.WallBump, Point = 9999 }), 1e-6f);
            Assert.AreEqual(ScooterCue.WALL_MIN, ScooterCue.WallSpeed(new ScooterCue { Kind = CueKind.WallBump, Point = -9999 }), 1e-6f);
            Assert.IsTrue(NetChecks.Defined(CueKind.WallBump));
            Assert.IsFalse(ScooterCue.HostOnly(CueKind.WallBump), "the scooter's own machine sends it");
            Assert.AreEqual(-1, ScooterCue.EmoteIndex(CueKind.WallBump));
            Assert.IsNull(RemoteSound.KeyFor(CueKind.WallBump), "no sound of its own");
        }

        [Test]
        public void HitWall_Offline_SwaysInAMatch_SendsNothing()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            sentCount = 0;
            CueSync.Played += Sent;
            try
            {
                player.HitWall(20f);
                Assert.AreEqual(GhostAction.WallBump, player.Asked);
                Assert.AreEqual(0, sentCount, "offline");

                matchOn = false;
                player.HitWall(20f);
                Assert.AreEqual(1, player.AskCount, "not outside a match");
            }
            finally
            {
                CueSync.Played -= Sent;
            }
        }

        [Test]
        public void HitWall_Online_SendsItsSeatsBump_ShowWallBumpSendsNothing()
        {
            NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes player = NewPlayer("Player 2", Vector3.zero);
            sentCount = 0;
            CueSync.Played += Sent;
            GameAuthority.Role = NetworkRole.Client;
            try
            {
                player.HitWall(17.2f);
                Assert.AreEqual(GhostAction.WallBump, player.Asked);
                Assert.AreEqual(1, sentCount);
                Assert.AreEqual(1, sentSeat);
                Assert.AreEqual(ScooterCue.WallBump(17.2f), sentCue);

                player.ShowWallBump(25f);
                Assert.AreEqual(2, player.AskedFor(GhostAction.WallBump));
                Assert.AreEqual(1, sentCount, "another machine's bump isn't sent on");
            }
            finally
            {
                GameAuthority.Role = NetworkRole.Offline;
                CueSync.Played -= Sent;
            }
        }

        [Test]
        public void OnDisable_StopsListening()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            player.OnDisable();

            ScoreFeedback.AnnounceGain(0, GainReason.Delivered, 10, "");
            Reflect.Raise(player.Handler.ball, nameof(BallDriving.OnBoostStart));

            Assert.AreEqual(0, player.AskCount);
            Assert.AreEqual(0, Reflect.HandlerCount(player.Handler.ball, nameof(BallDriving.OnBoostStart), player));
        }

        [Test]
        public void OnEnable_Twice_ListensOnce()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            player.OnEnable();

            ScoreFeedback.AnnounceGain(0, GainReason.Delivered, 10, "");

            Assert.AreEqual(1, player.AskCount);
            Assert.AreEqual(1, Reflect.HandlerCount(player.Handler.ball, nameof(BallDriving.OnBoostStart), player));
        }

        // ---- PlayerEmotes: the poll ----

        [Test]
        public void Tick_InTheAirPointFour_WheeeOnce()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            player.Tick(0.15f);
            player.Handler.ball.grounded = false;

            // Launched: rising as it goes
            for (int i = 0; i < 2; i++)
                Up(player, 0.15f);
            Assert.AreEqual(0, player.AskCount, "0.3 s up");

            Up(player, 0.15f);
            Assert.AreEqual(GhostAction.Wheee, player.Asked);
            for (int i = 0; i < 10; i++)
                Up(player, 0.15f);
            Assert.AreEqual(1, player.AskCount, "once per time in the air");

            player.Handler.ball.grounded = true;
            player.Tick(0.15f);
            player.Handler.ball.grounded = false;
            for (int i = 0; i < 3; i++)
                Up(player, 0.15f);
            Assert.AreEqual(2, player.AskCount, "again after landing");
        }

        [Test]
        public void Tick_DroppingOffAnEdge_NoWheee()
        {
            PlayerEmotes player = NewPlayer("Player 1", new Vector3(0f, 5f, 0f));
            player.Tick(0.15f);
            player.Handler.ball.grounded = false;
            for (int i = 0; i < 10; i++)
                Up(player, 0.15f, -0.5f);
            Assert.AreEqual(0, player.AskCount, "falling all the way: into the river, say");
        }

        // A frame in the air, the ball going up (or down) by dy
        static void Up(PlayerEmotes player, float dt, float dy = 0.2f)
        {
            MoveTo(player, player.Handler.transform.position + new Vector3(0f, dy, 0f));
            player.Tick(dt);
        }

        [Test]
        public void Tick_OutsideAMatch_NothingAndTimersForgotten()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            player.Handler.ball.grounded = false;
            player.Tick(0.3f);
            matchOn = false;
            player.Tick(0.3f);
            Assert.AreEqual(0, player.AskCount);

            matchOn = true;
            player.Tick(0.3f);
            Assert.AreEqual(0, player.AskCount, "the air time started over");
        }

        [Test]
        public void Tick_GoldenOrder_TreasureHoldOnPickup_ThenEveryFifteenSeconds()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            player.Tick(1f);
            Assert.AreEqual(0, player.AskCount);

            Hold(player, Constants.OrderValue.Golden);
            player.Tick(0.1f);
            Assert.AreEqual(GhostAction.TreasureHold, player.Asked);
            Assert.AreEqual(1, player.AskCount);

            // Moving, so it isn't bored meanwhile
            for (int i = 0; i < 148; i++)
            {
                MoveTo(player, player.Handler.transform.position + Vector3.forward);
                player.Tick(0.1f);
            }
            Assert.AreEqual(1, player.AskCount, "14.8 s on");
            for (int i = 0; i < 3; i++)
            {
                MoveTo(player, player.Handler.transform.position + Vector3.forward);
                player.Tick(0.1f);
            }
            Assert.AreEqual(2, player.AskCount, "15.1 s on");
            Assert.AreEqual(GhostAction.TreasureHold, player.Asked);
        }

        [Test]
        public void Tick_AnOrdinaryOrder_NoTreasureHold()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            Hold(player);
            player.Tick(0.1f);
            Assert.AreEqual(0, player.AskCount);
        }

        [Test]
        public void Tick_StillTwelveSeconds_BoredOnce_AgainOnlyAfterMoving()
        {
            PlayerEmotes player = NewPlayer("Player 1", Vector3.zero);
            for (int i = 0; i < 119; i++)
                player.Tick(0.1f);
            Assert.AreEqual(0, player.AskCount, "11.9 s");
            player.Tick(0.1f);
            player.Tick(0.1f);
            Assert.AreEqual(GhostAction.Bored, player.Asked);
            for (int i = 0; i < 200; i++)
                player.Tick(0.1f);
            Assert.AreEqual(1, player.AskCount, "not again while still");

            // A burst of driving, then still again for long enough
            for (int i = 0; i < 5; i++)
            {
                MoveTo(player, player.Handler.transform.position + Vector3.forward);
                player.Tick(0.1f);
            }
            for (int i = 0; i < 160; i++)
                player.Tick(0.1f);
            Assert.AreEqual(2, player.AskCount);
        }

        [Test]
        public void Tick_CarrierWithARivalClosingFromBehindWithinFour_JumpScare()
        {
            PlayerEmotes carrier = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes rival = NewPlayer("Player 2", new Vector3(0f, 0f, -6f));
            Hold(carrier);

            // The rival gains 3 m/s on the carrier, from 6 m behind: within 4 m after about 0.7 s
            Drive(carrier, new Vector3(0, 0, 5f), rival, new Vector3(0, 0, 8f), 10);

            Assert.AreEqual(GhostAction.JumpScare, carrier.Asked);
            Assert.AreEqual(GhostAction.Scheme, rival.Asked, "and the rival, holding nothing, schemes");
        }

        [Test]
        public void Tick_CarrierWithARivalSittingBehindWithinTwelve_SideEyeAtMostEveryEight()
        {
            PlayerEmotes carrier = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes rival = NewPlayer("Player 2", new Vector3(0f, 0f, -10f));
            Hold(carrier);

            // Both at the same speed: not closing, 10 m behind (the rival counts from its first frame measured)
            Drive(carrier, new Vector3(0, 0, 5f), rival, new Vector3(0, 0, 5f), 2);
            Assert.AreEqual(GhostAction.SideEye, carrier.Asked);
            Drive(carrier, new Vector3(0, 0, 5f), rival, new Vector3(0, 0, 5f), 78);
            Assert.AreEqual(1, carrier.AskCount, "7.8 s on");
            Drive(carrier, new Vector3(0, 0, 5f), rival, new Vector3(0, 0, 5f), 3);
            Assert.AreEqual(2, carrier.AskCount, "8.1 s on");
            Assert.AreEqual(0, rival.AskCount, "not closing: the rival doesn't scheme");
        }

        [Test]
        public void Tick_CarrierWithARivalAheadOrFar_Nothing()
        {
            PlayerEmotes carrier = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes ahead = NewPlayer("Player 2", new Vector3(0f, 0f, 3f));
            PlayerEmotes far = NewPlayer("Player 3", new Vector3(0f, 0f, -20f));
            Hold(carrier);
            far.Tick(0.1f);

            Drive(carrier, new Vector3(0, 0, 5f), ahead, new Vector3(0, 0, 5f), 3);

            Assert.AreEqual(0, carrier.AskCount);
        }

        [Test]
        public void Tick_CarrierWithStealsOff_NoRivalLooks()
        {
            HouseRules.Playing = new HouseRules(MatchLength.Normal, false, true, true);
            PlayerEmotes carrier = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes rival = NewPlayer("Player 2", new Vector3(0f, 0f, -6f));
            Hold(carrier);

            Drive(carrier, new Vector3(0, 0, 5f), rival, new Vector3(0, 0, 8f), 10);

            Assert.AreEqual(0, carrier.AskCount);
        }

        [Test]
        public void Tick_HoldingNothing_ClosingOnACarrierAheadWithinTen_Scheme()
        {
            PlayerEmotes chaser = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes carrier = NewPlayer("Player 2", new Vector3(0f, 0f, 9f));
            Hold(carrier);

            Drive(chaser, new Vector3(0, 0, 8f), carrier, new Vector3(0, 0, 5f), 3);

            Assert.AreEqual(GhostAction.Scheme, chaser.Asked);
        }

        [Test]
        public void Tick_HoldingNothing_ACarrierPullingAway_NoScheme()
        {
            PlayerEmotes chaser = NewPlayer("Player 1", Vector3.zero);
            PlayerEmotes carrier = NewPlayer("Player 2", new Vector3(0f, 0f, 6f));
            Hold(carrier);

            // The carrier measured first, so the chaser never sees it standing still
            Drive(carrier, new Vector3(0, 0, 8f), chaser, new Vector3(0, 0, 5f), 3);

            Assert.AreEqual(0, chaser.AskCount);
        }

        // ---- A real ghost ----

        [Test]
        public void Attach_OnTheRealAvatar_FindsItsGhostAndPlaysOnIt()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR);
            Assert.IsNotNull(prefab, "the avatar prefab");
            GameObject avatar = Object.Instantiate(prefab);
            objects.Keep(avatar);

            PlayerEmotes player = PlayerEmotes.Attach(avatar);
            player.SeatOverride = 0;
            player.OnEnable();

            Assert.IsNotNull(player.Emotes, "its GhostEmotes, on the OrderHandler's Animator");
            Assert.IsNotNull(player.Emotes.Rig, "its rig");
            Assert.AreSame(player.Handler.PlayerAnimator.gameObject, player.Emotes.gameObject);

            player.HearBoost();
            Assert.IsNotNull(player.Emotes.Playing);
            Assert.AreEqual(GhostAction.HoldYourHat, player.Emotes.Playing.Action);
        }
    }
}

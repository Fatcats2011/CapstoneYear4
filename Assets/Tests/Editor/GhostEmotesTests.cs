using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The ghost emote rules (priorities, cooldowns, interrupts) and the pose and move types
    /// </summary>
    public class GhostEmotesTests
    {
        [Test]
        public void Priority_FollowsTheOrder()
        {
            GhostAction[] order =
            {
                GhostAction.FistShake, GhostAction.Cackle, GhostAction.SeeingStars, GhostAction.JumpScare,
                GhostAction.DeliveryFlourish, GhostAction.Wheee, GhostAction.HoldYourHat, GhostAction.TreasureHold,
                GhostAction.Taunt, GhostAction.SideEye, GhostAction.Scheme, GhostAction.Bored
            };

            for (int i = 0; i < order.Length - 1; i++)
            {
                Assert.Greater(GhostEmoteRules.Priority(order[i]), GhostEmoteRules.Priority(order[i + 1]),
                    order[i] + " should outrank " + order[i + 1]);
            }
        }

        [Test]
        public void Priority_MaterialiseMatchesDelivery_LobbyActionsAreFifty()
        {
            Assert.AreEqual(60, GhostEmoteRules.Priority(GhostAction.Materialise));
            Assert.AreEqual(GhostEmoteRules.Priority(GhostAction.DeliveryFlourish), GhostEmoteRules.Priority(GhostAction.Materialise));
            Assert.AreEqual(50, GhostEmoteRules.Priority(GhostAction.HatShowOff));
            Assert.AreEqual(50, GhostEmoteRules.Priority(GhostAction.CrownPolish));
        }

        [Test]
        public void PriorityAndCooldown_AreTheseValues()
        {
            Dictionary<GhostAction, (int priority, float cooldown)> values = new Dictionary<GhostAction, (int, float)>
            {
                { GhostAction.FistShake, (100, 2f) }, { GhostAction.Cackle, (90, 2f) },
                { GhostAction.SeeingStars, (80, 3f) }, { GhostAction.JumpScare, (70, 5f) },
                { GhostAction.DeliveryFlourish, (60, 2f) }, { GhostAction.Materialise, (60, 0f) },
                { GhostAction.Wheee, (50, 3f) }, { GhostAction.HoldYourHat, (40, 20f) },
                { GhostAction.TreasureHold, (30, 8f) }, { GhostAction.Taunt, (25, 1.5f) },
                { GhostAction.BigWave, (25, 1.5f) }, { GhostAction.Raspberry, (25, 1.5f) },
                { GhostAction.SideEye, (20, 8f) }, { GhostAction.Scheme, (15, 10f) }, { GhostAction.Bored, (10, 12f) },
                { GhostAction.HatShowOff, (50, 0f) }, { GhostAction.WarmUp, (50, 0f) }, { GhostAction.RevBounce, (50, 0f) },
                { GhostAction.Victory, (50, 0f) }, { GhostAction.Tantrum, (50, 0f) }, { GhostAction.FadeAway, (50, 0f) },
                { GhostAction.Impatient, (50, 0f) }, { GhostAction.MirrorCheck, (50, 0f) },
                { GhostAction.KnuckleCrack, (50, 0f) }, { GhostAction.CrownPolish, (50, 0f) }
            };
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(GhostAction)), values.Keys, "every action listed");
            foreach (KeyValuePair<GhostAction, (int priority, float cooldown)> pair in values)
            {
                Assert.AreEqual(pair.Value.priority, GhostEmoteRules.Priority(pair.Key), pair.Key + " priority");
                Assert.AreEqual(pair.Value.cooldown, GhostEmoteRules.Cooldown(pair.Key), 1e-6f, pair.Key + " cooldown");
            }
        }

        [Test]
        public void EveryAction_HasAPriorityAndACooldown()
        {
            foreach (GhostAction action in Enum.GetValues(typeof(GhostAction)))
            {
                Assert.DoesNotThrow(() => GhostEmoteRules.Priority(action), action.ToString());
                Assert.GreaterOrEqual(GhostEmoteRules.Cooldown(action), 0f, action.ToString());
            }
        }

        [Test]
        public void CanStart_FalseWhileCoolingDown()
        {
            Assert.IsFalse(GhostEmoteRules.CanStart(GhostAction.FistShake, null, 0.5f));
            Assert.IsFalse(GhostEmoteRules.CanStart(GhostAction.FistShake, GhostAction.Bored, 0.5f));
        }

        [Test]
        public void CanStart_TrueWhenNothingPlaying()
        {
            Assert.IsTrue(GhostEmoteRules.CanStart(GhostAction.Bored, null, 0f));
        }

        [Test]
        public void CanStart_EqualPriorityDoesNotInterrupt()
        {
            Assert.IsFalse(GhostEmoteRules.CanStart(GhostAction.Wheee, GhostAction.Wheee, 0f));
            Assert.IsFalse(GhostEmoteRules.CanStart(GhostAction.Victory, GhostAction.HatShowOff, 0f));
        }

        [Test]
        public void CanStart_HigherInterruptsLowerNeverTheReverse()
        {
            Assert.IsTrue(GhostEmoteRules.CanStart(GhostAction.Cackle, GhostAction.Bored, 0f));
            Assert.IsFalse(GhostEmoteRules.CanStart(GhostAction.Bored, GhostAction.Cackle, 0f));
            Assert.IsTrue(GhostEmoteRules.CanStart(GhostAction.FistShake, GhostAction.Cackle, 0f));
            Assert.IsFalse(GhostEmoteRules.CanStart(GhostAction.Cackle, GhostAction.FistShake, 0f));
        }

        [Test]
        public void MoveAt_ClampsShareBelowZeroAndAboveOne()
        {
            GhostMove move = new GhostMove(GhostAction.Bored, "Test", 1f, (t, s) =>
            {
                GhostPose pose = GhostPose.Rest;
                pose.Lift = t;
                return pose;
            });

            Assert.AreEqual(0f, move.At(-0.5f, 1f).Lift, 1e-6f);
            Assert.AreEqual(1f, move.At(1.5f, 1f).Lift, 1e-6f);
            Assert.AreEqual(0.25f, move.At(0.25f, 1f).Lift, 1e-6f);
        }

        [Test]
        public void MoveAt_PassesSideThrough()
        {
            GhostMove move = new GhostMove(GhostAction.Bored, "Test", 1f, (t, s) =>
            {
                GhostPose pose = GhostPose.Rest;
                pose.Yaw = s * 10f;
                return pose;
            });

            Assert.AreEqual(-10f, move.At(0.5f, -1f).Yaw, 1e-6f);
        }

        [Test]
        public void Pose_RestIsRest_LiftedIsNot()
        {
            Assert.IsTrue(GhostPose.Rest.IsRest());
            Assert.AreEqual(1f, GhostPose.Rest.Tall);
            Assert.AreEqual(1f, GhostPose.Rest.Wide);

            GhostPose lifted = GhostPose.Rest;
            lifted.Lift = 0.1f;
            Assert.IsFalse(lifted.IsRest());
        }

        static readonly (GhostAction, string)[] expected =
        {
            (GhostAction.Cackle, "Shoulder Shake"), (GhostAction.Cackle, "Turn And Cackle"), (GhostAction.FistShake, "Fist Pump"),
            (GhostAction.Wheee, "Floating Hat"), (GhostAction.SeeingStars, "Head Rolls"), (GhostAction.SeeingStars, "Sway"),
            (GhostAction.SeeingStars, "Hat Spin"), (GhostAction.SeeingStars, "Bobble Head"), (GhostAction.TreasureHold, "Check Behind"),
            (GhostAction.JumpScare, "Leap"), (GhostAction.JumpScare, "Hide"), (GhostAction.Bored, "Nap"),
            (GhostAction.Taunt, "Bum Wiggle"), (GhostAction.BigWave, "Overhead Wave"), (GhostAction.HatShowOff, "Spin Round"),
            (GhostAction.HatShowOff, "Hat Flip"), (GhostAction.Materialise, "Wisp"), (GhostAction.Materialise, "Drop In"),
            (GhostAction.WarmUp, "Big Stretch"), (GhostAction.WarmUp, "Neck Roll"), (GhostAction.RevBounce, "Bounce"),
            (GhostAction.RevBounce, "Rock"), (GhostAction.FadeAway, "Sink"), (GhostAction.FadeAway, "Hat Left Behind"),
            (GhostAction.FadeAway, "Blown Away"), (GhostAction.SideEye, "Look Back"), (GhostAction.SideEye, "Double Take"),
            (GhostAction.Raspberry, "Tail Point"), (GhostAction.KnuckleCrack, "Neck Crack"), (GhostAction.Cackle, "Parcel Shake"),
            (GhostAction.Cackle, "Parcel Spin"), (GhostAction.FistShake, "Arm Flail"), (GhostAction.FistShake, "Hat Throw"),
            (GhostAction.FistShake, "Lunge Grab"), (GhostAction.DeliveryFlourish, "Sweeping Bow"), (GhostAction.DeliveryFlourish, "High Fist"),
            (GhostAction.DeliveryFlourish, "Hat Lift"), (GhostAction.DeliveryFlourish, "Brush Off"), (GhostAction.SeeingStars, "Hat Clamp"),
            (GhostAction.HoldYourHat, "Plane Arms"), (GhostAction.HoldYourHat, "Superhero"), (GhostAction.Wheee, "Arms Up"),
            (GhostAction.Wheee, "Starfish"), (GhostAction.TreasureHold, "Parcel Overhead"), (GhostAction.TreasureHold, "Parcel Hug"),
            (GhostAction.TreasureHold, "Presenting"), (GhostAction.JumpScare, "Hat Grab"), (GhostAction.Bored, "Big Stretch Up"),
            (GhostAction.Bored, "Slump Over"), (GhostAction.Bored, "Shrug"), (GhostAction.Taunt, "Ear Waggle"),
            (GhostAction.Taunt, "Point Back"), (GhostAction.Taunt, "Come At Me"), (GhostAction.BigWave, "Turn And Wave"),
            (GhostAction.BigWave, "High Five"), (GhostAction.HatShowOff, "Hat Twirl"), (GhostAction.Materialise, "Ta Da"),
            (GhostAction.WarmUp, "Shadow Box"), (GhostAction.RevBounce, "Race Starter"), (GhostAction.Scheme, "Hand Rub"),
            (GhostAction.Scheme, "Sly Rub"), (GhostAction.Scheme, "Excellent"), (GhostAction.SideEye, "Parcel Clutch"),
            (GhostAction.Impatient, "Drum Solo"), (GhostAction.Impatient, "Watch Check"), (GhostAction.Impatient, "Big Yawn"),
            (GhostAction.Raspberry, "Thumbs In Ears"), (GhostAction.MirrorCheck, "Admire"), (GhostAction.MirrorCheck, "Hand Mirror"),
            (GhostAction.KnuckleCrack, "Lace And Push"), (GhostAction.Victory, "Crown Overhead"), (GhostAction.Victory, "Arm Pump Dance"),
            (GhostAction.Victory, "Arms Out Spin"), (GhostAction.Victory, "Raise The Roof"), (GhostAction.Tantrum, "Fist Pound"),
            (GhostAction.Tantrum, "Flop Back"), (GhostAction.Tantrum, "Sob"), (GhostAction.CrownPolish, "Polish")
        };

        static float[] Fields(GhostPose p)
        {
            return new[]
            {
                p.Lift, p.Push, p.Shift, p.Pitch, p.Yaw, p.Roll, p.Tall, p.Wide, p.HeadYaw, p.HeadPitch, p.HeadRoll,
                p.HeadPop, p.TailUp, p.TailWag, p.HatLift, p.HatSpin, p.HatTip, p.Fade, p.HatFade, p.HatShift
            };
        }

        [Test]
        public void Catalogue_HasThe78VersionsUnderTheRightActions()
        {
            Assert.AreEqual(78, GhostMoves.All.Count());
            Assert.AreEqual(78, expected.Length);
            foreach ((GhostAction action, string name) in expected)
                Assert.IsTrue(GhostMoves.For(action).Any(m => m.Name == name && m.Action == action), action + " / " + name);
        }

        /// <summary>
        /// The versions whose read is the hat (lifted, spun, thrown, clamped or left behind) need one; a hat bump on the
        /// side doesn't. Every action keeps a version a hatless ghost can play
        /// </summary>
        [Test]
        public void Catalogue_TheHatVersionsNeedAHat_EveryActionKeepsAHatlessOne()
        {
            string[] needHat =
            {
                "Hat Throw", "Hat Lift", "Hat Clamp", "Floating Hat", "Hat Spin", "Hat Grab", "Hat Twirl", "Hat Flip",
                "Hat Left Behind"
            };
            CollectionAssert.AreEquivalent(needHat, GhostMoves.All.Where(m => m.NeedsHat).Select(m => m.Name).ToArray());
            foreach (GhostAction action in Enum.GetValues(typeof(GhostAction)))
                Assert.IsTrue(GhostMoves.For(action).Any(m => !m.NeedsHat), action + " has a hatless version");
        }

        /// <summary>The boost's emote has no version whose read is a hand on the head (lost under the boost's purple tint)</summary>
        [Test]
        public void Catalogue_HoldYourHat_IsArmsOutOnly()
        {
            CollectionAssert.AreEquivalent(new[] { "Plane Arms", "Superhero" },
                GhostMoves.For(GhostAction.HoldYourHat).Select(m => m.Name).ToArray());
            Assert.AreEqual(GhostAction.SeeingStars, GhostMoves.All.First(m => m.Name == "Hat Clamp").Action);
        }

        [Test]
        public void Catalogue_ActionWithoutVersionsGivesEmptyArray()
        {
            // Every action has versions now: an undefined one stands in
            Assert.IsNotNull(GhostMoves.For((GhostAction)999));
            Assert.AreEqual(0, GhostMoves.For((GhostAction)999).Length);
        }

        [Test]
        public void Catalogue_ClipVersionsNameTheirClips_OthersHaveNone()
        {
            Dictionary<string, string> clips = new Dictionary<string, string>
            {
                { "Bum Wiggle", "GE_10b_BumWiggle" },
                { "Tail Point", "GE_22b_TailPoint" },
                { "Parcel Shake", "GE_1b_ParcelShake" },
                { "Parcel Spin", "GE_1d_ParcelSpin" },
                { "Arm Flail", "GE_2b_ArmFlail" },
                { "Hat Throw", "GE_2c_HatThrow" },
                { "Lunge Grab", "GE_2d_LungeGrab" },
                { "Sweeping Bow", "GE_3a_SweepingBow" },
                { "High Fist", "GE_3b_HighFist" },
                { "Hat Lift", "GE_3c_HatLift" },
                { "Brush Off", "GE_3d_BrushOff" },
                { "Hat Clamp", "GE_4a_HatClamp" },
                { "Plane Arms", "GE_4b_PlaneArms" },
                { "Superhero", "GE_4c_Superhero" },
                { "Arms Up", "GE_5a_ArmsUp" },
                { "Starfish", "GE_5c_Starfish" },
                { "Parcel Overhead", "GE_7a_ParcelOverhead" },
                { "Parcel Hug", "GE_7b_ParcelHug" },
                { "Presenting", "GE_7d_Presenting" },
                { "Hat Grab", "GE_8b_HatGrab" },
                { "Big Stretch Up", "GE_9a_BigStretchUp" },
                { "Slump Over", "GE_9b_SlumpOver" },
                { "Shrug", "GE_9c_Shrug" },
                { "Ear Waggle", "GE_10a_EarWaggle" },
                { "Point Back", "GE_10c_PointBack" },
                { "Come At Me", "GE_10d_ComeAtMe" },
                { "Turn And Wave", "GE_11b_TurnAndWave" },
                { "High Five", "GE_11c_HighFive" },
                { "Hat Twirl", "GE_12a_HatTwirl" },
                { "Ta Da", "GE_13b_TaDa" },
                { "Shadow Box", "GE_14c_ShadowBox" },
                { "Race Starter", "GE_15c_RaceStarter" },
                { "Hand Rub", "GE_19a_HandRub" },
                { "Sly Rub", "GE_19b_SlyRub" },
                { "Excellent", "GE_19c_Excellent" },
                { "Parcel Clutch", "GE_20c_ParcelClutch" },
                { "Drum Solo", "GE_21a_DrumSolo" },
                { "Watch Check", "GE_21b_WatchCheck" },
                { "Big Yawn", "GE_21c_BigYawn" },
                { "Thumbs In Ears", "GE_22a_ThumbsInEars" },
                { "Admire", "GE_23a_Admire" },
                { "Hand Mirror", "GE_23b_HandMirror" },
                { "Lace And Push", "GE_24a_LaceAndPush" },
                { "Crown Overhead", "GE_16a_CrownOverhead" },
                { "Arm Pump Dance", "GE_16b_ArmPumpDance" },
                { "Arms Out Spin", "GE_16c_ArmsOutSpin" },
                { "Raise The Roof", "GE_16d_RaiseTheRoof" },
                { "Fist Pound", "GE_17a_FistPound" },
                { "Flop Back", "GE_17b_FlopBack" },
                { "Sob", "GE_17c_Sob" },
                { "Polish", "GE_25a_Polish" },
                { "Fist Pump", "GE_2a_FistPump" },
                { "Overhead Wave", "GE_11a_OverheadWave" },
                { "Big Stretch", "GE_14a_BigStretch" }
            };
            foreach (GhostMove move in GhostMoves.All)
            {
                string clip;
                Assert.AreEqual(clips.TryGetValue(move.Name, out clip) ? clip : null, move.Clip, move.Name);
                Assert.AreEqual(move.Clip != null ? UnityEngine.Animator.StringToHash(move.Clip) : 0, move.ClipHash, move.Name);
            }
            Assert.AreEqual(54, GhostMoves.All.Count(m => m.Clip != null), "a version per clip in GhostEmotes.fbx but GE_0_RestCheck");
        }

        [Test]
        public void Catalogue_ForReturnsTheSameArrayEachTime()
        {
            Assert.AreSame(GhostMoves.For(GhostAction.Cackle), GhostMoves.For(GhostAction.Cackle));
        }

        [Test]
        public void Catalogue_NapHasZzz_OthersDoNot()
        {
            foreach (GhostMove move in GhostMoves.All)
                Assert.AreEqual(move.Name == "Nap" ? GhostExtra.Zzz : GhostExtra.None, move.Extra, move.Name);
        }

        [Test]
        public void Catalogue_LengthsArePositive()
        {
            foreach (GhostMove move in GhostMoves.All)
                Assert.Greater(move.Length, 0f, move.Name);
        }

        [Test]
        public void Catalogue_NoNaNOrInfinityAndRangesHold()
        {
            foreach (GhostMove move in GhostMoves.All)
            {
                foreach (float side in new[] { 1f, -1f })
                {
                    for (int i = 0; i <= 200; i++)
                    {
                        GhostPose p = move.At(i / 200f, side);
                        string where = move.Name + " side " + side + " i " + i;
                        foreach (float f in Fields(p))
                            Assert.IsTrue(!float.IsNaN(f) && !float.IsInfinity(f), where);
                        // Hide sinks deepest: into the seat, only the hat showing
                        Assert.GreaterOrEqual(p.Lift, -1.1f - 1e-4f, where);
                        Assert.GreaterOrEqual(p.Fade, 0f, where);
                        Assert.LessOrEqual(p.Fade, 1f, where);
                        Assert.GreaterOrEqual(p.HatFade, 0f, where);
                        Assert.LessOrEqual(p.HatFade, 1f, where);
                        Assert.Greater(p.Tall, 0f, where);
                        Assert.Greater(p.Wide, 0f, where);
                    }
                }
            }
        }

        [Test]
        public void Catalogue_MovesStartAndEndAtRest_EntrancesStartHidden()
        {
            foreach (GhostMove move in GhostMoves.All)
            {
                foreach (float side in new[] { 1f, -1f })
                {
                    Assert.IsTrue(move.At(1f, side).IsRest(), move.Name + " end");
                    if (move.Entrance)
                    {
                        Assert.AreEqual(1f, move.At(0f, side).Fade, 1e-6f, move.Name + " start fade");
                    }
                    else
                    {
                        Assert.IsTrue(move.At(0f, side).IsRest(), move.Name + " start");
                    }
                }
            }

            Assert.AreEqual(3, GhostMoves.All.Count(m => m.Entrance), "Wisp, Drop In and Ta Da");
        }

        [Test]
        public void Catalogue_LengthsAreOneAndAHalfToTwoAndAHalfSeconds()
        {
            foreach (GhostMove move in GhostMoves.All)
            {
                Assert.GreaterOrEqual(move.Length, 1.5f - 1e-4f, move.Name);
                Assert.LessOrEqual(move.Length, 2.5f + 1e-4f, move.Name);
                Assert.AreEqual(Math.Round(move.Length, 1), move.Length, 1e-4f, move.Name + " in tenths");
            }
            // The countdown's second RevBounce waits for a longer first one to end (PlayerEmotes.TickLobby)
            Assert.AreEqual(1.8f, GhostMoves.All.First(m => m.Name == "Bounce").Length, 1e-6f, "Bounce: two bounces and the Go lunge");
        }

        // Each field: its name, whether it is an angle (compared the short way round), the most it may move between
        // two of 400 samples, and whether it belongs to the hat (hidden by HatFade) rather than the body (Fade)
        static readonly (string name, bool angle, float most, bool hat)[] fieldInfo =
        {
            ("Lift", false, 0.6f, false), ("Push", false, 0.6f, false), ("Shift", false, 0.6f, false),
            ("Pitch", true, 35f, false), ("Yaw", true, 35f, false), ("Roll", true, 35f, false),
            ("Tall", false, 0.2f, false), ("Wide", false, 0.2f, false), ("HeadYaw", true, 35f, false),
            ("HeadPitch", true, 35f, false), ("HeadRoll", true, 35f, false), ("HeadPop", false, 0.6f, false),
            ("TailUp", true, 35f, false), ("TailWag", true, 35f, false), ("HatLift", false, 0.6f, true),
            ("HatSpin", true, 35f, true), ("HatTip", true, 35f, true), ("Fade", false, 0.35f, false),
            ("HatFade", false, 0.35f, true), ("HatShift", false, 0.6f, true)
        };

        /// <summary>
        /// No pops: between neighbouring samples no field jumps further than a sane step. The only jumps allowed are
        /// the documented hidden ones: a part may jump while it is gone (Fade, or HatFade for the hat, at 0.99 or more
        /// in either sample)
        /// </summary>
        [Test]
        public void Catalogue_EveryMoveIsContinuous_ButWhileHidden()
        {
            const int STEPS = 400;
            Assert.AreEqual(fieldInfo.Length, Fields(GhostPose.Rest).Length);
            foreach (GhostMove move in GhostMoves.All)
            {
                foreach (float side in new[] { 1f, -1f })
                {
                    GhostPose before = move.At(0f, side);
                    for (int i = 1; i <= STEPS; i++)
                    {
                        GhostPose now = move.At(i / (float)STEPS, side);
                        bool bodyGone = before.Fade >= 0.99f || now.Fade >= 0.99f;
                        bool hatGone = bodyGone || before.HatFade >= 0.99f || now.HatFade >= 0.99f;
                        float[] a = Fields(before), b = Fields(now);
                        for (int f = 0; f < a.Length; f++)
                        {
                            if (fieldInfo[f].hat ? hatGone : bodyGone)
                                continue;
                            float step = fieldInfo[f].angle ? Mathf.Abs(Mathf.DeltaAngle(a[f], b[f])) : Mathf.Abs(b[f] - a[f]);
                            Assert.LessOrEqual(step, fieldInfo[f].most, move.Name + " side " + side + ": " + fieldInfo[f].name
                                + " jumps " + a[f] + " to " + b[f] + " at step " + i);
                        }
                        before = now;
                    }
                }
            }
        }

        /// <summary>
        /// A move that turns to a side mirrors exactly on the other: the sideways fields (Yaw, Roll, HeadYaw, HeadRoll,
        /// Shift, TailWag) flip and the rest stay. Every other move ignores the side
        /// </summary>
        [Test]
        public void Catalogue_SideMovesMirror_OthersIgnoreTheSide()
        {
            string[] sided =
            {
                "Turn And Cackle", "Check Behind", "Look Back", "Double Take", "Parcel Hug", "Shrug", "Turn And Wave",
                "Sly Rub", "Thumbs In Ears", "Ear Waggle"
            };
            int[] flips = { 4, 5, 8, 10, 2, 13, 19 };   // Yaw, Roll, HeadYaw, HeadRoll, Shift, TailWag, HatShift
            List<string> found = new List<string>();
            foreach (GhostMove move in GhostMoves.All)
            {
                bool usesSide = false;
                for (int i = 0; i <= 50 && !usesSide; i++)
                {
                    float[] a = Fields(move.At(i / 50f, 1f)), b = Fields(move.At(i / 50f, -1f));
                    for (int f = 0; f < a.Length; f++)
                        if (Mathf.Abs(a[f] - b[f]) > 1e-5f)
                            usesSide = true;
                }
                if (usesSide)
                    found.Add(move.Name);
                for (int i = 0; i <= 50; i++)
                {
                    float[] a = Fields(move.At(i / 50f, 1f)), b = Fields(move.At(i / 50f, -1f));
                    for (int f = 0; f < a.Length; f++)
                    {
                        float want = usesSide && flips.Contains(f) ? -a[f] : a[f];
                        Assert.AreEqual(want, b[f], 1e-4f, move.Name + " " + fieldInfo[f].name + " at " + (i / 50f));
                    }
                }
            }
            CollectionAssert.AreEquivalent(sided, found, "the moves that turn to a side");
            foreach (string name in new[] { "Shrug", "Turn And Wave", "Sly Rub", "Thumbs In Ears", "Ear Waggle" })
            {
                GhostMove move = GhostMoves.All.First(m => m.Name == name);
                Assert.AreNotEqual(0f, move.At(0.5f, 1f).HeadYaw, name + " turns its head");
                Assert.AreNotEqual(0f, move.At(0.5f, 1f).Yaw, name + " turns its body");
            }
        }

        /// <summary>The turn round to the camera: the head starts round before the body, and starts back first too</summary>
        [Test]
        public void Round_TheHeadLeads()
        {
            GhostMove move = GhostMoves.All.First(m => m.Name == "Turn And Wave");
            GhostPose early = move.At(0.06f, 1f);
            Assert.Greater(early.HeadYaw / 35f, early.Yaw / 140f + 0.05f, "the head is further round early on");
            GhostPose late = move.At(0.9f, 1f);
            Assert.Less(late.HeadYaw / 35f, late.Yaw / 140f - 0.02f, "the head is further home on the way back");
            // Body three-quarters to the camera (so the hands' left and right read as its sides), face round to it
            Assert.AreEqual(140f, move.At(0.5f, 1f).Yaw, 1e-3f, "both all the way round in the middle");
            Assert.AreEqual(35f, move.At(0.5f, 1f).HeadYaw, 1e-3f);
        }

        [Test]
        public void PoseLerp_FieldByField_AnglesTheShortWay()
        {
            GhostPose a = GhostPose.Rest, b = GhostPose.Rest;
            a.Lift = 1f; b.Lift = 3f;
            b.Fade = 1f;
            a.HatSpin = 350f;
            a.Tall = 0.8f;
            GhostPose half = GhostPose.Lerp(a, b, 0.5f);
            Assert.AreEqual(2f, half.Lift, 1e-5f);
            Assert.AreEqual(0.5f, half.Fade, 1e-5f);
            Assert.AreEqual(0.9f, half.Tall, 1e-5f);
            Assert.AreEqual(0f, Mathf.DeltaAngle(355f, half.HatSpin), 1e-3f, "350 to 0 goes by 355");
            Assert.AreEqual(b.Lift, GhostPose.Lerp(a, b, 1f).Lift, 1e-6f);
            Assert.AreEqual(a.Lift, GhostPose.Lerp(a, b, -1f).Lift, 1e-6f);
        }
    }
}

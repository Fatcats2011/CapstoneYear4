using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// A ghost's emotes outside the match: in player select (Materialise as it joins, WarmUp or KnuckleCrack as it
    /// readies, HatShowOff or MirrorCheck for a new look, Impatient while it waits), at the 3, 2, 1 (RevBounce twice) and
    /// on the results' podium (PodiumEmotes, with PodiumPerformer's acts waiting while an emote plays). Players are made
    /// as PlayerEmotesTests makes them; the asked-for emote is checked
    /// </summary>
    public class LobbyEmotesTests
    {
        const string VIEW = "Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab";

        readonly TestObjects objects = new TestObjects();
        static bool lobbyOn;
        static bool matchOn;
        static readonly bool?[] ready = new bool?[Constants.MAX_PLAYERS];

        static bool Lobby() { return lobbyOn; }
        static bool Match() { return matchOn; }
        static bool? Ready(int seat) { return ready[seat]; }

        [SetUp]
        public void SetUp()
        {
            lobbyOn = true;
            matchOn = false;
            for (int i = 0; i < ready.Length; i++)
                ready[i] = null;
            PlayerEmotes.MatchOn = Match;
            PlayerEmotes.LobbyOn = Lobby;
            PlayerEmotes.SeatReady = Ready;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (PlayerEmotes player in Object.FindObjectsOfType<PlayerEmotes>(true))
                player.OnDisable();
            PlayerEmotes.MatchOn = null;
            PlayerEmotes.LobbyOn = null;
            PlayerEmotes.SeatReady = null;
            PodiumPerformer.StopAll();
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        // A player in the next free seat (as PlayerEmotesTests), with their emotes on
        PlayerEmotes NewPlayer(string name)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            handler.ball.grounded = true;
            PlayerEmotes emotes = PlayerEmotes.Attach(player);
            emotes.OnEnable(); // Edit Mode: nothing switches it on
            emotes.Random = new System.Random(5);
            return emotes;
        }

        // A bare ghost whose emotes can play: an Animator over the bones GhostRig looks for
        GhostEmotes NewGhost()
        {
            GameObject model = objects.NewGameObject("Ghost");
            Animator animator = model.AddComponent<Animator>();
            Transform armature = new GameObject("Armature.001").transform;
            armature.SetParent(model.transform);
            Transform torso = new GameObject("Torso").transform;
            torso.SetParent(armature);
            new GameObject("Head").transform.SetParent(torso);
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(3));
            Assert.IsNotNull(emotes.Rig, "the bare ghost has a rig");
            return emotes;
        }

        static int Picked(PlayerEmotes player, GhostAction a, GhostAction b)
        {
            return player.AskedFor(a) + player.AskedFor(b);
        }

        // ---- PlayerEmoteRules

        [Test]
        public void Pick_EitherAtFirst_ThenNeverTheSameTwiceRunning()
        {
            System.Random random = new System.Random(1);
            bool sawA = false, sawB = false;
            for (int i = 0; i < 40; i++)
            {
                GhostAction first = PlayerEmoteRules.Pick(GhostAction.Victory, GhostAction.CrownPolish, null, random);
                sawA |= first == GhostAction.Victory;
                sawB |= first == GhostAction.CrownPolish;
            }
            Assert.IsTrue(sawA && sawB, "either one first");

            GhostAction? last = null;
            for (int i = 0; i < 20; i++)
            {
                GhostAction next = PlayerEmoteRules.Pick(GhostAction.Victory, GhostAction.CrownPolish, last, random);
                Assert.AreNotEqual(last, next, "pick " + i);
                last = next;
            }
            Assert.AreEqual(GhostAction.CrownPolish, PlayerEmoteRules.Pick(GhostAction.Victory, GhostAction.CrownPolish, GhostAction.Victory, null));
        }

        [Test]
        public void ReadiedAndRestyled_AreTheirPairs()
        {
            System.Random random = new System.Random(2);
            for (int i = 0; i < 10; i++)
            {
                GhostAction ready = PlayerEmoteRules.Readied(null, random);
                Assert.IsTrue(ready == GhostAction.WarmUp || ready == GhostAction.KnuckleCrack, ready.ToString());
                GhostAction look = PlayerEmoteRules.Restyled(null, random);
                Assert.IsTrue(look == GhostAction.HatShowOff || look == GhostAction.MirrorCheck, look.ToString());
            }
        }

        [Test]
        public void Restyled_WithNoHatShowing_IsAlwaysMirrorCheck()
        {
            System.Random random = new System.Random(4);
            foreach (GhostAction? last in new GhostAction?[] { null, GhostAction.HatShowOff, GhostAction.MirrorCheck })
            {
                for (int i = 0; i < 6; i++)
                    Assert.AreEqual(GhostAction.MirrorCheck, PlayerEmoteRules.Restyled(last, random, false), "after " + last);
            }
            Assert.AreEqual(GhostAction.HatShowOff, PlayerEmoteRules.Restyled(GhostAction.MirrorCheck, random, true),
                "a hat on: they take turns");
        }

        [Test]
        public void NewLook_NotTheFirstLook_NorNoChange()
        {
            Assert.IsFalse(PlayerEmoteRules.NewLook(-1, 0), "the first look, as they appear");
            Assert.IsFalse(PlayerEmoteRules.NewLook(-1, 3), "the first look, as they appear");
            Assert.IsFalse(PlayerEmoteRules.NewLook(2, 2), "no change");
            Assert.IsTrue(PlayerEmoteRules.NewLook(2, 3));
            Assert.IsTrue(PlayerEmoteRules.NewLook(0, 5));
        }

        [Test]
        public void ImpatientDue_AtFive_ThenEveryEight()
        {
            Assert.IsFalse(PlayerEmoteRules.ImpatientDue(0f, 4.9f));
            Assert.IsTrue(PlayerEmoteRules.ImpatientDue(4.9f, 5f));
            Assert.IsFalse(PlayerEmoteRules.ImpatientDue(5f, 12.9f));
            Assert.IsTrue(PlayerEmoteRules.ImpatientDue(12.9f, 13f));
            Assert.IsTrue(PlayerEmoteRules.ImpatientDue(20.9f, 21.1f));
            Assert.IsFalse(PlayerEmoteRules.ImpatientDue(0f, 0f));
        }

        [Test]
        public void ForPodium_WinnerAndLastOnly()
        {
            Assert.IsTrue(PlayerEmoteRules.ForPodium(PodiumMood.Winner, out GhostAction a, out GhostAction b));
            Assert.AreEqual(GhostAction.Victory, a);
            Assert.AreEqual(GhostAction.CrownPolish, b);
            Assert.IsTrue(PlayerEmoteRules.ForPodium(PodiumMood.Last, out a, out b));
            Assert.AreEqual(GhostAction.Tantrum, a);
            Assert.AreEqual(GhostAction.FadeAway, b);
            Assert.IsFalse(PlayerEmoteRules.ForPodium(PodiumMood.Runner, out a, out b), "runners have their acts");

            Assert.AreEqual(PlayerEmoteRules.PODIUM_MIN, PlayerEmoteRules.PodiumGap(0.0), 1e-4f);
            Assert.AreEqual(PlayerEmoteRules.PODIUM_MAX, PlayerEmoteRules.PodiumGap(1.0), 1e-4f);
            Assert.AreEqual(4f, PlayerEmoteRules.PODIUM_MIN);
            Assert.AreEqual(6f, PlayerEmoteRules.PODIUM_MAX);
        }

        // ---- Joining

        [Test]
        public void Joined_MaterialisesAFrameLater_InPlayerSelect()
        {
            PlayerEmotes player = NewPlayer("P1");
            player.Joined();
            Assert.AreEqual(0, player.AskCount, "not as it joins: its ghost isn't in place yet");
            player.Tick(0.016f);
            Assert.AreEqual(0, player.AskCount, "one frame on");
            player.Tick(0.016f);
            Assert.AreEqual(GhostAction.Materialise, player.Asked);
            for (int i = 0; i < 10; i++)
                player.Tick(0.016f);
            Assert.AreEqual(1, player.AskedFor(GhostAction.Materialise), "once");
        }

        [Test]
        public void Joined_OnTheMainMenu_MaterialisesAsItReachesPlayerSelect_NeverInAMatch()
        {
            PlayerEmotes player = NewPlayer("P1");
            lobbyOn = false;
            player.Joined();
            for (int i = 0; i < 10; i++)
                player.Tick(0.1f);
            Assert.AreEqual(0, player.AskCount, "the main menu");
            lobbyOn = true;
            player.Tick(0.1f);
            Assert.AreEqual(1, player.AskedFor(GhostAction.Materialise), "player select");

            PlayerEmotes late = NewPlayer("P2");
            lobbyOn = false;
            late.Joined();
            matchOn = true;
            late.Tick(0.1f);
            late.Tick(0.1f);
            matchOn = false;
            lobbyOn = true;
            late.Tick(0.1f);
            late.Tick(0.1f);
            Assert.AreEqual(0, late.AskedFor(GhostAction.Materialise), "a join a match went by is forgotten");
        }

        // ---- Readying up

        [Test]
        public void SetRemoteReady_OnlyAChangeToReady_WarmsUp()
        {
            PlayerInstantiate players = PlayerInstantiate.Instance;
            PlayerEmotes first = NewPlayer("P1");
            PlayerEmotes second = NewPlayer("P2");

            players.SetRemoteReady(1, true);
            Assert.AreEqual(1, Picked(second, GhostAction.WarmUp, GhostAction.KnuckleCrack));
            Assert.AreEqual(0, first.AskCount, "only their ghost");

            players.SetRemoteReady(1, true);
            Assert.AreEqual(1, Picked(second, GhostAction.WarmUp, GhostAction.KnuckleCrack), "still ready (sent every frame) is no change");

            players.SetRemoteReady(1, false);
            Assert.AreEqual(1, Picked(second, GhostAction.WarmUp, GhostAction.KnuckleCrack), "unready");

            GhostAction before = second.Asked.Value;
            players.SetRemoteReady(1, true);
            Assert.AreEqual(2, Picked(second, GhostAction.WarmUp, GhostAction.KnuckleCrack), "ready again");
            Assert.AreNotEqual(before, second.Asked.Value, "the other one of the pair this time");
            players.SetRemoteReady(1, false);
        }

        [Test]
        public void ReadyUp_WarmsUpOnce_PressingAgainIsNoChange()
        {
            PlayerInstantiate players = PlayerInstantiate.Instance;
            PlayerEmotes first = NewPlayer("P1");
            NewPlayer("P2"); // not ready: no countdown starts
            PlayerUIHandler ui = first.gameObject.AddComponent<PlayerUIHandler>();
            ui.customizationSelector = first.gameObject.AddComponent<CustomizationSelector>();

            players.ReadyUp(0);
            Assert.AreEqual(1, Picked(first, GhostAction.WarmUp, GhostAction.KnuckleCrack));
            players.ReadyUp(0);
            Assert.AreEqual(1, Picked(first, GhostAction.WarmUp, GhostAction.KnuckleCrack), "already ready");
            players.UnreadyUp(0);
        }

        [Test]
        public void Readied_AndRestyled_OnlyInPlayerSelect()
        {
            PlayerEmotes player = NewPlayer("P1");
            lobbyOn = false;
            player.Readied();
            player.Restyled();
            Assert.AreEqual(0, player.AskCount);
            lobbyOn = true;
            player.Restyled();
            Assert.AreEqual(1, Picked(player, GhostAction.HatShowOff, GhostAction.MirrorCheck));
            GhostAction before = player.Asked.Value;
            player.Restyled();
            Assert.AreNotEqual(before, player.Asked.Value, "they take turns");
        }

        // ---- A new hat or colour

        [Test]
        public void ANewHat_ShowsItOff_TheFirstLookDoesnt()
        {
            GameObject view = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VIEW));
            objects.Keep(view);
            PlayerInstantiate.Instance.Roster.JoinLocal(view.GetComponent<PlayerInput>());
            PlayerEmotes player = PlayerEmotes.Attach(view);
            Assert.IsNotNull(player);
            CustomizationSelector selector = view.GetComponentInChildren<CustomizationSelector>(true);

            selector.UpdateHat(); // the first look (Start) is no new look
            Assert.AreEqual(0, player.AskCount);

            SerializedObject picking = new SerializedObject(selector);
            picking.FindProperty("customizationChanging").enumValueIndex = 1; // the hat slider
            picking.ApplyModifiedPropertiesWithoutUndo();
            selector.MarkLook(); // the pop-up opens
            selector.SetCustomizationValue(true);
            Assert.AreEqual(0, player.AskCount, "a scroll alone shows nothing off (playtest, 2026-11-25)");
            Assert.IsTrue(selector.TakeChanged(), "DONE: the hat changed while it was open");
            selector.ShowOff();
            Assert.AreEqual(1, Picked(player, GhostAction.HatShowOff, GhostAction.MirrorCheck));

            selector.SetDisableOptionsCustomization(true); // ready: the sliders are locked
            selector.SetCustomizationValue(true);
            Assert.IsFalse(selector.TakeChanged(), "nothing changed");
        }

        [Test]
        public void ARestyleThatLeavesNoHat_ChecksTheMirror()
        {
            GameObject view = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(VIEW));
            objects.Keep(view);
            PlayerInstantiate.Instance.Roster.JoinLocal(view.GetComponent<PlayerInput>());
            PlayerEmotes player = PlayerEmotes.Attach(view);
            Assert.IsNotNull(player);
            Assert.IsNotNull(player.Emotes, "the view's ghost has emotes");
            CustomizationSelector selector = view.GetComponentInChildren<CustomizationSelector>(true);
            selector.UpdateHat();

            SerializedObject picking = new SerializedObject(selector);
            picking.FindProperty("customizationChanging").enumValueIndex = 1; // the hat slider
            picking.ApplyModifiedPropertiesWithoutUndo();
            int hatless = 0, hatted = 0;
            for (int i = 0; i < selector.Hats.Count + 1; i++)
            {
                player.Emotes.Stop();
                selector.SetCustomizationValue(true);
                selector.ShowOff(); // as DONE would
                if (player.Emotes.ShowsHat)
                {
                    hatted++;
                    continue;
                }
                hatless++;
                Assert.AreEqual(GhostAction.MirrorCheck, player.Asked, "no hat to show off");
            }
            Assert.Greater(hatless, 0, "went past the NoHat look");
            Assert.Greater(hatted, 0, "and past a hat");
        }

        // ---- Waiting

        [Test]
        public void Impatient_ReadyWhileSomeoneIsnt_AfterFive_ThenEveryEight()
        {
            PlayerEmotes player = NewPlayer("P1");
            NewPlayer("P2");
            ready[0] = true;
            ready[1] = false;

            for (int i = 0; i < 49; i++)
                player.Tick(0.1f);
            Assert.AreEqual(0, player.AskedFor(GhostAction.Impatient), "4.9 s");
            player.Tick(0.1f);
            player.Tick(0.05f);
            Assert.AreEqual(1, player.AskedFor(GhostAction.Impatient), "5 s");
            for (int i = 0; i < 79; i++)
                player.Tick(0.1f);
            Assert.AreEqual(1, player.AskedFor(GhostAction.Impatient), "12.9 s");
            player.Tick(0.2f);
            Assert.AreEqual(2, player.AskedFor(GhostAction.Impatient), "13 s");

            ready[1] = true; // everyone's ready
            for (int i = 0; i < 200; i++)
                player.Tick(0.1f);
            Assert.AreEqual(2, player.AskedFor(GhostAction.Impatient), "nobody to wait for");

            ready[1] = false;
            ready[0] = false; // not ready itself
            for (int i = 0; i < 200; i++)
                player.Tick(0.1f);
            Assert.AreEqual(2, player.AskedFor(GhostAction.Impatient), "not ready itself");

            ready[0] = true;
            lobbyOn = false; // out of player select
            for (int i = 0; i < 200; i++)
                player.Tick(0.1f);
            Assert.AreEqual(2, player.AskedFor(GhostAction.Impatient), "only in player select");
        }

        [Test]
        public void Impatient_AloneOrWithEmptySeats_Never()
        {
            PlayerEmotes player = NewPlayer("P1");
            ready[0] = true; // the rest are empty seats (or CPU drivers)
            for (int i = 0; i < 300; i++)
                player.Tick(0.1f);
            Assert.AreEqual(0, player.AskedFor(GhostAction.Impatient));
        }

        // ---- The countdown

        [Test]
        public void Countdown_EveryGhostRevs_AndAgainOneAndAHalfSecondsLater()
        {
            lobbyOn = false;
            matchOn = true;
            PlayerEmotes first = NewPlayer("P1");
            PlayerEmotes second = NewPlayer("P2");

            PlayerEmotes.Countdown();
            Assert.AreEqual(1, first.AskedFor(GhostAction.RevBounce));
            Assert.AreEqual(1, second.AskedFor(GhostAction.RevBounce));
            for (int i = 0; i < 14; i++)
            {
                first.Tick(0.1f);
                second.Tick(0.1f);
            }
            Assert.AreEqual(1, first.AskedFor(GhostAction.RevBounce), "1.4 s");
            first.Tick(0.15f);
            second.Tick(0.15f);
            Assert.AreEqual(2, first.AskedFor(GhostAction.RevBounce), "1.5 s");
            Assert.AreEqual(2, second.AskedFor(GhostAction.RevBounce));
            for (int i = 0; i < 50; i++)
                first.Tick(0.1f);
            Assert.AreEqual(2, first.AskedFor(GhostAction.RevBounce), "twice only");
        }

        // ---- The results

        [Test]
        public void PodiumEmotes_FirstAfterOneSecond_ThenEveryFourToSix_TakingTurns()
        {
            GhostEmotes ghost = NewGhost();
            PodiumEmotes winner = PodiumEmotes.For(PodiumMood.Winner, ghost, new System.Random(9));
            Assert.IsNull(PodiumEmotes.For(PodiumMood.Runner, ghost), "runners have only their acts");
            Assert.IsNull(PodiumEmotes.For(PodiumMood.Winner, null), "no emotes on the ghost");

            winner.Tick(0.9f, false);
            Assert.AreEqual(0, winner.AskCount, "0.9 s");
            winner.Tick(0.11f, false);
            Assert.AreEqual(1, winner.AskCount, "1 s");
            Assert.IsTrue(winner.Asked == GhostAction.Victory || winner.Asked == GhostAction.CrownPolish);

            float since = 0f;
            GhostAction? last = winner.Asked;
            float shortest = float.MaxValue, longest = 0f;
            for (int step = 0; step < 6000; step++)
            {
                winner.Tick(0.01f, false);
                ghost.Step(0.01f);
                since += 0.01f;
                if (winner.AskCount > 1 && winner.Asked != last || since > 7f)
                {
                    Assert.AreNotEqual(last, winner.Asked, "never the same twice running");
                    shortest = Mathf.Min(shortest, since);
                    longest = Mathf.Max(longest, since);
                    last = winner.Asked;
                    since = 0f;
                }
            }
            Assert.Greater(winner.AskCount, 8);
            Assert.GreaterOrEqual(shortest, PlayerEmoteRules.PODIUM_MIN - 0.02f);
            Assert.LessOrEqual(longest, PlayerEmoteRules.PODIUM_MAX + 0.02f);

            PodiumEmotes last4 = PodiumEmotes.For(PodiumMood.Last, NewGhost(), new System.Random(4));
            last4.Tick(1f, false);
            Assert.IsTrue(last4.Asked == GhostAction.Tantrum || last4.Asked == GhostAction.FadeAway);
        }

        [Test]
        public void PodiumEmotes_WaitForTheActPlaying()
        {
            PodiumEmotes winner = PodiumEmotes.For(PodiumMood.Winner, NewGhost(), new System.Random(9));
            winner.Tick(2f, true);
            Assert.AreEqual(0, winner.AskCount, "an act is playing");
            winner.Tick(0.01f, false);
            Assert.AreEqual(1, winner.AskCount, "as it ends");
        }

        [Test]
        public void APerformer_StartsNoActWhileAnEmotePlays_AndStopsItsEmotes()
        {
            GhostEmotes ghost = NewGhost();
            Animator animator = ghost.GetComponent<Animator>();
            Transform model = objects.NewGameObject("Model").transform;

            // Fourth of four: the last place, Tantrum or FadeAway
            PodiumPerformer performer = PodiumPerformer.On(model, animator, 3, 4, null, new System.Random(7));
            Assert.IsNotNull(performer.Emoting, "the last place emotes");
            Assert.IsNull(PodiumPerformer.On(objects.NewGameObject("Runner").transform, animator, 1, 4, null, new System.Random(7)).Emoting,
                "a runner doesn't");
            PodiumPerformer.StopAll();
            performer = PodiumPerformer.On(model, animator, 3, 4, null, new System.Random(7));

            // An emote on the ghost: however long the rest runs out, no act starts until it's over
            Assert.IsTrue(ghost.Play(GhostAction.Bored));
            for (int i = 0; i < 20; i++)
            {
                performer.Tick(0.1f);
                Assert.IsNull(performer.Playing, "no act while the ghost emotes (" + i + ")");
            }
            Assert.IsNotNull(ghost.Playing);
            Assert.IsTrue(ghost.Playing.Action == GhostAction.Tantrum || ghost.Playing.Action == GhostAction.FadeAway,
                "its own emote came at 1 s and took over (Bored is lower)");

            for (int i = 0; i < 200 && ghost.Playing != null; i++)
            {
                performer.Tick(0.05f);
                ghost.Step(0.05f);
                if (ghost.Playing != null)
                    Assert.IsNull(performer.Playing, "still emoting");
            }
            Assert.IsNull(ghost.Playing, "the emote ended");
            performer.Tick(0.01f);
            Assert.IsNotNull(performer.Playing, "the act waiting starts");

            // An act going on finishes, whatever starts on the ghost
            ghost.Play(GhostAction.FadeAway);
            performer.Tick(0.01f);
            Assert.IsNotNull(performer.Playing, "an act in progress goes on");

            // The results end: its emote stops with it
            PodiumPerformer.StopAll();
            Assert.IsNull(ghost.Playing, "its emote stopped with the results");
        }
    }
}

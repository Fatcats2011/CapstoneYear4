using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// The boost ring under a scooter: it shows the horn gauge's fill, counts how long the boost has sat ready (a spent
    /// boost starts again), offers the idle hint once, keeps the button prompt from bots and gives it its owner's world
    /// layer; a slowed recharge follows the package; another machine's scooter hides it; and the editor step that builds
    /// it into the player prefab can run twice
    /// </summary>
    public class BoostRingTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<Material> materials = new List<Material>();
        int savedSeen;
        GameState state;

        [SetUp]
        public void SetUp()
        {
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
            savedSeen = Hints.Seen;
            Hints.Seen = 0;
            state = GameState.Begin;
            Hints.State = () => state; // a match, whatever game manager another test left behind
        }

        [TearDown]
        public void TearDown()
        {
            Hints.Seen = savedSeen;
            Hints.State = null;
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
            foreach (Material material in materials)
                Object.DestroyImmediate(material);
            materials.Clear();
        }

        // A seated player's scooter with a ring under it, as the setup builds one: Control (BallDriving) > Boost Ring >
        // Ring (a renderer) and Prompt. Time passes a second a call unless a test says otherwise
        BoostRing NewRing(out GameObject player)
        {
            player = objects.NewGameObject("Player 1");
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            BallDriving driving = control.AddComponent<BallDriving>();

            GameObject holder = new GameObject("Boost Ring");
            holder.transform.SetParent(control.transform);
            BoostRing ring = holder.AddComponent<BoostRing>();

            GameObject quad = new GameObject("Ring");
            quad.transform.SetParent(holder.transform);
            Material material = new Material(Shader.Find("Unlit/Color"));
            materials.Add(material);
            quad.AddComponent<MeshRenderer>().sharedMaterial = material;

            GameObject prompt = new GameObject("Prompt");
            prompt.transform.SetParent(holder.transform);
            new GameObject("Button").transform.SetParent(prompt.transform);
            prompt.SetActive(false);

            ring.ring = quad.GetComponent<MeshRenderer>();
            ring.prompt = prompt;
            ring.control = driving;
            ring.DeltaTime = () => 1f;
            return ring;
        }

        [Test]
        public void OutsideAMatch_NoRingAndNoPrompt()
        {
            // In the lobby its button prompt sat inside the scooter on Dispatch's stand (playtest, 2026-11-20)
            BoostRing ring = NewRing(out GameObject _);
            state = GameState.PlayerSelect;
            ring.Show(1f, false);
            ring.Show(1f, false);
            Assert.IsFalse(ring.ring.enabled, "no ring in the lobby");
            Assert.IsFalse(ring.prompt.activeSelf, "no prompt in the lobby");

            state = GameState.Begin;
            ring.Show(1f, false);
            Assert.IsTrue(ring.ring.enabled, "back in a match");

            // On the results' podium its prompt floated inside a scooter (playtest, 2026-11-25)
            state = GameState.Results;
            ring.Show(1f, false);
            Assert.IsFalse(ring.ring.enabled, "no ring on the podium");
            Assert.IsFalse(ring.prompt.activeSelf, "no prompt on the podium");
        }

        static float Shown(BoostRing ring, string property)
        {
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            ring.ring.GetPropertyBlock(block);
            return block.GetFloat(property);
        }

        [Test]
        public void Show_SetsFillOnMaterial()
        {
            BoostRing ring = NewRing(out _);

            ring.Show(0.4f, false);
            Assert.AreEqual(0.4f, Shown(ring, "_Fill"), 0.0001f);
            Assert.AreEqual(0f, Shown(ring, "_Dashed"), "a normal recharge is solid");

            ring.Show(0.6f, true);
            Assert.AreEqual(0.6f, Shown(ring, "_Fill"), 0.0001f);
            Assert.AreEqual(1f, Shown(ring, "_Dashed"), "carrying a package: dashed");

            ring.Show(1f, true);
            Assert.AreEqual(0f, Shown(ring, "_Dashed"), "a full ring is solid, package or not");
        }

        [Test]
        public void Show_CountsTimeFull_AndResetsOnBurst()
        {
            BoostRing ring = NewRing(out _);
            ring.DeltaTime = () => 0.5f;

            ring.Show(0.9f, false);
            Assert.AreEqual(0f, ring.TimeFull, "not full yet");
            ring.Show(1f, false);
            ring.Show(1f, false);
            ring.Show(1f, false);
            Assert.AreEqual(1.5f, ring.TimeFull, 0.0001f);

            ring.Burst();
            Assert.AreEqual(0f, ring.TimeFull, "spent");
            ring.DeltaTime = () => 0f;
            ring.Show(0f, false);
            Assert.AreEqual(1f, Shown(ring, "_BurstAlpha"), 0.0001f, "the burst starts solid");
            Assert.AreEqual(1f, Shown(ring, "_BurstScale"), 0.0001f, "at the ring's own size");
        }

        [Test]
        public void Burst_FollowsTheBoostStarting()
        {
            BoostRing ring = NewRing(out _);
            ring.Show(1f, false);
            ring.Show(1f, false);
            Assert.Greater(ring.TimeFull, 0f);

            ring.control.OnBoostStart.Invoke();

            Assert.AreEqual(0f, ring.TimeFull);
        }

        [Test]
        public void IdleHint_OfferedOnceAfter15s()
        {
            BoostRing ring = NewRing(out _);
            List<(int seat, HintKind kind)> offers = new List<(int, HintKind)>();
            System.Action<int, HintKind> hear = (seat, kind) => offers.Add((seat, kind));
            Hints.Offered += hear;
            try
            {
                for (int second = 0; second < 14; second++)
                    ring.Show(1f, false);
                Assert.IsEmpty(offers, "not yet");

                for (int second = 0; second < 6; second++)
                    ring.Show(1f, false);
                Assert.AreEqual(1, offers.Count, "once a full spell");
                Assert.AreEqual((0, HintKind.BoostReady), offers[0], "to the ring's seat");

                ring.Burst();
                ring.Show(0.2f, false);
                for (int second = 0; second < 16; second++)
                    ring.Show(1f, false);
                Assert.AreEqual(2, offers.Count, "a new full spell offers again (Hints keeps it to once a machine)");
            }
            finally
            {
                Hints.Offered -= hear;
            }
        }

        [Test]
        public void EnteringAMatch_RestartsTimeFull_SoTheHintWaitsAgain()
        {
            BoostRing ring = NewRing(out _);
            List<HintKind> offers = new List<HintKind>();
            System.Action<int, HintKind> hear = (seat, kind) => offers.Add(kind);
            Hints.Offered += hear;
            try
            {
                state = GameState.Results; // full and unused on the results screen
                for (int second = 0; second < 20; second++)
                    ring.Show(1f, false);
                Assert.IsEmpty(offers, "no hint outside a match");
                Assert.AreEqual(20f, ring.TimeFull, 0.0001f);

                state = GameState.Begin; // the rematch
                ring.Show(1f, false);
                Assert.AreEqual(1f, ring.TimeFull, 0.0001f, "counting again from the match's start");
                for (int second = 0; second < 13; second++)
                    ring.Show(1f, false);
                Assert.IsEmpty(offers, "14 s into the match: not yet");
                ring.Show(1f, false);
                Assert.AreEqual(new[] { HintKind.BoostReady }, offers, "15 s in");
            }
            finally
            {
                Hints.Offered -= hear;
            }
        }

        [Test]
        public void PausingAMatch_KeepsTimeFull()
        {
            BoostRing ring = NewRing(out _);
            for (int second = 0; second < 5; second++)
                ring.Show(1f, false);

            state = GameState.Paused;
            ring.Show(1f, false);
            state = GameState.GoldenCutscene;
            ring.Show(1f, false);
            state = GameState.MainLoop;
            ring.Show(1f, false);

            Assert.AreEqual(8f, ring.TimeFull, 0.0001f);
        }

        [Test]
        public void Prompt_ShownWhenFull_ForAPlayer()
        {
            BoostRing ring = NewRing(out GameObject player);
            player.AddComponent<PlayerCameraResizer>().playerWorldLayer = 10;

            ring.Show(0.5f, false);
            Assert.IsFalse(ring.prompt.activeSelf, "charging");
            ring.Show(1f, false);
            Assert.IsTrue(ring.prompt.activeSelf, "full, offline: at once");
            ring.Burst();
            ring.Show(0f, false);
            Assert.IsFalse(ring.prompt.activeSelf, "spent");
        }

        [Test]
        public void Prompt_HiddenForBot()
        {
            BoostRing ring = NewRing(out GameObject player);
            player.AddComponent<PlayerCameraResizer>().playerWorldLayer = 10;
            ring.control.gameObject.AddComponent<BotDriver>();

            for (int second = 0; second < 5; second++)
                ring.Show(1f, false);

            Assert.IsFalse(ring.prompt.activeSelf);
        }

        [Test]
        public void Prompt_HiddenWithoutAView_RingStillShows()
        {
            BoostRing ring = NewRing(out _);

            for (int second = 0; second < 5; second++)
                ring.Show(1f, false);

            Assert.IsFalse(ring.prompt.activeSelf, "no player's camera to keep it to: every camera would see it");
            Assert.IsTrue(ring.ring.enabled, "the ring still shows");
            Assert.AreEqual(1f, Shown(ring, "_Fill"), 0.0001f);
        }

        [Test]
        public void Prompt_TakesTheOwnersWorldLayer_OnceTheirViewIsSet()
        {
            BoostRing ring = NewRing(out GameObject player);
            PlayerCameraResizer resizer = player.AddComponent<PlayerCameraResizer>();

            ring.Show(1f, false);
            Assert.IsFalse(ring.prompt.activeSelf, "no view yet: every camera would see it");

            resizer.playerWorldLayer = 11;
            ring.Show(1f, false);
            Assert.IsTrue(ring.prompt.activeSelf);
            Assert.AreEqual(11, ring.prompt.layer);
            Assert.AreEqual(11, ring.prompt.transform.GetChild(0).gameObject.layer, "and its button");
        }

        [Test]
        public void BallDriving_RechargeSlowed_FollowsPackage()
        {
            BallDriving driving = objects.Add<BallDriving>();

            driving.SetBoostModifier(true);
            Assert.IsTrue(driving.RechargeSlowed, "carrying");
            driving.SetBoostModifier(false);
            Assert.IsFalse(driving.RechargeSlowed, "empty-handed");

            // The tutorial's own recharge: slowed only when longer than the usual one (0 here: no Awake in edit mode)
            driving.HardCodeBoostModifier(5f);
            Assert.IsTrue(driving.RechargeSlowed);
            driving.HardCodeBoostModifier(0f);
            Assert.IsFalse(driving.RechargeSlowed);
        }

        [Test]
        public void RemoteAvatar_HidesRing()
        {
            BoostRing ring = NewRing(out GameObject player);
            GameObject ball = new GameObject("Ball");
            ball.transform.SetParent(player.transform);
            ball.AddComponent<Rigidbody>();
            ring.control.sphere = ball;
            ring.Show(1f, false);
            Assert.IsTrue(ring.ring.enabled);

            RemoteAvatar remote = player.AddComponent<RemoteAvatar>();
            Reflect.Invoke(remote, "TurnOffOwnerParts");

            Assert.IsFalse(ring.ring.enabled, "no ring");
            Assert.IsFalse(ring.prompt.activeSelf, "no prompt");
            ring.Show(1f, false);
            Assert.IsFalse(ring.ring.enabled, "and it stays hidden");
            Assert.IsFalse(ring.prompt.activeSelf);
        }

        [Test]
        public void Shader_Compiles_AndTheMaterialUsesIt()
        {
            Shader shader = Shader.Find("DoA/Boost Ring");
            Assert.IsNotNull(shader);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), "compiles here");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(BoostRingSetup.MATERIAL);
            Assert.AreSame(shader, material.shader);
            foreach (string property in new[] { "_Fill", "_Pulse", "_Dashed", "_Fade", "_ColorLow", "_ColorFull", "_BurstScale", "_BurstAlpha", "_RingRadius", "_Thickness",
                "_Track", "_TrackFloor", "_Backing", "_Outline", "_DepthBias" })
                Assert.IsTrue(material.HasProperty(property), property);
            Assert.Greater(material.GetFloat("_DepthBias"), 0.1f, "pulled past low clutter (playtest, 2026-11-25)");
            Assert.Less(material.GetFloat("_DepthBias"), 0.6f, "but no x-ray through walls");
        }

        [Test]
        public void PlayerPrefab_RingIsThickAndLifted_AndStaysInsideItsQuad()
        {
            // It got lost among sidewalk bricks and grass, and was too thin (playtest, 2026-11-25)
            GameObject root = PrefabUtility.LoadPrefabContents(BoostRingSetup.PREFAB);
            try
            {
                BoostRing ring = root.GetComponentInChildren<BoostRing>(true);
                Assert.IsNotNull(ring);
                Assert.GreaterOrEqual(ring.thickness, 0.2f, "about twice the old 0.12");
                Assert.GreaterOrEqual(ring.lift, 0.1f, "off the ground clutter");
                Assert.Less(BoostRing.OuterReach(ring.thickness), 0.5f, "the outline and the burst fit inside the quad");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void OuterReach_GrowsWithThickness_AndTheBurstIsTheWidest()
        {
            float ringRadius = 0.5f / BoostRing.QUAD_SPAN;
            Assert.Greater(BoostRing.OuterReach(0.4f), BoostRing.OuterReach(0.24f));
            Assert.AreEqual(ringRadius * 1.6f + ringRadius * 0.24f * 0.5f * 0.7f, BoostRing.OuterReach(0.24f), 1e-5f);
        }

        [Test]
        public void BoostRingSetup_IsIdempotent()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BoostRingSetup.PREFAB);
            try
            {
                BoostRingSetup.Build(root);
                BoostRingSetup.Build(root);

                BoostRing[] rings = root.GetComponentsInChildren<BoostRing>(true);
                Assert.AreEqual(1, rings.Length, "one ring");
                BoostRing ring = rings[0];
                PhaseIndicator horns = root.GetComponentInChildren<PhaseIndicator>(true);
                Assert.AreSame(ring, horns.ring, "the horns drive it");
                Assert.AreSame(horns.GetComponent<BallDriving>(), ring.control);
                Assert.AreEqual(1, ring.GetComponentsInChildren<MeshRenderer>(true).Length, "one ring renderer");
                Assert.AreEqual(BoostRingSetup.MATERIAL, AssetDatabase.GetAssetPath(ring.ring.sharedMaterial));
                Assert.IsNull(ring.ring.GetComponent<Collider>(), "nothing to bump into");
                Assert.IsNotNull(ring.prompt, "a prompt");
                Assert.IsFalse(ring.prompt.activeSelf, "hidden until full");
                Assert.AreEqual(1, ring.prompt.GetComponentsInChildren<ButtonGlyph>(true).Length, "one button, following the device");
                ButtonGlyph glyph = ring.prompt.GetComponentInChildren<ButtonGlyph>(true);
                Assert.IsNotNull(glyph.xbox, "A");
                Assert.IsNotNull(glyph.playStation, "Cross");
                Assert.IsNotNull(glyph.keyboard, "Space");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}

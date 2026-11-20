using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The ghost's rig (GhostRig) and its emote player (GhostEmotes) on a real ghost: a stage ghost built with
    /// TitleActor.Create from PlayerAvatar.prefab, in Edit Mode (its Animator doesn't run, so nothing rewrites the bones:
    /// the case where offsets would pile up)
    /// </summary>
    public class GhostRigTests
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        TitleActor actor;
        Animator animator;

        [SetUp]
        public void SetUp()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR);
            Assert.IsNotNull(prefab, "the avatar prefab");
            actor = TitleActor.Create(prefab, null, "Emote Test Ghost");
            Transform model = actor.Model.Find("Scooter/" + ScooterLook.MODEL + "/Player Model");
            Assert.IsNotNull(model, "the ghost's model");
            animator = model.GetComponent<Animator>();
            Assert.IsNotNull(animator, "the ghost's Animator");
        }

        [TearDown]
        public void TearDown()
        {
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }

        // Every transform under the model, as it stands (local)
        struct Snap
        {
            public Transform transform;
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        List<Snap> Take()
        {
            return animator.GetComponentsInChildren<Transform>(true).Select(t => new Snap
            {
                transform = t, position = t.localPosition, rotation = t.localRotation, scale = t.localScale
            }).ToList();
        }

        static void AssertSame(List<Snap> before, string when, float tolerance = 1e-4f)
        {
            foreach (Snap s in before)
            {
                Transform t = s.transform;
                Assert.Less((t.localPosition - s.position).magnitude, tolerance, t.name + " position " + when);
                Assert.Less((t.localScale - s.scale).magnitude, tolerance, t.name + " scale " + when);
                Quaternion r = t.localRotation;
                float same = Mathf.Max(Mathf.Abs(r.x - s.rotation.x), Mathf.Abs(r.y - s.rotation.y),
                    Mathf.Abs(r.z - s.rotation.z), Mathf.Abs(r.w - s.rotation.w));
                float flipped = Mathf.Max(Mathf.Abs(r.x + s.rotation.x), Mathf.Abs(r.y + s.rotation.y),
                    Mathf.Abs(r.z + s.rotation.z), Mathf.Abs(r.w + s.rotation.w));
                Assert.Less(Mathf.Min(same, flipped), tolerance, t.name + " rotation " + when);
            }
        }

        static GhostPose Busy()
        {
            GhostPose pose = GhostPose.Rest;
            pose.Lift = 0.2f; pose.Push = 0.1f; pose.Shift = -0.1f;
            pose.Pitch = 15f; pose.Yaw = 40f; pose.Roll = -10f;
            pose.Tall = 1.2f; pose.Wide = 0.9f;
            pose.HeadYaw = 30f; pose.HeadPitch = 10f; pose.HeadRoll = 12f; pose.HeadPop = 0.2f;
            pose.TailUp = 20f; pose.TailWag = 25f;
            pose.HatLift = 0.3f; pose.HatSpin = 90f; pose.HatTip = 30f;
            pose.Fade = 0.3f; pose.HatFade = 0.2f;
            return pose;
        }

        static GhostPose Only(System.Action<GhostPoseBox> set)
        {
            GhostPoseBox box = new GhostPoseBox { Pose = GhostPose.Rest };
            set(box);
            return box.Pose;
        }

        class GhostPoseBox
        {
            public GhostPose Pose;
        }

        Transform TailTip(GhostRig rig)
        {
            Transform tip = rig.TailIK != null ? rig.TailIK.Find("Tail3") : null;
            return tip != null ? tip : rig.TailIK;
        }

        [Test]
        public void Find_FindsEveryBone()
        {
            GhostRig rig = GhostRig.Find(animator);
            Assert.IsNotNull(rig);
            Assert.IsNotNull(rig.Torso, "Torso");
            Assert.IsNotNull(rig.Head, "Head");
            Assert.IsNotNull(rig.Tail, "Tail1");
            Assert.IsNotNull(rig.TailIK, "TailIK");
            Assert.IsNotNull(rig.HandLeft, "Hand.Left");
            Assert.IsNotNull(rig.HandRight, "Hand.Right");
            Assert.IsNotNull(rig.Hat, "Hat Object");
            Assert.IsNotNull(rig.TailIK.Find("Tail3"), "Tail3 under TailIK");
            Assert.IsNotNull(rig.Tail.Find("Tail2"), "Tail2 under Tail1");
        }

        [Test]
        public void Find_NullWithoutTheBones()
        {
            Assert.IsNull(GhostRig.Find(null));
            GameObject bare = new GameObject("Bare", typeof(Animator));
            try
            {
                Assert.IsNull(GhostRig.Find(bare.GetComponent<Animator>()));
            }
            finally
            {
                Object.DestroyImmediate(bare);
            }
        }

        [Test]
        public void Axes_UpForwardRightAreTheGhosts()
        {
            GhostRig rig = GhostRig.Find(animator);
            Vector3 hands = (rig.HandLeft.position + rig.HandRight.position) * 0.5f;
            TestContext.WriteLine("up " + rig.Up.ToString("F3") + " forward " + rig.Forward.ToString("F3") + " right "
                + rig.Right.ToString("F3") + " U " + rig.U.ToString("F3") + " tall axis " + rig.TallAxis
                + " up.modelUp " + Vector3.Dot(rig.Up, actor.Model.up).ToString("F3")
                + " forward.modelForward " + Vector3.Dot(rig.Forward, actor.Model.forward).ToString("F3"));

            Assert.Greater(Vector3.Dot(rig.Up, actor.Model.up), 0.9f, "up is about the model's up");
            Assert.Greater(Vector3.Dot(rig.Forward, hands - rig.Tail.position), 0f, "forward runs from the tail to the hands");
            Assert.Greater(Vector3.Dot(rig.Forward, actor.Model.forward), 0.7f, "forward is about the scooter's nose");
            Assert.Less(Mathf.Abs(Vector3.Dot(rig.Right, rig.Up)), 1e-3f, "right is across up");
            Assert.Less(Mathf.Abs(Vector3.Dot(rig.Right, rig.Forward)), 1e-3f, "right is across forward");
            Assert.Greater(Vector3.Dot(rig.Right, actor.Model.right), 0.7f, "right is the scooter's right");
            Assert.Greater(Vector3.Dot(rig.HandRight.position - rig.HandLeft.position, rig.Right), 0f, "Hand.Right is on its right");
            Assert.IsTrue(rig.RestFromBind, "the axes come from the skin's bind pose");
            // U is Torso to Head at rest (the bind pose): about the distance in the prefab's pose too
            float now = (rig.Head.position - rig.Torso.position).magnitude;
            Assert.AreEqual(now, rig.U, now * 0.02f, "U is Torso to Head");
        }

        /// <summary>
        /// The axes are the rest pose's, not whatever pose the bones hold when the rig is made: an Animator may have the
        /// ghost leaning, turned or bobbing on that first frame
        /// </summary>
        [Test]
        public void Axes_ALeaningFirstFrameDoesNotSkewThem()
        {
            GhostRig still = GhostRig.Find(animator);
            Vector3 up = still.Up, forward = still.Forward, right = still.Right;
            float u = still.U;
            int tall = still.TallAxis;

            Transform torso = animator.transform.Find(GhostRig.TORSO);
            Transform hand = animator.transform.Find(GhostRig.HAND_LEFT);
            torso.localRotation = torso.localRotation * Quaternion.Euler(25f, 30f, -20f);
            torso.localPosition += new Vector3(0.05f, 0.1f, -0.03f);
            hand.localPosition += new Vector3(0.2f, -0.1f, 0.15f);
            GhostRig leaning = GhostRig.Find(animator);

            Assert.Greater(Vector3.Dot(leaning.Up, up), 0.9999f, "up");
            Assert.Greater(Vector3.Dot(leaning.Forward, forward), 0.9999f, "forward");
            Assert.Greater(Vector3.Dot(leaning.Right, right), 0.9999f, "right");
            Assert.AreEqual(u, leaning.U, 1e-4f, "U");
            Assert.AreEqual(tall, leaning.TallAxis, "the Tall axis");
        }

        [Test]
        public void ApplyRest_ChangesNothing()
        {
            GhostRig rig = GhostRig.Find(animator);
            List<Snap> before = Take();
            rig.Apply(GhostPose.Rest);
            AssertSame(before, "after Rest");

            rig.Apply(Busy());
            rig.Apply(GhostPose.Rest);
            AssertSame(before, "after a pose then Rest");
        }

        [Test]
        public void Apply_TwiceWithNothingRewritingTheBones_IsTheSameAsOnce()
        {
            GhostRig rig = GhostRig.Find(animator);
            rig.BeginHat();
            rig.Apply(Busy());
            List<Snap> once = Take();
            rig.Apply(Busy());
            AssertSame(once, "after the same pose twice");
            rig.Apply(Busy());
            AssertSame(once, "after the same pose three times");
        }

        [Test]
        public void Apply_BuildsOnABoneTheAnimatorRewrote()
        {
            GhostRig rig = GhostRig.Find(animator);
            GhostPose lift = Only(p => p.Pose.Lift = 0.5f);
            Vector3 rest = rig.Torso.localPosition;
            rig.Apply(lift);
            // As if the Animator wrote a new pose (its own values, every component): the lift goes on top of that
            Vector3 animated = rest + new Vector3(0.01f, 0.02f, -0.01f);
            rig.Torso.localPosition = animated;
            Vector3 world = rig.Torso.parent.TransformPoint(animated);
            rig.Apply(lift);
            Assert.Less((rig.Torso.position - (world + rig.Up * 0.5f * rig.U)).magnitude, 1e-4f);
        }

        // A lift, stretch and head pop that grows and falls back to nothing over the frames
        static GhostPose Swell(float share)
        {
            float k = Mathf.Sin(Mathf.PI * share);
            if (share >= 1f)
                k = 0f;
            GhostPose pose = GhostPose.Rest;
            pose.Lift = 0.9f * k;
            pose.Tall = 1f + 0.3f * k;
            pose.Wide = 1f - 0.2f * k;
            pose.HeadPop = 0.45f * k;
            pose.HatLift = 0.3f * k;
            return pose;
        }

        /// <summary>
        /// The capture's bug: the Animator rewrote the Torso's and Head's rotation each frame but not their position or
        /// scale, the whole bone counted as rewritten, and each frame's lift, stretch and pop built on the last one's
        /// </summary>
        [Test]
        public void Apply_AnimatorRewritingOnlyRotation_DoesNotPileUp()
        {
            GhostRig rig = GhostRig.Find(animator);
            List<Snap> before = Take();
            Transform torso = rig.Torso, head = rig.Head;
            Quaternion torsoTurn = torso.localRotation, headTurn = head.localRotation;
            Vector3 torsoScale = torso.localScale;
            Vector3 torsoWorld = torso.position;
            Vector3 up = rig.Up;
            float u = rig.U;
            rig.BeginHat();

            const int FRAMES = 60;
            for (int i = 0; i <= FRAMES; i++)
            {
                // The "Animator": a new rotation on both, nothing else
                torso.localRotation = torsoTurn * Quaternion.Euler(0f, 0f, 0.05f * i);
                head.localRotation = headTurn * Quaternion.Euler(0.05f * i, 0f, 0f);
                GhostPose pose = Swell(i / (float)FRAMES);
                rig.Apply(pose);
                if (pose.IsRest(1e-5f))
                    continue;
                string at = "frame " + i;
                Assert.AreEqual(torsoScale[rig.TallAxis] * pose.Tall, torso.localScale[rig.TallAxis], 1e-4f, "Tall doesn't compound, " + at);
                Assert.Less((torso.position - (torsoWorld + up * pose.Lift * u)).magnitude, 1e-4f, "Lift doesn't compound, " + at);
                Assert.Less((head.position - torso.position).magnitude, u * (pose.Tall + pose.HeadPop) + 0.05f * u, "the head pop doesn't compound, " + at);
            }

            // The Animator's rotations stay theirs; position and scale are back exactly
            torso.localRotation = torsoTurn;
            head.localRotation = headTurn;
            rig.Clear();
            AssertSame(before, "after the swell");
        }

        /// <summary>The same with the real Animator updating between frames (when it evaluates in Edit Mode)</summary>
        [Test]
        public void Apply_WithTheAnimatorUpdating_FollowsTheCurve()
        {
            animator.Rebind();
            animator.Update(0f);
            GhostRig rig = GhostRig.Find(animator);
            Transform torso = rig.Torso, head = rig.Head;
            Vector3 headRest = head.localPosition, torsoScale = torso.localScale;
            Quaternion torsoTurn = torso.localRotation;
            Vector3 torsoAt = torso.localPosition;
            animator.Update(0.25f);
            TestContext.WriteLine("Animator moved the Torso: " + (!torso.localRotation.Equals(torsoTurn) || !torso.localPosition.Equals(torsoAt)));
            Assume.That(!torso.localRotation.Equals(torsoTurn) || !torso.localPosition.Equals(torsoAt),
                "the Animator evaluates in Edit Mode");

            const int FRAMES = 60;
            float peak = 0f;
            for (int i = 0; i <= FRAMES; i++)
            {
                animator.Update(1f / 30f);
                GhostPose pose = Swell(i / (float)FRAMES);
                rig.Apply(pose);
                if (pose.IsRest(1e-5f))
                    continue;
                peak = Mathf.Max(peak, (head.position - torso.position).magnitude);
                Assert.AreEqual(torsoScale[rig.TallAxis] * pose.Tall, torso.localScale[rig.TallAxis], 1e-3f, "Tall, frame " + i);
            }
            Assert.Less(peak, rig.U * 1.8f, "the head stays on");
            Assert.Less((head.localPosition - headRest).magnitude, 1e-4f, "the head's position is back");
            Assert.Less((torso.localScale - torsoScale).magnitude, 1e-4f, "the Torso's scale is back");
        }

        [Test]
        public void Squash_KeepsTheHatsShape()
        {
            GhostRig rig = GhostRig.Find(animator);
            Transform hat = rig.Hat;
            Vector3 size = hat.lossyScale;
            TestContext.WriteLine("head in torso " + rig.Head.localRotation.eulerAngles.ToString("F1") + ", hat lossy " + size.ToString("F4"));
            rig.BeginHat();
            GhostPose squash = Only(p => { p.Pose.Tall = 0.7f; p.Pose.Wide = 1.25f; });
            rig.Apply(squash);
            TestContext.WriteLine("squashed hat lossy " + hat.lossyScale.ToString("F4"));
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(size[i], hat.lossyScale[i], Mathf.Abs(size[i]) * 0.02f + 1e-5f, "hat axis " + i);
            Assert.Less(rig.Head.position.y, rig.Torso.position.y + rig.U, "the head still goes down with the squash");
        }

        [Test]
        public void FadingBody_LeavesTheHatWhereItWas()
        {
            GhostRig rig = GhostRig.Find(animator);
            Transform hat = rig.Hat;
            Vector3 at = hat.position, size = hat.lossyScale;
            rig.BeginHat();
            rig.Apply(Only(p => p.Pose.Fade = 1f));
            Assert.Less((hat.position - at).magnitude, 1e-3f, "the hat stays put");
            for (int i = 0; i < 3; i++)
                Assert.AreEqual(size[i], hat.lossyScale[i], Mathf.Abs(size[i]) * 0.01f + 1e-5f, "hat axis " + i);
            Assert.Less((rig.Head.position - rig.Torso.position).magnitude, rig.U * 0.1f, "the body shrank");
        }

        [Test]
        public void Apply_MovesEachPartTheRightWay()
        {
            GhostRig rig = GhostRig.Find(animator);
            Transform torso = rig.Torso, head = rig.Head, tail = rig.Tail, hat = rig.Hat, tip = TailTip(rig);
            Vector3 up = rig.Up, forward = rig.Forward, right = rig.Right;
            float u = rig.U;
            Vector3 torsoAt = torso.position, headAt = head.position, tailAt = tail.position, hatAt = hat.position, tipAt = tip.position;
            Vector3 handsAt = (rig.HandLeft.position + rig.HandRight.position) * 0.5f;

            rig.Apply(Only(p => p.Pose.Lift = 0.5f));
            Assert.Less((torso.position - (torsoAt + up * 0.5f * u)).magnitude, 1e-4f, "Lift moves the Torso up");
            Assert.Less((tail.position - (tailAt + up * 0.5f * u)).magnitude, 1e-4f, "Lift moves Tail1 with it");

            rig.Apply(Only(p => p.Pose.Push = 0.5f));
            Assert.Less((torso.position - (torsoAt + forward * 0.5f * u)).magnitude, 1e-4f, "Push moves it forward");
            rig.Apply(Only(p => p.Pose.Shift = 0.5f));
            Assert.Less((torso.position - (torsoAt + right * 0.5f * u)).magnitude, 1e-4f, "Shift moves it right");

            rig.Apply(Only(p => p.Pose.Pitch = 30f));
            Assert.Greater(Vector3.Dot(head.position - headAt, forward), 0f, "Pitch tips it forward");
            Assert.Less((torso.position - torsoAt).magnitude, 1e-4f, "about the Torso's pivot");
            rig.Apply(Only(p => p.Pose.Roll = 30f));
            Assert.Greater(Vector3.Dot(head.position - headAt, right), 0f, "Roll leans it right");
            rig.Apply(Only(p => p.Pose.Yaw = 30f));
            Vector3 hands = (rig.HandLeft.position + rig.HandRight.position) * 0.5f;
            Assert.Greater(Vector3.Dot(hands - handsAt, right), 0f, "Yaw turns its front to its right");
            Assert.Less(Vector3.Dot(tail.position - tailAt, right), 0f, "Yaw swings Tail1 round with the body");

            rig.Apply(Only(p => p.Pose.HeadPitch = 30f));
            Assert.Greater(Vector3.Dot(hat.position - hatAt, forward), 0f, "HeadPitch looks down: the top of the head goes forward");
            Assert.Less((torso.position - torsoAt).magnitude, 1e-4f, "the head alone");
            rig.Apply(Only(p => p.Pose.HeadPop = 0.5f));
            Assert.Less((head.position - (headAt + up * 0.5f * u)).magnitude, 1e-4f, "HeadPop moves the head up");

            // The head's own front and up, in its local axes, to see where they point after
            rig.Apply(GhostPose.Rest);
            Vector3 headFront = head.InverseTransformDirection(forward), headUp = head.InverseTransformDirection(up);
            rig.Apply(Only(p => p.Pose.HeadYaw = 30f));
            Assert.Greater(Vector3.Dot(head.TransformDirection(headFront), right), 0.3f, "HeadYaw turns its face to its right");
            Assert.Less((torso.position - torsoAt).magnitude, 1e-4f, "the head alone");
            rig.Apply(Only(p => p.Pose.HeadRoll = 30f));
            Assert.Greater(Vector3.Dot(head.TransformDirection(headUp), right), 0.3f, "HeadRoll leans its head to its right");
            Assert.Greater(Vector3.Dot(hat.position - hatAt, right), 0f, "the hat goes with it");

            rig.Apply(GhostPose.Rest);
            Vector3 hatUp = hat.InverseTransformDirection(up);
            rig.Apply(Only(p => p.Pose.HatTip = 30f));
            Assert.Greater(Vector3.Dot(hat.TransformDirection(hatUp), forward), 0.3f, "HatTip flips the hat forward");
            Assert.Less((head.position - headAt).magnitude, 1e-4f, "the hat alone");

            rig.Apply(Only(p => p.Pose.TailUp = 30f));
            Assert.Greater(Vector3.Dot(tip.position - tipAt, up), 0f, "TailUp raises the tail");
            rig.Apply(Only(p => p.Pose.TailWag = 30f));
            Assert.Greater(Vector3.Dot(tip.position - tipAt, right), 0f, "TailWag swings it to its right");

            rig.Apply(Only(p => p.Pose.HatLift = 0.5f));
            Assert.Less((hat.position - (hatAt + up * 0.5f * u)).magnitude, 1e-4f, "HatLift lifts the hat");

            rig.Apply(Only(p => p.Pose.Tall = 1.5f));
            Vector3 tallAxis = torso.localScale;
            rig.Apply(GhostPose.Rest);
            Assert.AreEqual(rig.Torso.localScale[rig.TallAxis] * 1.5f, tallAxis[rig.TallAxis], 1e-4f, "Tall stretches along up");
        }

        [Test]
        public void Clear_PutsTheHatAndBonesBack()
        {
            GhostRig rig = GhostRig.Find(animator);
            List<Snap> before = Take();
            Transform hat = rig.Hat;
            Vector3 hatAt = hat.localPosition;
            rig.BeginHat();
            rig.Apply(Busy());
            Assert.Greater((hat.localPosition - hatAt).magnitude, 1e-3f, "the hat moved");
            rig.Clear();
            AssertSame(before, "after Clear");
        }

        [Test]
        public void Clear_KeepsAHatSetMidMove()
        {
            GhostRig rig = GhostRig.Find(animator);
            Transform hat = rig.Hat;
            rig.BeginHat();
            rig.Apply(Busy());
            // ScooterLook.ShowHat mid move (the Hat Thief): the new hat's pose stays
            Vector3 dressed = new Vector3(0.1f, 0.2f, 0.3f);
            hat.localPosition = dressed;
            rig.Clear();
            Assert.Less((hat.localPosition - dressed).magnitude, 1e-6f);
        }

        [Test]
        public void Fade_DithersTheBody_SwitchesItOffAtOne_ClearPutsItBack()
        {
            GhostRig rig = GhostRig.Find(animator);
            SkinnedMeshRenderer body = animator.transform.Find("Player_Model").GetComponent<SkinnedMeshRenderer>();
            Renderer hatLook = rig.Hat.GetComponent<Renderer>();
            Assert.IsFalse(body.HasPropertyBlock(), "no block to start with");

            rig.Apply(Only(p => p.Pose.Fade = 0.5f));
            Assert.IsTrue(body.HasPropertyBlock(), "the body dithers");
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            Assert.AreEqual(0.5f, block.GetFloat("_DitherThreshold"), 1e-4f);
            Assert.IsFalse(body.forceRenderingOff, "still drawn halfway");

            GhostPose gone = Only(p => { p.Pose.Fade = 1f; p.Pose.HatFade = 1f; });
            rig.Apply(gone);
            Assert.IsTrue(body.forceRenderingOff, "the body is off at Fade 1");
            Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, body.shadowCastingMode, "no shadow");
            if (hatLook != null)
                Assert.IsTrue(hatLook.forceRenderingOff, "the hat is off at HatFade 1");

            rig.Clear();
            Assert.IsFalse(body.HasPropertyBlock(), "the block is gone");
            Assert.IsFalse(body.forceRenderingOff, "drawn again");
            Assert.AreNotEqual(UnityEngine.Rendering.ShadowCastingMode.Off, body.shadowCastingMode, "its shadow is back");
            if (hatLook != null)
                Assert.IsFalse(hatLook.forceRenderingOff, "the hat is back");
        }

        [Test]
        public void Play_NoVersions_Lower_Higher()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(7));
            Assert.AreSame(emotes, GhostEmotes.On(animator), "one a model");
            Assert.IsFalse(emotes.Play((GhostAction)999), "an action with no versions");
            Assert.IsNull(emotes.Playing);

            Assert.IsTrue(emotes.Play(GhostAction.Cackle));
            Assert.AreEqual(GhostAction.Cackle, emotes.Playing.Action);
            Assert.IsFalse(emotes.Play(GhostAction.Bored), "lower doesn't interrupt");
            Assert.IsFalse(emotes.Play(GhostAction.SeeingStars), "lower doesn't interrupt");
            Assert.IsFalse(emotes.Play((GhostAction)999), "an action with no versions");
            Assert.AreEqual(GhostAction.Cackle, emotes.Playing.Action);
        }

        [Test]
        public void Play_HigherInterrupts()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(7));
            Assert.IsTrue(emotes.Play(GhostAction.Bored));
            emotes.Step(0.5f);
            Assert.IsTrue(emotes.Play(GhostAction.SeeingStars), "higher interrupts");
            Assert.AreEqual(GhostAction.SeeingStars, emotes.Playing.Action);
            Assert.IsTrue(emotes.Play(GhostAction.Cackle), "higher again");
            Assert.AreEqual(GhostAction.Cackle, emotes.Playing.Action);
        }

        [Test]
        public void Cooldown_CountsFromTheStart()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(3));
            float cooldown = GhostEmoteRules.Cooldown(GhostAction.SeeingStars);
            float longest = GhostMoves.For(GhostAction.SeeingStars).Max(m => m.Length) + 0.1f;
            Assert.Less(longest + 0.2f, cooldown, "the test needs the cooldown to outlast every version");
            Assert.IsTrue(emotes.Play(GhostAction.SeeingStars));
            emotes.Step(longest);   // longer than any of its versions
            Assert.IsNull(emotes.Playing, "it ended");
            Assert.IsFalse(emotes.Play(GhostAction.SeeingStars), "some of its cooldown left");
            emotes.Step(cooldown - longest - 0.1f);
            Assert.IsFalse(emotes.Play(GhostAction.SeeingStars), "0.1 s left");
            emotes.Step(0.2f);
            Assert.IsTrue(emotes.Play(GhostAction.SeeingStars), "its cooldown after it began");
        }

        [Test]
        public void Versions_NeverTheSameTwiceRunning()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(11));
            string last = null;
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < 12; i++)
            {
                Assert.IsTrue(emotes.Play(GhostAction.FadeAway), "round " + i);
                string name = emotes.Playing.Name;
                Assert.AreNotEqual(last, name, "twice running");
                seen.Add(name);
                last = name;
                emotes.Stop();
            }
            Assert.AreEqual(GhostMoves.For(GhostAction.FadeAway).Length, seen.Count, "every version comes up");
        }

        [Test]
        public void Step_PlaysEveryMoveAndEndsAtRest()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(5));
            List<Snap> before = Take();
            int children = animator.transform.root.GetComponentsInChildren<Transform>(true).Length;
            foreach (GhostMove move in GhostMoves.All)
            {
                foreach (float side in new[] { 1f, -1f })
                {
                    emotes.Play(move, side);
                    Assert.AreSame(move, emotes.Playing);
                    for (float t = 0f; t < move.Length + 0.2f; t += 1f / 30f)
                        emotes.Step(1f / 30f);
                    Assert.IsNull(emotes.Playing, move.Name + " ended");
                    AssertSame(before, "after " + move.Name + " side " + side);
                }
            }
            // The crown moves' crown is kept for next time (hidden): nothing else is left behind
            Transform[] after = animator.transform.root.GetComponentsInChildren<Transform>(true);
            Assert.AreEqual(children, after.Count(t => t != emotes.Props.Crown?.transform), "nothing left behind (no Zzz outside play)");
            Assert.IsFalse(emotes.Props.CrownShown, "the crown hidden");
        }

        SkinnedMeshRenderer Body()
        {
            return animator.transform.Find("Player_Model").GetComponent<SkinnedMeshRenderer>();
        }

        /// <summary>After a fade move: drawn again, no block left over, its shadow back (each renderer as it was)</summary>
        [Test]
        public void FadeMove_EndsWithTheRenderersAsTheyWere()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(5));
            Renderer[] looks = animator.GetComponentsInChildren<Renderer>(true);
            bool[] blocks = looks.Select(l => l.HasPropertyBlock()).ToArray();
            bool[] off = looks.Select(l => l.forceRenderingOff).ToArray();
            UnityEngine.Rendering.ShadowCastingMode[] shadows = looks.Select(l => l.shadowCastingMode).ToArray();
            foreach (GhostMove move in GhostMoves.All.Where(m => Enumerable.Range(0, 41).Any(i => m.At(i / 40f, 1f).Fade > 0.5f)))
            {
                emotes.Play(move, 1f);
                bool hidden = Body().forceRenderingOff;   // an entrance hides it at once
                for (float t = 0f; t < move.Length + 0.1f; t += 1f / 60f)
                {
                    emotes.Step(1f / 60f);
                    hidden |= Body().forceRenderingOff || Body().shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                Assert.IsTrue(hidden, move.Name + " hid the body on the way");
                Assert.IsNull(emotes.Playing, move.Name + " ended");
                for (int i = 0; i < looks.Length; i++)
                {
                    string what = move.Name + ": " + looks[i].name;
                    Assert.AreEqual(blocks[i], looks[i].HasPropertyBlock(), what + " block");
                    Assert.AreEqual(off[i], looks[i].forceRenderingOff, what + " drawn");
                    Assert.AreEqual(shadows[i], looks[i].shadowCastingMode, what + " shadow");
                }
            }
        }

        /// <summary>A block another system put on the body keeps its values; only the dither goes back to what it was</summary>
        [Test]
        public void Fade_KeepsAnotherSystemsBlock_PutsBackOnlyTheDither()
        {
            GhostRig rig = GhostRig.Find(animator);
            SkinnedMeshRenderer body = Body();
            MaterialPropertyBlock theirs = new MaterialPropertyBlock();
            theirs.SetFloat("_SomeoneElses", 0.42f);
            theirs.SetFloat("_DitherThreshold", 0.8f);
            body.SetPropertyBlock(theirs);

            rig.Apply(Only(p => p.Pose.Fade = 0.5f));
            // Another system writes during the fade: that stays too
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            block.SetFloat("_WrittenMidFade", 3f);
            body.SetPropertyBlock(block);
            rig.Apply(Only(p => p.Pose.Fade = 0.7f));
            rig.Clear();

            Assert.IsTrue(body.HasPropertyBlock(), "the block is still there");
            body.GetPropertyBlock(block);
            Assert.AreEqual(0.42f, block.GetFloat("_SomeoneElses"), 1e-6f, "their value stays");
            Assert.AreEqual(3f, block.GetFloat("_WrittenMidFade"), 1e-6f, "a value written mid fade stays");
            Assert.AreEqual(0.8f, block.GetFloat("_DitherThreshold"), 1e-6f, "the dither back to theirs");
            body.SetPropertyBlock(null);
        }

        /// <summary>An entrance is hidden as soon as it begins, before its first step</summary>
        [Test]
        public void Entrance_IsHiddenFromTheStart()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(5));
            foreach (GhostMove move in GhostMoves.All.Where(m => m.Entrance))
            {
                Assert.IsFalse(Body().forceRenderingOff, move.Name + " drawn before");
                emotes.Play(move, 1f);
                Assert.IsTrue(Body().forceRenderingOff, move.Name + " hidden at once");
                emotes.Stop();
                Assert.IsFalse(Body().forceRenderingOff, move.Name + " drawn after Stop");
            }
        }

        /// <summary>An interrupt eases the body over from where the interrupted move had it, not a snap</summary>
        [Test]
        public void Interrupt_BlendsFromTheInterruptedPose()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(5));
            GhostRig rig = emotes.Rig;
            Vector3 rest = rig.Torso.position;
            float u = rig.U;
            GhostMove hide = GhostMoves.All.First(m => m.Name == "Hide"), leap = GhostMoves.All.First(m => m.Name == "Leap");
            emotes.Play(hide, 1f);
            for (int i = 0; i < 60; i++)
                emotes.Step(1f / 60f);
            Vector3 low = rig.Torso.position;
            Assert.Less(Vector3.Dot(low - rest, rig.Up), -0.4f * u, "sunk low");

            emotes.Play(leap, 1f);
            emotes.Step(0.01f);
            Assert.Less((rig.Torso.position - low).magnitude, 0.1f * u, "no snap to the new move");
            for (int i = 0; i < 15; i++)
                emotes.Step(0.01f);
            GhostPose own = leap.At(0.16f / leap.Length, 1f);
            Assert.AreEqual(own.Lift * u, Vector3.Dot(rig.Torso.position - rest, rig.Up), 0.01f * u, "the new move's own pose once blended");
            emotes.Stop();
        }

        [Test]
        public void Stop_PutsEverythingBackMidMove()
        {
            GhostEmotes emotes = GhostEmotes.On(animator, new System.Random(5));
            List<Snap> before = Take();
            emotes.Play(GhostMoves.All.First(m => m.Name == "Hat Flip"), 1f);
            emotes.Step(0.4f);
            emotes.Stop();
            Assert.IsNull(emotes.Playing);
            AssertSame(before, "after Stop");
        }
    }
}

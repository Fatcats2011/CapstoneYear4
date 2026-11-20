using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The ghost's hand clips: GhostEmotes.fbx's import, the upper-body mask, the Emotes layer of PlayerAnimator, the
    /// moves that play them, the round trip from Blender (GE_0_RestCheck poses the bones as the model's own rest pose)
    /// and GhostEmotes easing the layer's weight (GhostEmoteLayerSetup builds all of it)
    /// </summary>
    public class GhostClipTests
    {
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";
        /// <summary>One frame of the clips (GhostEmotes.fbx is exported at 60 fps)</summary>
        const float FRAME = 1f / 60f;
        static readonly string[] BONES =
        {
            "Armature.001/Torso/Head", "Armature.001/Torso/Hand.Left", "Armature.001/Torso/Hand.Left/Finger.Left",
            "Armature.001/Torso/Hand.Right", "Armature.001/Torso/Hand.Right/Finger.Right"
        };

        TitleActor actor;
        Animator animator;

        [TearDown]
        public void TearDown()
        {
            if (actor != null)
                Object.DestroyImmediate(actor.gameObject);
        }

        Animator Ghost()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR);
            Assert.IsNotNull(prefab, "the avatar prefab");
            actor = TitleActor.Create(prefab, null, "Clip Test Ghost");
            Transform model = actor.Model.Find("Scooter/" + ScooterLook.MODEL + "/Player Model");
            Assert.IsNotNull(model, "the ghost's model");
            animator = model.GetComponent<Animator>();
            Assert.IsNotNull(animator, "the ghost's Animator");
            return animator;
        }

        static List<AnimationClip> Clips()
        {
            return AssetDatabase.LoadAllAssetsAtPath(GhostEmoteLayerSetup.FBX).OfType<AnimationClip>()
                .Where(c => c.name.StartsWith(GhostEmoteLayerSetup.PREFIX)).ToList();
        }

        static AnimatorControllerLayer Layer(out AnimatorController controller)
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GhostEmoteLayerSetup.CONTROLLER);
            Assert.IsNotNull(controller, "the controller");
            AnimatorControllerLayer layer = controller.layers.FirstOrDefault(l => l.name == GhostEmoteLayerSetup.LAYER);
            Assert.IsNotNull(layer, "the Emotes layer");
            return layer;
        }

        [Test]
        public void ClipName_TakesTheGEPart()
        {
            Assert.AreEqual("GE_2a_FistPump", GhostEmoteLayerSetup.ClipName("Armature.001|GE_2a_FistPump"));
            Assert.AreEqual("GE_2a_FistPump", GhostEmoteLayerSetup.ClipName("GE_2a_FistPump"));
            Assert.IsNull(GhostEmoteLayerSetup.ClipName("Armature.001|Action"));
            Assert.IsNull(GhostEmoteLayerSetup.ClipName(null));
        }

        [Test]
        public void Fbx_GenericNoAvatar_AClipPerTake_LoopOff()
        {
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(GhostEmoteLayerSetup.FBX);
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.AreEqual(ModelImporterAvatarSetup.NoAvatar, importer.avatarSetup);
            Assert.IsTrue(importer.importAnimation);

            string[] takes = importer.importedTakeInfos.Select(t => GhostEmoteLayerSetup.ClipName(t.name))
                .Where(n => n != null).Distinct().OrderBy(n => n).ToArray();
            Assert.Contains(GhostEmoteLayerSetup.REST_CHECK, takes);
            List<AnimationClip> clips = Clips();
            CollectionAssert.AreEquivalent(takes, clips.Select(c => c.name).ToArray(), "a clip per GE_ take");
            foreach (AnimationClip clip in clips)
            {
                Assert.IsFalse(AnimationUtility.GetAnimationClipSettings(clip).loopTime, clip.name + " loop time");
                Assert.IsFalse(clip.isLooping, clip.name + " loops");
                Assert.Greater(clip.length, 0f, clip.name);
            }
        }

        [Test]
        public void Mask_OnlyTheHeadHandsAndFingers()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(GhostEmoteLayerSetup.MASK);
            Assert.IsNotNull(mask, "the mask");
            List<string> on = new List<string>(), off = new List<string>();
            for (int i = 0; i < mask.transformCount; i++)
                (mask.GetTransformActive(i) ? on : off).Add(mask.GetTransformPath(i));
            CollectionAssert.AreEquivalent(new[]
            {
                "Armature.001/Torso/Head",
                "Armature.001/Torso/Hand.Left", "Armature.001/Torso/Hand.Left/Finger.Left",
                "Armature.001/Torso/Hand.Right", "Armature.001/Torso/Hand.Right/Finger.Right"
            }, on);
            Assert.Contains("Armature.001/Tail1", off, "the tail is in the mask, off");
            // Every take holds them too (Blender bakes every bone), at Blender's rest: on, they move the ghost off its scooter
            Assert.Contains("Armature.001", off, "the armature node is off");
            Assert.Contains("Armature.001/Torso", off, "the Torso is off (the Base Layer floats it)");
            CollectionAssert.AreEquivalent(GhostEmoteLayerSetup.ModelPaths(GhostEmoteLayerSetup.FBX),
                on.Concat(off).ToList(), "every path in the model, once");
        }

        [Test]
        public void Controller_EmotesLayer_OverrideMaskedWeightZero_AStatePerClip()
        {
            AnimatorControllerLayer layer = Layer(out AnimatorController controller);
            Assert.AreEqual("Base Layer", controller.layers[0].name, "the Base Layer stays first");
            Assert.AreEqual(AnimatorLayerBlendingMode.Override, layer.blendingMode);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<AvatarMask>(GhostEmoteLayerSetup.MASK), layer.avatarMask);
            Assert.AreEqual(0f, layer.defaultWeight);

            AnimatorState empty = layer.stateMachine.defaultState;
            Assert.IsNotNull(empty);
            Assert.AreEqual(GhostEmoteLayerSetup.EMPTY, empty.name);
            Assert.IsNull(empty.motion);

            Dictionary<string, AnimationClip> clips = Clips().Where(c => c.name != GhostEmoteLayerSetup.REST_CHECK)
                .ToDictionary(c => c.name);
            AnimatorState[] states = layer.stateMachine.states.Select(s => s.state).ToArray();
            CollectionAssert.AreEquivalent(clips.Keys.Concat(new[] { GhostEmoteLayerSetup.EMPTY }).ToArray(),
                states.Select(s => s.name).ToArray(), "Empty and a state per clip, no RestCheck");
            foreach (AnimatorState state in states)
            {
                Assert.IsFalse(state.writeDefaultValues, state.name + " write defaults");
                if (state != empty)
                    Assert.AreSame(clips[state.name], state.motion, state.name + " plays its clip");
            }
        }

        [Test]
        public void EveryClipMove_NamesAStateAndLastsAsLongAsItsClip()
        {
            AnimatorControllerLayer layer = Layer(out _);
            Dictionary<string, AnimationClip> clips = Clips().ToDictionary(c => c.name);
            foreach (GhostMove move in GhostMoves.All.Where(m => m.Clip != null))
            {
                Assert.IsTrue(layer.stateMachine.states.Any(s => s.state.name == move.Clip), move.Name + ": state " + move.Clip);
                Assert.IsTrue(clips.ContainsKey(move.Clip), move.Name + ": clip " + move.Clip);
                Assert.AreEqual(clips[move.Clip].length, move.Length, FRAME + 1e-4f, move.Name + " length");
            }
            CollectionAssert.AreEquivalent(clips.Keys.Where(k => k != GhostEmoteLayerSetup.REST_CHECK).ToArray(),
                GhostMoves.All.Where(m => m.Clip != null).Select(m => m.Clip).ToArray(), "a version per clip but RestCheck");
        }

        // Rotation and position of a bone against its rest: the angle in degrees, the distance in metres, and the
        // turn between them as Euler angles (the axis pattern, when it fails)
        static string Delta(string bone, Quaternion rest, Vector3 restAt, Transform t, out float angle, out float distance)
        {
            angle = Quaternion.Angle(rest, t.localRotation);
            distance = (t.localPosition - restAt).magnitude;
            Vector3 euler = (Quaternion.Inverse(rest) * t.localRotation).eulerAngles;
            for (int i = 0; i < 3; i++)
                if (euler[i] > 180f)
                    euler[i] -= 360f;
            return bone + ": " + angle.ToString("F2") + " deg (delta euler " + euler.ToString("F1") + "), "
                + (distance * 1000f).ToString("F2") + " mm (rest " + restAt.ToString("F4") + " now "
                + t.localPosition.ToString("F4") + ", rest rot " + rest.eulerAngles.ToString("F1") + " now "
                + t.localRotation.eulerAngles.ToString("F1") + ")";
        }

        // Most a bone may be off its rest, in degrees and metres
        const float ANGLE = 0.5f, DISTANCE = 0.001f;

        static bool Check(GameObject model, AnimationClip clip, float time, StringBuilder report)
        {
            return Check(model, clip, time, report, path => ANGLE, path => DISTANCE);
        }

        static bool Check(GameObject model, AnimationClip clip, float time, StringBuilder report,
            System.Func<string, float> angleLimit, System.Func<string, float> distanceLimit)
        {
            Transform root = model.transform;
            Transform[] bones = BONES.Select(p => root.Find(p)).ToArray();
            for (int i = 0; i < bones.Length; i++)
                Assert.IsNotNull(bones[i], BONES[i]);
            Quaternion[] rotations = bones.Select(b => b.localRotation).ToArray();
            Vector3[] positions = bones.Select(b => b.localPosition).ToArray();

            clip.SampleAnimation(model, time);
            bool good = true;
            for (int i = 0; i < bones.Length; i++)
            {
                report.AppendLine(Delta(BONES[i], rotations[i], positions[i], bones[i], out float angle, out float distance));
                good &= angle <= angleLimit(BONES[i]) && distance <= distanceLimit(BONES[i]);
                bones[i].localRotation = rotations[i];
                bones[i].localPosition = positions[i];
            }
            return good;
        }

        // Where a difference comes from: each bone as the skin's bind pose has it (Blender's rest when it imports the
        // FBX) and as Official_Anim_4.fbx's own nodes have it (Unity's default), against the prefab's
        static void Sources(GameObject model, StringBuilder report)
        {
            SkinnedMeshRenderer skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .FirstOrDefault(s => s.bones.Any(b => b != null && b.name == "Torso"));
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Animations/Character Animations/Official_Anim_4.fbx");
            foreach (string path in BONES)
            {
                Transform bone = model.transform.Find(path);
                report.Append("sources " + path + ": prefab " + bone.localPosition.ToString("F4") + " "
                    + bone.localRotation.eulerAngles.ToString("F1"));
                if (skin != null)
                {
                    int i = System.Array.IndexOf(skin.bones, bone), p = System.Array.IndexOf(skin.bones, bone.parent);
                    if (i >= 0 && p >= 0)
                    {
                        Matrix4x4 local = skin.sharedMesh.bindposes[p] * skin.sharedMesh.bindposes[i].inverse;
                        report.Append(", skin bind " + ((Vector3)local.GetColumn(3)).ToString("F4") + " "
                            + local.rotation.eulerAngles.ToString("F1"));
                    }
                }
                Transform node = source != null ? source.transform.Find(path) : null;
                if (node != null)
                    report.Append(", Official_Anim_4 node " + node.localPosition.ToString("F4") + " "
                        + node.localRotation.eulerAngles.ToString("F1"));
                report.AppendLine();
            }
        }

        [Test]
        public void RoundTrip_RestCheckPosesTheBonesAsTheModelsRest()
        {
            Animator ghost = Ghost();
            AnimationClip rest = Clips().FirstOrDefault(c => c.name == GhostEmoteLayerSetup.REST_CHECK);
            Assert.IsNotNull(rest, GhostEmoteLayerSetup.REST_CHECK);
            // Head, Hand.Right and Finger.Right come through exactly: the export's axes are right. The left hand doesn't
            // quite: Official_Anim_4.fbx saved Hand.Left about 6 degrees and 1 mm off its skin's bind pose (Finger.Left
            // about 3 degrees), Blender takes the bind pose as its rest, and Unity the saved pose. Accepted: the Emotes
            // layer eases in and out over the clip's first and last moments, and the Base Layer moves the hands anyway,
            // so it doesn't show. The left hand gets 7 degrees and 2 mm, enough for that and no more
            System.Func<string, bool> left = path => path.Contains(".Left");
            StringBuilder report = new StringBuilder();
            bool good = Check(ghost.gameObject, rest, 0f, report, path => left(path) ? 7f : ANGLE,
                path => left(path) ? 0.002f : DISTANCE);
            Sources(ghost.gameObject, report);
            TestContext.WriteLine(report.ToString());
            Assert.IsTrue(good, "the round trip moved the bones (0.5 deg and 1 mm; the left hand 7 deg and 2 mm):\n" + report);
        }

        [Test]
        public void EveryClip_StartsAndEndsAtRest()
        {
            // Against Blender's rest (GE_0_RestCheck), so this checks the clips' own start and end: whether that rest
            // is the model's is the round trip's test
            Animator ghost = Ghost();
            AnimationClip rest = Clips().FirstOrDefault(c => c.name == GhostEmoteLayerSetup.REST_CHECK);
            Assert.IsNotNull(rest, GhostEmoteLayerSetup.REST_CHECK);
            rest.SampleAnimation(ghost.gameObject, 0f);
            StringBuilder report = new StringBuilder();
            bool good = true;
            foreach (AnimationClip clip in Clips().Where(c => c != rest))
            {
                foreach (float time in new[] { 0f, clip.length })
                {
                    report.AppendLine(clip.name + " at " + time.ToString("F2") + " s");
                    good &= Check(ghost.gameObject, clip, time, report);
                }
            }
            TestContext.WriteLine(report.ToString());
            Assert.IsTrue(good, "a clip doesn't start or end at rest (0.5 deg, 1 mm):\n" + report);
        }

        GhostEmotes Bound()
        {
            Animator ghost = Ghost();
            ghost.Rebind();
            ghost.Update(0f);
            Assert.IsTrue(ghost.isInitialized, "the Animator is bound in Edit Mode");
            GhostEmotes emotes = GhostEmotes.On(ghost, new System.Random(3));
            Assert.GreaterOrEqual(emotes.ClipLayer, 1, "the Emotes layer");
            Assert.AreEqual(0f, ghost.GetLayerWeight(emotes.ClipLayer), "weight 0 at rest");
            return emotes;
        }

        int StateNow(int layer)
        {
            animator.Update(0f);
            return animator.GetCurrentAnimatorStateInfo(layer).shortNameHash;
        }

        [Test]
        public void ClipMove_EasesTheLayerUpThenDown_EndsOnEmpty()
        {
            GhostEmotes emotes = Bound();
            int layer = emotes.ClipLayer;
            GhostMove move = GhostMoves.All.First(m => m.Clip != null);

            emotes.Play(move, 1f);
            Assert.AreEqual(move.ClipHash, StateNow(layer), "its clip plays on the layer");

            const float DT = 0.01f;
            float time = 0f, peak = 0f;
            while (time < move.Length + 0.3f)
            {
                emotes.Step(DT);
                time += DT;
                float w = emotes.ClipWeight;
                Assert.AreEqual(w, animator.GetLayerWeight(layer), 1e-5f, "the Animator has the weight at " + time);
                peak = Mathf.Max(peak, w);
                if (time < GhostEmotes.CLIP_IN * 0.75f)
                    Assert.Less(w, 1f, "still easing up at " + time);
                if (time > GhostEmotes.CLIP_IN + 0.02f && time < move.Length - GhostEmotes.CLIP_OUT - 0.02f)
                    Assert.AreEqual(1f, w, 1e-4f, "full in the middle at " + time);
                if (time > move.Length - GhostEmotes.CLIP_OUT * 0.5f && time < move.Length)
                    Assert.Less(w, 0.75f, "easing down at " + time);
                if (time >= move.Length + DT)
                    Assert.AreEqual(0f, w, "down after the end at " + time);
            }
            Assert.AreEqual(1f, peak, 1e-4f);
            Assert.IsNull(emotes.Playing);
            Assert.AreEqual(Animator.StringToHash(GhostEmotes.EMPTY), StateNow(layer), "back on Empty");
        }

        [Test]
        public void ClipMove_InterruptedByOneWithout_EasesOut_StopIsAtOnce()
        {
            GhostEmotes emotes = Bound();
            int layer = emotes.ClipLayer;
            GhostMove clip = GhostMoves.All.First(m => m.Clip != null);
            GhostMove plain = GhostMoves.All.First(m => m.Clip == null && m.Length > 1f);

            emotes.Play(clip, 1f);
            for (int i = 0; i < 30; i++)
                emotes.Step(0.01f);
            Assert.AreEqual(1f, emotes.ClipWeight, 1e-4f);

            emotes.Play(plain, 1f);
            emotes.Step(0.05f);
            Assert.AreEqual(0.5f, emotes.ClipWeight, 1e-3f, "halfway out after 0.05 s");
            emotes.Step(0.06f);
            Assert.AreEqual(0f, emotes.ClipWeight, "out after 0.1 s");
            Assert.AreEqual(0f, animator.GetLayerWeight(layer));
            Assert.AreEqual(Animator.StringToHash(GhostEmotes.EMPTY), StateNow(layer), "Empty");

            emotes.Stop();
            emotes.Play(clip, 1f);
            for (int i = 0; i < 30; i++)
                emotes.Step(0.01f);
            emotes.Stop();
            Assert.AreEqual(0f, emotes.ClipWeight, "Stop: 0 at once");
            Assert.AreEqual(0f, animator.GetLayerWeight(layer));
            Assert.AreEqual(Animator.StringToHash(GhostEmotes.EMPTY), StateNow(layer), "Empty after Stop");
        }

        /// <summary>A clip move straight after another cross-fades the hands across (from weight 0 it starts at once)</summary>
        [Test]
        public void ClipMove_AfterAnother_CrossFades()
        {
            GhostEmotes emotes = Bound();
            int layer = emotes.ClipLayer;
            GhostMove[] clips = GhostMoves.All.Where(m => m.Clip != null).Take(2).ToArray();

            emotes.Play(clips[0], 1f);
            Assert.AreEqual(clips[0].ClipHash, StateNow(layer), "from weight 0: at once");
            for (int i = 0; i < 30; i++)
                emotes.Step(0.01f);
            Assert.AreEqual(1f, emotes.ClipWeight, 1e-4f);

            emotes.Play(clips[1], 1f);
            animator.Update(0.01f);
            Assert.IsTrue(animator.IsInTransition(layer), "cross-fading");
            Assert.AreEqual(clips[0].ClipHash, animator.GetCurrentAnimatorStateInfo(layer).shortNameHash, "from the first");
            Assert.AreEqual(clips[1].ClipHash, animator.GetNextAnimatorStateInfo(layer).shortNameHash, "to the second");
            emotes.Step(0.01f);
            Assert.Greater(emotes.ClipWeight, 0.9f, "the layer stays up across");
            animator.Update(GhostEmotes.CLIP_CROSS + 0.02f);
            Assert.AreEqual(clips[1].ClipHash, animator.GetCurrentAnimatorStateInfo(layer).shortNameHash, "on the second after");
            emotes.Stop();
        }

        /// <summary>
        /// Switched off mid clip (the Animator with it, keeping its state): back on, the layer is down on Empty and
        /// nothing plays
        /// </summary>
        [Test]
        public void SwitchedOffMidClip_ComesBackWithTheLayerDown()
        {
            GhostEmotes emotes = Bound();
            int layer = emotes.ClipLayer;
            animator.keepAnimatorStateOnDisable = true;
            emotes.Play(GhostMoves.All.First(m => m.Clip != null), 1f);
            for (int i = 0; i < 30; i++)
                emotes.Step(0.01f);
            Assert.AreEqual(1f, animator.GetLayerWeight(layer), 1e-4f, "the clip is up");

            // Edit Mode doesn't call OnDisable and OnEnable on it: as Play Mode would, around the switch
            Call(emotes, "OnDisable");
            animator.gameObject.SetActive(false);
            animator.gameObject.SetActive(true);
            Call(emotes, "OnEnable");
            animator.Update(0f);
            Assume.That(animator.isInitialized, "the Animator is bound again in Edit Mode");
            emotes.Step(0.01f);
            Assert.IsNull(emotes.Playing, "nothing plays");
            Assert.AreEqual(0f, emotes.ClipWeight);
            Assert.AreEqual(0f, animator.GetLayerWeight(layer), "the layer is down");
            Assert.AreEqual(Animator.StringToHash(GhostEmotes.EMPTY), StateNow(layer), "on Empty");
        }

        static void Call(GhostEmotes emotes, string method)
        {
            typeof(GhostEmotes).GetMethod(method, System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic).Invoke(emotes, null);
        }

        [Test]
        public void PlainMove_LeavesTheLayerAlone()
        {
            GhostEmotes emotes = Bound();
            emotes.Play(GhostMoves.All.First(m => m.Clip == null), 1f);
            for (int i = 0; i < 20; i++)
            {
                emotes.Step(0.05f);
                Assert.AreEqual(0f, emotes.ClipWeight);
                Assert.AreEqual(0f, animator.GetLayerWeight(emotes.ClipLayer));
            }
        }
    }
}

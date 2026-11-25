using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Not a check: writes down, every frame as the stage's camera finishes filming it (after everything has moved), where the title skit's ghosts,
    /// their tilts, heads and hands, and its packages are, for TitleShowTests.TitleMotionTrace. A snap or a jerk shows as
    /// a jump in speed between two frames
    /// </summary>
    public class TitleMotionRecorder
    {
        public TitleStage stage;
        public string skit;
        public readonly StringBuilder rows = new StringBuilder();
        int frame;

        public void Start()
        {
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Filmed;
        }

        public void Stop()
        {
            UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Filmed;
        }

        void Filmed(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
        {
            if (stage != null && camera == stage.stageCamera)
                Record();
        }

        void Record()
        {
            if (stage == null || stage.CurrentCast == null)
                return;
            Transform space = stage.transform;
            TitleActor[] cast = stage.CurrentCast;
            for (int i = 0; i < cast.Length; i++)
            {
                TitleActor actor = cast[i];
                Vector3 at = actor.transform.localPosition;
                int seen = Mathf.Abs(at.x) < stage.EdgeX(at.z) + 1.2f ? 1 : 0;
                Row("a" + i + ".root", seen, at, actor.transform.localRotation, actor.transform.localScale);
                Row("a" + i + ".model", seen, space.InverseTransformPoint(actor.Model.position),
                    Quaternion.Inverse(space.rotation) * actor.Model.rotation, actor.Model.localScale);
                Animator animator = actor.Animator;
                if (animator == null)
                    continue;
                Transform root = animator.transform;
                Bone(root, GhostRig.HEAD, "a" + i + ".head", seen, actor.Model);
                Bone(root, GhostRig.TORSO, "a" + i + ".torso", seen, actor.Model);
                Bone(root, GhostRig.HAND_LEFT, "a" + i + ".handL", seen, actor.Model);
                Bone(root, GhostRig.HAND_RIGHT, "a" + i + ".handR", seen, actor.Model);
            }
            foreach (Transform package in stage.PackagesOut)
            {
                Vector3 at = space.InverseTransformPoint(package.position);
                int seen = Mathf.Abs(at.x) < stage.EdgeX(at.z) + 1.2f ? 1 : 0;
                Row("p" + package.GetInstanceID(), seen, at, Quaternion.Inverse(space.rotation) * package.rotation, package.lossyScale);
            }
            frame++;
        }

        void Bone(Transform root, string path, string name, int seen, Transform model)
        {
            Transform bone = root.Find(path);
            if (bone == null)
                return;
            // In the tilt's space: what the animator and the emotes do, not where the ghost rides
            Row(name, seen, model.InverseTransformPoint(bone.position), Quaternion.Inverse(model.rotation) * bone.rotation,
                bone.lossyScale);
        }

        void Row(string name, int seen, Vector3 p, Quaternion q, Vector3 s)
        {
            CultureInfo c = CultureInfo.InvariantCulture;
            rows.Append(skit).Append(',').Append(frame).Append(',').Append(name).Append(',').Append(seen);
            foreach (float f in new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w, s.x, s.y, s.z })
                rows.Append(',').Append(f.ToString("F5", c));
            rows.Append('\n');
        }
    }
}

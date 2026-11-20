using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Puts a GhostPose on the ghost's bones, on top of what its Animator wrote this frame (call Apply in LateUpdate).
/// The ghost's own up, forward and right come from its rest pose, so no import axis is assumed:
/// up = Torso to Head, forward = Tail1 to the middle of the hands (flattened against up), right = Vector3.Cross(up,
/// forward). The rest pose is the skin's bind pose (SkinnedMeshRenderer bindposes), not whatever pose the bones hold when
/// the rig is made: an Animator may already have the ghost leaning or bobbing on that frame. Without a skin holding all
/// of those bones it falls back to the pose as it stands. Unity is left-handed with +y up and +z forward, and Cross(up, forward) = +x there, the right of something
/// facing +z: so this is the ghost's own right, with no flip. A bone the Animator doesn't rewrite (Head, the hat) still
/// holds what Apply wrote last frame: Apply sees that and builds on the pose under it, so offsets never pile up.
/// Fade: the body's URP/OToonLit dithers pixels away through _DitherThreshold in its lit and outline passes (1 = all
/// drawn, 0 = none; no keyword), but the horns (URP/Lit), the eyes (URP/Unlit) and every hat (URP/Lit) can't, so the
/// body also shrinks as it fades and the hat only shrinks; at the end both are switched off, shadow and all. The fade
/// writes only _DitherThreshold into a renderer's property block, on top of whatever else the block holds, and puts
/// back just that value afterwards
/// </summary>
public sealed class GhostRig
{
    public const string TORSO = "Armature.001/Torso";
    public const string HEAD = TORSO + "/Head";
    public const string HAND_LEFT = TORSO + "/Hand.Left";
    public const string HAND_RIGHT = TORSO + "/Hand.Right";
    public const string TAIL = "Armature.001/Tail1";
    public const string TAIL_IK = TORSO + "/TailIK";
    public const string HAT = HEAD + "/Hat Object";

    static readonly int DITHER = Shader.PropertyToID("_DitherThreshold");
    /// <summary>How much of the body's size goes as it fades (at Fade 1 it is 5% of its size)</summary>
    const float SHRINK = 0.95f;
    /// <summary>From this much fade the part is switched off altogether</summary>
    const float GONE = 0.999f;
    /// <summary>From this much fade the body casts no shadow (the shadow pass doesn't dither)</summary>
    const float NO_SHADOW = 0.5f;
    /// <summary>The hat's smallest scale (a zero scale breaks its matrix)</summary>
    const float TINY = 1e-4f;

    /// <summary>
    /// A bone this rig moves: the pose it builds on, and what it wrote last. Kept apart for position, rotation and
    /// scale, and for each component of position and scale: the Animator may rewrite a bone's rotation every frame
    /// and leave its position or scale as this rig wrote them (Idle Float keys only some channels), and taking the
    /// whole bone as rewritten then built each frame's offset on the last one's
    /// </summary>
    sealed class Bone
    {
        public readonly Transform transform;
        Vector3 basePosition, baseScale;
        Quaternion baseRotation;
        bool written;
        Vector3 wrotePosition, wroteScale;
        Quaternion wroteRotation;

        public Bone(Transform transform)
        {
            this.transform = transform;
        }

        /// <summary>
        /// Takes the base, channel by channel (what's there now, unless it still holds what this rig wrote: then
        /// nothing rewrote it and the base from before stays) and puts the bone back on it
        /// </summary>
        public void Begin()
        {
            basePosition = Base(transform.localPosition, wrotePosition, basePosition);
            Quaternion rotation = transform.localRotation;
            if (!written || !rotation.Equals(wroteRotation))
                baseRotation = rotation;
            baseScale = Base(transform.localScale, wroteScale, baseScale);
            transform.localPosition = basePosition;
            transform.localRotation = baseRotation;
            transform.localScale = baseScale;
        }

        // Exact, per component: a component nothing touched reads back the very same number
        Vector3 Base(Vector3 now, Vector3 wrote, Vector3 before)
        {
            if (!written)
                return now;
            return new Vector3(now.x == wrote.x ? before.x : now.x, now.y == wrote.y ? before.y : now.y,
                now.z == wrote.z ? before.z : now.z);
        }

        public Vector3 BaseScale => baseScale;

        /// <summary>Remembers what was written (as the transform holds it)</summary>
        public void End()
        {
            written = true;
            wrotePosition = transform.localPosition;
            wroteRotation = transform.localRotation;
            wroteScale = transform.localScale;
        }

        /// <summary>Puts back the base of every channel nothing rewrote since, and forgets the bone</summary>
        public void Restore()
        {
            if (transform != null && written)
            {
                Vector3 position = transform.localPosition, scale = transform.localScale;
                transform.localPosition = Back(position, wrotePosition, basePosition);
                if (transform.localRotation.Equals(wroteRotation))
                    transform.localRotation = baseRotation;
                transform.localScale = Back(scale, wroteScale, baseScale);
            }
            written = false;
        }

        static Vector3 Back(Vector3 now, Vector3 wrote, Vector3 rest)
        {
            return new Vector3(now.x == wrote.x ? rest.x : now.x, now.y == wrote.y ? rest.y : now.y,
                now.z == wrote.z ? rest.z : now.z);
        }
    }

    readonly Transform root;
    readonly Bone torso, head, tail, tailIK, hat;
    readonly Transform handLeft, handRight;
    readonly Bone[] bones;   // the ones found, parents before children

    // The ghost's axes and its size at rest, in the Animator's space
    readonly Vector3 upLocal, forwardLocal, rightLocal, sizeLocal;
    readonly int tallAxis;
    readonly bool restFromBind;

    // The body's renderers (body, horns, eyes, eyelids: every one under the model but the hat's)
    readonly Renderer[] body;
    readonly bool[] dithers;
    readonly bool[] hadBlock;
    readonly bool[] hadDither;      // the block held its own _DitherThreshold when the fade began
    readonly float[] ditherWas;     // that value, or the material's own when it held none
    readonly bool[] wasOff;
    readonly ShadowCastingMode[] shadows;
    readonly Renderer hatLook;
    bool hatWasOff;
    readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
    float fade, hatFade;

    public Transform Torso => torso.transform;
    public Transform Head => head.transform;
    /// <summary>Null when the model has none</summary>
    public Transform Tail => tail?.transform;
    public Transform TailIK => tailIK?.transform;
    public Transform Hat => hat?.transform;
    public Transform HandLeft => handLeft;
    public Transform HandRight => handRight;

    /// <summary>The ghost's up as it stood at rest, in the world now</summary>
    public Vector3 Up => root.TransformDirection(upLocal);
    public Vector3 Forward => root.TransformDirection(forwardLocal);
    public Vector3 Right => root.TransformDirection(rightLocal);
    /// <summary>U: the rest distance from Torso to Head, in the world now</summary>
    public float U => root.TransformVector(sizeLocal).magnitude;
    /// <summary>The Torso's local axis (0 x, 1 y, 2 z) closest to up: Tall scales it, Wide the other two</summary>
    public int TallAxis => tallAxis;
    /// <summary>True when the axes came from the skin's bind pose (false: from the pose the bones held at the start)</summary>
    public bool RestFromBind => restFromBind;

    /// <summary>The rig of the ghost under this Animator (null without a Torso and a Head)</summary>
    public static GhostRig Find(Animator animator)
    {
        if (animator == null)
            return null;
        Transform root = animator.transform;
        Transform torso = root.Find(TORSO);
        Transform head = root.Find(HEAD);
        if (torso == null || head == null)
            return null;
        return new GhostRig(root, torso, head);
    }

    GhostRig(Transform root, Transform torsoBone, Transform headBone)
    {
        this.root = root;
        torso = new Bone(torsoBone);
        head = new Bone(headBone);
        Transform found = root.Find(TAIL);
        tail = found != null ? new Bone(found) : null;
        found = root.Find(TAIL_IK);
        tailIK = found != null ? new Bone(found) : null;
        found = root.Find(HAT);
        hat = found != null ? new Bone(found) : null;
        handLeft = root.Find(HAND_LEFT);
        handRight = root.Find(HAND_RIGHT);

        int count = 2 + (tail != null ? 1 : 0) + (tailIK != null ? 1 : 0) + (hat != null ? 1 : 0);
        bones = new Bone[count];
        int i = 0;
        bones[i++] = torso;
        if (tail != null) bones[i++] = tail;
        bones[i++] = head;
        if (tailIK != null) bones[i++] = tailIK;
        if (hat != null) bones[i++] = hat;

        // The axes, from the rest pose: the skin's bind pose when a skin holds the bones, else the pose it stands in now
        Rest rest = new Rest(root, torsoBone, headBone, tail?.transform, handLeft, handRight);
        restFromBind = rest.FromBind;
        Vector3 size = rest.Head - rest.Torso;
        Vector3 up = size.sqrMagnitude > 1e-12f ? size.normalized : root.up;
        Vector3 from = tail != null ? rest.Tail : rest.Torso;
        Vector3 forward = handLeft != null && handRight != null
            ? (rest.HandLeft + rest.HandRight) * 0.5f - from
            : root.forward;
        forward -= up * Vector3.Dot(forward, up);
        if (forward.sqrMagnitude < 1e-10f)
        {
            forward = root.forward - up * Vector3.Dot(root.forward, up);
            if (forward.sqrMagnitude < 1e-10f)
                forward = Vector3.Cross(root.right, up);
        }
        forward.Normalize();
        Vector3 right = Vector3.Cross(up, forward);

        upLocal = root.InverseTransformDirection(up);
        forwardLocal = root.InverseTransformDirection(forward);
        rightLocal = root.InverseTransformDirection(right);
        sizeLocal = root.InverseTransformVector(size);

        Vector3 upInTorso = rest.InTorso(up);
        float x = Mathf.Abs(upInTorso.x), y = Mathf.Abs(upInTorso.y), z = Mathf.Abs(upInTorso.z);
        tallAxis = x >= y && x >= z ? 0 : (y >= z ? 1 : 2);

        // The renderers
        hatLook = hat != null ? hat.transform.GetComponent<Renderer>() : null;
        Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
        int bodyCount = 0;
        foreach (Renderer look in all)
            if (hat == null || !look.transform.IsChildOf(hat.transform))
                bodyCount++;
        body = new Renderer[bodyCount];
        dithers = new bool[bodyCount];
        hadBlock = new bool[bodyCount];
        hadDither = new bool[bodyCount];
        ditherWas = new float[bodyCount];
        wasOff = new bool[bodyCount];
        shadows = new ShadowCastingMode[bodyCount];
        int b = 0;
        foreach (Renderer look in all)
        {
            if (hat != null && look.transform.IsChildOf(hat.transform))
                continue;
            body[b] = look;
            foreach (Material material in look.sharedMaterials)
                if (material != null && material.HasProperty(DITHER))
                    dithers[b] = true;
            b++;
        }
    }

    /// <summary>
    /// The bones' rest positions in the world now (and the Torso's rest turn): from the bind pose of the skin that
    /// holds the Torso, the Head and every other bone given, else from the bones as they stand
    /// </summary>
    struct Rest
    {
        public Vector3 Torso, Head, Tail, HandLeft, HandRight;
        readonly Transform torso;
        readonly Transform skin;
        readonly Matrix4x4 torsoBind;   // the skin's space to the Torso's, at bind
        readonly bool bound;

        public Rest(Transform root, Transform torsoBone, Transform headBone, Transform tailBone, Transform left,
            Transform right)
        {
            torso = torsoBone;
            skin = null;
            torsoBind = Matrix4x4.identity;
            bound = false;
            Torso = torsoBone.position;
            Head = headBone.position;
            Tail = tailBone != null ? tailBone.position : Torso;
            HandLeft = left != null ? left.position : Torso;
            HandRight = right != null ? right.position : Torso;

            foreach (SkinnedMeshRenderer look in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Mesh mesh = look.sharedMesh;
                Transform[] bones = look.bones;
                if (mesh == null || bones == null || mesh.bindposes.Length != bones.Length)
                    continue;
                Matrix4x4[] binds = mesh.bindposes;
                int t = System.Array.IndexOf(bones, torsoBone), h = System.Array.IndexOf(bones, headBone);
                int tl = tailBone != null ? System.Array.IndexOf(bones, tailBone) : -2;
                int l = left != null ? System.Array.IndexOf(bones, left) : -2;
                int r = right != null ? System.Array.IndexOf(bones, right) : -2;
                if (t < 0 || h < 0 || tl == -1 || l == -1 || r == -1)
                    continue;
                skin = look.transform;
                torsoBind = binds[t];
                bound = true;
                Torso = At(look.transform, binds[t]);
                Head = At(look.transform, binds[h]);
                Tail = tl >= 0 ? At(look.transform, binds[tl]) : Torso;
                HandLeft = l >= 0 ? At(look.transform, binds[l]) : Torso;
                HandRight = r >= 0 ? At(look.transform, binds[r]) : Torso;
                break;
            }
        }

        /// <summary>True when the rest came from a skin's bind pose</summary>
        public bool FromBind => bound;

        // A bone's origin at bind, in the world now: its bind pose maps the skin's space to the bone's
        static Vector3 At(Transform skin, Matrix4x4 bind)
        {
            return skin.TransformPoint(bind.inverse.MultiplyPoint3x4(Vector3.zero));
        }

        /// <summary>A world direction in the Torso's own axes at rest (normalised)</summary>
        public Vector3 InTorso(Vector3 direction)
        {
            if (!bound)
                return torso.InverseTransformDirection(direction);
            return torsoBind.MultiplyVector(skin.InverseTransformDirection(direction)).normalized;
        }
    }

    /// <summary>
    /// Takes the hat's rest pose (when a move begins): as it is now, unless it still holds what Apply wrote
    /// </summary>
    public void BeginHat()
    {
        if (hat == null)
            return;
        hat.Begin();
        hat.End();
    }

    /// <summary>The pose on top of the Animator's. Rest puts everything back (as Clear)</summary>
    public void Apply(GhostPose pose)
    {
        if (pose.IsRest(1e-5f))
        {
            Clear();
            return;
        }

        for (int i = 0; i < bones.Length; i++)
            bones[i].Begin();

        Vector3 up = Up, forward = Forward, right = Right;
        float u = U;

        // The body: turned about the Torso's pivot, moved, stretched and squashed, smaller as it fades
        Transform torsoT = torso.transform;
        Vector3 pivot = torsoT.position;
        Quaternion turn = Quaternion.AngleAxis(pose.Yaw, up) * Quaternion.AngleAxis(pose.Pitch, right)
            * Quaternion.AngleAxis(pose.Roll, -forward);
        Vector3 offset = (up * pose.Lift + forward * pose.Push + right * pose.Shift) * u;
        float shrink = 1f - SHRINK * pose.Fade * pose.Fade;

        // Tail1 isn't under the Torso: it moves, turns and squashes with the body so the tail never stretches
        if (tail != null)
        {
            Transform tailT = tail.transform;
            Vector3 d = tailT.position - pivot;
            float along = Vector3.Dot(d, up);
            Vector3 across = d - up * along;
            d = (up * (along * pose.Tall) + across * pose.Wide) * shrink;
            tailT.SetPositionAndRotation(pivot + offset + turn * d, turn * tailT.rotation);
            tailT.localScale = tail.BaseScale * shrink;
        }

        torsoT.SetPositionAndRotation(pivot + offset, turn * torsoT.rotation);
        Vector3 scale = torso.BaseScale;
        for (int axis = 0; axis < 3; axis++)
            scale[axis] *= (axis == tallAxis ? pose.Tall : pose.Wide) * shrink;
        torsoT.localScale = scale;

        // The ghost's axes as the body now stands
        Vector3 up2 = turn * up, forward2 = turn * forward, right2 = turn * right;

        // The head, about its own pivot
        Transform headT = head.transform;
        Quaternion headTurn = Quaternion.AngleAxis(pose.HeadYaw, up2) * Quaternion.AngleAxis(pose.HeadPitch, right2)
            * Quaternion.AngleAxis(pose.HeadRoll, -forward2);
        headT.SetPositionAndRotation(headT.position + up2 * (pose.HeadPop * u), headTurn * headT.rotation);
        // The head (and the hat on it) keeps its shape when the body stretches or squashes: each of its axes is
        // scaled back by how much the Torso's Tall and Wide stretch it (exact when its axes line up with the Torso's)
        if (pose.Tall != 1f || pose.Wide != 1f)
        {
            Quaternion inTorso = headT.localRotation;
            Vector3 headScale = head.BaseScale;
            for (int axis = 0; axis < 3; axis++)
            {
                Vector3 along = Vector3.zero;
                along[axis] = 1f;
                along = inTorso * along;
                for (int i = 0; i < 3; i++)
                    along[i] *= i == tallAxis ? pose.Tall : pose.Wide;
                headScale[axis] /= Mathf.Max(TINY, along.magnitude);
            }
            headT.localScale = headScale;
        }

        // The tail swings whole about its root: Tail1 and the TailIK (with Tail3) about Tail1's pivot
        if (tail != null && (pose.TailUp != 0f || pose.TailWag != 0f))
        {
            Quaternion swing = Quaternion.AngleAxis(-pose.TailWag, up2) * Quaternion.AngleAxis(pose.TailUp, right2);
            Transform tailT = tail.transform;
            Vector3 tailRoot = tailT.position;
            tailT.rotation = swing * tailT.rotation;
            if (tailIK != null)
            {
                Transform ik = tailIK.transform;
                ik.SetPositionAndRotation(tailRoot + swing * (ik.position - tailRoot), swing * ik.rotation);
            }
        }

        // The hat, on top of its rest pose. It doesn't shrink with the fading body: it stays its size, where the head
        // would be (the Torso's uniform shrink scales everything under it about the Torso's position), and has its own fade
        if (hat != null)
        {
            Transform hatT = hat.transform;
            Vector3 body = torsoT.position;
            Vector3 at = body + (hatT.position - body) / shrink + up2 * (pose.HatLift * u);
            Quaternion hatTurn = Quaternion.AngleAxis(pose.HatSpin, up2) * Quaternion.AngleAxis(pose.HatTip, right2);
            hatT.SetPositionAndRotation(at, hatTurn * hatT.rotation);
            hatT.localScale = hat.BaseScale * (Mathf.Max(TINY, 1f - pose.HatFade) / shrink);
        }

        for (int i = 0; i < bones.Length; i++)
            bones[i].End();

        SetFade(Mathf.Clamp01(pose.Fade));
        SetHatFade(Mathf.Clamp01(pose.HatFade));
    }

    /// <summary>
    /// Everything back: the bones nothing rewrote (the hat, Head) to their base, the renderers as they were. The
    /// Animator owns the bones again
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < bones.Length; i++)
            bones[i].Restore();
        SetFade(0f);
        SetHatFade(0f);
    }

    /// <summary>Dithers the body away (where its shader can), drops its shadow halfway, and switches it off at the end</summary>
    void SetFade(float value)
    {
        if (value == fade)
            return;
        bool starting = fade == 0f;
        fade = value;
        for (int i = 0; i < body.Length; i++)
        {
            Renderer look = body[i];
            if (look == null)
                continue;
            if (starting)
            {
                hadBlock[i] = look.HasPropertyBlock();
                wasOff[i] = look.forceRenderingOff;
                shadows[i] = look.shadowCastingMode;
                // What the block held before, so only _DitherThreshold is put back after (other systems' values stay)
                hadDither[i] = false;
                ditherWas[i] = MaterialDither(look);
                if (hadBlock[i])
                {
                    look.GetPropertyBlock(block);
                    if (block.HasFloat(DITHER))
                    {
                        hadDither[i] = true;
                        ditherWas[i] = block.GetFloat(DITHER);
                    }
                }
            }

            if (dithers[i])
            {
                if (value == 0f && !hadBlock[i])
                {
                    // It had no block: the fade's was the only one (a block can't drop a single value)
                    look.SetPropertyBlock(null);
                }
                else
                {
                    // Read back first, so whatever else is in the block now stays; at 0 its own value goes back
                    look.GetPropertyBlock(block);
                    block.SetFloat(DITHER, value == 0f ? ditherWas[i] : 1f - value);
                    look.SetPropertyBlock(block);
                }
            }
            look.shadowCastingMode = value > NO_SHADOW ? ShadowCastingMode.Off : shadows[i];
            look.forceRenderingOff = wasOff[i] || value >= GONE;
        }
    }

    // The renderer's own _DitherThreshold, from its first material that has one (1 = all drawn)
    static float MaterialDither(Renderer look)
    {
        foreach (Material material in look.sharedMaterials)
            if (material != null && material.HasProperty(DITHER))
                return material.GetFloat(DITHER);
        return 1f;
    }

    /// <summary>The hat's shader can't dither: it shrinks (in Apply) and is switched off at the end</summary>
    void SetHatFade(float value)
    {
        if (value == hatFade)
            return;
        if (hatFade == 0f && hatLook != null)
            hatWasOff = hatLook.forceRenderingOff;
        hatFade = value;
        if (hatLook != null)
            hatLook.forceRenderingOff = hatWasOff || value >= GONE;
    }
}

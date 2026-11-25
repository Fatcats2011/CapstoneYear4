using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// What a ghost carries, as GhostProps sees it: the visible mesh of a parcel on its scooter, and whether it still has it
/// (PlayerEmotes answers from its OrderHandler; captures from a package at the order slot)
/// </summary>
public interface IGhostCarry
{
    /// <summary>The visible mesh of a parcel the ghost carries now (null: none)</summary>
    Transform Parcel();

    /// <summary>Whether the ghost still carries the parcel this mesh shows</summary>
    bool Carries(Transform mesh);
}

/// <summary>
/// The props in a ghost's hands while a move plays (GhostMove.Prop). Cosmetic only. Run by GhostEmotes, after the pose.
/// Parcel: the parcel the ghost carries (IGhostCarry) leaves its spot on the rack for the middle of the hands over
/// PARCEL_IN seconds, arcing up over the hat on the way (and on the way back: PARCEL_ARC, PARCEL_OVER), follows them (ParcelSpin: spins about up over the right hand) and goes back over the move's last
/// PARCEL_OUT seconds, or over PARCEL_CUT when a move without it interrupts. It keeps its parent; on its way to the hands it
/// shrinks to PARCEL_HELD of its size on the rack (a big bag would hide the head), growing back on the way back. Its world
/// position, turn and local scale are written; the tweens running on it are paused meanwhile, and its local position, turn
/// and scale go back exactly as they were before they resume. A parcel move that follows another carries on with the same parcel.
/// When the parcel leaves the ghost meanwhile (delivered, stolen, dropped, destroyed), it is let go at once: nothing is
/// put back that someone else set since, and only the tweens this paused that are still alive resume.
/// Crown: a camera-facing sprite of the winner's crown (RematchCrown.Art), CROWN_WIDE U across, made the first time it is
/// needed and kept (hidden) under the ghost: it grows out of the head, goes to the hands' middle (Crown) or the left
/// hand (CrownLeft) by the move's PropIn, stays there until PropOut, goes back to the head and shrinks away by the end.
/// It grows to CROWN_HELD times its size on the way to the hands (as the hands swell), so it reads at the podium's size
/// </summary>
public sealed class GhostProps
{
    /// <summary>Seconds: the parcel goes to the hands over the first, back over a move's last, back after an interrupt</summary>
    internal const float PARCEL_IN = 0.3f, PARCEL_OUT = 0.3f, PARCEL_CUT = 0.12f;
    /// <summary>
    /// In U: the parcel's path between the rack and the hands arcs up at least PARCEL_ARC (at its middle), and high
    /// enough that at its middle the parcel's bottom (its box, at its size there) passes PARCEL_OVER above the Head
    /// (over the hat), so it never goes through the head or the body, however big the parcel
    /// </summary>
    internal const float PARCEL_ARC = 1.5f, PARCEL_OVER = 2.0f;
    /// <summary>The parcel's size in the hands, as a share of its size on the rack</summary>
    internal const float PARCEL_HELD = 0.85f;
    /// <summary>In U: how far over the right hand the spun parcel's bottom sits</summary>
    const float SPIN_ABOVE = 0.15f;
    /// <summary>Degrees a second the spun parcel turns</summary>
    const float SPIN_SPEED = 540f;
    /// <summary>
    /// In U: the crown's width, and how far above the hands' point and the Head it sits (over the head it is above the
    /// hat, so it grows out of and shrinks back into the air over the hat, never in front of the face)
    /// </summary>
    internal const float CROWN_WIDE = 1.6f;
    internal const float CROWN_ABOVE_HANDS = 0.55f, CROWN_ABOVE_HEAD = 2.3f;
    /// <summary>The crown's size in the hands, times its size at the head (it grows on the way; its lift over the hands too)</summary>
    internal const float CROWN_HELD = 1.4f;
    /// <summary>The share of the crown's way out (and back) spent growing (and shrinking) at the head</summary>
    const float CROWN_GROW = 0.35f;

    readonly Transform owner;
    readonly GhostRig rig;

    /// <summary>What the ghost carries (null: it never holds a parcel)</summary>
    public IGhostCarry Carry;

    // The parcel held now (null when none), and what it was before
    Transform parcel;
    Transform parcelParent;
    Vector3 parcelPosition;          // its local position and turn as it was
    Quaternion parcelRotation;
    Vector3 parcelScale;             // its local scale as it was
    Quaternion parcelInTorso;        // its turn in the Torso's, as it was taken (it turns with the body)
    Vector3 parcelCentre;            // its box's middle, in its own space (the hands hold the middle, not the pivot)
    Vector3 parcelExtents;           // half its box, along its own axes, at its size in the world
    readonly List<Tween> paused = new List<Tween>();
    bool parcelPlaying;              // the move playing holds it (false: going back after an interrupt)
    GhostProp parcelKind;
    float parcelWeight;              // 0 on the rack, 1 in the hands
    float parcelFrom;                // the weight when the move began
    float spin;
    Vector3 wrotePosition;           // its local position and turn as last written
    Quaternion wroteRotation;
    Vector3 wroteScale;

    // The crown
    SpriteRenderer crown;
    bool crownPlaying;

    static readonly List<GhostProps> crowned = new List<GhostProps>();

    public GhostProps(Transform owner, GhostRig rig)
    {
        this.owner = owner;
        this.rig = rig;
    }

    /// <summary>The parcel's mesh in the hands now, or on its way there or back (null when none)</summary>
    internal Transform Parcel => parcel;

    /// <summary>How far the parcel is from the rack to the hands (0 to 1)</summary>
    internal float ParcelWeight => parcel != null ? parcelWeight : 0f;

    /// <summary>The crown (null until one was needed)</summary>
    internal SpriteRenderer Crown => crown;

    /// <summary>Whether the crown shows now</summary>
    internal bool CrownShown => crown != null && crown.enabled;

    bool HasHands => rig != null && rig.HandLeft != null && rig.HandRight != null;

    /// <summary>A move begins (interrupting or not)</summary>
    public void Begin(GhostMove move)
    {
        // The parcel: a parcel move holds the one in hand already, or takes what the ghost carries now
        if (move != null && move.HoldsParcel && HasHands)
        {
            if (parcel != null && !StillCarried())
                LetGo();
            if (parcel == null && Carry != null)
                Take(Carry.Parcel());
            parcelPlaying = parcel != null;
            parcelFrom = parcelWeight;
            parcelKind = move.Prop;
        }
        else
        {
            parcelPlaying = false;
        }

        // The crown starts from the head each time
        crownPlaying = move != null && move.HoldsCrown && HasHands;
        if (!crownPlaying)
            HideCrown();
    }

    /// <summary>One frame, after the pose: playing is the move on now (null when none), time how far into it</summary>
    public void Step(GhostMove playing, float time, float dt)
    {
        StepParcel(playing, time, dt);
        StepCrown(playing, time);
    }

    /// <summary>Everything back at once: the parcel to the rack as it was, the crown hidden</summary>
    public void Stop()
    {
        if (parcel != null || paused.Count > 0)
        {
            if (parcel != null && StillCarried())
                PutBack();
            else
                LetGo();
        }
        parcelPlaying = false;
        crownPlaying = false;
        HideCrown();
    }

    /// <summary>Stop, and the crown goes</summary>
    public void Destroy()
    {
        Stop();
        if (crown != null)
        {
            if (Application.isPlaying)
                Object.Destroy(crown.gameObject);
            else
                Object.DestroyImmediate(crown.gameObject);
        }
        crown = null;
    }

    // ---- The parcel

    void Take(Transform mesh)
    {
        if (mesh == null)
            return;
        parcel = mesh;
        parcelParent = mesh.parent;
        parcelPosition = mesh.localPosition;
        parcelRotation = mesh.localRotation;
        parcelScale = mesh.localScale;
        wrotePosition = parcelPosition;
        wroteRotation = parcelRotation;
        wroteScale = parcelScale;
        parcelInTorso = Quaternion.Inverse(rig.Torso.rotation) * mesh.rotation;
        parcelWeight = 0f;
        spin = 0f;

        // Its box (the mesh's bounds, at its own size): the hands hold its middle
        parcelCentre = Vector3.zero;
        parcelExtents = Vector3.one * (0.3f * rig.U);
        MeshFilter filter = mesh.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            Bounds box = filter.sharedMesh.bounds;
            parcelCentre = box.center;
            Vector3 size = mesh.lossyScale;
            parcelExtents = new Vector3(Mathf.Abs(box.extents.x * size.x), Mathf.Abs(box.extents.y * size.y),
                Mathf.Abs(box.extents.z * size.z));
        }

        // Its tweens (the float and the bob) wait while it is in hand
        paused.Clear();
        List<Tween> tweens = DOTween.TweensByTarget(mesh, true);
        if (tweens != null)
        {
            foreach (Tween tween in tweens)
            {
                if (tween == null || !tween.IsActive() || !tween.IsPlaying())
                    continue;
                tween.Pause();
                paused.Add(tween);
            }
        }
    }

    // It still has the parcel: the mesh is there, under the same parent, and the ghost still carries it
    bool StillCarried()
    {
        return parcel != null && parcel.parent == parcelParent && Carry != null && Carry.Carries(parcel);
    }

    void StepParcel(GhostMove playing, float time, float dt)
    {
        if (parcel == null && paused.Count == 0)
            return;
        if (!StillCarried())
        {
            LetGo();
            return;
        }

        if (parcelPlaying && playing != null && playing.HoldsParcel)
        {
            float up = Mathf.Lerp(parcelFrom, 1f, Mathf.SmoothStep(0f, 1f, time / PARCEL_IN));
            parcelWeight = up * Mathf.SmoothStep(0f, 1f, (playing.Length - time) / PARCEL_OUT);
            parcelKind = playing.Prop;
        }
        else
        {
            parcelPlaying = false;
            parcelWeight = Mathf.Max(0f, parcelWeight - dt / PARCEL_CUT);
            if (parcelWeight <= 0f)
            {
                PutBack();
                return;
            }
        }
        spin += SPIN_SPEED * dt;
        Place(parcelWeight);
    }

    // Between its rack spot (as it was, wherever the scooter is now) and the hands
    void Place(float weight)
    {
        float k = Mathf.SmoothStep(0f, 1f, weight);
        // Smaller in the hands: its box (measured on the rack) shrinks with it
        float shrink = Mathf.Lerp(1f, PARCEL_HELD, k);
        parcel.localScale = parcelScale * shrink;
        Vector3 up = rig.Up;
        Quaternion held = rig.Torso.rotation * parcelInTorso;
        Vector3 at;
        if (parcelKind == GhostProp.ParcelSpin)
        {
            held = Quaternion.AngleAxis(spin, up) * held;
            // Its bottom just over the hand: half its height along up, as it is turned now
            float half = shrink * (Mathf.Abs(Vector3.Dot(held * new Vector3(parcelExtents.x, 0f, 0f), up))
                + Mathf.Abs(Vector3.Dot(held * new Vector3(0f, parcelExtents.y, 0f), up))
                + Mathf.Abs(Vector3.Dot(held * new Vector3(0f, 0f, parcelExtents.z), up)));
            at = rig.HandRight.position + up * (SPIN_ABOVE * rig.U + half);
        }
        else
        {
            at = (rig.HandLeft.position + rig.HandRight.position) * 0.5f;
        }
        at -= held * Vector3.Scale(parcelCentre, parcel.lossyScale);

        Vector3 rackPosition = parcelParent != null ? parcelParent.TransformPoint(parcelPosition) : parcelPosition;
        Quaternion rackRotation = parcelParent != null ? parcelParent.rotation * parcelRotation : parcelRotation;
        // Up and over the hat on the way (the hands are still low inside the body when it sets off)
        Vector3 straight = Vector3.Lerp(rackPosition, at, k);
        Vector3 middle = (rackPosition + at) * 0.5f;
        // Half its height along up at the middle of the trip (turned and sized as it is there)
        Quaternion midTurn = Quaternion.Slerp(rackRotation, held, 0.5f);
        float midHalf = Mathf.Lerp(1f, PARCEL_HELD, 0.5f) * (Mathf.Abs(Vector3.Dot(midTurn * new Vector3(parcelExtents.x, 0f, 0f), up))
            + Mathf.Abs(Vector3.Dot(midTurn * new Vector3(0f, parcelExtents.y, 0f), up))
            + Mathf.Abs(Vector3.Dot(midTurn * new Vector3(0f, 0f, parcelExtents.z), up)));
        float arc = Mathf.Max(PARCEL_ARC * rig.U, Vector3.Dot(rig.Head.position - middle, up) + PARCEL_OVER * rig.U + midHalf);
        Vector3 path = straight + up * (arc * Mathf.Sin(Mathf.PI * k));
        parcel.SetPositionAndRotation(path, Quaternion.Slerp(rackRotation, held, k));
        wrotePosition = parcel.localPosition;
        wroteRotation = parcel.localRotation;
        wroteScale = parcel.localScale;
    }

    // Back on the rack exactly as it was, and its tweens go on from where they waited
    void PutBack()
    {
        if (parcel != null)
        {
            parcel.localPosition = parcelPosition;
            parcel.localRotation = parcelRotation;
            parcel.localScale = parcelScale;
        }
        Resume();
        parcel = null;
        parcelWeight = 0f;
        parcelPlaying = false;
    }

    // The parcel left the ghost: no more following. What nobody set since goes back (it is its own parent's, wherever
    // the parcel is now), and the tweens this paused that are still alive go on
    void LetGo()
    {
        if (parcel != null && parcel.parent == parcelParent)
        {
            if ((parcel.localPosition - wrotePosition).sqrMagnitude < 1e-8f)
                parcel.localPosition = parcelPosition;
            if (Quaternion.Angle(parcel.localRotation, wroteRotation) < 1e-3f)
                parcel.localRotation = parcelRotation;
        }
        // Its size goes back wherever it is now, unless someone else changed it since
        if (parcel != null && (parcel.localScale - wroteScale).sqrMagnitude < 1e-8f)
            parcel.localScale = parcelScale;
        Resume();
        parcel = null;
        parcelWeight = 0f;
        parcelPlaying = false;
    }

    void Resume()
    {
        foreach (Tween tween in paused)
            if (tween != null && tween.IsActive())
                tween.Play();
        paused.Clear();
    }

    // ---- The crown

    void StepCrown(GhostMove playing, float time)
    {
        if (!crownPlaying)
            return;
        if (playing == null || !playing.HoldsCrown)
        {
            crownPlaying = false;
            HideCrown();
            return;
        }
        if (!MakeCrown())
        {
            crownPlaying = false;
            return;
        }

        float share = Mathf.Clamp01(time / playing.Length);
        float u = rig.U;
        Vector3 up = rig.Up;
        Vector3 head = rig.Head.position + up * (CROWN_ABOVE_HEAD * u);
        Vector3 hands = playing.Prop == GhostProp.CrownLeft
            ? rig.HandLeft.position
            : (rig.HandLeft.position + rig.HandRight.position) * 0.5f;
        hands += up * (CROWN_ABOVE_HANDS * CROWN_HELD * u);

        // Out: grows at the head, then goes to the hands. Back: goes to the head, then shrinks away
        float size, way;
        float into = playing.PropIn > 0f ? share / playing.PropIn : 1f;
        float back = playing.PropOut < 1f ? (share - playing.PropOut) / (1f - playing.PropOut) : 0f;
        if (share < playing.PropIn)
        {
            size = Mathf.SmoothStep(0f, 1f, into / CROWN_GROW);
            way = Mathf.SmoothStep(0f, 1f, (into - CROWN_GROW) / (1f - CROWN_GROW));
        }
        else if (share > playing.PropOut)
        {
            way = 1f - Mathf.SmoothStep(0f, 1f, back / (1f - CROWN_GROW));
            size = 1f - Mathf.SmoothStep(0f, 1f, (back - (1f - CROWN_GROW)) / CROWN_GROW);
        }
        else
        {
            size = 1f;
            way = 1f;
        }

        Transform t = crown.transform;
        t.position = Vector3.Lerp(head, hands, way);
        float across = crown.sprite != null ? crown.sprite.bounds.size.x : 1f;
        float held = Mathf.Lerp(1f, CROWN_HELD, way);
        SetWorldScale(t, Mathf.Max(1e-4f, size) * held * CROWN_WIDE * u / Mathf.Max(1e-4f, across));
        Camera view = Camera.main;
        if (view != null)
            t.rotation = view.transform.rotation;
        bool show = size > 1e-3f;
        if (crown.enabled != show)
            crown.enabled = show;
        if (!crowned.Contains(this))
        {
            if (crowned.Count == 0)
                RenderPipelineManager.beginCameraRendering += FaceCamera;
            crowned.Add(this);
        }
    }

    bool MakeCrown()
    {
        if (crown != null)
            return true;
        Sprite art = RematchCrown.Art();
        if (art == null || owner == null)
            return false;
        GameObject holder = new GameObject("Emote Crown");
        holder.layer = owner.gameObject.layer;
        // A child of the ghost, so it goes with it
        holder.transform.SetParent(owner, false);
        crown = holder.AddComponent<SpriteRenderer>();
        crown.sprite = art;
        crown.enabled = false;
        return true;
    }

    void HideCrown()
    {
        if (crown != null && crown.enabled)
            crown.enabled = false;
        if (crowned.Remove(this) && crowned.Count == 0)
            RenderPipelineManager.beginCameraRendering -= FaceCamera;
    }

    // Each camera sees the crown face on (split screen: each player's camera)
    static void FaceCamera(ScriptableRenderContext context, Camera camera)
    {
        for (int i = crowned.Count - 1; i >= 0; i--)
        {
            GhostProps props = crowned[i];
            if (props.crown == null)
            {
                crowned.RemoveAt(i);
                continue;
            }
            props.crown.transform.rotation = camera.transform.rotation;
        }
        if (crowned.Count == 0)
            RenderPipelineManager.beginCameraRendering -= FaceCamera;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        RenderPipelineManager.beginCameraRendering -= FaceCamera;
        crowned.Clear();
    }

    /// <summary>A uniform world size under its parent, whatever the parent's own scale</summary>
    static void SetWorldScale(Transform t, float size)
    {
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(size / Mathf.Max(1e-6f, Mathf.Abs(parent.x)),
            size / Mathf.Max(1e-6f, Mathf.Abs(parent.y)), size / Mathf.Max(1e-6f, Mathf.Abs(parent.z)));
    }
}

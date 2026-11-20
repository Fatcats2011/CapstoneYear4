using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Plays the ghost's emotes (GhostMoves) on the bones of its model, on top of what its Animator plays. One move at a
/// time: a higher priority interrupts, and each action waits out its cooldown, counted from when it began
/// (GhostEmoteRules). Each action plays its versions in a shuffled round (never the same twice running), turning to a
/// random side. Sits on the Animator's GameObject; moves are put on in LateUpdate, after the Animator.
/// A move with a Clip also plays that state on the Animator's "Emotes" layer (an override layer masked to the head,
/// hands and fingers), easing the layer's weight up at the start and back down at the end; the layer rests on its
/// "Empty" state at weight 0 otherwise; a clip move that follows another cross-fades the hands across. The weight set
/// in a frame's LateUpdate is what the next frame's Animator uses.
/// A move that interrupts another eases the body over from where the interrupted one left it (BLEND); an entrance
/// starts hidden at once instead.
/// A move with a Prop has it in its hands (GhostProps: the parcel it carries, through Carry, or a crown)
/// </summary>
public class GhostEmotes : MonoBehaviour
{
    static readonly int ACTIONS = Enum.GetValues(typeof(GhostAction)).Length;

    /// <summary>The Animator layer the hand clips play on, and its resting state</summary>
    public const string LAYER = "Emotes";
    public const string EMPTY = "Empty";
    static readonly int EMPTY_HASH = Animator.StringToHash(EMPTY);
    /// <summary>Seconds: the layer's weight eases up over the first of these, down over the last, and out on an interrupt</summary>
    internal const float CLIP_IN = 0.12f, CLIP_OUT = 0.15f, CLIP_CUT = 0.1f;
    /// <summary>Seconds: one clip move's hands cross-fade into the next's over this, when the layer is already up</summary>
    internal const float CLIP_CROSS = 0.12f;
    /// <summary>Above this layer weight a clip that follows another cross-fades instead of starting at once</summary>
    const float CROSS_FROM = 0.05f;
    /// <summary>Seconds: a move that interrupts another eases the body over from the interrupted pose</summary>
    internal const float BLEND = 0.12f;

    /// <summary>The Zzz: a new z this often, each rising for this long</summary>
    const float ZZZ_EVERY = 0.7f, ZZZ_LIFE = 1.4f;
    /// <summary>In U: where a z starts over the Head, how far it rises, and its height as it starts and ends</summary>
    const float ZZZ_ABOVE = 0.9f, ZZZ_RISE = 1.6f, ZZZ_SMALL = 0.6f, ZZZ_BIG = 1.1f;
    const string BANGERS = "Fonts & Materials/Bangers SDF";

    GhostRig rig;
    GhostProps props;
    System.Random random;
    TitlePlaylist[] playlists;
    float[] cooldowns;

    GhostMove playing;
    float time;     // seconds into the move
    float side;     // +1 or -1: which way it turns
    bool resting;   // the move just ended: put the rest pose on once more

    Animator animator;
    int layer = -2;         // the Emotes layer's index: -1 none, -2 not looked up yet
    bool clipOn;            // a clip state is playing on the layer (Empty goes back on once the weight is down)
    bool clipPlaying;       // the move playing drives the layer (false: easing out after its end or an interrupt)
    float weight;           // the layer's weight as last set
    float weightFrom;       // the weight when the move began (eases up from there, so an interrupt doesn't snap)
    bool layerDirty;        // the layer couldn't be put back (the Animator was off): do it once it is bound again

    GhostPose shown = GhostPose.Rest;   // the pose last put on the bones
    GhostPose blendFrom;                // the interrupted move's pose, eased away from over BLEND
    bool blending;

    struct Zed
    {
        public Transform transform;
        public TextMeshPro text;
        public Vector3 from;    // where it started, in the ghost's space (it follows the ghost)
        public Vector3 up;      // the ghost's up, in the ghost's space
        public float size;
        public float age;
    }
    readonly List<Zed> zeds = new List<Zed>();
    float nextZed;

    /// <summary>The move playing (null when none)</summary>
    public GhostMove Playing => playing;

    /// <summary>
    /// A move began on a ghost (each one that actually starts, from Play or a preview). For debug captures (the trailer
    /// tool's emote log): nothing in the game listens
    /// </summary>
    public static event Action<GhostEmotes, GhostMove> Began;

    /// <summary>The rig it moves (null when the model has no Torso or Head)</summary>
    internal GhostRig Rig => rig;

    /// <summary>The props in its hands (null when the model has no rig)</summary>
    internal GhostProps Props => props;

    /// <summary>What the ghost carries, for the parcel moves to hold (null: they play without one)</summary>
    public IGhostCarry Carry
    {
        get { return props != null ? props.Carry : null; }
        set { if (props != null) props.Carry = value; }
    }

    /// <summary>The Emotes layer's weight as last set (0 when no clip plays)</summary>
    internal float ClipWeight => weight;

    /// <summary>The Emotes layer's index (-1 when the Animator has none)</summary>
    internal int ClipLayer => Layer();

    /// <summary>The emotes of the ghost under this Animator (added the first time)</summary>
    public static GhostEmotes On(Animator animator, System.Random random = null)
    {
        if (animator == null)
            return null;
        GhostEmotes emotes = animator.GetComponent<GhostEmotes>();
        if (emotes == null)
            emotes = animator.gameObject.AddComponent<GhostEmotes>();
        emotes.animator = animator;
        if (emotes.rig == null)
        {
            emotes.rig = GhostRig.Find(animator);
            if (emotes.rig != null)
                emotes.props = new GhostProps(emotes.transform, emotes.rig);
        }
        if (random != null || emotes.random == null)
        {
            emotes.random = random ?? new System.Random();
            emotes.playlists = new TitlePlaylist[ACTIONS];
        }
        if (emotes.cooldowns == null)
            emotes.cooldowns = new float[ACTIONS];
        return emotes;
    }

    /// <summary>
    /// Starts a version of the action. False when it has no versions, is cooling down, or something at least as
    /// important is playing
    /// </summary>
    public bool Play(GhostAction action)
    {
        GhostMove[] moves = GhostMoves.For(action);
        if (moves.Length == 0 || rig == null || cooldowns == null)
            return false;
        GhostAction? now = playing != null ? playing.Action : (GhostAction?)null;
        if (!GhostEmoteRules.CanStart(action, now, cooldowns[(int)action]))
            return false;

        TitlePlaylist list = playlists[(int)action];
        if (list == null)
            list = playlists[(int)action] = new TitlePlaylist(moves.Length, random);
        Begin(moves[list.Next()], random.Next(2) == 0 ? -1f : 1f);
        return true;
    }

    /// <summary>Whether Play(action) would start it now (it has versions, isn't cooling down, nothing as important plays)</summary>
    public bool CanPlay(GhostAction action)
    {
        if (GhostMoves.For(action).Length == 0 || rig == null || cooldowns == null)
            return false;
        GhostAction? now = playing != null ? playing.Action : (GhostAction?)null;
        return GhostEmoteRules.CanStart(action, now, cooldowns[(int)action]);
    }

    /// <summary>Plays this version, whatever is playing (previews and captures). Side is +1 or -1</summary>
    public void Play(GhostMove move, float side)
    {
        if (move == null)
        {
            Stop();
            return;
        }
        if (rig == null || cooldowns == null)
            return;
        Begin(move, side < 0f ? -1f : 1f);
    }

    void Begin(GhostMove move, float turnSide)
    {
        bool interrupting = playing != null;
        if (!interrupting)
            rig.BeginHat();
        if (interrupting && playing.Extra != move.Extra)
            ClearZeds();
        // An interrupt eases over from the pose on show; an entrance doesn't (it starts hidden)
        blending = interrupting && !move.Entrance;
        blendFrom = shown;
        playing = move;
        time = 0f;
        side = turnSide;
        resting = false;
        nextZed = 0f;
        cooldowns[(int)move.Action] = GhostEmoteRules.Cooldown(move.Action);

        // Its hand clip, when the Animator has the layer and the state; otherwise a clip still on eases out
        int index = move.Clip != null ? Layer() : -1;
        if (index >= 0 && animator.HasState(index, move.ClipHash))
        {
            // Following another clip still up on the layer: cross-fade the hands across instead of snapping them
            if (clipOn && weight > CROSS_FROM)
                animator.CrossFadeInFixedTime(move.ClipHash, CLIP_CROSS, index, 0f);
            else
                animator.Play(move.ClipHash, index, 0f);
            clipOn = true;
            clipPlaying = true;
            weightFrom = weight;
        }
        else
        {
            clipPlaying = false;
        }

        props.Begin(move);
        Began?.Invoke(this, move);

        // An entrance is hidden from its very first frame, not one frame later
        if (move.Entrance)
            Show(move.At(0f, side));
    }

    /// <summary>The pose on the bones, remembered (what an interrupt eases away from)</summary>
    void Show(GhostPose pose)
    {
        rig.Apply(pose);
        shown = pose;
    }

    /// <summary>Stops at once and puts everything back</summary>
    public void Stop()
    {
        playing = null;
        resting = false;
        blending = false;
        shown = GhostPose.Rest;
        ClipOff();
        if (rig != null)
            rig.Clear();
        if (props != null)
            props.Stop();
        ClearZeds();
    }

    /// <summary>The Emotes layer's index, looked up once the Animator is bound (-1 when it has none)</summary>
    int Layer()
    {
        if (layer == -2 && Bound())
            layer = animator.GetLayerIndex(LAYER);
        return layer >= 0 ? layer : -1;
    }

    bool Bound()
    {
        return animator != null && animator.isActiveAndEnabled && animator.isInitialized
            && animator.runtimeAnimatorController != null;
    }

    /// <summary>The layer's weight: up over CLIP_IN while a clip move plays, down over its last CLIP_OUT, out over CLIP_CUT after</summary>
    void StepClip(float dt)
    {
        if (!clipOn && weight == 0f)
            return;
        float goal;
        if (clipPlaying && playing != null)
        {
            float up = Mathf.Lerp(weightFrom, 1f, Mathf.SmoothStep(0f, 1f, time / CLIP_IN));
            goal = up * Mathf.SmoothStep(0f, 1f, (playing.Length - time) / CLIP_OUT);
        }
        else
        {
            clipPlaying = false;
            goal = Mathf.Max(0f, weight - dt / CLIP_CUT);
        }
        SetWeight(goal);
        if (goal <= 0f && !clipPlaying)
            ClipOff();
    }

    /// <summary>The layer back on Empty at weight 0, at once</summary>
    void ClipOff()
    {
        clipPlaying = false;
        if (weight != 0f)
            SetWeight(0f);
        if (clipOn)
        {
            clipOn = false;
            if (layer >= 0 && Bound())
                animator.Play(EMPTY_HASH, layer, 0f);
            else if (layer >= 0)
                layerDirty = true;
        }
    }

    void SetWeight(float value)
    {
        weight = value;
        if (layer >= 0 && Bound())
            animator.SetLayerWeight(layer, value);
        else if (layer >= 0)
            layerDirty = true;
    }

    /// <summary>
    /// The layer back on Empty at weight 0, when it was left up while the Animator was off (switched off mid move:
    /// the Animator can't take it then, and may keep its state when it comes back on)
    /// </summary>
    void CleanLayer()
    {
        if (!layerDirty || layer < 0 || !Bound())
            return;
        layerDirty = false;
        if (clipOn || weight > 0f)
            return;
        animator.SetLayerWeight(layer, 0f);
        animator.Play(EMPTY_HASH, layer, 0f);
    }

    /// <summary>Moves the clock and the cooldowns on; a move that runs out ends (the rest pose goes on next)</summary>
    internal void Tick(float dt)
    {
        if (cooldowns != null)
        {
            for (int i = 0; i < cooldowns.Length; i++)
                if (cooldowns[i] > 0f)
                    cooldowns[i] = Mathf.Max(0f, cooldowns[i] - dt);
        }

        if (playing == null)
            return;
        time += dt;
        if (time >= playing.Length)
        {
            playing = null;
            resting = true;
        }
    }

    /// <summary>One frame: Tick, then the pose on the bones (what LateUpdate does)</summary>
    internal void Step(float dt)
    {
        CleanLayer();
        Tick(dt);
        StepClip(dt);
        if (rig == null)
            return;

        if (playing != null)
        {
            GhostPose pose = playing.At(time / playing.Length, side);
            if (blending)
            {
                if (time >= BLEND)
                    blending = false;
                else
                    pose = GhostPose.Lerp(blendFrom, pose, Mathf.SmoothStep(0f, 1f, time / BLEND));
            }
            Show(pose);
        }
        else if (resting)
        {
            rig.Apply(GhostPose.Rest);
            rig.Clear();
            shown = GhostPose.Rest;
            blending = false;
            resting = false;
        }

        // The props after the pose: the hands are where they show this frame
        props.Step(playing, time, dt);
        StepZeds(dt);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        Began = null;
    }

    void LateUpdate()
    {
        Step(Time.deltaTime);
    }

    void OnEnable()
    {
        // Whatever was left from before it was switched off: nothing playing, the layer down (now, or once bound)
        playing = null;
        resting = false;
        blending = false;
        shown = GhostPose.Rest;
        clipOn = false;
        clipPlaying = false;
        weight = 0f;
        if (layer >= 0)
            layerDirty = true;
        CleanLayer();
    }

    void OnDisable()
    {
        Stop();
        // Bound() is false while the Animator goes off with it, so Stop couldn't reach the layer: it goes down once it
        // can (an Animator that keeps its state on disable would otherwise come back with the clip at weight 1)
        if (animator != null && layer >= 0)
            layerDirty = true;
    }

    void OnDestroy()
    {
        Stop();
        if (props != null)
            props.Destroy();
    }

    /// <summary>The Zzz: a z over the head every so often while a move with it plays, each rising, growing and fading</summary>
    void StepZeds(float dt)
    {
        if (playing != null && playing.Extra == GhostExtra.Zzz && Application.isPlaying)
        {
            nextZed -= dt;
            if (nextZed <= 0f)
            {
                nextZed += ZZZ_EVERY;
                MakeZed();
            }
        }

        if (zeds.Count == 0)
            return;
        Camera view = Camera.main;
        for (int i = zeds.Count - 1; i >= 0; i--)
        {
            Zed zed = zeds[i];
            zed.age += dt;
            if (zed.transform == null || zed.age >= ZZZ_LIFE)
            {
                if (zed.transform != null)
                    Destroy(zed.transform.gameObject);
                zeds.RemoveAt(i);
                continue;
            }
            float k = zed.age / ZZZ_LIFE;
            // In the ghost's space, so it follows the ghost; sized and turned to the camera in the world
            Vector3 up = transform.TransformDirection(zed.up);
            zed.transform.position = transform.TransformPoint(zed.from) + up * (zed.size * ZZZ_RISE * k);
            SetWorldScale(zed.transform, Mathf.Lerp(ZZZ_SMALL, ZZZ_BIG, k) * zed.size);
            if (view != null)
                zed.transform.rotation = view.transform.rotation;
            Color colour = zed.text.color;
            colour.a = 1f - k * k;
            zed.text.color = colour;
            zeds[i] = zed;
        }
    }

    void MakeZed()
    {
        float u = rig.U;
        Vector3 up = rig.Up;
        GameObject holder = new GameObject("Zzz");
        holder.layer = gameObject.layer;
        TextMeshPro text = holder.AddComponent<TextMeshPro>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(BANGERS);
        if (font != null)
            text.font = font;
        text.text = "z";
        text.fontSize = 10f;   // about one unit tall at scale 1: the scale sizes it
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.richText = false;
        text.color = Color.white;
        if (Application.isPlaying)
        {
            // White with a dark edge, so it reads against the sky and the ground from far away
            text.outlineWidth = 0.3f;
            text.outlineColor = new Color32(20, 20, 40, 255);
        }
        text.rectTransform.sizeDelta = new Vector2(2f, 2f);
        // A little to its side each time, so they don't stack
        Vector3 from = rig.Head.position + up * (ZZZ_ABOVE * u) + rig.Right * (0.15f * u * (zeds.Count % 2 == 0 ? 1f : -1f));
        // A child of the ghost, so it goes where the ghost goes (and goes with it)
        holder.transform.SetParent(transform, false);
        holder.transform.position = from;
        SetWorldScale(holder.transform, ZZZ_SMALL * u);
        Camera view = Camera.main;
        if (view != null)
            holder.transform.rotation = view.transform.rotation;
        // The mesh is otherwise built a frame later: a capture that renders in the same frame would see nothing
        text.ForceMeshUpdate();
        zeds.Add(new Zed
        {
            transform = holder.transform, text = text, from = transform.InverseTransformPoint(from),
            up = transform.InverseTransformDirection(up), size = u, age = 0f
        });
    }

    /// <summary>A uniform world size under the ghost, whatever the ghost's own scale</summary>
    static void SetWorldScale(Transform t, float size)
    {
        Vector3 parent = t.parent != null ? t.parent.lossyScale : Vector3.one;
        t.localScale = new Vector3(size / Mathf.Max(1e-6f, Mathf.Abs(parent.x)),
            size / Mathf.Max(1e-6f, Mathf.Abs(parent.y)), size / Mathf.Max(1e-6f, Mathf.Abs(parent.z)));
    }

    void ClearZeds()
    {
        foreach (Zed zed in zeds)
        {
            if (zed.transform == null)
                continue;
            if (Application.isPlaying)
                Destroy(zed.transform.gameObject);
            else
                DestroyImmediate(zed.transform.gameObject);
        }
        zeds.Clear();
    }
}

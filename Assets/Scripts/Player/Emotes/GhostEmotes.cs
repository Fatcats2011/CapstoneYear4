using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Plays the ghost's emotes (GhostMoves) on the bones of its model, on top of what its Animator plays. One move at a
/// time: a higher priority interrupts, and each action waits out its cooldown, counted from when it began
/// (GhostEmoteRules). Each action plays its versions in a shuffled round (never the same twice running), turning to a
/// random side; a ghost with no hat showing passes over the versions that need one (GhostMove.NeedsHat), and an action
/// with none left doesn't play. Sits on the Animator's GameObject; moves are put on in LateUpdate, after the Animator.
/// A move with a Clip also plays that state on the Animator's "Emotes" layer (an override layer masked to the head,
/// hands and fingers), easing the layer's weight up at the start and back down at the end; the layer rests on its
/// "Empty" state at weight 0 otherwise; a clip move that follows another cross-fades the hands across. The weight set
/// in a frame's LateUpdate is what the next frame's Animator uses. The hands (and fingers) also grow with that weight,
/// up to 1 + GhostRig.HAND_SWELL times their size, so they read at game size (GhostRig.SetHands, back exactly at 0).
/// A move that interrupts another eases the body over from where the interrupted one left it (BLEND); an entrance
/// starts hidden at once instead.
/// A move with a Prop has it in its hands (GhostProps: the parcel it carries, through Carry, or a crown).
/// A move can play smaller (Play's strength: a wall hit's sway by its speed), and end early, easing back to rest
/// (EaseOut: a Wheee as the ghost lands)
/// </summary>
public class GhostEmotes : MonoBehaviour
{
    static readonly int ACTIONS = Enum.GetValues(typeof(GhostAction)).Length;

    /// <summary>The Animator layer the hand clips play on, and its resting state</summary>
    public const string LAYER = "Emotes";
    public const string EMPTY = "Empty";
    static readonly int EMPTY_HASH = Animator.StringToHash(EMPTY);
    /// <summary>Seconds: the layer's weight eases up over the first of these, down over the last, and out on an interrupt</summary>
    internal const float CLIP_IN = 0.25f, CLIP_OUT = 0.2f, CLIP_CUT = 0.1f;
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
    int[] picked;           // each action's version picked last (-1: none yet)
    float[] cooldowns;

    GhostMove playing;
    float time;     // seconds into the move
    float side;     // +1 or -1: which way it turns
    bool resting;   // the move just ended: put the rest pose on once more
    float strength = 1f;    // how big the move plays: its pose eased this far from rest (1: in full)
    float easeAt = -1f;     // seconds into the move it began easing out early (-1: it plays to its end)
    float easeFor;          // seconds that ease takes

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

    /// <summary>The camera the Zzz turn to face (null: the main camera). The title screen's stage films with its own</summary>
    public Camera View { get; set; }

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
            emotes.picked = new int[ACTIONS];
            for (int i = 0; i < ACTIONS; i++)
                emotes.picked[i] = -1;
        }
        if (emotes.cooldowns == null)
            emotes.cooldowns = new float[ACTIONS];
        return emotes;
    }

    /// <summary>Whether the ghost wears a hat that shows (GhostRig.HasHat): the NeedsHat versions are picked only then</summary>
    public bool ShowsHat => rig != null && rig.HasHat;

    /// <summary>Whether this ghost can show the version now: it doesn't need a hat, or the ghost wears one</summary>
    public bool CanShow(GhostMove move)
    {
        return move != null && (!move.NeedsHat || ShowsHat);
    }

    /// <summary>
    /// Starts a version of the action. False when it has no versions it can show (a hatless ghost skips the NeedsHat
    /// ones), is cooling down, or something at least as important is playing
    /// </summary>
    public bool Play(GhostAction action)
    {
        return Play(action, 1f);
    }

    /// <summary>
    /// As Play(action), at a size: its pose eased from rest by strength (0 to 1; a wall hit's sway by its speed)
    /// </summary>
    public bool Play(GhostAction action, float size)
    {
        GhostMove[] moves = GhostMoves.For(action);
        if (!CanPlay(action))
            return false;
        int pick = Pick(action, moves);
        if (pick < 0)
            return false;
        Begin(moves[pick], random.Next(2) == 0 ? -1f : 1f);
        strength = Mathf.Clamp01(size);
        return true;
    }

    /// <summary>
    /// Ends the action early, when it plays: its pose (and hand clip) eases back to rest over this many seconds from now,
    /// and it ends then (a Wheee as the ghost lands). False when it isn't playing, or already easing out
    /// </summary>
    public bool EaseOut(GhostAction action, float seconds)
    {
        if (playing == null || playing.Action != action || easeAt >= 0f)
            return false;
        float left = playing.Length - time;
        if (left <= seconds)
            return false;
        easeAt = time;
        easeFor = Mathf.Max(1e-3f, seconds);
        return true;
    }

    /// <summary>0 to 1: how far an early ease out has gone (0 when there's none)</summary>
    float Eased()
    {
        return easeAt >= 0f ? Mathf.SmoothStep(0f, 1f, (time - easeAt) / easeFor) : 0f;
    }

    /// <summary>
    /// Whether Play(action) would start it now (it has versions this ghost can show, isn't cooling down, nothing as
    /// important plays)
    /// </summary>
    public bool CanPlay(GhostAction action)
    {
        if (rig == null || cooldowns == null || Showable(GhostMoves.For(action)) == 0)
            return false;
        GhostAction? now = playing != null ? playing.Action : (GhostAction?)null;
        return GhostEmoteRules.CanStart(action, now, cooldowns[(int)action]);
    }

    int Showable(GhostMove[] moves)
    {
        int count = 0;
        foreach (GhostMove move in moves)
            if (CanShow(move))
                count++;
        return count;
    }

    /// <summary>
    /// The next version of the action from its shuffled round (TitlePlaylist), passing over the ones it can't show now,
    /// and never the one it picked last time unless that is the only one it can show. -1 when it can show none
    /// </summary>
    int Pick(GhostAction action, GhostMove[] moves)
    {
        int showable = Showable(moves);
        if (showable == 0)
            return -1;
        TitlePlaylist list = playlists[(int)action];
        if (list == null)
            list = playlists[(int)action] = new TitlePlaylist(moves.Length, random);
        int last = picked[(int)action];
        // The rest of this round and a whole new one hold every version, so one that fits comes up within these
        for (int tries = 0; tries < 2 * moves.Length; tries++)
        {
            int i = list.Next();
            if (!CanShow(moves[i]) || (i == last && showable > 1))
                continue;
            return picked[(int)action] = i;
        }
        for (int i = 0; i < moves.Length; i++)
        {
            if (CanShow(moves[i]) && (i != last || showable == 1))
                return picked[(int)action] = i;
        }
        return -1;
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
        strength = 1f;
        easeAt = -1f;
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
        easeAt = -1f;
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
            goal = up * Mathf.SmoothStep(0f, 1f, (playing.Length - time) / CLIP_OUT) * (1f - Eased());
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
        if (time >= playing.Length || (easeAt >= 0f && time >= easeAt + easeFor))
        {
            playing = null;
            resting = true;
            easeAt = -1f;
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
            if (strength < 1f)
                pose = GhostPose.Lerp(GhostPose.Rest, pose, strength);
            if (easeAt >= 0f)
                pose = GhostPose.Lerp(pose, GhostPose.Rest, Eased());
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

        // The hands swell with the Emotes layer's weight (so they read at game size), on what the Animator wrote
        rig.SetHands(1f + GhostRig.HAND_SWELL * weight);

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
        easeAt = -1f;
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
        Camera view = View != null ? View : Camera.main;
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
        Camera view = View != null ? View : Camera.main;
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

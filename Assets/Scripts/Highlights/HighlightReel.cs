using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.VFX;

/// <summary>
/// The highlight reel playing on this machine: stand-in scooters act out the recording (ReelRecording) under one
/// full-screen camera placed by each clip's shots (ShotDirector), with the hidden-camera overlay (ReelOverlay) and the
/// cutscene's skip vote (CutsceneSkip) on top. The real players are hidden (their renderers and HUD switched off, then
/// back) and left as they are. Built in the active scene, so a scene load ends it. End restores everything.
/// See docs/superpowers/specs/2026-10-28-highlight-reel-design.md
/// </summary>
public class HighlightReel : MonoBehaviour
{
    const string DRIVING_CANVAS = "Control/Driving Canvas";
    const float TRAIL_TIME = 0.4f;
    const float TRAIL_WIDTH = 0.35f;
    const float PARCEL_SIZE = 0.4f;
    static readonly Vector3 PARCEL_OFFSET = new Vector3(0f, 1.1f, -0.5f); // over the basket, from the scooter model

    // One seat's stand-in
    class Actor
    {
        public GameObject Body;
        public TrailRenderer Trail;
        public GameObject Parcel;
        public Material ParcelMaterial;
        public byte Carrying;
        public bool WasHidden = true;
    }

    /// <summary>The reel has finished: played to the end, skipped, or ended</summary>
    public bool Done { get; private set; }

    /// <summary>The reel was destroyed before it ended (a scene load, a quit to the menu), not played out or skipped</summary>
    internal bool CutShort { get; private set; }

    /// <summary>Round time now, in the reel (the recording's clock)</summary>
    internal double Clock { get; private set; }

    /// <summary>The game's clock the reel follows; tests swap it</summary>
    internal Func<double> Now = HighlightDirector.Now;

    /// <summary>The skip vote</summary>
    internal CutsceneSkip Skipper { get; private set; }

    internal Camera Camera { get; private set; }

    ReelList list;
    ReelRecording recording;
    ReelOverlay overlay;
    readonly Actor[] standIns = new Actor[Constants.MAX_PLAYERS];
    readonly List<Renderer> hiddenRenderers = new List<Renderer>();
    readonly List<Behaviour> hiddenParts = new List<Behaviour>(); // the HUD's canvases, the headlights and logo decals
    bool restored;
    int clipShown = -1;
    int shotShown = -1;
    int cameraNumber = 1;
    Vector3 lastSubjectPlace;

    /// <summary>
    /// Starts playing a reel: the players are hidden, the stand-ins and the camera made, and the skip vote opened for
    /// every roster seat (decides: whether this machine decides it, GameAuthority). If anything throws, the half-built
    /// reel is ended (the players come back) before the exception goes on
    /// </summary>
    public static HighlightReel Play(ReelList list, ReelRecording recording, bool decides)
    {
        GameObject holder = new GameObject("Highlight Reel");
        HighlightReel reel = holder.AddComponent<HighlightReel>();
        try
        {
            reel.list = list;
            reel.recording = recording;
            reel.HidePlayers();
            reel.BuildStandIns();
            reel.BuildCamera();
            reel.overlay = ReelOverlay.Build(holder.transform);

            reel.Skipper = holder.AddComponent<CutsceneSkip>();
            reel.Skipper.PromptParent = reel.overlay.transform;
            reel.Skipper.Skip = reel.End;
            reel.Skipper.Open(RosterSeats(), decides);

            reel.Show(reel.Now());
        }
        catch
        {
            reel.End();
            throw;
        }
        return reel;
    }

    /// <summary>
    /// The reel's clock at a time on the game's clock: before the start, the first clip's start; then the matching
    /// moment in the clips, played in order with hard cuts between them and each slow shot played slowed; past the end,
    /// positive infinity (done). A machine that starts late lands at the same moment as the rest
    /// </summary>
    internal static double StartClock(ReelList list, double now)
    {
        if (!Locate(list, now, out _, out double clock))
            return double.PositiveInfinity;
        return clock;
    }

    /// <summary>The stand-in for a seat (null: none)</summary>
    internal Transform StandIn(int seat)
    {
        Actor standIn = seat >= 0 && seat < standIns.Length ? standIns[seat] : null;
        return standIn != null && standIn.Body != null ? standIn.Body.transform : null;
    }

    /// <summary>Stops the reel: the skip closes, the players come back as they were, and the reel goes. Safe to call again</summary>
    public void End()
    {
        if (Done)
            return;
        Done = true;
        Restore();
        if (this != null)
            Destroy(gameObject);
    }

    void LateUpdate()
    {
        if (Done)
            return;

        // A throw here must not leave the reel up, the players hidden and the host's RunReel waiting for ever
        try
        {
            Show(Now());
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            End();
        }
    }

    void OnDestroy()
    {
        // A scene load (or a client's reel cut short) still gives the players back
        if (!Done)
            CutShort = true;
        Done = true;
        Restore();
    }

    // ---- Time ----

    // Which clip, and the clip time in it, at a time on the game's clock; false once the reel is over
    static bool Locate(ReelList list, double now, out int clip, out double time)
    {
        clip = 0;
        time = 0;
        if (list.Clips == null || list.Clips.Length == 0)
            return false;

        time = list.Clips[0].Start;
        if (now < list.StartAt)
            return true;

        double real = now - list.StartAt;
        for (clip = 0; clip < list.Clips.Length; clip++)
        {
            ReelClip entry = list.Clips[clip];
            double length = entry.RealSeconds; // the same walk as RealLength, which list.Length adds up
            if (real < length)
            {
                time = entry.TimeAfter(real);
                return true;
            }
            real -= length;
        }
        return false;
    }

    // ---- Each frame ----

    void Show(double now)
    {
        if (!Locate(list, now, out int index, out double time))
        {
            End();
            return;
        }

        Clock = time;
        ReelClip clip = list.Clips[index];
        bool cut = index != clipShown;
        for (int seat = 0; seat < standIns.Length; seat++)
        {
            if (standIns[seat] != null)
                Pose(standIns[seat], seat, time, cut);
        }

        int shot = clip.ShotAt(time);
        if (shot >= 0)
            PlaceCamera(clip.Shots[shot], time);
        if (cut || shot != shotShown)
            cameraNumber = ReelOverlay.CameraNumber(Camera.transform.position);
        clipShown = index;
        shotShown = shot;

        float timeLeft = clip.TimeLeftAtStart - (float)(time - clip.Start);
        bool captioned = time >= clip.KeyTime - ReelOverlay.CAPTION_BEFORE && time <= clip.KeyTime + ReelOverlay.CAPTION_AFTER;
        overlay.Show(cameraNumber, timeLeft, captioned ? clip.Caption : (Caption?)null);
    }

    // A stand-in where its seat was at a time: hidden while the rider was (or with nothing recorded), trailing while boosting
    void Pose(Actor standIn, int seat, double time, bool cut)
    {
        bool shown = recording.TryGet(seat, time, out ReelSample sample) && !sample.Flags.Hidden;
        if (standIn.Body.activeSelf != shown)
            standIn.Body.SetActive(shown);
        if (!shown)
        {
            standIn.Parcel.SetActive(false);
            standIn.WasHidden = true;
            return;
        }

        Transform body = standIn.Body.transform;
        body.SetPositionAndRotation(sample.Pose.ModelPosition, sample.Pose.ModelRotation);
        if (standIn.Trail != null)
        {
            if (cut || standIn.WasHidden)
                standIn.Trail.Clear(); // no streak across a cut or a respawn
            standIn.Trail.emitting = sample.Flags.Boosting;
        }
        standIn.WasHidden = false;

        bool carrying = sample.Carrying > 0;
        if (standIn.Parcel.activeSelf != carrying)
            standIn.Parcel.SetActive(carrying);
        if (!carrying)
            return;
        if (sample.Carrying != standIn.Carrying)
        {
            standIn.Carrying = sample.Carrying;
            standIn.ParcelMaterial.color = ParcelColour(sample.Carrying);
        }
        standIn.Parcel.transform.SetPositionAndRotation(body.position + body.rotation * PARCEL_OFFSET, body.rotation);
    }

    // A static shot sits where it was placed; a moving one rides its subject's recorded ball. Either looks at the framed stand-ins
    void PlaceCamera(ReelShot shot, double time)
    {
        Transform camera = Camera.transform;
        if (shot.Kind == ShotKind.SideTrack || shot.Kind == ShotKind.Behind)
        {
            int subject = shot.Subject;
            double clamped = Math.Min(Math.Max(time, recording.First(subject)), recording.Last(subject));
            if (recording.TryGet(subject, clamped, out ReelSample sample))
                lastSubjectPlace = sample.Pose.Ball + Quaternion.Euler(0f, sample.Pose.Heading, 0f) * shot.Offset;
            camera.position = lastSubjectPlace;
        }
        else
        {
            camera.position = shot.Position;
        }

        Vector3 sum = Vector3.zero;
        int count = 0;
        for (int seat = 0; seat < standIns.Length; seat++)
        {
            if ((shot.FramedMask & (1 << seat)) == 0 || standIns[seat] == null || !standIns[seat].Body.activeSelf)
                continue;
            sum += standIns[seat].Body.transform.position;
            count++;
        }
        if (count == 0)
            return;
        Vector3 look = sum / count - camera.position;
        if (look.sqrMagnitude > 1e-6f)
            camera.rotation = Quaternion.LookRotation(look);
    }

    static Color ParcelColour(byte carrying)
    {
        switch ((Constants.OrderValue)(carrying - 1))
        {
            case Constants.OrderValue.Easy: return new Color(0.35f, 0.8f, 0.35f);
            case Constants.OrderValue.Medium: return new Color(1f, 0.6f, 0.15f);
            case Constants.OrderValue.Hard: return new Color(0.9f, 0.2f, 0.2f);
            case Constants.OrderValue.Golden: return new Color(1f, 0.84f, 0f);
            default: return Color.white;
        }
    }

    // ---- Building and restoring ----

    static List<int> RosterSeats()
    {
        var seats = new List<int>();
        if (PlayerInstantiate.Instance != null)
        {
            foreach (PlayerSlot player in PlayerInstantiate.Instance.Roster.Players)
                seats.Add(player.Index);
        }
        return seats;
    }

    static PlayerSlot Slot(int seat)
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        return players != null ? players.Roster[seat] : null;
    }

    // The scooter every machine sees: a local player's view holds it, a remote player is one
    static Transform Avatar(PlayerSlot slot)
    {
        if (slot == null || slot.Player == null)
            return null;
        BallDriving driving = slot.Player.GetComponentInChildren<BallDriving>(true);
        return driving != null && driving.transform.parent != null ? driving.transform.parent : slot.Player.transform;
    }

    // Every roster player's renderers, headlights, logo decals and driving HUD off, remembering which were on (never
    // their objects: Respawn reads its model's active state)
    void HidePlayers()
    {
        if (PlayerInstantiate.Instance == null)
            return;
        foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
        {
            Transform avatar = Avatar(slot);
            if (avatar == null)
                continue;
            foreach (Renderer renderer in avatar.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.enabled)
                    continue;
                renderer.enabled = false;
                hiddenRenderers.Add(renderer);
            }
            foreach (Light light in avatar.GetComponentsInChildren<Light>(true))
                Hide(light);
            foreach (DecalProjector decal in avatar.GetComponentsInChildren<DecalProjector>(true))
                Hide(decal);

            Transform hud = slot.IsLocal ? avatar.Find(DRIVING_CANVAS) : null;
            if (hud == null)
                continue;
            foreach (Canvas canvas in hud.GetComponentsInChildren<Canvas>(true))
                Hide(canvas);
        }
    }

    void Hide(Behaviour part)
    {
        if (!part.enabled)
            return;
        part.enabled = false;
        hiddenParts.Add(part);
    }

    // Exactly what HidePlayers switched off comes back on (a player gone since is skipped), and the skip closes
    void Restore()
    {
        if (restored)
            return;
        restored = true;
        if (Skipper != null)
            Skipper.Close();

        foreach (Renderer renderer in hiddenRenderers)
        {
            if (renderer != null)
                renderer.enabled = true;
        }
        foreach (Behaviour part in hiddenParts)
        {
            if (part != null)
                part.enabled = true;
        }
        hiddenRenderers.Clear();
        hiddenParts.Clear();

        foreach (Actor standIn in standIns)
        {
            if (standIn != null && standIn.ParcelMaterial != null)
                Destroy(standIn.ParcelMaterial);
        }
    }

    void BuildCamera()
    {
        GameObject holder = new GameObject("Reel Camera");
        holder.transform.SetParent(transform, false);
        Camera = holder.AddComponent<Camera>(); // no AudioListener: the players' still hear
        Camera.depth = CameraBudget.COVER_DEPTH + 20f;
        Camera.clearFlags = CameraClearFlags.Skybox;
        Camera.fieldOfView = ShotDirector.FOV;
        Camera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    // A stand-in for each seat in any clip that has a player to copy
    void BuildStandIns()
    {
        int seats = 0;
        foreach (ReelClip clip in list.Clips)
            seats |= clip.SeatsMask;

        // Copies are made under an inactive holder, so none of their scripts wake before they're stripped
        GameObject staging = new GameObject("Staging");
        staging.SetActive(false);
        staging.transform.SetParent(transform, false);
        for (int seat = 0; seat < standIns.Length; seat++)
        {
            if ((seats & (1 << seat)) != 0)
                standIns[seat] = BuildStandIn(seat, staging.transform);
        }
        Destroy(staging);
    }

    Actor BuildStandIn(int seat, Transform staging)
    {
        PlayerSlot slot = Slot(seat);
        Transform avatar = Avatar(slot);
        Transform model = avatar != null ? avatar.Find(ScooterLook.MODEL) : null;
        if (model == null)
            return null;

        GameObject body = Instantiate(model.gameObject, staging, false);
        body.name = "Stand-in P" + (seat + 1);
        Strip(body);
        body.transform.localScale = model.lossyScale;
        body.transform.SetParent(transform, true);
        body.SetActive(false);

        var standIn = new Actor { Body = body };
        TrailHandler trails = avatar.GetComponentInChildren<TrailHandler>(true);
        Material trailMaterial = trails != null ? trails.BoostTrailMaterial : null;
        if (trailMaterial != null)
        {
            standIn.Trail = body.AddComponent<TrailRenderer>();
            standIn.Trail.sharedMaterial = trailMaterial;
            standIn.Trail.time = TRAIL_TIME;
            standIn.Trail.widthCurve = AnimationCurve.Linear(0f, TRAIL_WIDTH, 1f, 0f);
            standIn.Trail.emitting = false;
        }

        standIn.Parcel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        standIn.Parcel.name = "Parcel P" + (seat + 1);
        DestroyImmediate(standIn.Parcel.GetComponent<Collider>());
        standIn.Parcel.transform.SetParent(transform, false);
        standIn.Parcel.transform.localScale = Vector3.one * PARCEL_SIZE;
        Renderer parcel = standIn.Parcel.GetComponent<Renderer>();
        standIn.ParcelMaterial = parcel.material;
        standIn.Parcel.SetActive(false);
        return standIn;
    }

    // Only the look stays: no scripts, physics, animation, sound or effects, and no order the player carried
    static void Strip(GameObject copy)
    {
        foreach (Order order in copy.GetComponentsInChildren<Order>(true))
        {
            if (order != null && order.gameObject != copy)
                DestroyImmediate(order.gameObject);
        }
        foreach (ParticleSystem particles in copy.GetComponentsInChildren<ParticleSystem>(true))
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var doomed = new List<Component>();
        foreach (Component part in copy.GetComponentsInChildren<Component>(true))
        {
            if (Doomed(part))
                doomed.Add(part);
        }

        // A part another needs goes after it: keep passing until nothing more can go
        bool removed = true;
        while (doomed.Count > 0 && removed)
        {
            removed = false;
            for (int i = doomed.Count - 1; i >= 0; i--)
            {
                if (Needed(doomed[i]))
                    continue;
                DestroyImmediate(doomed[i]);
                doomed.RemoveAt(i);
                removed = true;
            }
        }
    }

    static bool Doomed(Component part)
    {
        if (part == null)
            return false;
        if (part is MonoBehaviour behaviour)
            return !IsLook(behaviour);
        return part is Collider || part is Rigidbody || part is Joint || part is Animator || part is AudioSource
            || part is VisualEffect || part is TrailRenderer;
    }

    // Render pipeline parts that are the scooter's look (the company logo decals, the headlight's settings): kept
    static bool IsLook(MonoBehaviour behaviour)
    {
        return behaviour is DecalProjector || behaviour is UniversalAdditionalLightData;
    }

    // Whether another part on the same object still requires this one
    static bool Needed(Component part)
    {
        Type type = part.GetType();
        foreach (Component other in part.GetComponents<Component>())
        {
            if (other == null || other == part)
                continue;
            foreach (RequireComponent require in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                if (Requires(require.m_Type0, type) || Requires(require.m_Type1, type) || Requires(require.m_Type2, type))
                    return true;
            }
        }
        return false;
    }

    static bool Requires(Type required, Type type)
    {
        return required != null && required.IsAssignableFrom(type);
    }
}

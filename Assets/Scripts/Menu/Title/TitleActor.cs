using DG.Tweening;
using UnityEngine;

/// <summary>
/// A ghost on the title screen's stage (TitleStage): a scooter (PlayerAvatar.prefab) with everything a player's machine
/// runs switched off, as RemoteAvatar, and more: no scripts of the game's at all, no colliders, sounds, lights or
/// trails. A skit moves its root along a path and tilts its model; the ghost's own animator plays its driving pose and
/// the grab, cheer, clap and slump, and its GhostEmotes the emotes players see in a match. Dressed with ScooterLook.
/// Its LateUpdate puts the tilt on before the emotes pose the bones (so they pose on this frame's tilt)
/// </summary>
[DefaultExecutionOrder(-10)]
public class TitleActor : MonoBehaviour
{
    /// <summary>The ghost's poses on the animator's End Status (PlayerAnimator.controller)</summary>
    const int DRIVING = 0, HOORAY = 1, CLAP = 3, LOSE = 4;
    /// <summary>Where a carried package sits, on the back of the scooter (in the tilt's space: +z is the nose)</summary>
    static readonly Vector3 CARRY_AT = new Vector3(0f, 0.95f, -0.75f);
    /// <summary>The rack (model space): a package stands on this height, its front face at this z, behind the seat and the ghost's tail</summary>
    static readonly Vector3 RACK = new Vector3(0f, 0.62f, -0.72f);
    /// <summary>The soft shadow under the scooter: across and along it, how dark, and just above the road</summary>
    static readonly Vector2 SHADOW_SIZE = new Vector2(1.0f, 2.3f);
    const float SHADOW_DARK = 0.55f, SHADOW_Y = 0.02f;
    /// <summary>The wake: how far round the scooter the mist is pushed aside, and how hard</summary>
    const float WAKE_RANGE = 1.7f, WAKE_PUSH = -1.2f;

    /// <summary>Seconds the driving lean takes to settle on a new speed, so a stop or a getaway doesn't snap it</summary>
    const float LEAN_SETTLE = 0.18f;

    GameObject avatar; // the scooter (PlayerAvatar), under the tilt
    Animator animator;
    GhostEmotes emotes;
    Transform model;
    Transform package;
    Transform shadow;
    Renderer shadowLook;
    MaterialPropertyBlock shadowTint;

    float speedGoal, speedShown, speedPace;   // the driving lean: where it's going, where it is, how fast it moves

    /// <summary>A squash on the go: how big, how long, and how far in (seconds)</summary>
    struct Squish
    {
        public float amount, length, age;
    }
    readonly System.Collections.Generic.List<Squish> squishes = new System.Collections.Generic.List<Squish>();

    /// <summary>
    /// The tilt, in degrees: nose up (a wheelie, a leap) and lean (a wobble, a swerve: + leans left). Skits tween these
    /// rather than the model's rotation, so a wheelie and a wobble at once both show; they go on the model in LateUpdate
    /// </summary>
    public float Pitch { get; set; }
    public float Roll { get; set; }
    /// <summary>The tween driving each of them (one at a time: a new one takes over from where the last left it)</summary>
    internal Tween PitchTween, RollTween, HopTween;

    /// <summary>The level pivot the scooter sits on, +z its nose: lean, wheelies and wobbles go here, and the root is the path</summary>
    public Transform Model => model;
    /// <summary>The hat it wears (the one it was dressed in, or stole)</summary>
    public PlayerHatInformationSO Hat { get; private set; }
    public PlayerColorInformationSO Colour { get; private set; }
    public CompanyInformation Company { get; private set; }
    /// <summary>The package on its scooter (null without one)</summary>
    public Transform Package => package;
    /// <summary>The ghost's own Animator (its poses, the grab)</summary>
    internal Animator Animator => animator;

    /// <summary>
    /// Makes a stage ghost under parent. It's built switched off, so none of the scooter's scripts ever start
    /// </summary>
    public static TitleActor Create(GameObject avatarPrefab, Transform parent, string name, Material shadowMaterial = null)
    {
        // The ghost's own root (the path) and a level pivot on the floor under it (the tilt), then the scooter: the
        // scooter's own parts are turned this way and that, so skits never touch them
        GameObject root = new GameObject(name);
        root.SetActive(false);
        GameObject tilt = new GameObject("Tilt");
        tilt.transform.SetParent(root.transform, false);
        GameObject avatar = Instantiate(avatarPrefab, tilt.transform);
        avatar.name = "Scooter";
        TitleActor actor = root.AddComponent<TitleActor>();
        actor.avatar = avatar;
        actor.model = tilt.transform;
        actor.TurnOffTheGame();
        if (shadowMaterial != null)
            actor.MakeShadow(shadowMaterial);
        actor.MakeWake();
        root.transform.SetParent(parent, false);
        root.SetActive(true);
        // Awake runs on switching on even for a switched-off script: the scooter's SoundPool has just made its pool
        // of sources, which are silenced too
        foreach (AudioSource sound in actor.GetComponentsInChildren<AudioSource>(true))
            sound.enabled = false;
        // The emotes a player's ghost plays in a match, played by the skits (it is the one game script left on)
        actor.emotes = GhostEmotes.On(actor.animator);
        if (actor.emotes != null)
            actor.emotes.enabled = true;
        return actor;
    }

    /// <summary>The ghost's emotes (null when its model has no rig)</summary>
    public GhostEmotes Emotes => emotes;

    /// <summary>
    /// Plays one version of an in-game emote (GhostMoves), by its name, turned towards the camera. Returns how long it
    /// lasts (0 when there's no such version or the ghost can't show it: a hat move on a bare head)
    /// </summary>
    public float Emote(GhostAction action, string version)
    {
        if (emotes == null)
            return 0f;
        foreach (GhostMove move in GhostMoves.For(action))
        {
            if (move.Name != version || !emotes.CanShow(move))
                continue;
            // Side-on to the camera, which looks along +z: a ghost riding right has its right side to it
            emotes.Play(move, transform.forward.x >= 0f ? 1f : -1f);
            return move.Length;
        }
        return 0f;
    }

    /// <summary>The emote playing eases back to rest over this long and ends (nothing when none plays)</summary>
    public void EndEmote(float seconds)
    {
        if (emotes != null && emotes.Playing != null)
            emotes.EaseOut(emotes.Playing.Action, seconds);
    }

    /// <summary>
    /// A squash and its spring back (a bump, a start, a surprise): wider and lower, then a little taller, then still.
    /// Squashes add up, so two at once never leave the scooter the wrong size
    /// </summary>
    public void Squash(float amount, float seconds)
    {
        squishes.Add(new Squish { amount = amount, length = Mathf.Max(0.05f, seconds) });
    }

    /// <summary>A soft dark patch on the road under the scooter, so it sits on the road rather than over it</summary>
    void MakeShadow(Material material)
    {
        GameObject quad = new GameObject("Shadow");
        quad.transform.SetParent(transform, false);
        quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // flat on the road, its length along the nose
        quad.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer look = quad.AddComponent<MeshRenderer>();
        look.sharedMaterial = material;
        look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        look.receiveShadows = false;
        shadow = quad.transform;
        shadowLook = look;
        shadowTint = new MaterialPropertyBlock();
        PlaceShadow();
    }

    /// <summary>The mist parts round the scooter as it rides through (the stage's mist feels external forces)</summary>
    void MakeWake()
    {
        GameObject wake = new GameObject("Wake");
        wake.transform.SetParent(transform, false);
        wake.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        ParticleSystemForceField field = wake.AddComponent<ParticleSystemForceField>();
        field.shape = ParticleSystemForceFieldShape.Sphere;
        field.startRange = 0f;
        field.endRange = WAKE_RANGE;
        field.gravity = WAKE_PUSH;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        // The driving lean eases to its speed (the Animator reads it next frame)
        if (animator != null && animator.isActiveAndEnabled && !Mathf.Approximately(speedShown, speedGoal))
        {
            speedShown = Mathf.SmoothDamp(speedShown, speedGoal, ref speedPace, LEAN_SETTLE, Mathf.Infinity, dt);
            if (Mathf.Abs(speedShown - speedGoal) < 0.002f)
                speedShown = speedGoal;
            animator.SetFloat(HashReference._speedFloat, speedShown);
        }

        // The tilt and the squashes, put together
        model.localRotation = Quaternion.Euler(-Pitch, 0f, Roll);
        float squash = 0f;
        for (int i = squishes.Count - 1; i >= 0; i--)
        {
            Squish s = squishes[i];
            s.age += dt;
            if (s.age >= s.length)
            {
                squishes.RemoveAt(i);
                continue;
            }
            squishes[i] = s;
            squash += s.amount * Squished(s.age / s.length);
        }
        model.localScale = new Vector3(1f + squash, 1f - squash, 1f + squash);

        if (shadow != null)
            PlaceShadow();
    }

    /// <summary>
    /// A squash's shape over its length (0 to 1): in within a few frames, a full squash, a smaller stretch, a last little
    /// squash, and still at the end, with no jump anywhere
    /// </summary>
    internal static float Squished(float u)
    {
        u = Mathf.Clamp01(u);
        float rest = 1f - u;
        return Mathf.Sin(3f * Mathf.PI * u) * rest * rest * Mathf.SmoothStep(0f, 1f, u / 0.06f) / 0.7f;
    }

    /// <summary>The shadow stays on the road under the ghost: smaller and fainter the higher it is (a hop, a leap)</summary>
    void PlaceShadow()
    {
        float lift = Mathf.Max(0f, transform.localPosition.y + (model != null ? model.localPosition.y : 0f));
        float k = 1f / (1f + lift * 0.45f);
        shadow.localPosition = new Vector3(0f, SHADOW_Y - transform.localPosition.y, 0f);
        shadow.localScale = new Vector3(SHADOW_SIZE.x * k, SHADOW_SIZE.y * k, 1f);
        shadowTint.SetColor("_Color", new Color(0f, 0f, 0f, SHADOW_DARK * k));
        shadowLook.SetPropertyBlock(shadowTint);
    }

    void TurnOffTheGame()
    {
        // The game's own scripts (not Unity's: the logo decals and the like stay)
        System.Reflection.Assembly game = typeof(TitleActor).Assembly;
        foreach (MonoBehaviour script in avatar.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (script != this && script.GetType().Assembly == game)
                script.enabled = false;
        }
        foreach (Collider collider in GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
        foreach (Rigidbody body in GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }
        foreach (AudioSource sound in GetComponentsInChildren<AudioSource>(true))
            sound.enabled = false;
        foreach (AudioListener ears in GetComponentsInChildren<AudioListener>(true))
            ears.enabled = false;
        foreach (Light light in GetComponentsInChildren<Light>(true))
            light.enabled = false;
        foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
            trail.enabled = false;
        foreach (ParticleSystem particles in GetComponentsInChildren<ParticleSystem>(true))
            particles.gameObject.SetActive(false);

        // What only a match shows: the arrows over a scooter and the respawn wisp
        Hide("Control/Player Dependent Objects");
        Hide("Control/Groundcheck/Basket/DeathWisp");
        foreach (BoostRing ring in avatar.GetComponentsInChildren<BoostRing>(true))
            ring.Hide();
        Hide("Control/Boost Ring"); // its quad draws by itself, whether the ring's script woke or not

        animator = avatar.transform.Find(ScooterLook.MODEL).GetComponentInChildren<Animator>(true);
    }

    void Hide(string path)
    {
        Transform part = avatar.transform.Find(path);
        if (part != null)
            part.gameObject.SetActive(false);
    }

    /// <summary>Dresses the ghost (while it's off screen)</summary>
    public void Wear(PlayerHatInformationSO hat, PlayerColorInformationSO colour, CompanyInformation company)
    {
        Colour = colour;
        Company = company;
        ScooterLook.ShowCompany(avatar, company);
        ScooterLook.ShowColour(avatar, colour);
        SetHat(hat);
    }

    /// <summary>Changes the hat alone: the Hat Thief's steal, in view</summary>
    public void SetHat(PlayerHatInformationSO hat)
    {
        Hat = hat;
        ScooterLook.ShowHat(avatar, hat);
    }

    /// <summary>The hat on its head (the Hat Object: switched off when it wears none)</summary>
    public Transform HatModel => avatar.transform.Find(ScooterLook.HAT);

    /// <summary>
    /// Shows or hides the hat it wears without taking it off: the Hat Thief's thief wears the stolen hat hidden while a
    /// copy of it flies over, so it lands exactly where the hat sits
    /// </summary>
    public void ShowHat(bool on)
    {
        Transform hat = HatModel;
        if (hat != null && hat.TryGetComponent(out MeshRenderer look))
            look.enabled = on;
    }

    GameObject thrownHat;

    /// <summary>
    /// A copy of the hat it wears, on the stage where the hat is, for a skit to throw (the Hat Thief). Null when it
    /// wears none. The ghost keeps its own hat until the skit takes it off; Park puts the copy away
    /// </summary>
    public Transform ThrowHat()
    {
        Transform hat = HatModel;
        if (hat == null || !hat.gameObject.activeInHierarchy || !hat.TryGetComponent(out MeshFilter shape)
            || !hat.TryGetComponent(out MeshRenderer look))
            return null;
        if (thrownHat == null)
        {
            thrownHat = new GameObject("Thrown Hat");
            thrownHat.AddComponent<MeshFilter>();
            MeshRenderer made = thrownHat.AddComponent<MeshRenderer>();
            made.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        Transform copy = thrownHat.transform;
        copy.SetParent(transform.parent, false);
        thrownHat.layer = hat.gameObject.layer;
        thrownHat.GetComponent<MeshFilter>().sharedMesh = shape.sharedMesh;
        thrownHat.GetComponent<MeshRenderer>().sharedMaterial = look.sharedMaterial;
        copy.SetPositionAndRotation(hat.position, hat.rotation);
        copy.localScale = InStage(hat.lossyScale);
        thrownHat.SetActive(true);
        return copy;
    }

    /// <summary>The thrown hat's copy goes away (its flight is over)</summary>
    public void CatchHat()
    {
        if (thrownHat != null)
            thrownHat.SetActive(false);
    }

    /// <summary>A world size as a scale on the stage</summary>
    internal Vector3 InStage(Vector3 world)
    {
        Vector3 stage = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        return new Vector3(world.x / stage.x, world.y / stage.y, world.z / stage.z);
    }

    /// <summary>
    /// Where this package rides (stage space): stood on the rack with its front face just behind the seat, whatever
    /// its size and wherever its pivot is, so it never sinks into the ghost or the scooter
    /// </summary>
    public Vector3 RackPoint(Transform carried)
    {
        return model.TransformPoint(RackAt(carried));
    }

    /// <summary>The same in the model's space: where the package's pivot goes</summary>
    Vector3 RackAt(Transform carried)
    {
        if (carried == null)
            return CARRY_AT;

        // The package's box about its own pivot, unrotated, at the size it is on the stage
        Bounds box = new Bounds();
        bool any = false;
        Matrix4x4 toPackage = carried.worldToLocalMatrix;
        Vector3 scale = carried.localScale;
        foreach (MeshFilter filter in carried.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;
            Matrix4x4 toRoot = toPackage * filter.transform.localToWorldMatrix;
            Bounds mesh = filter.sharedMesh.bounds;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 at = mesh.center + Vector3.Scale(mesh.extents,
                    new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                at = Vector3.Scale(toRoot.MultiplyPoint3x4(at), scale);
                if (any)
                    box.Encapsulate(at);
                else
                    box = new Bounds(at, Vector3.zero);
                any = true;
            }
        }
        if (!any)
            return CARRY_AT;

        return new Vector3(RACK.x - box.center.x, RACK.y - box.min.y, RACK.z - box.max.z);
    }

    /// <summary>Over its head (stage space): where a word pops up</summary>
    public Vector3 OverHead => transform.localPosition + Vector3.up * 3.1f;

    /// <summary>Puts a package on the back of the scooter</summary>
    public void Carry(Transform carried)
    {
        package = carried;
        carried.DOKill();
        carried.SetParent(model, false);
        carried.localRotation = Quaternion.identity;
        carried.localPosition = RackAt(carried);
    }

    /// <summary>Lets go of the package, left where it is on the stage (null without one)</summary>
    public Transform Drop()
    {
        Transform dropped = package;
        package = null;
        if (dropped != null)
            dropped.SetParent(transform.parent, true);
        return dropped;
    }

    /// <summary>The steal</summary>
    public void Grab()
    {
        if (animator != null && animator.isActiveAndEnabled)
            animator.SetTrigger(HashReference._stealLeftTrigger);
    }

    public void Cheer() { Pose(HOORAY); }
    public void Clap() { Pose(CLAP); }
    public void Slump() { Pose(LOSE); }

    /// <summary>Back to the driving pose, leaning into its speed (0 stopped, 1 flat out): the lean eases over</summary>
    public void Drive(float speed01)
    {
        Pose(DRIVING);
        speedGoal = speed01;
    }

    void Pose(int endStatus)
    {
        if (animator != null && animator.isActiveAndEnabled)
            animator.SetInteger(HashReference._endStatusFloat, endStatus);
    }

    /// <summary>
    /// Off the stage and as it was made: no tweens, no package, the model level, the driving pose. The stage takes the
    /// package back (TitleStage.Halt)
    /// </summary>
    public void Park(Vector3 offStage)
    {
        transform.DOKill();
        model.DOKill();
        PitchTween = RollTween = HopTween = null;
        package = null;
        Pitch = Roll = 0f;
        squishes.Clear();
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        transform.localPosition = offStage;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        if (emotes != null)
            emotes.Stop();
        CatchHat();
        ShowHat(true);
        Drive(0f);
        speedShown = speedGoal;
        speedPace = 0f;
        if (animator != null && animator.isActiveAndEnabled)
        {
            animator.ResetTrigger(HashReference._stealLeftTrigger);
            animator.SetFloat(HashReference._speedFloat, speedShown);
        }
    }

    /// <summary>
    /// Puts the ghost on its mark at a speed, the lean already there (it rides on at that speed, rather than leaning
    /// into it in view)
    /// </summary>
    public void Settle(float speed01)
    {
        Drive(speed01);
        speedShown = speed01;
        speedPace = 0f;
        if (animator != null && animator.isActiveAndEnabled)
            animator.SetFloat(HashReference._speedFloat, speed01);
    }
}

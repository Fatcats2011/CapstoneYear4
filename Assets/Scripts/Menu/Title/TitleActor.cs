using DG.Tweening;
using UnityEngine;

/// <summary>
/// A ghost on the title screen's stage (TitleStage): a scooter (PlayerAvatar.prefab) with everything a player's machine
/// runs switched off, as RemoteAvatar, and more: no scripts of the game's at all, no colliders, sounds, lights or
/// trails. A skit moves its root along a path and tilts its model; the ghost's own animator plays its driving pose and
/// the grab, cheer, clap and slump. Dressed with ScooterLook
/// </summary>
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

    GameObject avatar; // the scooter (PlayerAvatar), under the tilt
    Animator animator;
    Transform model;
    Transform package;
    Transform shadow;
    Renderer shadowLook;
    MaterialPropertyBlock shadowTint;

    /// <summary>The level pivot the scooter sits on, +z its nose: lean, wheelies and wobbles go here, and the root is the path</summary>
    public Transform Model => model;
    /// <summary>The hat it wears (the one it was dressed in, or stole)</summary>
    public PlayerHatInformationSO Hat { get; private set; }
    public PlayerColorInformationSO Colour { get; private set; }
    public CompanyInformation Company { get; private set; }
    /// <summary>The package on its scooter (null without one)</summary>
    public Transform Package => package;

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
        return actor;
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
        if (shadow != null)
            PlaceShadow();
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

    /// <summary>Back to the driving pose, leaning into its speed (0 stopped, 1 flat out)</summary>
    public void Drive(float speed01)
    {
        Pose(DRIVING);
        if (animator != null && animator.isActiveAndEnabled)
            animator.SetFloat(HashReference._speedFloat, speed01);
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
        package = null;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;
        model.localScale = Vector3.one;
        transform.localPosition = offStage;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        Drive(0f);
    }
}

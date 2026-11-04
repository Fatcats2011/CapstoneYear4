using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Draws a timed mode (ModeMatch) on every machine: the crown over its holder (or lying on the road), the cursed package
/// over its holder (ticking and glowing faster as the fuse burns), infected ghosts in the first hunter's colour, and
/// players who are out hidden, with this machine's own out players watching someone still in (boost: the next one).
/// Everything goes back when the match ends. Made at launch; does nothing outside a timed mode
/// </summary>
public class ModeVisuals : MonoBehaviour
{
    const float ABOVE = 3.2f;
    const float SPIN = 90f;

    static ModeVisuals instance;

    GameObject crown, potato;
    Light potatoLight;
    AudioSource sounds;
    float nextTick;
    int shownCrown = ModeMatch.NONE, shownPotato = ModeMatch.NONE, shownInfected, shownOut;

    // What changed on each seat, to put back
    readonly Dictionary<int, Material> ghostWas = new Dictionary<int, Material>();
    readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
    readonly Dictionary<int, Spectating> spectators = new Dictionary<int, Spectating>();

    class Spectating
    {
        public CinemachineVirtualCamera Camera;
        public Transform Follow, LookAt;
        public IDriveInput Input;
        public System.Action<bool> OnBoost;
        public int Watching = ModeMatch.NONE;
        public bool Next;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make()
    {
        if (instance != null)
            return;
        GameObject go = new GameObject("Mode Visuals");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ModeVisuals>();
    }

    void Awake()
    {
        sounds = gameObject.AddComponent<AudioSource>();
        sounds.playOnAwake = false;
        ModeMatch.Changed += OnChanged;
        ModeMatch.Happened += OnHappened;
    }

    void OnDestroy()
    {
        ModeMatch.Changed -= OnChanged;
        ModeMatch.Happened -= OnHappened;
    }

    static Transform BallOf(int seat)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
        return ball != null && ball.Sphere != null ? ball.Sphere.transform : null;
    }

    static GameObject PlayerOf(int seat)
    {
        if (PlayerInstantiate.Instance == null || seat < 0 || seat >= Constants.MAX_PLAYERS)
            return null;
        PlayerSlot slot = PlayerInstantiate.Instance.Roster[seat];
        return slot != null ? slot.Player : null;
    }

    ModeMatch hinted;

    void OnChanged()
    {
        ModeMatch match = ModeMatch.Current;
        if (match == null)
        {
            PutEverythingBack();
            hinted = null;
            return;
        }

        // A new match: what to do, on every machine
        if (match != hinted)
        {
            hinted = match;
            if (ControllerPrompts.Instance != null)
                ControllerPrompts.Instance.ShowHint(Loc.Get("mode.start." + match.Mode.ToString().ToLowerInvariant()), 6f);
        }

        if (match.Mode == GameMode.Infection && match.Infected != shownInfected)
            ShowInfected(match);
        if (match.Mode == GameMode.HotPotato && match.Out != shownOut)
            ShowOut(match);
        if (match.Mode == GameMode.KingsCrown && match.CrownSeat != shownCrown)
        {
            if (match.CrownSeat != ModeMatch.NONE)
                Play("pickup");
            shownCrown = match.CrownSeat;
        }
        shownPotato = match.PotatoSeat;
    }

    void OnHappened(ModeMoment moment)
    {
        if (moment.Kind == ModeMomentKind.PotatoExploded)
            Play("death");
    }

    void Play(string key)
    {
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(key, sounds);
    }

    void LateUpdate()
    {
        ModeMatch match = ModeMatch.Current;
        bool crownShows = match != null && match.Mode == GameMode.KingsCrown;
        bool potatoShows = match != null && match.Mode == GameMode.HotPotato && match.PotatoSeat != ModeMatch.NONE;

        if (crownShows)
        {
            Transform holder = BallOf(match.CrownSeat);
            Show(ref crown, MakeCrown).transform.position = holder != null ? holder.position + Vector3.up * ABOVE
                : match.CrownSpot + Vector3.up * (0.5f + 0.25f * Mathf.Sin(Time.time * 2f));
            crown.transform.Rotate(0f, SPIN * Time.deltaTime, 0f, Space.World);
        }
        else if (crown != null)
            crown.SetActive(false);

        if (potatoShows)
        {
            Transform holder = BallOf(match.PotatoSeat);
            float progress = TimedModeRules.FuseProgress(Time.time, match.FuseLit, match.FuseEnd);
            Show(ref potato, MakePotato);
            potato.SetActive(holder != null);
            if (holder != null)
                potato.transform.position = holder.position + Vector3.up * ABOVE;
            float beat = Mathf.PingPong(Time.time / TimedModeRules.TickInterval(progress), 1f);
            potato.transform.localScale = Vector3.one * (1.4f + 0.3f * beat * progress);
            potato.transform.Rotate(0f, SPIN * (1f + 3f * progress) * Time.deltaTime, 0f, Space.World);
            if (potatoLight != null)
                potatoLight.intensity = 2f + 10f * progress * beat;
            if (Time.time >= nextTick)
            {
                Play("scroll");
                nextTick = Time.time + TimedModeRules.TickInterval(progress);
            }
        }
        else if (potato != null)
            potato.SetActive(false);

        if (match != null && match.Mode == GameMode.HotPotato)
            Spectate(match);
    }

    static GameObject Show(ref GameObject shown, System.Func<GameObject> make)
    {
        if (shown == null)
            shown = make();
        shown.SetActive(true);
        return shown;
    }

    // A package's look from the scene's orders (their materials are in the build), or a plain one
    static Order AnyOrder() => FindObjectOfType<Order>(true);

    GameObject MakeCrown()
    {
        Order order = AnyOrder();
        Material gold = order != null ? order.MaterialOf(3) : null;
        GameObject root = new GameObject("Crown");
        DontDestroyOnLoad(root);
        Part(PrimitiveType.Cylinder, root.transform, Vector3.zero, new Vector3(1.4f, 0.25f, 1.4f), Quaternion.identity, gold);
        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f;
            Vector3 at = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.62f, 0.45f, 0f);
            Part(PrimitiveType.Cube, root.transform, at, new Vector3(0.28f, 0.28f, 0.28f), Quaternion.Euler(45f, angle, 45f), gold);
        }
        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.3f);
        light.range = 8f;
        light.intensity = 4f;
        return root;
    }

    GameObject MakePotato()
    {
        Order order = AnyOrder();
        GameObject root = new GameObject("Cursed Package");
        DontDestroyOnLoad(root);
        GameObject package = new GameObject("Package", typeof(MeshFilter), typeof(MeshRenderer));
        package.transform.SetParent(root.transform, false);
        if (order != null && order.MeshOf(2) != null)
        {
            package.GetComponent<MeshFilter>().sharedMesh = order.MeshOf(2);
            package.GetComponent<MeshRenderer>().sharedMaterial = order.MaterialOf(2);
        }
        else
        {
            Destroy(package);
            Part(PrimitiveType.Cube, root.transform, Vector3.zero, Vector3.one * 0.8f, Quaternion.identity, null);
        }
        potatoLight = root.AddComponent<Light>();
        potatoLight.type = LightType.Point;
        potatoLight.color = new Color(1f, 0.15f, 0.05f);
        potatoLight.range = 10f;
        return root;
    }

    static void Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Quaternion rotation, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(type);
        Destroy(part.GetComponent<Collider>());
        part.transform.SetParent(parent, false);
        part.transform.localPosition = at;
        part.transform.localRotation = rotation;
        part.transform.localScale = scale;
        if (material != null)
            part.GetComponent<Renderer>().sharedMaterial = material;
    }

    // Infection: every caught ghost wears the first hunter's colour
    void ShowInfected(ModeMatch match)
    {
        Material hunterColour = GhostOf(match.FirstHunter) != null ? GhostOf(match.FirstHunter).sharedMaterials[0] : null;
        foreach (int seat in TimedModeRules.Seats(match.Infected & ~shownInfected))
        {
            if (seat == match.FirstHunter || hunterColour == null)
                continue;
            if (!ghostWas.ContainsKey(seat))
                ghostWas[seat] = GhostOf(seat) != null ? GhostOf(seat).sharedMaterials[0] : null;
            Dress(seat, hunterColour);
            Play("whoosh");
        }
        shownInfected = match.Infected;
    }

    static SkinnedMeshRenderer GhostOf(int seat)
    {
        Transform ghost = Find(PlayerOf(seat), "Player_Model");
        return ghost != null ? ghost.GetComponent<SkinnedMeshRenderer>() : null;
    }

    static void Dress(int seat, Material colour)
    {
        SkinnedMeshRenderer ghost = GhostOf(seat);
        if (ghost == null || colour == null)
            return;
        Material[] materials = ghost.sharedMaterials;
        materials[0] = colour;
        ghost.sharedMaterials = materials;
        foreach (string eyelid in new[] { "Eyelid Left", "Eyelid Right" })
        {
            Transform lid = Find(PlayerOf(seat), eyelid);
            if (lid != null && lid.GetComponent<MeshRenderer>() != null)
                lid.GetComponent<MeshRenderer>().sharedMaterial = colour;
        }
    }

    static Transform Find(GameObject root, string name)
    {
        if (root == null)
            return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            if (child.name == name)
                return child;
        return null;
    }

    // Hot Potato: a player out disappears and stops (this machine's own players), and watches someone still in
    void ShowOut(ModeMatch match)
    {
        foreach (int seat in TimedModeRules.Seats(match.Out & ~shownOut))
        {
            GameObject player = PlayerOf(seat);
            if (player == null)
                continue;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || hidden.ContainsKey(renderer) || IsViewPart(renderer))
                    continue;
                hidden[renderer] = renderer.enabled;
                renderer.enabled = false;
            }
            OrderHandler handler = OrderSync.HandlerIn(seat);
            BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
            if (ball != null && !RemoteAvatar.IsRemote(handler))
            {
                ball.FreezeBall(true);
                Rigidbody body = ball.Sphere != null ? ball.Sphere.GetComponent<Rigidbody>() : null;
                if (body != null)
                    body.isKinematic = true;
                StartSpectating(seat, handler);
                if (ControllerPrompts.Instance != null)
                    ControllerPrompts.Instance.ShowHint(Loc.Get("hotpotato.out"), 5f);
            }
            foreach (Collider collider in player.GetComponentsInChildren<Collider>(true))
                if (!collider.isTrigger)
                    collider.enabled = false;
        }
        shownOut = match.Out;
    }

    // The view's own parts (HUD canvases are UI, not renderers; the camera rig has none) stay as they are
    static bool IsViewPart(Renderer renderer) => renderer.GetComponentInParent<Canvas>() != null;

    void StartSpectating(int seat, OrderHandler handler)
    {
        OrbitalCamera orbit = PlayerOf(seat) != null ? PlayerOf(seat).GetComponentInChildren<OrbitalCamera>(true) : null;
        if (orbit == null || orbit.virtualCameraMain == null || spectators.ContainsKey(seat))
            return;
        Spectating watch = new Spectating
        {
            Camera = orbit.virtualCameraMain,
            Follow = orbit.virtualCameraMain.Follow,
            LookAt = orbit.virtualCameraMain.LookAt,
            Input = orbit.DriveInput,
        };
        watch.OnBoost = pressed => { if (pressed) watch.Next = true; };
        if (watch.Input != null)
            watch.Input.BoostButton += watch.OnBoost;
        spectators[seat] = watch;
    }

    void Spectate(ModeMatch match)
    {
        foreach (Spectating watch in spectators.Values)
        {
            bool lost = watch.Watching == ModeMatch.NONE || match.IsOut(watch.Watching) || BallOf(watch.Watching) == null;
            if (!lost && !watch.Next)
                continue;
            watch.Watching = NextIn(match, watch.Watching);
            watch.Next = false;
            Transform target = BallOf(watch.Watching);
            if (target != null && watch.Camera != null)
            {
                watch.Camera.Follow = target;
                watch.Camera.LookAt = target;
            }
        }
    }

    static int NextIn(ModeMatch match, int after)
    {
        for (int step = 1; step <= Constants.MAX_PLAYERS; step++)
        {
            int seat = ((after < 0 ? -1 : after) + step + Constants.MAX_PLAYERS) % Constants.MAX_PLAYERS;
            if ((match.Players & 1 << seat) != 0 && !match.IsOut(seat) && BallOf(seat) != null)
                return seat;
        }
        return ModeMatch.NONE;
    }

    void PutEverythingBack()
    {
        foreach (KeyValuePair<int, Material> was in ghostWas)
            Dress(was.Key, was.Value);
        ghostWas.Clear();

        foreach (KeyValuePair<Renderer, bool> was in hidden)
            if (was.Key != null)
                was.Key.enabled = was.Value;
        hidden.Clear();

        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            if ((shownOut & 1 << seat) == 0)
                continue;
            GameObject player = PlayerOf(seat);
            if (player != null)
                foreach (Collider collider in player.GetComponentsInChildren<Collider>(true))
                    collider.enabled = true;
            OrderHandler handler = OrderSync.HandlerIn(seat);
            BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
            if (ball != null && !RemoteAvatar.IsRemote(handler))
            {
                Rigidbody body = ball.Sphere != null ? ball.Sphere.GetComponent<Rigidbody>() : null;
                if (body != null)
                    body.isKinematic = false;
                ball.FreezeBall(false);
            }
        }

        foreach (Spectating watch in spectators.Values)
        {
            if (watch.Input != null)
                watch.Input.BoostButton -= watch.OnBoost;
            if (watch.Camera != null)
            {
                watch.Camera.Follow = watch.Follow;
                watch.Camera.LookAt = watch.LookAt;
            }
        }
        spectators.Clear();

        if (crown != null)
            crown.SetActive(false);
        if (potato != null)
            potato.SetActive(false);
        shownCrown = shownPotato = ModeMatch.NONE;
        shownInfected = shownOut = 0;
    }
}

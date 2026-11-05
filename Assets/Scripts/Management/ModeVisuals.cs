using System.Collections.Generic;
using Cinemachine;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Draws a timed mode (ModeMatch) on every machine. Kings Crown: a gold crown over its holder (or lying on the road) under a
/// tall beacon, on every compass, and its holder's boost recharges slower. Hot Potato: the cursed package over its holder,
/// large, purple, in pink flame, with the seconds to elimination on it; players who are out hidden, with this machine's own
/// out players watching someone still in through that player's own camera (boost: the next one, named on screen).
/// Infection: infected ghosts in a colour nobody can pick, and a red ghost on every compass. Everything goes back when the
/// match ends. Made at launch; does nothing outside a timed mode
/// </summary>
public class ModeVisuals : MonoBehaviour
{
    const float ABOVE = 3.2f;
    const float SPIN = 90f;
    const float CROWN_SCALE = 1.5f;
    const float BEACON_WIDTH = 5f, BEACON_HEIGHT = 90f;
    const float POTATO_SCALE = 1.7f;
    const float FLAME_SIZE = 4.2f;
    const float FLAME_FPS = 14f;
    const float NUMBER_ABOVE = 1.9f;
    const float NUMBER_SIZE = 16f;
    const float FLAME_BEHIND = 0.9f;  // towards the far side of the package, from each camera
    static readonly Color BEACON = new Color(1f, 0.82f, 0.25f, 0.45f);
    static readonly Color CURSED = new Color(0.26f, 0.02f, 0.55f, 1f);
    static readonly Color CURSED_GLOW = new Color(0.9f, 0.1f, 0.7f, 1f);
    static readonly Color CROWN_GOLD = new Color(1f, 0.74f, 0.1f, 1f);
    // The infected colour: ashen white, bleeding red. No ghost colour on offer is near it (purple, red, green, yellow,
    // blue, orange), and it stands out at night and without colour vision
    static readonly Color INFECTED_BODY = new Color(0.9f, 0.93f, 0.86f, 1f);
    static readonly Color INFECTED_RED = new Color(1f, 0.05f, 0.08f, 1f);

    static ModeVisuals instance;

    GameObject crown, potato, beacon;
    Transform potatoPulse;
    Light potatoLight;
    SpriteRenderer potatoFlame;
    TMP_Text fuseNumber;
    CompassMarker crownMarker;
    bool crownOnCompass;
    AudioSource sounds;
    float nextTick;
    int shownCrown = ModeMatch.NONE, shownPotato = ModeMatch.NONE, shownInfected, shownOut;

    // What changed on each seat, to put back
    readonly Dictionary<int, Material> ghostWas = new Dictionary<int, Material>();
    readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
    readonly Dictionary<int, Spectating> spectators = new Dictionary<int, Spectating>();
    readonly Dictionary<int, GameObject> infectedMarkers = new Dictionary<int, GameObject>();
    readonly Dictionary<Material, Material> infectedCopies = new Dictionary<Material, Material>();
    Canvas spectateCanvas;

    class Spectating
    {
        public CinemachineVirtualCamera Camera;
        public Transform Follow, LookAt;
        public IDriveInput Input;
        public System.Action<bool> OnBoost;
        public int Watching = ModeMatch.NONE;
        public bool Next;
        public RectTransform Label;
        public TMP_Text Name;
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
        RenderPipelineManager.beginCameraRendering += FaceCamera;
    }

    void OnDestroy()
    {
        ModeMatch.Changed -= OnChanged;
        RenderPipelineManager.beginCameraRendering -= FaceCamera;
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

    void OnChanged()
    {
        ModeMatch match = ModeMatch.Current;
        if (match == null)
        {
            PutEverythingBack();
            return;
        }

        ShowPlayers(match);
        if (match.Mode == GameMode.KingsCrown && match.CrownSeat != shownCrown)
        {
            if (match.CrownSeat != ModeMatch.NONE)
                Play("pickup");
            SetSlowBoost(shownCrown, false);
            SetSlowBoost(match.CrownSeat, true);
            shownCrown = match.CrownSeat;
        }
        shownPotato = match.PotatoSeat;
    }

    // Infected and out players. A seat whose player isn't here yet is tried again next frame
    void ShowPlayers(ModeMatch match)
    {
        if (match.Mode == GameMode.Infection && (match.Infected & ~shownInfected) != 0)
            ShowInfected(match);
        if (match.Mode == GameMode.HotPotato && (match.Out & match.Players & ~shownOut) != 0)
            ShowOut(match);
    }

    // Kings Crown: wearing the crown recharges the boost as carrying a package does. The machine that drives them does it
    static void SetSlowBoost(int seat, bool slow)
    {
        OrderHandler handler = OrderSync.HandlerIn(seat);
        BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
        if (ball != null && !RemoteAvatar.IsRemote(handler))
            ball.SetBoostModifier(slow);
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
            ShowCrown(match);
        else if (crown != null)
            crown.SetActive(false);

        // Gold Rush's Mother Lode gets the same pillar of light, wherever it is
        Order lode = Order.CurrentLode;
        if (lode != null)
        {
            Show(ref beacon, MakeBeacon);
            beacon.transform.position = lode.transform.position;
        }
        else if (beacon != null && !crownShows)
            beacon.SetActive(false);

        if (potatoShows)
            ShowPotato(match);
        else if (potato != null)
            potato.SetActive(false);

        if (match != null)
            ShowPlayers(match);
        if (match != null && match.Mode == GameMode.HotPotato)
        {
            KeepOut();
            Spectate(match);
        }
    }

    void ShowCrown(ModeMatch match)
    {
        Transform holder = BallOf(match.CrownSeat);
        Show(ref crown, MakeCrown);
        Vector3 at = holder != null ? holder.position + Vector3.up * ABOVE
            : match.CrownSpot + Vector3.up * (0.5f + 0.25f * Mathf.Sin(Time.time * 2f));
        crown.transform.position = at;
        crown.transform.Rotate(0f, SPIN * Time.deltaTime, 0f, Space.World);
        // Every frame: dropping packages (a fall in the water) puts a holder's recharge back to normal
        SetSlowBoost(match.CrownSeat, true);

        Show(ref beacon, MakeBeacon);
        beacon.transform.position = at;

        if (!crownOnCompass)
        {
            crownOnCompass = true;
            AddToCompasses(crownMarker);
        }
    }

    void ShowPotato(ModeMatch match)
    {
        Transform holder = BallOf(match.PotatoSeat);
        float progress = TimedModeRules.FuseProgress(Time.time, match.FuseLit, match.FuseEnd);
        Show(ref potato, MakePotato);
        potato.SetActive(holder != null);
        if (holder != null)
            potato.transform.position = holder.position + Vector3.up * ABOVE;
        float beat = Mathf.PingPong(Time.time / TimedModeRules.TickInterval(progress), 1f);
        potatoPulse.localScale = Vector3.one * (POTATO_SCALE + 0.35f * beat * progress);
        potatoPulse.Rotate(0f, SPIN * (1f + 3f * progress) * Time.deltaTime, 0f, Space.World);
        if (potatoLight != null)
            potatoLight.intensity = 1.5f + 8f * progress * beat;

        Sprite[] frames = PurpleFlame.PinkFrames;
        potatoFlame.sprite = frames[Mathf.FloorToInt(Time.time * FLAME_FPS) % frames.Length];
        string seconds = TimedModeRules.FuseSeconds(Time.time, match.FuseEnd).ToString();
        if (fuseNumber.text != seconds)
            fuseNumber.text = seconds;

        if (Time.time >= nextTick)
        {
            Play("scroll");
            nextTick = Time.time + TimedModeRules.TickInterval(progress);
        }
    }

    // What always faces each camera as that camera draws (split screen): the flame (behind the package from there), the
    // countdown, and the beacon, which only turns about its height
    void FaceCamera(ScriptableRenderContext context, Camera camera)
    {
        if (potato != null && potato.activeInHierarchy)
        {
            potatoFlame.transform.rotation = camera.transform.rotation;
            potatoFlame.transform.position = potato.transform.position + camera.transform.forward * FLAME_BEHIND;
            fuseNumber.transform.rotation = camera.transform.rotation;
        }
        if (beacon != null && beacon.activeInHierarchy)
        {
            Vector3 toCamera = camera.transform.position - beacon.transform.position;
            toCamera.y = 0f;
            if (toCamera.sqrMagnitude > 0.01f)
                beacon.transform.rotation = Quaternion.LookRotation(-toCamera);
        }
    }

    static GameObject Show(ref GameObject shown, System.Func<GameObject> make)
    {
        if (shown == null)
            shown = make();
        shown.SetActive(true);
        return shown;
    }

    // A package's look from the scene's orders (their materials are in the build), or a plain one
    internal static Order AnyOrder() => FindObjectOfType<Order>(true);

    GameObject MakeCrown()
    {
        Order order = AnyOrder();
        Material gold = Glowing(order != null ? order.MaterialOf(3) : null, CROWN_GOLD, CROWN_GOLD * 0.35f, "Crown Gold");
        GameObject root = new GameObject("Crown", typeof(MeshFilter), typeof(MeshRenderer));
        DontDestroyOnLoad(root);
        root.transform.localScale = Vector3.one * CROWN_SCALE;
        root.GetComponent<MeshFilter>().sharedMesh = CrownMesh();
        if (gold != null)
            root.GetComponent<MeshRenderer>().sharedMaterial = gold;

        // A gold ball on each point, a red jewel under it
        Material jewel = Glowing(order != null ? order.MaterialOf(3) : null, INFECTED_RED, INFECTED_RED * 0.5f, "Crown Jewel");
        for (int i = 0; i < CROWN_POINTS; i++)
        {
            float angle = i * 360f / CROWN_POINTS;
            Part(PrimitiveType.Sphere, root.transform, Quaternion.Euler(0f, angle, 0f) * new Vector3(CROWN_RADIUS, CROWN_TIP, 0f), Vector3.one * 0.2f, Quaternion.identity, gold);
            Part(PrimitiveType.Sphere, root.transform, Quaternion.Euler(0f, angle, 0f) * new Vector3(CROWN_RADIUS + 0.04f, CROWN_BAND * 0.5f, 0f), Vector3.one * 0.17f, Quaternion.identity, jewel);
        }

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.85f, 0.3f);
        light.range = 10f;
        light.intensity = 5f;

        crownMarker = root.AddComponent<CompassMarker>();
        crownMarker.icon = ModeIcons.Crown();
        return root;
    }

    internal const int CROWN_POINTS = 5;
    internal const float CROWN_RADIUS = 0.72f;
    internal const float CROWN_BAND = 0.36f; // the band's height, under the points
    internal const float CROWN_TIP = 0.95f;  // how high the points reach
    const int CROWN_STEPS_PER_POINT = 2;

    /// <summary>The crown's height at a turn round it (0 to 1): a band rising to CROWN_POINTS points</summary>
    internal static float CrownTop(float turn)
    {
        float along = Mathf.Repeat(turn * CROWN_POINTS, 1f);
        float dip = 1f - Mathf.Abs(along * 2f - 1f); // 0 on a point, 1 halfway between two
        return CROWN_BAND + (CROWN_TIP - CROWN_BAND) * (1f - dip);
    }

    /// <summary>A ring whose top zigzags into points, outside and inside, flat-shaded</summary>
    internal static Mesh CrownMesh()
    {
        int steps = CROWN_POINTS * CROWN_STEPS_PER_POINT;
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        const float INNER = 0.92f; // the inside wall's radius, of the outside's
        for (int i = 0; i < steps; i++)
        {
            float a0 = (float)i / steps, a1 = (float)(i + 1) / steps;
            Vector3 d0 = Quaternion.Euler(0f, a0 * 360f, 0f) * Vector3.right * CROWN_RADIUS;
            Vector3 d1 = Quaternion.Euler(0f, a1 * 360f, 0f) * Vector3.right * CROWN_RADIUS;
            Vector3 t0 = Vector3.up * CrownTop(a0), t1 = Vector3.up * CrownTop(a1);
            Quad(vertices, triangles, d0, d1, d1 + t1, d0 + t0);                                 // outside
            Quad(vertices, triangles, d1 * INNER, d0 * INNER, d0 * INNER + t0, d1 * INNER + t1); // inside
            Quad(vertices, triangles, d0 + t0, d1 + t1, d1 * INNER + t1, d0 * INNER + t0);       // the top edge
            Quad(vertices, triangles, d0 * INNER, d1 * INNER, d1, d0);                           // underneath
        }
        Mesh mesh = new Mesh { name = "Crown" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void Quad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
    }

    // A tall pillar of light, soft at its sides and fading upwards, seen from all over the town
    GameObject MakeBeacon()
    {
        const int W = 32, H = 128;
        Color[] pixels = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float up = (y + 0.5f) / H;
            for (int x = 0; x < W; x++)
            {
                float across = Mathf.Abs((x + 0.5f) / W * 2f - 1f);
                pixels[y * W + x] = new Color(1f, 1f, 1f, (1f - across * across) * (1f - up * up));
            }
        }
        Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = "Crown Beacon", wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels);
        texture.Apply();
        GameObject root = new GameObject("Crown Beacon");
        DontDestroyOnLoad(root);
        SpriteRenderer sprite = root.AddComponent<SpriteRenderer>();
        sprite.sprite = Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(0.5f, 0f), W);
        sprite.color = BEACON;
        root.transform.localScale = new Vector3(BEACON_WIDTH, BEACON_HEIGHT / (H / (float)W), 1f);
        return root;
    }

    GameObject MakePotato()
    {
        Order order = AnyOrder();
        GameObject root = new GameObject("Cursed Package");
        DontDestroyOnLoad(root);
        GameObject pulse = new GameObject("Pulse");
        pulse.transform.SetParent(root.transform, false);
        potatoPulse = pulse.transform;

        // The large package, as an order draws it (its mesh is small and scaled up), in cursed purple
        Mesh mesh = order != null ? order.MeshOf(2) : null;
        if (mesh != null)
        {
            GameObject package = new GameObject("Package", typeof(MeshFilter), typeof(MeshRenderer));
            package.transform.SetParent(pulse.transform, false);
            package.transform.localScale = Vector3.one * order.meshScale[2];
            package.transform.localPosition = -mesh.bounds.center * order.meshScale[2];
            package.GetComponent<MeshFilter>().sharedMesh = mesh;
            package.GetComponent<MeshRenderer>().sharedMaterial = Glowing(order.MaterialOf(2), CURSED, CURSED_GLOW * 0.12f, "Cursed Package");
        }
        else
        {
            Part(PrimitiveType.Cube, pulse.transform, Vector3.zero, Vector3.one, Quaternion.identity, null);
        }

        potatoFlame = new GameObject("Pink Flame").AddComponent<SpriteRenderer>();
        potatoFlame.transform.SetParent(root.transform, false);
        potatoFlame.sprite = PurpleFlame.PinkFrames[0];
        float side = potatoFlame.sprite.bounds.size.x;
        potatoFlame.transform.localScale = Vector3.one * (FLAME_SIZE / side);

        TextMeshPro number = new GameObject("Fuse Number").AddComponent<TextMeshPro>();
        number.transform.SetParent(root.transform, false);
        number.transform.localPosition = Vector3.up * NUMBER_ABOVE;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
        if (font != null)
            number.font = font;
        number.fontSize = NUMBER_SIZE;
        number.color = Color.white;
        number.alignment = TextAlignmentOptions.Center;
        number.enableWordWrapping = false;
        number.rectTransform.sizeDelta = new Vector2(6f, 3f);
        number.outlineColor = new Color32(40, 0, 50, 255);
        number.outlineWidth = 0.25f;
        number.sortingOrder = 5;
        fuseNumber = number;

        potatoLight = root.AddComponent<Light>();
        potatoLight.type = LightType.Point;
        potatoLight.color = new Color(1f, 0.2f, 0.75f);
        potatoLight.range = 12f;
        return root;
    }

    // A copy of a package material, tinted and lit from within (null without one)
    internal static Material Glowing(Material original, Color colour, Color glow, string name)
    {
        if (original == null)
            return null;
        Material copy = new Material(original) { name = name };
        Colourblind.Tint(copy, "_BaseColor", colour);
        Colourblind.Tint(copy, "_Color", colour);
        Colourblind.Tint(copy, "_MainColor", colour);
        Colourblind.Tint(copy, "_HalfToneColor", Color.Lerp(colour, Color.white, 0.15f));
        Colourblind.Tint(copy, "_ShadowColor", colour * 0.6f);
        Colourblind.Tint(copy, "_RimColor", glow);
        if (copy.HasProperty("_EmissionColor"))
        {
            copy.EnableKeyword("_EMISSION");
            copy.SetColor("_EmissionColor", glow);
        }
        return copy;
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

    static void AddToCompasses(CompassMarker marker)
    {
        if (PlayerInstantiate.Instance == null)
            return;
        foreach (PlayerSlot viewer in PlayerInstantiate.Instance.Roster.LocalPlayers)
        {
            Compass compass = viewer.Player != null ? viewer.Player.GetComponentInChildren<Compass>() : null;
            if (compass != null)
                compass.AddCompassMarker(marker);
        }
    }

    static void RemoveFromCompasses(CompassMarker marker)
    {
        if (PlayerInstantiate.Instance == null || marker == null)
            return;
        foreach (PlayerSlot viewer in PlayerInstantiate.Instance.Roster.LocalPlayers)
        {
            Compass compass = viewer.Player != null ? viewer.Player.GetComponentInChildren<Compass>() : null;
            if (compass != null)
                compass.RemoveCompassMarker(marker);
        }
    }

    // Infection: every infected ghost wears the infected colour, and shows on the compass as a red ghost
    void ShowInfected(ModeMatch match)
    {
        foreach (int seat in TimedModeRules.Seats(match.Infected & ~shownInfected))
        {
            SkinnedMeshRenderer ghost = GhostOf(seat);
            if (ghost == null)
                continue;
            if (!ghostWas.ContainsKey(seat))
                ghostWas[seat] = ghost.sharedMaterials[0];
            Dress(seat, InfectedCopy(ghostWas[seat]));
            AddInfectedMarker(seat);
            Play("whoosh");
            shownInfected |= 1 << seat;
        }
    }

    // In the same material as the ghost wore, its colours moved: the shader and its other settings stay
    Material InfectedCopy(Material original)
    {
        if (original == null)
            return null;
        if (!infectedCopies.TryGetValue(original, out Material copy) || copy == null)
        {
            copy = new Material(original) { name = "Infected" };
            Colourblind.Tint(copy, "_BaseColor", INFECTED_BODY);
            Colourblind.Tint(copy, "_MainColor", INFECTED_BODY);
            Colourblind.Tint(copy, "_HalfToneColor", INFECTED_RED);
            Colourblind.Tint(copy, "_RimColor", INFECTED_RED);
            Colourblind.Tint(copy, "_ShadowColor", new Color(0.55f, 0.08f, 0.1f));
            infectedCopies[original] = copy;
        }
        return copy;
    }

    void AddInfectedMarker(int seat)
    {
        Transform ball = BallOf(seat);
        if (ball == null || (infectedMarkers.TryGetValue(seat, out GameObject had) && had != null))
            return;
        GameObject holder = new GameObject("Infected Marker");
        holder.transform.SetParent(ball, false);
        CompassMarker marker = holder.AddComponent<CompassMarker>();
        marker.icon = ModeIcons.Infected();
        infectedMarkers[seat] = holder;
        AddToCompasses(marker);
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
        foreach (int seat in TimedModeRules.Seats(match.Out & match.Players & ~shownOut))
        {
            GameObject player = PlayerOf(seat);
            if (player == null)
                continue;
            Play("death");
            TakeOut(seat, player);
            OrderHandler handler = OrderSync.HandlerIn(seat);
            BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
            if (ball != null && !RemoteAvatar.IsRemote(handler) && ball.GetComponent<BotDriver>() == null)
            {
                StartSpectating(seat, player);
                if (ControllerPrompts.Instance != null)
                    ControllerPrompts.Instance.ShowHint(Loc.Get("hotpotato.out"), 3f);
            }
            shownOut |= 1 << seat;
        }
    }

    // Unseen, stopped and not there to hit
    void TakeOut(int seat, GameObject player)
    {
        foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || IsViewPart(renderer))
                continue;
            if (!hidden.ContainsKey(renderer))
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
        }
        foreach (Collider collider in player.GetComponentsInChildren<Collider>(true))
            if (!collider.isTrigger)
                collider.enabled = false;
    }

    // A player who went out mid-respawn comes back when the respawn ends: out again
    void KeepOut()
    {
        foreach (int seat in TimedModeRules.Seats(shownOut))
        {
            OrderHandler handler = OrderSync.HandlerIn(seat);
            BallDriving ball = handler != null ? handler.GetComponent<BallDriving>() : null;
            Collider collider = ball != null && ball.Sphere != null ? ball.Sphere.GetComponent<Collider>() : null;
            if (collider != null && collider.enabled && !RemoteAvatar.IsRemote(handler))
                TakeOut(seat, PlayerOf(seat));
        }
    }

    // The view's own parts (HUD canvases are UI, not renderers; the camera rig has none) stay as they are
    static bool IsViewPart(Renderer renderer) => renderer.GetComponentInParent<Canvas>() != null;

    void StartSpectating(int seat, GameObject player)
    {
        OrbitalCamera orbit = player.GetComponentInChildren<OrbitalCamera>(true);
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
        MakeLabel(watch, seat);
        spectators[seat] = watch;
    }

    // Who they're watching, low in the middle of this player's own view
    void MakeLabel(Spectating watch, int seat)
    {
        if (spectateCanvas == null)
        {
            GameObject holder = new GameObject("Spectating", typeof(RectTransform));
            DontDestroyOnLoad(holder);
            spectateCanvas = GameCard.Canvas(holder, ModeInfoBox.SORTING_ORDER);
        }
        RectTransform box = GameCard.Box(spectateCanvas.transform, "Watching " + (seat + 1), out RectTransform content);
        Rect view = ViewOf(seat);
        box.anchorMin = box.anchorMax = new Vector2(view.x + view.width / 2f, view.y + view.height * 0.15f);
        box.pivot = new Vector2(0.5f, 0f);
        box.anchoredPosition = Vector2.zero;
        watch.Label = box;
        watch.Name = GameCard.Words(content, "Name", 40f, GameCard.GOLD);
        watch.Name.richText = false; // names are other players' text
        GameCard.Words(content, "Switch", 26f, Color.white).text = Loc.Get("spectate.switch");
    }

    // The part of the screen a seat's view has
    static Rect ViewOf(int seat)
    {
        PlayerSlot slot = PlayerInstantiate.Instance != null ? PlayerInstantiate.Instance.Roster[seat] : null;
        PlayerCameraResizer resizer = slot != null && slot.Input != null ? slot.Input.GetComponent<PlayerCameraResizer>() : null;
        return resizer != null && resizer.PlayerReferenceCamera != null ? resizer.PlayerReferenceCamera.rect : new Rect(0f, 0f, 1f, 1f);
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
            Watch(watch);
        }
    }

    // Their camera rides behind the player watched, framed as that player's own view is
    static void Watch(Spectating watch)
    {
        GameObject player = PlayerOf(watch.Watching);
        OrbitalCamera orbit = player != null ? player.GetComponentInChildren<OrbitalCamera>(true) : null;
        Transform ball = BallOf(watch.Watching);
        Transform follow = orbit != null && orbit.virtualCameraMain != null && orbit.virtualCameraMain.Follow != null ? orbit.virtualCameraMain.Follow : ball;
        Transform lookAt = orbit != null && orbit.virtualCameraMain != null && orbit.virtualCameraMain.LookAt != null ? orbit.virtualCameraMain.LookAt : ball;
        if (follow != null && watch.Camera != null)
        {
            watch.Camera.Follow = follow;
            watch.Camera.LookAt = lookAt;
        }
        if (watch.Name != null)
            watch.Name.text = watch.Watching == ModeMatch.NONE ? "" : Loc.Get("spectate.watching", Scoreboard.ShownName(watch.Watching));
    }

    /// <summary>Who an out player watches next: the next seat round from the last that's still in, or NONE</summary>
    internal static int NextIn(int players, int outMask, int after, System.Func<int, bool> there)
    {
        for (int step = 1; step <= Constants.MAX_PLAYERS; step++)
        {
            int seat = ((after < 0 ? -1 : after) + step + Constants.MAX_PLAYERS) % Constants.MAX_PLAYERS;
            if ((players & 1 << seat) != 0 && (outMask & 1 << seat) == 0 && there(seat))
                return seat;
        }
        return ModeMatch.NONE;
    }

    static int NextIn(ModeMatch match, int after) => NextIn(match.Players, match.Out, after, seat => BallOf(seat) != null);

    void PutEverythingBack()
    {
        foreach (KeyValuePair<int, Material> was in ghostWas)
            Dress(was.Key, was.Value);
        ghostWas.Clear();

        foreach (GameObject marker in infectedMarkers.Values)
        {
            if (marker != null)
            {
                RemoveFromCompasses(marker.GetComponent<CompassMarker>());
                Destroy(marker);
            }
        }
        infectedMarkers.Clear();

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
            if (watch.Label != null)
                Destroy(watch.Label.gameObject);
        }
        spectators.Clear();

        SetSlowBoost(shownCrown, false);
        if (crownOnCompass)
            RemoveFromCompasses(crownMarker);
        crownOnCompass = false;
        if (crown != null)
            crown.SetActive(false);
        if (beacon != null)
            beacon.SetActive(false);
        if (potato != null)
            potato.SetActive(false);
        shownCrown = shownPotato = ModeMatch.NONE;
        shownInfected = shownOut = 0;
    }
}

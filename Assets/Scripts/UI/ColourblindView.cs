using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Colourblind mode in the world (Colourblind): each seated player's shape badge over their scooter, and their ghost in
/// its seat's safe colour through a match. Built in code; nothing on the players' prefabs. With the mode off, the
/// badges hide and the ghosts wear the colours they picked again
/// </summary>
public class ColourblindView : MonoBehaviour
{
    static ColourblindView instance;

    // What's known of each seated player's scooter
    class Dressed
    {
        public GameObject Player;
        public Transform Avatar;
        public ColourblindBadge Badge;
        public Renderer[] Ghost; // the body, then the eyelids
        public readonly Dictionary<Renderer, Material> Picked = new Dictionary<Renderer, Material>();
    }

    readonly Dressed[] seats = new Dressed[Constants.MAX_PLAYERS];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null)
            return;
        GameObject holder = new GameObject("Colourblind View");
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<ColourblindView>();
    }

    void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // In Update: the scoreboard's ghost portraits are taken in LateUpdate, of the colours dressed here
    void Update()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return;
        GameManager game = GameManager.Instance;
        bool safe = Colourblind.On && Colourblind.SafeColoursIn(game != null ? game.MainState : GameState.Default);

        for (int seat = 0; seat < seats.Length; seat++)
        {
            PlayerSlot slot = players.Roster[seat];
            GameObject player = slot != null ? slot.Player : null;
            if (player == null)
            {
                seats[seat] = null;
                continue;
            }
            Dressed dressed = seats[seat];
            if (dressed == null || dressed.Player != player)
                dressed = seats[seat] = Find(player, seat);

            if (Colourblind.On && dressed.Badge == null && dressed.Avatar != null)
                dressed.Badge = ColourblindBadge.Attach(player, seat);
            if (dressed.Badge != null && dressed.Badge.gameObject.activeSelf != Colourblind.On)
                dressed.Badge.gameObject.SetActive(Colourblind.On);
            Dress(dressed, seat, safe);
        }
    }

    static Dressed Find(GameObject player, int seat)
    {
        Transform avatar = HighlightReel.Avatar(PlayerInstantiate.Instance.Roster[seat]);
        Dressed dressed = new Dressed { Player = player, Avatar = avatar };
        List<Renderer> ghost = new List<Renderer>();
        foreach (string path in new[] { ScooterLook.GHOST, ScooterLook.EYELID_LEFT, ScooterLook.EYELID_RIGHT })
        {
            Transform part = avatar != null ? avatar.Find(path) : null;
            Renderer renderer = part != null ? part.GetComponent<Renderer>() : null;
            if (renderer != null)
                ghost.Add(renderer);
        }
        dressed.Ghost = ghost.ToArray();
        return dressed;
    }

    // The ghost's first material: its seat's safe copy, or the one picked. A colour picked again while safe (code
    // dressing the ghost) becomes the one to go back to
    static void Dress(Dressed dressed, int seat, bool safe)
    {
        foreach (Renderer renderer in dressed.Ghost)
        {
            if (renderer == null)
                continue;
            Material[] materials = renderer.sharedMaterials;
            if (materials.Length == 0 || materials[0] == null)
                continue;
            Material shown = materials[0];
            if (!Colourblind.IsSafeCopy(shown))
                dressed.Picked[renderer] = shown;
            if (!dressed.Picked.TryGetValue(renderer, out Material picked))
                continue;

            Material wanted = safe ? Colourblind.SafeCopy(picked, seat) : picked;
            if (wanted != shown)
            {
                materials[0] = wanted;
                renderer.sharedMaterials = materials;
            }
        }
    }
}

/// <summary>
/// A seat's shape badge and number over its scooter (Colourblind), turning to face each camera as it draws, like the
/// Steam icon: over that icon when the scooter has one. Off the minimap and the scoreboard's portraits (cameras drawing
/// to a texture), and hidden with the rider while they respawn. Its renderers are the scooter's, so the highlight reel
/// hides it with them
/// </summary>
public class ColourblindBadge : MonoBehaviour
{
    /// <summary>How high over the scooter's base the badge's centre floats, in metres</summary>
    internal const float HEIGHT = 2.8f;

    /// <summary>...or over a Steam icon, with one</summary>
    internal const float OVER_ICON = SteamIcon.HEIGHT + 0.85f;

    /// <summary>The badge's width, in metres</summary>
    internal const float SIZE = 0.85f;

    static readonly List<ColourblindBadge> shown = new List<ColourblindBadge>();
    static TMP_FontAsset font;

    Respawn respawn;
    GameObject picture;

    /// <summary>A seat's badge over a player's scooter (its driving object, as the Steam icon)</summary>
    public static ColourblindBadge Attach(GameObject player, int seat)
    {
        BallDriving driving = player != null ? player.GetComponentInChildren<BallDriving>(true) : null;
        if (driving == null)
            return null;
        GameObject holder = new GameObject("Colourblind Badge");
        holder.transform.SetParent(driving.transform, false);
        ColourblindBadge badge = holder.AddComponent<ColourblindBadge>();
        badge.respawn = player.GetComponentInChildren<Respawn>(true);
        badge.Build(seat);
        holder.transform.localPosition = new Vector3(0f, driving.GetComponentInChildren<SteamIcon>(true) != null ? OVER_ICON : HEIGHT, 0f);
        return badge;
    }

    void Build(int seat)
    {
        picture = new GameObject("Picture");
        picture.transform.SetParent(transform, false);
        picture.SetActive(false);

        SpriteRenderer shape = new GameObject("Shape").AddComponent<SpriteRenderer>();
        shape.transform.SetParent(picture.transform, false);
        shape.sprite = Colourblind.BadgeOf(seat);
        shape.transform.localScale = Vector3.one * SIZE;

        TextMeshPro number = new GameObject("Number").AddComponent<TextMeshPro>();
        number.transform.SetParent(picture.transform, false);
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/Bangers SDF");
        if (font != null)
            number.font = font;
        number.text = (seat + 1).ToString();
        number.color = Colourblind.INK;
        number.alignment = TextAlignmentOptions.Center;
        number.enableWordWrapping = false;
        number.enableAutoSizing = true;
        number.fontSizeMin = 0.5f;
        number.fontSizeMax = 6f;
        number.rectTransform.sizeDelta = Vector2.one * SIZE * 0.5f;
        // A triangle's middle sits low
        number.rectTransform.localPosition = new Vector3(0f, Colourblind.ShapeOf(seat) == Colourblind.Shape.Triangle ? -SIZE * 0.08f : 0f, -0.01f);
        number.sortingOrder = 1; // over its shape
    }

    void OnEnable()
    {
        if (shown.Count == 0)
            RenderPipelineManager.beginCameraRendering += FaceCamera;
        shown.Add(this);
    }

    void OnDisable()
    {
        shown.Remove(this);
        if (shown.Count == 0)
            RenderPipelineManager.beginCameraRendering -= FaceCamera;
        if (picture != null)
            picture.SetActive(false);
    }

    static void FaceCamera(ScriptableRenderContext context, Camera camera)
    {
        for (int i = 0; i < shown.Count; i++)
            shown[i].Face(camera);
    }

    void Face(Camera camera)
    {
        bool on = SteamIcon.ShowsFor(true, respawn == null || !respawn.RiderHidden, camera.targetTexture != null);
        if (picture.activeSelf != on)
            picture.SetActive(on);
        if (on)
            picture.transform.rotation = camera.transform.rotation;
    }
}

using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The title screen's ghost show. A small stage far from the menu's world: four ghosts on scooters (TitleActor) and a
/// few packages, filmed by its own camera into a see-through texture that the title canvas shows between its backdrop
/// and the logo. While the title screen is up (GameState.Menu) it plays its skits (TitleSkit) one after another in a
/// shuffled order (TitlePlaylist), dressing the cast differently each time (TitleWardrobe). Anywhere else (player
/// select, Options, Credits) it stops at once, parks everyone and switches its camera off.
/// Stage space: x across the screen, y up, the camera looking along +z at the lane (z = 0)
/// </summary>
public class TitleStage : MonoBehaviour
{
    /// <summary>The ghosts made for the show: no skit needs more</summary>
    public const int POOL = 4;
    /// <summary>The pause between two skits (seconds)</summary>
    public const float GAP = 1.5f;
    /// <summary>The title screen fading up from black, once (seconds)</summary>
    /// <summary>How long a package left lying on the stage takes to shrink away when its skit ends (seconds)</summary>
    public const float LEFT_BEHIND = 0.3f;
    /// <summary>How far past the screen's edge a ghost waits and leaves: more than a scooter's length</summary>
    public const float OFF_MARGIN = 3f;
    /// <summary>The tallest thing in the lane (a ghost with a stack of packages), for the screen's edge</summary>
    const float LANE_TOP = 4f;

    // The camera: low and far, a long lens, so the ghosts ride past nearly side-on with a little of the floor showing
    internal static readonly Vector3 CAMERA_AT = new Vector3(0f, 2.6f, -19f);
    internal const float CAMERA_PITCH = 4f;
    internal const float CAMERA_FOV = 26f;

    [Header("The cast")]
    [SerializeField] internal GameObject avatarPrefab;
    [Tooltip("Small, medium, large")]
    [SerializeField] internal GameObject[] packagePrefabs;
    [SerializeField] internal Material goldenMaterial;

    [Header("The wardrobe")]
    [SerializeField] internal PlayerHatInformationSO[] hats;
    [SerializeField] internal PlayerColorInformationSO[] colours;
    [SerializeField] internal CompanyInformation[] companies;

    [Header("The picture")]
    [SerializeField] internal Camera stageCamera;
    [Tooltip("On the title canvas, between the backdrop and the logo")]
    [SerializeField] internal RawImage ghostImage;
    [Tooltip("The phase into the menu: the show waits for its crew to leave before the first skit")]
    [SerializeField] internal MenuGhostReveal intro;

    [Tooltip("The font of the words that pop up over ghosts (TextMesh Pro's default when empty)")]
    [SerializeField] internal TMP_FontAsset popFont;
    readonly List<TextMeshPro> pops = new List<TextMeshPro>();

    internal TitleActor[] actors;
    internal TitleSkit[] skits;
    internal System.Random random = new System.Random();
    TitleWardrobe wardrobe;
    TitlePlaylist playlist;
    RenderTexture picture;
    bool filming;

    readonly List<Transform> packages = new List<Transform>();       // every package made
    readonly List<Transform> packagesOut = new List<Transform>();    // those a skit holds
    readonly Dictionary<Transform, int> packageSize = new Dictionary<Transform, int>();
    readonly Dictionary<Transform, Vector3> packageScale = new Dictionary<Transform, Vector3>();
    /// <summary>A small, medium and large package's longest side on the stage (a scooter is about 2 long)</summary>
    static readonly float[] PACKAGE_SIZE = { 0.55f, 0.75f, 1.05f };
    readonly Dictionary<Transform, Material[]> packageLooks = new Dictionary<Transform, Material[]>();

    /// <summary>Whether the show is on</summary>
    public bool Running { get; private set; }
    /// <summary>The skit being played (null between two)</summary>
    internal TitleSkit Current { get; private set; }
    /// <summary>The cast of the skit being played, as dressed</summary>
    internal TitleActor[] CurrentCast { get; private set; }

    /// <summary>Tests: the picture's size, whatever the screen's (zero: the screen's)</summary>
    internal Vector2Int pictureSize;
    /// <summary>The see-through picture of the stage (null while the show is off and before its first frame)</summary>
    internal RenderTexture Picture => picture;
    /// <summary>Tests: the show is started and stopped by hand, whatever the game's state</summary>
    internal bool manual;
    /// <summary>Tests: a skit has just finished, its cast still where it left them</summary>
    internal System.Action<TitleSkit, TitleActor[]> SkitEnded;

    /// <summary>The 12 skits, in the design's order</summary>
    internal static TitleSkit[] AllSkits()
    {
        return new TitleSkit[]
        {
            new TheSnatch(), new PassTheParcel(), new WrongWay(), new ShowOff(), new HatThief(), new Tower(),
            new TugOfWar(), new ChainChase(), new AirMail(), new Sneak(), new Formation(), new GoldenParcel(),
        };
    }

    void Awake()
    {
        skits = AllSkits();
        playlist = new TitlePlaylist(skits.Length, random);
        wardrobe = new TitleWardrobe(hats.Length, colours.Length, companies.Length, random);

        actors = new TitleActor[POOL];
        for (int i = 0; i < POOL; i++)
        {
            actors[i] = TitleActor.Create(avatarPrefab, transform, "Title Ghost " + (i + 1));
            actors[i].Park(ParkingSpot(i));
        }

        if (stageCamera != null)
            stageCamera.enabled = false;
        if (ghostImage != null)
            ghostImage.enabled = false;
    }

    void OnDestroy()
    {
        ReleasePicture();
    }

    void Update()
    {
        // The show follows the game's state, whoever changed it. Without a game (a test's bare stage) it's run by hand
        GameManager game = GameManager.Instance;
        if (game != null && !manual)
        {
            bool title = game.MainState == GameState.Menu;
            if (title && !Running)
                Begin();
            else if (!title && Running)
                Halt();
        }

        if (filming)
            KeepPicture();
    }

    /// <summary>Starts the show with a fresh skit</summary>
    internal void Begin()
    {
        if (Running)
            return;

        Running = true;
        Film(true);
        StartCoroutine(Show());
    }

    /// <summary>Stops the show at once: the skit is dropped, everyone parks, the packages come back, the camera is off</summary>
    internal void Halt()
    {
        StopAllCoroutines();
        Running = false;
        Clear();
        Film(false);
    }

    /// <summary>The camera and its picture on the title canvas, on or off</summary>
    internal void Film(bool on)
    {
        filming = on;
        if (stageCamera != null)
            stageCamera.enabled = on;
        if (on)
            KeepPicture();
        if (ghostImage != null)
            ghostImage.enabled = on;
    }

    IEnumerator Show()
    {
        // The stage is the intro crew's until they have ridden off
        while (intro != null && !intro.Done)
            yield return null;

        while (true)
        {
            yield return PlayOne(playlist.Next());
            yield return new WaitForSeconds(GAP);
        }
    }

    /// <summary>Dresses a cast, plays one skit through and clears the stage</summary>
    internal IEnumerator PlayOne(int index)
    {
        TitleSkit skit = skits[index];
        TitleActor[] cast = Dress(skit);
        Current = skit;
        CurrentCast = cast;
        yield return skit.Play(this, cast);

        // Packages left lying in view shrink away, so none blinks out
        bool left = false;
        foreach (Transform package in packagesOut)
        {
            Vector3 at = transform.InverseTransformPoint(package.position);
            if (package.GetComponentInParent<TitleActor>() == null && Mathf.Abs(at.x) < EdgeX(at.z) + 0.5f)
            {
                package.DOKill();
                package.DOScale(0f, LEFT_BEHIND).SetEase(Ease.InBack).SetLink(package.gameObject);
                left = true;
            }
        }
        if (left)
            yield return new WaitForSeconds(LEFT_BEHIND + 0.05f);

        SkitEnded?.Invoke(skit, cast);
        Clear();
    }

    /// <summary>
    /// The skit's cast, dressed while off screen: no two alike in hat or colour (TitleWardrobe), and a hat that shows
    /// on whoever the skit says needs one
    /// </summary>
    internal TitleActor[] Dress(TitleSkit skit)
    {
        TitleOutfit[] outfits = wardrobe.Deal(skit.Cast);
        TitleActor[] cast = new TitleActor[skit.Cast];

        for (int i = 0; i < cast.Length; i++)
        {
            if (!skit.NeedsHat(i) || hats[outfits[i].hat].displayHat)
                continue;

            // Trade with a castmate who wears one and needn't, else take any hat nobody in the skit wears
            int trade = -1;
            for (int j = 0; j < cast.Length && trade < 0; j++)
            {
                if (j != i && !skit.NeedsHat(j) && hats[outfits[j].hat].displayHat)
                    trade = j;
            }
            if (trade >= 0)
            {
                int mine = outfits[i].hat;
                outfits[i].hat = outfits[trade].hat;
                outfits[trade].hat = mine;
            }
            else
            {
                for (int hat = 0; hat < hats.Length; hat++)
                {
                    if (hats[hat].displayHat && !Worn(outfits, hat))
                    {
                        outfits[i].hat = hat;
                        break;
                    }
                }
            }
        }

        for (int i = 0; i < cast.Length; i++)
        {
            cast[i] = actors[i];
            cast[i].Wear(hats[outfits[i].hat], colours[outfits[i].colour], companies[outfits[i].company]);
        }
        return cast;
    }

    /// <summary>The wardrobe's "no hat" (null when every hat shows)</summary>
    public PlayerHatInformationSO BareHead
    {
        get
        {
            foreach (PlayerHatInformationSO hat in hats)
            {
                if (!hat.displayHat)
                    return hat;
            }
            return null;
        }
    }

    static bool Worn(TitleOutfit[] outfits, int hat)
    {
        foreach (TitleOutfit outfit in outfits)
        {
            if (outfit.hat == hat)
                return true;
        }
        return false;
    }

    /// <summary>Everyone off the stage and every package back, with nothing left moving</summary>
    void Clear()
    {
        Current = null;
        CurrentCast = null;
        for (int i = 0; i < actors.Length; i++)
            actors[i].Park(ParkingSpot(i));
        for (int i = packagesOut.Count - 1; i >= 0; i--)
            ReturnPackage(packagesOut[i]);
        ClearPops();
    }

    /// <summary>Where a ghost waits between skits: well off the stage, each on its own spot</summary>
    Vector3 ParkingSpot(int actor)
    {
        return new Vector3(-(OffX(0f) + 20f + actor * 5f), 0f, 0f);
    }

    // ---- Packages ----

    /// <summary>
    /// The scale that makes a package this big across its longest side on the stage. The prefabs are sized for the
    /// orders' holders in a match, not for the world
    /// </summary>
    static Vector3 FitScale(Transform package, float longest)
    {
        package.localScale = Vector3.one;
        package.localRotation = Quaternion.identity;
        Bounds bounds = new Bounds(package.position, Vector3.zero);
        bool any = false;
        foreach (Renderer renderer in package.GetComponentsInChildren<Renderer>(true))
        {
            if (any)
                bounds.Encapsulate(renderer.bounds);
            else
                bounds = renderer.bounds;
            any = true;
        }
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        return Vector3.one * (any && size > 0.0001f ? longest / size : 1f);
    }

    /// <summary>
    /// A package for a skit (0 small, 1 medium, 2 large), golden or plain, on the stage at its origin. The stage takes
    /// back any a skit still holds when it ends
    /// </summary>
    public Transform TakePackage(int size, bool golden = false)
    {
        size = Mathf.Clamp(size, 0, packagePrefabs.Length - 1);
        Transform package = null;
        foreach (Transform made in packages)
        {
            if (packageSize[made] == size && !packagesOut.Contains(made))
            {
                package = made;
                break;
            }
        }

        if (package == null)
        {
            package = Instantiate(packagePrefabs[size], transform).transform;
            package.name = "Title Package";
            foreach (Collider collider in package.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (Rigidbody body in package.GetComponentsInChildren<Rigidbody>(true))
                body.isKinematic = true;
            foreach (MonoBehaviour script in package.GetComponentsInChildren<MonoBehaviour>(true))
                script.enabled = false;
            packages.Add(package);
            packageSize[package] = size;
            packageScale[package] = FitScale(package, PACKAGE_SIZE[Mathf.Min(size, PACKAGE_SIZE.Length - 1)]);
            Renderer look = package.GetComponentInChildren<Renderer>(true);
            packageLooks[package] = look != null ? look.sharedMaterials : null;
        }

        packagesOut.Add(package);
        package.SetParent(transform, false);
        package.localPosition = Vector3.zero;
        package.localRotation = Quaternion.identity;
        package.localScale = packageScale[package];

        Renderer renderer = package.GetComponentInChildren<Renderer>(true);
        if (renderer != null && packageLooks[package] != null)
        {
            Material[] materials = (Material[])packageLooks[package].Clone();
            if (golden && goldenMaterial != null)
            {
                for (int i = 0; i < materials.Length; i++)
                    materials[i] = goldenMaterial;
            }
            renderer.sharedMaterials = materials;
        }

        package.gameObject.SetActive(true);
        return package;
    }

    public void ReturnPackage(Transform package)
    {
        if (package == null || !packagesOut.Remove(package))
            return;

        package.DOKill();
        package.SetParent(transform, false);
        package.gameObject.SetActive(false);
    }

    /// <summary>The packages a skit holds right now</summary>
    internal IReadOnlyList<Transform> PackagesOut => packagesOut;

    // ---- Words ----

    /// <summary>
    /// A word popping up on the stage like a comic's ("!", "Zzz", "?"): it grows in, drifts up and fades. Returns it,
    /// for a skit that wants to move it along with a ghost
    /// </summary>
    public Transform Pop(string word, Vector3 at, float seconds, Color colour, float size = 9f)
    {
        TextMeshPro text = null;
        foreach (TextMeshPro made in pops)
        {
            if (!made.gameObject.activeSelf)
            {
                text = made;
                break;
            }
        }
        if (text == null)
        {
            GameObject popObject = new GameObject("Title Pop");
            popObject.transform.SetParent(transform, false);
            text = popObject.AddComponent<TextMeshPro>();
            if (popFont != null)
                text.font = popFont;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.fontStyle = FontStyles.Bold;
            pops.Add(text);
        }

        text.DOKill();
        text.transform.DOKill();
        text.text = word;
        text.fontSize = size;
        text.color = colour;
        text.transform.localPosition = at;
        text.transform.localRotation = Quaternion.identity;
        text.transform.localScale = Vector3.zero;
        text.gameObject.SetActive(true);

        GameObject link = text.gameObject;
        text.transform.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetLink(link);
        text.transform.DOLocalMoveY(at.y + 0.9f, seconds).SetEase(Ease.OutQuad).SetLink(link);
        text.DOFade(0f, seconds * 0.35f).SetDelay(seconds * 0.65f).SetLink(link)
            .OnComplete(() => link.SetActive(false));
        return text.transform;
    }

    void ClearPops()
    {
        foreach (TextMeshPro text in pops)
        {
            text.DOKill();
            text.transform.DOKill();
            text.gameObject.SetActive(false);
        }
    }

    // ---- The screen's edges ----

    /// <summary>The screen's edge in the lane (|x|), at this depth</summary>
    public float EdgeX(float z = 0f)
    {
        // The camera looks straight down the stage, only pitched, so x is half the view's width at the point's distance
        // along its forward. The farthest of the lane's floor and top is the widest
        Vector3 forward = Quaternion.Euler(CAMERA_PITCH, 0f, 0f) * Vector3.forward;
        float low = Vector3.Dot(new Vector3(0f, 0f, z) - CAMERA_AT, forward);
        float high = Vector3.Dot(new Vector3(0f, LANE_TOP, z) - CAMERA_AT, forward);
        float aspect = stageCamera != null ? stageCamera.aspect : 16f / 9f;
        return Mathf.Max(low, high) * Mathf.Tan(CAMERA_FOV * 0.5f * Mathf.Deg2Rad) * aspect;
    }

    /// <summary>Just out of sight (|x|), at this depth: where ghosts come on from and leave to</summary>
    public float OffX(float z = 0f)
    {
        return EdgeX(z) + OFF_MARGIN;
    }

    /// <summary>Just out of sight in the lane itself</summary>
    public float OffScreenX => OffX(0f);

    // ---- The picture ----

    /// <summary>Puts the stage's camera where the show is framed from (the builder and tests share it)</summary>
    internal static void Frame(Camera camera)
    {
        camera.transform.localPosition = CAMERA_AT;
        camera.transform.localRotation = Quaternion.Euler(CAMERA_PITCH, 0f, 0f);
        camera.orthographic = false;
        camera.fieldOfView = CAMERA_FOV;
        camera.nearClipPlane = 0.3f;
        camera.farClipPlane = 80f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        camera.allowHDR = false;
    }

    /// <summary>The see-through texture the camera films into, the size of the screen (made again when that changes)</summary>
    void KeepPicture()
    {
        if (stageCamera == null)
            return;

        int width = pictureSize.x > 0 ? pictureSize.x : Mathf.Max(Screen.width, 16);
        int height = pictureSize.y > 0 ? pictureSize.y : Mathf.Max(Screen.height, 16);
        if (picture != null && picture.width == width && picture.height == height)
            return;

        ReleasePicture();
        picture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        picture.name = "Title Stage";
        picture.antiAliasing = 4;
        stageCamera.targetTexture = picture;
        if (ghostImage != null)
            ghostImage.texture = picture;
    }

    void ReleasePicture()
    {
        if (picture == null)
            return;

        if (stageCamera != null)
            stageCamera.targetTexture = null;
        picture.Release();
        Destroy(picture);
        picture = null;
    }

    /// <summary>A bare stage for tests: no canvas, its own camera, never started by a game state</summary>
    internal static TitleStage CreateForTest(GameObject avatarPrefab, GameObject[] packagePrefabs,
        PlayerHatInformationSO[] hats, PlayerColorInformationSO[] colours, CompanyInformation[] companies, int seed)
    {
        GameObject stageObject = new GameObject("Title Stage (test)");
        stageObject.SetActive(false);
        TitleStage stage = stageObject.AddComponent<TitleStage>();
        stage.avatarPrefab = avatarPrefab;
        stage.packagePrefabs = packagePrefabs;
        stage.hats = hats;
        stage.colours = colours;
        stage.companies = companies;
        stage.random = new System.Random(seed);
        GameObject cameraObject = new GameObject("Title Stage Camera");
        cameraObject.transform.SetParent(stageObject.transform, false);
        stage.stageCamera = cameraObject.AddComponent<Camera>();
        Frame(stage.stageCamera);
        stageObject.SetActive(true);
        return stage;
    }
}

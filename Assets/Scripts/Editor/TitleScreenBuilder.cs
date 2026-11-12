using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools → Dead on Arrival → Title Screen: builds the title screen's look in the menu scene. On the title canvas the
/// purple and pink corners and the old ghost wipe go, and a dark backdrop, drifting fog, a floor glow and the ghost
/// show's picture go in behind the logo (which stays where it is, like the entries and everything they're wired to).
/// The old ghost rig is replaced by the Title Stage (TitleStage), wired to the game's hats, colours, companies, scooter
/// and packages. It also wires the boot intro's engine sound in the splash scene. Both can be run again
/// </summary>
public static class TitleScreenBuilder
{
    const string MENU = "Tools/Dead on Arrival/Title Screen/";
    public const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
    public const string SPLASH_SCENE = "Assets/Scenes/SplashScreen.unity";

    public const string CANVAS = "Main Menu Canvas";
    public const string STAGE = "Title Stage";
    /// <summary>The phase into the menu: the crew's rig, and its picture over the title canvas</summary>
    public const string INTRO_RIG = "Menu Ghost Animations";
    public const string INTRO_PICTURE = "Menu Ghost Reveal";
    public const string BACKDROP = "Title Backdrop";
    public const string FOG = "Title Fog";
    public const string FLOOR = "Title Floor";
    public const string GHOSTS = "Title Ghosts";
    public const string FADE = "Title Fade";
    /// <summary>What the builder puts behind the logo, back to front</summary>
    public static readonly string[] Behind = { BACKDROP, FOG, FLOOR, GHOSTS };
    /// <summary>What the old title screen had that the new one doesn't</summary>
    public static readonly string[] Old = { "Background Purple", "Background Pink", FADE };

    const string BACKDROP_SPRITE = "Assets/Sprites/Title/TitleBackdrop.png";
    const string FOG_TEXTURE = "Assets/Sprites/Title/TitleFog.png";
    const string FLOOR_SPRITE = "Assets/Sprites/Title/TitleFloor.png";
    const string GOLDEN = "Assets/Materials/Title/TitleGolden.mat";
    const string ENGINE = "Assets/Audio/SFX/IntroEngine.wav";
    const string SPLASH_CLIP = "Assets/Animations/SplashScreen/Animate.anim";
    const string FONT = "Assets/Plugins/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset";
    const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";
    static readonly string[] Packages =
    {
        "Assets/Prefabs/Player Prefabs/SmallPackage.prefab",
        "Assets/Prefabs/Player Prefabs/MediumPackage.prefab",
        "Assets/Prefabs/Player Prefabs/LargePackage.prefab",
    };
    const string HATS = "Assets/Scriptable Ojects/Player Hats";
    const string COLOURS = "Assets/Scriptable Ojects/Player Colors";
    const string COMPANIES = "Assets/Scriptable Ojects/Company Information";

    // Far from the menu's world, out of every other camera's sight: behind and to one side of the intro's rig
    // (at -1119.75, 1.69, -9.18, its cameras looking along +z), so neither films the other
    static readonly Vector3 StageAt = new Vector3(-1419.75f, 1.69f, -309.18f);
    const float FOG_ALPHA = 0.22f;
    /// <summary>The mist lies along the road: from and to, up the screen</summary>
    const float FOG_FROM = 0.17f, FOG_TO = 0.56f;
    // The floor glow: a soft band where the lane meets the screen (canvas units from the screen's middle)
    static readonly Color FloorColour = new Color(0.52f, 0.30f, 0.85f, 0.20f);
    const float FLOOR_Y = -235f;
    const float FLOOR_HEIGHT = 260f;
    /// <summary>The engine starts this long before the wiper does, and revs away as it crosses</summary>
    public const float ENGINE_LEAD = 1f;

    [MenuItem(MENU + "Build", false, 70)]
    public static void Build()
    {
        Scene scene = OpenScene(MENU_SCENE);
        BuildTitle(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Title screen built in " + MENU_SCENE);
    }

    [MenuItem(MENU + "Wire Intro Sound", false, 71)]
    public static void WireIntroSound()
    {
        Scene scene = OpenScene(SPLASH_SCENE);
        WireSplash(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Intro engine sound wired in " + SPLASH_SCENE);
    }

    /// <summary>Both, from the command line (-executeMethod TitleScreenBuilder.BuildFromCommandLine)</summary>
    public static void BuildFromCommandLine()
    {
        Scene menu = EditorSceneManager.OpenScene(MENU_SCENE, OpenSceneMode.Single);
        BuildTitle(menu);
        EditorSceneManager.SaveScene(menu);
        Scene splash = EditorSceneManager.OpenScene(SPLASH_SCENE, OpenSceneMode.Single);
        WireSplash(splash);
        EditorSceneManager.SaveScene(splash);
        AssetDatabase.SaveAssets();
    }

    // ---------------------------------------------------------------- The title screen

    public static TitleStage BuildTitle(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        GameObject canvas = roots.FirstOrDefault(r => r.name == CANVAS);
        if (canvas == null)
            throw new System.InvalidOperationException("No " + CANVAS + " in " + scene.path);

        // The old look, and anything an earlier build made
        foreach (string name in Old.Concat(Behind))
        {
            Transform child = canvas.transform.Find(name);
            if (child != null)
                Object.DestroyImmediate(child.gameObject);
        }
        foreach (GameObject root in roots)
        {
            if (root != null && root.name == STAGE)
                Object.DestroyImmediate(root);
        }

        // Behind the logo, back to front
        Image backdrop = Ui<Image>(BACKDROP, canvas.transform);
        backdrop.sprite = SpriteAt(BACKDROP_SPRITE);
        backdrop.color = Color.white;

        RawImage fog = Ui<RawImage>(FOG, canvas.transform);
        fog.texture = FogTexture();
        fog.color = new Color(0.75f, 0.6f, 1f, FOG_ALPHA);
        fog.rectTransform.anchorMin = new Vector2(0f, FOG_FROM);
        fog.rectTransform.anchorMax = new Vector2(1f, FOG_TO);
        fog.gameObject.AddComponent<TitleBackdrop>();

        Image floor = Ui<Image>(FLOOR, canvas.transform);
        floor.sprite = FloorSprite();
        floor.color = FloorColour;
        RectTransform floorRect = floor.rectTransform;
        floorRect.anchorMin = new Vector2(0f, 0.5f);
        floorRect.anchorMax = new Vector2(1f, 0.5f);
        floorRect.pivot = new Vector2(0.5f, 0.5f);
        floorRect.anchoredPosition = new Vector2(0f, FLOOR_Y);
        floorRect.sizeDelta = new Vector2(0f, FLOOR_HEIGHT);

        RawImage ghosts = Ui<RawImage>(GHOSTS, canvas.transform);
        ghosts.color = Color.white;
        ghosts.enabled = false; // the stage turns it on with the show

        for (int i = 0; i < Behind.Length; i++)
            canvas.transform.Find(Behind[i]).SetSiblingIndex(i);

        // Over everything: the phase into the menu, which the scene keeps from before
        Transform introPicture = canvas.transform.Find(INTRO_PICTURE);
        GameObject introRig = roots.FirstOrDefault(r => r != null && r.name == INTRO_RIG);
        if (introPicture == null || introRig == null)
            throw new System.InvalidOperationException("The menu scene has lost its intro (" + INTRO_RIG + " and " + INTRO_PICTURE + ").");
        introPicture.SetAsLastSibling();
        introPicture.GetComponent<Graphic>().raycastTarget = false;

        // The stage
        GameObject stageObject = new GameObject(STAGE);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(stageObject, scene);
        stageObject.transform.position = StageAt;
        TitleStage stage = stageObject.AddComponent<TitleStage>();

        GameObject cameraObject = new GameObject("Title Stage Camera");
        cameraObject.transform.SetParent(stageObject.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        TitleStage.Frame(camera);
        camera.enabled = false;
        camera.depth = -10;
        UniversalAdditionalCameraData data = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = false;
        data.renderShadows = false;
        data.antialiasing = AntialiasingMode.None;

        // The scene's moonlight is the key. Two rims from behind pick the ghosts out of the dark: the logo's green and purple
        Rim(stageObject.transform, "Title Rim Green", new Vector3(-9f, 6f, 7f), new Color(0.45f, 1f, 0.4f), 110f);
        Rim(stageObject.transform, "Title Rim Purple", new Vector3(9f, 5f, 6f), new Color(0.75f, 0.4f, 1f), 110f);
        Rim(stageObject.transform, "Title Fill", new Vector3(0f, 6f, -11f), new Color(1f, 0.96f, 0.9f), 150f);

        stage.avatarPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AVATAR);
        stage.packagePrefabs = Packages.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>(p)).ToArray();
        stage.hats = All<PlayerHatInformationSO>(HATS);
        stage.colours = All<PlayerColorInformationSO>(COLOURS);
        stage.companies = All<CompanyInformation>(COMPANIES);
        stage.goldenMaterial = GoldenMaterial();
        stage.popFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        stage.stageCamera = camera;
        stage.ghostImage = ghosts;
        stage.intro = introRig.GetComponent<MenuGhostReveal>();
        EditorUtility.SetDirty(stage);

        if (stage.avatarPrefab == null || stage.packagePrefabs.Any(p => p == null))
            throw new System.InvalidOperationException("The scooter or a package prefab is missing.");
        return stage;
    }

    static void Rim(Transform stage, string name, Vector3 at, Color colour, float intensity)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.transform.SetParent(stage, false);
        lightObject.transform.localPosition = at;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = colour;
        light.intensity = intensity;
        light.range = 60f;
        light.shadows = LightShadows.None;
        light.lightmapBakeType = LightmapBakeType.Realtime;
    }

    // ---------------------------------------------------------------- The boot intro

    public static SplashScreen WireSplash(Scene scene)
    {
        SplashScreen splash = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<SplashScreen>(true)).FirstOrDefault();
        if (splash == null)
            throw new System.InvalidOperationException("No SplashScreen in " + scene.path);

        float wiperStart, wiperEnd;
        WiperSweep(out wiperStart, out wiperEnd);
        splash.engineClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ENGINE);
        splash.engineDelay = Mathf.Max(0f, wiperStart - ENGINE_LEAD);
        splash.revAt = wiperStart;
        splash.revTime = Mathf.Max(0.1f, wiperEnd - wiperStart);
        EditorUtility.SetDirty(splash);

        if (splash.engineClip == null)
            throw new System.InvalidOperationException(ENGINE + " is missing: run tools/intro-engine.py.");
        return splash;
    }

    /// <summary>When the splash animation's wiper starts across, and when it's done (seconds into the clip)</summary>
    public static void WiperSweep(out float start, out float end)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SPLASH_CLIP);
        start = 0f;
        end = clip != null ? clip.length : 0f;
        if (clip == null)
            return;

        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.path != "Wiper" || binding.propertyName != "m_AnchoredPosition.x")
                continue;

            // The last key before it moves any distance, and the last key
            Keyframe[] keys = AnimationUtility.GetEditorCurve(clip, binding).keys;
            for (int i = 0; i + 1 < keys.Length; i++)
            {
                if (Mathf.Abs(keys[i + 1].value - keys[i].value) > 10f)
                {
                    start = keys[i].time;
                    end = keys[keys.Length - 1].time;
                    return;
                }
            }
        }
    }

    // ---------------------------------------------------------------- Assets

    static Sprite SpriteAt(string path)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
            throw new System.InvalidOperationException(path + " is missing: run tools/title-backdrop.py.");
        // Uncompressed: block compression shows as squares on dark, smooth pictures like these
        if (importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Texture2D FogTexture()
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(FOG_TEXTURE);
        if (importer == null)
            throw new System.InvalidOperationException(FOG_TEXTURE + " is missing: run tools/title-backdrop.py.");
        if (importer.wrapMode != TextureWrapMode.Repeat || !importer.alphaIsTransparency
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(FOG_TEXTURE);
    }

    /// <summary>A soft white ellipse, clear at its edges: the floor glow, tinted and stretched on the canvas</summary>
    static Sprite FloorSprite()
    {
        if (!File.Exists(FLOOR_SPRITE))
        {
            const int W = 256, H = 64;
            Texture2D texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float dx = (x + 0.5f) / W * 2f - 1f, dy = (y + 0.5f) / H * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float a = d * d * (3f - 2f * d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            File.WriteAllBytes(FLOOR_SPRITE, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(FLOOR_SPRITE);
        }
        return SpriteAt(FLOOR_SPRITE);
    }

    /// <summary>The golden parcel's look: gold that glows a little by itself, since the stage has no bloom</summary>
    static Material GoldenMaterial()
    {
        Material golden = AssetDatabase.LoadAssetAtPath<Material>(GOLDEN);
        if (golden != null)
            return golden;

        Directory.CreateDirectory(Path.GetDirectoryName(GOLDEN));
        AssetDatabase.Refresh();
        golden = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        golden.SetColor("_BaseColor", new Color(1f, 0.78f, 0.18f));
        golden.SetFloat("_Metallic", 0.6f);
        golden.SetFloat("_Smoothness", 0.7f);
        golden.EnableKeyword("_EMISSION");
        golden.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        golden.SetColor("_EmissionColor", new Color(0.9f, 0.6f, 0.08f));
        AssetDatabase.CreateAsset(golden, GOLDEN);
        return golden;
    }

    static T[] All<T>(string folder) where T : Object
    {
        return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<T>).Where(a => a != null).ToArray();
    }

    // ---------------------------------------------------------------- Helpers

    static Scene OpenScene(string path)
    {
        Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        if (scene.isLoaded)
            return scene;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            throw new System.OperationCanceledException("Title screen build cancelled: the open scene has unsaved changes.");
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
    }

    /// <summary>A full-screen picture on the canvas that the mouse passes through</summary>
    static T Ui<T>(string name, Transform parent) where T : Graphic
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(T));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        T graphic = go.GetComponent<T>();
        graphic.raycastTarget = false;
        return graphic;
    }
}

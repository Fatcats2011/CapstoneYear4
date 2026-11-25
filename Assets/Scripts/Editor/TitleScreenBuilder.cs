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
    /// <summary>The phase into the menu: a purple wipe on its picture, over the title canvas</summary>
    public const string INTRO_PICTURE = "Menu Ghost Reveal";
    /// <summary>The ghost crew the phase-in used to pull back to (user, 2026-11-23: removed)</summary>
    public const string OLD_INTRO_RIG = "Menu Ghost Animations";
    const string INTRO_WIPE = "Assets/Rendering/Shaders/MenuTransition.mat";
    public const string BACKDROP = "Title Backdrop";
    /// <summary>The first build's painted fog and floor glow, now real on the stage</summary>
    public const string FOG = "Title Fog";
    public const string FLOOR = "Title Floor";
    public const string ROAD = "Title Road";
    public const string VERGE = "Title Verge";
    public const string MIST = "Title Mist";
    public const string GROUND_MIST = "Title Ground Mist";
    public const string GHOSTS = "Title Ghosts";
    public const string FADE = "Title Fade";
    /// <summary>What the builder puts behind the logo, back to front</summary>
    public static readonly string[] Behind = { BACKDROP, GHOSTS };
    /// <summary>What the old title screen had that the new one doesn't</summary>
    public static readonly string[] Old = { "Background Purple", "Background Pink", FADE, FOG, FLOOR };

    const string BACKDROP_SPRITE = "Assets/Sprites/Title/TitleBackdrop.png";
    const string ROAD_TEXTURE = "Assets/Sprites/Title/TitleRoad.png";
    const string PUFF_TEXTURE = "Assets/Sprites/Title/TitleMistPuff.png";
    const string SHADOW_TEXTURE = "Assets/Sprites/Title/TitleShadow.png";
    const string ROAD_MATERIAL = "Assets/Materials/Title/TitleRoad.mat";
    const string MIST_MATERIAL = "Assets/Materials/Title/TitleMist.mat";
    const string SHADOW_MATERIAL = "Assets/Materials/Title/TitleShadow.mat";
    const string VERGE_MATERIAL = "Assets/Materials/Title/TitleVerge.mat";
    const string PICTURE_MATERIAL = "Assets/Materials/Title/TitleStagePicture.mat";
    const string MIST_SHADER = "DoA/Title Mist";
    public const string PICTURE_SHADER = "DoA/UI Premultiplied";
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

    // Far from the menu's world, out of every other camera's sight
    static readonly Vector3 StageAt = new Vector3(-1419.75f, 1.69f, -309.18f);
    // The road: across the lane from z -2.6 to 3.6, long enough to run past both edges of the screen. Its texture is
    // ROAD_TILE units long (tools/title-backdrop.py)
    const float ROAD_NEAR = -2.6f, ROAD_FAR = 3.6f, ROAD_LENGTH = 64f, ROAD_TILE = 8f;
    /// <summary>The near verge runs from the road to past the bottom of the screen</summary>
    const float VERGE_NEAR = -14f;

    /// <summary>A layer of mist: where its puffs are born (a box on the stage), how big, how long they live, how fast they drift right and their colours</summary>
    struct MistLayer
    {
        public string name;
        public Vector3 centre, box;
        public float sizeMin, sizeMax, lifeMin, lifeMax, driftMin, driftMax;
        public int count;
        public Color a, b;
    }

    static readonly MistLayer[] MistLayers =
    {
        // Low and thick along the road, round the wheels
        new MistLayer
        {
            name = GROUND_MIST, centre = new Vector3(0f, 0.3f, 0.8f), box = new Vector3(28f, 0.3f, 5.2f),
            sizeMin = 1.4f, sizeMax = 2.6f, lifeMin = 10f, lifeMax = 16f, driftMin = 0.15f, driftMax = 0.35f, count = 170,
            a = new Color(0.80f, 0.74f, 1f, 0.17f), b = new Color(0.66f, 0.60f, 0.92f, 0.11f),
        },
        // Higher, bigger and fainter, behind the lane, so it never hangs over the screen below the road
        new MistLayer
        {
            name = MIST, centre = new Vector3(0f, 1.3f, 4.5f), box = new Vector3(30f, 1f, 5f),
            sizeMin = 2.8f, sizeMax = 5f, lifeMin = 14f, lifeMax = 22f, driftMin = 0.25f, driftMax = 0.45f, count = 80,
            a = new Color(0.78f, 0.70f, 1f, 0.09f), b = new Color(0.60f, 0.52f, 0.90f, 0.05f),
        },
    };

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
            if (root != null && (root.name == STAGE || root.name == OLD_INTRO_RIG))
                Object.DestroyImmediate(root);
        }

        // Behind the logo, back to front
        Image backdrop = Ui<Image>(BACKDROP, canvas.transform);
        backdrop.sprite = SpriteAt(BACKDROP_SPRITE);
        backdrop.color = Color.white;

        RawImage ghosts = Ui<RawImage>(GHOSTS, canvas.transform);
        ghosts.color = Color.white;
        ghosts.material = PictureMaterial();
        ghosts.enabled = false; // the stage turns it on with the show

        for (int i = 0; i < Behind.Length; i++)
            canvas.transform.Find(Behind[i]).SetSiblingIndex(i);

        // Over everything: the phase into the menu, a purple wipe the scene keeps from before
        Transform introPicture = canvas.transform.Find(INTRO_PICTURE);
        if (introPicture == null)
            throw new System.InvalidOperationException("The menu scene has lost its intro (" + INTRO_PICTURE + ").");
        introPicture.SetAsLastSibling();
        Image introImage = introPicture.GetComponent<Image>();
        introImage.raycastTarget = false;
        MenuGhostReveal reveal = introPicture.GetComponent<MenuGhostReveal>();
        if (reveal == null)
            reveal = introPicture.gameObject.AddComponent<MenuGhostReveal>();
        reveal.menuGhostRevealObject = introImage;
        reveal.menuGhostRevealMaterialReference = IntroWipe();
        EditorUtility.SetDirty(reveal);

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
        data.requiresDepthTexture = true; // the mist fades where it meets the road

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
        stage.shadowMaterial = MistMaterial(SHADOW_MATERIAL, SHADOW_TEXTURE, new Color(0f, 0f, 0f, 0.55f), 0f);
        Road(stageObject.transform);
        Material mist = MistMaterial(MIST_MATERIAL, PUFF_TEXTURE, Color.white, 1.2f);
        stage.mist = MistLayers.Select(layer => Mist(stageObject.transform, layer, mist)).ToArray();
        stage.popFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);
        stage.stageCamera = camera;
        stage.ghostImage = ghosts;
        stage.intro = reveal;
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

    /// <summary>The road the ghosts ride along: a long flat strip</summary>
    static void Road(Transform stage)
    {
        GameObject road = new GameObject(ROAD);
        road.transform.SetParent(stage, false);
        road.transform.localPosition = new Vector3(0f, 0f, (ROAD_NEAR + ROAD_FAR) / 2f);
        road.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // flat, facing up; the texture's top is the far kerb
        road.transform.localScale = new Vector3(ROAD_LENGTH, ROAD_FAR - ROAD_NEAR, 1f);
        road.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer look = road.AddComponent<MeshRenderer>();
        look.sharedMaterial = RoadMaterial();
        look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        look.receiveShadows = false;

        // The near verge is the backdrop's painting; this only gives the mist something to fade against there
        GameObject verge = new GameObject(VERGE);
        verge.transform.SetParent(stage, false);
        verge.transform.localPosition = new Vector3(0f, 0f, (VERGE_NEAR + ROAD_NEAR) / 2f);
        verge.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        verge.transform.localScale = new Vector3(ROAD_LENGTH, ROAD_NEAR - VERGE_NEAR, 1f);
        verge.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
        MeshRenderer ground = verge.AddComponent<MeshRenderer>();
        ground.sharedMaterial = MaterialAt(VERGE_MATERIAL, Shader.Find("DoA/Depth Only"));
        ground.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ground.receiveShadows = false;
    }

    /// <summary>One layer of mist: soft puffs born all along the road, drifting slowly right and turning as they go</summary>
    static ParticleSystem Mist(Transform stage, MistLayer layer, Material material)
    {
        GameObject holder = new GameObject(layer.name);
        holder.transform.SetParent(stage, false);
        holder.transform.localPosition = layer.centre;
        ParticleSystem mist = holder.AddComponent<ParticleSystem>();
        mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = mist.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(layer.lifeMin, layer.lifeMax);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(layer.sizeMin, layer.sizeMax);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, 2f * Mathf.PI);
        main.startColor = new ParticleSystem.MinMaxGradient(layer.a, layer.b);
        main.maxParticles = layer.count;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = mist.emission;
        emission.rateOverTime = layer.count / ((layer.lifeMin + layer.lifeMax) / 2f);

        ParticleSystem.ShapeModule shape = mist.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = layer.box;

        ParticleSystem.VelocityOverLifetimeModule drift = mist.velocityOverLifetime;
        drift.enabled = true;
        drift.space = ParticleSystemSimulationSpace.Local;
        drift.x = new ParticleSystem.MinMaxCurve(layer.driftMin, layer.driftMax);
        drift.y = new ParticleSystem.MinMaxCurve(0f, 0.02f);
        drift.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

        ParticleSystem.ColorOverLifetimeModule fade = mist.colorOverLifetime;
        fade.enabled = true;
        Gradient inAndOut = new Gradient();
        inAndOut.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = inAndOut;

        ParticleSystem.RotationOverLifetimeModule turn = mist.rotationOverLifetime;
        turn.enabled = true;
        turn.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

        ParticleSystem.SizeOverLifetimeModule grow = mist.sizeOverLifetime;
        grow.enabled = true;
        grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.85f, 1f, 1.2f));

        // Pushed aside by a passing scooter (TitleActor's wake), then settling again
        ParticleSystem.ExternalForcesModule forces = mist.externalForces;
        forces.enabled = true;
        forces.multiplier = 1f;
        ParticleSystem.LimitVelocityOverLifetimeModule settle = mist.limitVelocityOverLifetime;
        settle.enabled = true;
        settle.limit = 10f;
        settle.dampen = 0f;
        settle.drag = 0.8f;

        ParticleSystemRenderer look = holder.GetComponent<ParticleSystemRenderer>();
        look.renderMode = ParticleSystemRenderMode.Billboard;
        look.alignment = ParticleSystemRenderSpace.View;
        look.sortMode = ParticleSystemSortMode.Distance;
        look.sharedMaterial = material;
        look.maxParticleSize = 4f;
        look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        look.receiveShadows = false;
        return mist;
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

    /// <summary>A texture of the stage's, imported uncompressed with mipmaps (they are seen at a slant), wrapping or not</summary>
    static Texture2D StageTexture(string path, bool repeat)
    {
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null)
            throw new System.InvalidOperationException(path + " is missing: run tools/title-backdrop.py.");
        TextureWrapMode wrap = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        if (importer.textureType != TextureImporterType.Default || importer.wrapMode != wrap || !importer.mipmapEnabled
            || importer.textureCompression != TextureImporterCompression.Uncompressed)
        {
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = wrap;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.alphaIsTransparency = !repeat;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>A material asset, made the first time and set up again every build</summary>
    static Material MaterialAt(string path, Shader shader)
    {
        if (shader == null)
            throw new System.InvalidOperationException("A shader for " + path + " is missing.");
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        return material;
    }

    /// <summary>
    /// Worn asphalt, unlit: it shows its texture's night colours as they are, like the painted backdrop it meets. Lit,
    /// it took the menu scene's flat grey ambient light and turned pale
    /// </summary>
    static Material RoadMaterial()
    {
        Material road = MaterialAt(ROAD_MATERIAL, Shader.Find("Universal Render Pipeline/Unlit"));
        road.SetTexture("_BaseMap", StageTexture(ROAD_TEXTURE, true));
        road.SetTextureScale("_BaseMap", new Vector2(ROAD_LENGTH / ROAD_TILE, 1f));
        road.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(road);
        return road;
    }

    /// <summary>A see-through card of the stage's (DoA/Title Mist): a mist puff, or a shadow when soft is zero</summary>
    static Material MistMaterial(string path, string texture, Color tint, float soft)
    {
        Material mist = MaterialAt(path, Shader.Find(MIST_SHADER));
        mist.SetTexture("_MainTex", StageTexture(texture, false));
        mist.SetColor("_Color", tint);
        mist.SetFloat("_SoftDistance", soft);
        mist.renderQueue = 3000;
        EditorUtility.SetDirty(mist);
        return mist;
    }

    /// <summary>The canvas picture of the stage: its texture holds premultiplied colour</summary>
    static Material PictureMaterial()
    {
        Material picture = MaterialAt(PICTURE_MATERIAL, Shader.Find(PICTURE_SHADER));
        EditorUtility.SetDirty(picture);
        return picture;
    }

    /// <summary>
    /// The phase-in's wipe, without the crew's cameras: their render textures were its pictures, and MenuGhostReveal
    /// gives it a purple one of its own
    /// </summary>
    static Material IntroWipe()
    {
        Material wipe = AssetDatabase.LoadAssetAtPath<Material>(INTRO_WIPE);
        if (wipe == null)
            throw new System.InvalidOperationException(INTRO_WIPE + " is missing.");
        foreach (string slot in new[] { "_MainTex", "_ColorCamera", "_BlackCamera", "_RevealColorTex" })
        {
            if (wipe.HasProperty(slot))
                wipe.SetTexture(slot, null);
        }
        EditorUtility.SetDirty(wipe);
        return wipe;
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

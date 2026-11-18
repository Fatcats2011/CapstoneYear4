using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// DoA/Setup/Boost Ring builds the boost ring into PlayerAvatar.prefab: under Control (the BallDriving object) a
/// "Boost Ring" (BoostRing) holding the flat ring quad (BoostRing.mat) and the boost button prompt (a small world-space
/// canvas whose Image follows the player's device: ButtonGlyph, the A / Cross / Space art), and PhaseIndicator's
/// reference to it. Running it again finds what is there and sets it up again, adding nothing twice
/// </summary>
public static class BoostRingSetup
{
    public const string PREFAB = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";
    public const string MATERIAL = "Assets/Rendering/Boost Ring/BoostRing.mat";
    const string SHADER = "DoA/Boost Ring";

    const string HOLDER = "Boost Ring";
    const string RING = "Ring";
    const string PROMPT = "Boost Prompt";
    const string BUTTON = "Button";

    // Boost is the south face button (A), as the pause menu's controls show it; ButtonGlyphSetup adds Cross and Space
    const string XBOX_SHEET = "Assets/Sprites/UI/MenUI_Buttons.png";
    const string BOOST_BUTTON = "MenUI_Buttons_7";

    // The prompt: 100 canvas units across, shown 0.45 m wide (added UI stays small)
    const float PROMPT_UNITS = 100f;
    const float PROMPT_METRES = 0.45f;

    [MenuItem("DoA/Setup/Boost Ring")]
    static void Run()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PREFAB);
        try
        {
            Build(root);
            PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
            Debug.Log("Boost ring: built into " + PREFAB);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>Builds or refreshes the ring under the player avatar's root (loaded prefab contents), and returns it</summary>
    public static BoostRing Build(GameObject root)
    {
        PhaseIndicator horns = root.GetComponentInChildren<PhaseIndicator>(true);
        if (horns == null)
            throw new InvalidOperationException("Boost ring: no PhaseIndicator under " + root.name);
        BallDriving driving = horns.GetComponent<BallDriving>();
        if (driving == null)
            driving = root.GetComponentInChildren<BallDriving>(true);
        if (driving == null)
            throw new InvalidOperationException("Boost ring: no BallDriving under " + root.name);

        Transform control = driving.transform;
        int layer = control.gameObject.layer;

        // The holder: placed on the ground every frame by BoostRing, so its own transform starts at rest
        Transform holder = Child(control, HOLDER, layer, false);
        BoostRing ring = holder.GetComponent<BoostRing>();
        if (ring == null)
            ring = holder.gameObject.AddComponent<BoostRing>();

        MeshRenderer quad = BuildRing(holder, layer);
        GameObject prompt = BuildPrompt(holder, layer);

        SerializedObject ringFields = new SerializedObject(ring);
        ringFields.FindProperty("ring").objectReferenceValue = quad;
        ringFields.FindProperty("prompt").objectReferenceValue = prompt;
        ringFields.FindProperty("control").objectReferenceValue = driving;
        ringFields.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject hornFields = new SerializedObject(horns);
        hornFields.FindProperty("ring").objectReferenceValue = ring;
        hornFields.ApplyModifiedPropertiesWithoutUndo();

        return ring;
    }

    static MeshRenderer BuildRing(Transform holder, int layer)
    {
        Transform quad = Child(holder, RING, layer, false);
        quad.localRotation = Quaternion.Euler(90f, 0f, 0f); // flat, facing up
        quad.localScale = Vector3.one * 4.2f; // BoostRing's default size (1.2 m radius, with room for the burst); it sizes it again at start

        MeshFilter filter = quad.GetComponent<MeshFilter>();
        if (filter == null)
            filter = quad.gameObject.AddComponent<MeshFilter>();
        filter.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = quad.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Material();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        foreach (Collider collider in quad.GetComponents<Collider>())
            UnityEngine.Object.DestroyImmediate(collider, true);
        return renderer;
    }

    static GameObject BuildPrompt(Transform holder, int layer)
    {
        Transform prompt = Child(holder, PROMPT, layer, true);
        Canvas canvas = prompt.GetComponent<Canvas>();
        if (canvas == null)
            canvas = prompt.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        RectTransform area = (RectTransform)prompt;
        area.sizeDelta = new Vector2(PROMPT_UNITS, PROMPT_UNITS);
        area.localScale = Vector3.one * (PROMPT_METRES / PROMPT_UNITS);

        RectTransform button = (RectTransform)Child(prompt, BUTTON, layer, true);
        button.anchorMin = Vector2.zero;
        button.anchorMax = Vector2.one;
        button.offsetMin = Vector2.zero;
        button.offsetMax = Vector2.zero;
        Image image = button.GetComponent<Image>();
        if (image == null)
            image = button.gameObject.AddComponent<Image>();
        image.sprite = AssetDatabase.LoadAllAssetsAtPath(XBOX_SHEET).OfType<Sprite>().FirstOrDefault(s => s.name == BOOST_BUTTON);
        image.preserveAspect = true;
        image.raycastTarget = false;
        if (image.sprite == null)
            Debug.LogWarning("Boost ring: no " + BOOST_BUTTON + " in " + XBOX_SHEET + "; the prompt has no button");
        else
            ButtonGlyphSetup.Attach(image);

        prompt.gameObject.SetActive(false); // BoostRing shows it when the boost is full
        return prompt.gameObject;
    }

    // A child by name, made when missing (as a UI object when ui), on a layer
    static Transform Child(Transform parent, string name, int layer, bool ui)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject made = ui ? new GameObject(name, typeof(RectTransform)) : new GameObject(name);
            child = made.transform;
            child.SetParent(parent, false);
        }
        child.gameObject.layer = layer;
        child.localPosition = Vector3.zero;
        child.localRotation = Quaternion.identity;
        child.localScale = Vector3.one;
        return child;
    }

    // BoostRing.mat, made from the shader if it's missing
    static Material Material()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL);
        if (material != null)
            return material;

        material = new Material(Shader.Find(SHADER));
        AssetDatabase.CreateAsset(material, MATERIAL);
        return material;
    }
}

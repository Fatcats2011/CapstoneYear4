using UnityEngine;

/// <summary>
/// A player's ghost, head and hat, as a small picture for the scoreboard: a still copy of their scooter's model (already
/// in their colour and hat: ScooterLook, CustomizationSelector) stands far outside the city, where no player's camera
/// reaches, with its own camera and light. The camera draws it for a few frames, then stops: the picture stays in its
/// texture. Nothing on the copy runs: it's built switched off and keeps only its meshes
/// </summary>
public class GhostPortrait
{
    /// <summary>Where the copies stand: past every camera's far plane (1000 m) from the city</summary>
    internal static readonly Vector3 ORIGIN = new Vector3(20000f, 0f, 20000f);

    /// <summary>How far apart the seats' copies stand, past each portrait camera's far plane</summary>
    internal const float SPACING = 20f;

    const int SIZE = 128;
    const int FRAMES = 3; // drawn this many frames (the first can come before the skinned meshes settle)
    const string HEAD = "Player Model/Armature.001/Torso/Head";
    static readonly Color BACKDROP = new Color(0.82f, 0.8f, 0.86f, 1f);

    /// <summary>The picture</summary>
    public RenderTexture Texture { get; private set; }

    GameObject holder;
    Camera camera;
    int framesLeft = FRAMES;

    /// <summary>
    /// The portrait of the ghost on a scooter (a player's avatar: HighlightReel.Avatar), for a seat, its copy under
    /// parent. Null without a scooter model to copy
    /// </summary>
    public static GhostPortrait Of(Transform avatar, int seat, Transform parent)
    {
        Transform model = avatar != null ? avatar.Find(ScooterLook.MODEL) : null;
        Transform control = avatar != null ? avatar.Find("Control") : null;
        if (model == null)
            return null;

        GhostPortrait portrait = new GhostPortrait();
        portrait.holder = new GameObject("Ghost Portrait " + (seat + 1));
        portrait.holder.SetActive(false); // nothing on the copy wakes before it's stripped
        portrait.holder.transform.SetParent(parent, false);
        portrait.holder.transform.position = ORIGIN + Vector3.right * SPACING * seat;

        GameObject copy = Object.Instantiate(model.gameObject, portrait.holder.transform);
        // Facing as on its scooter, the scooter facing +z
        Quaternion facing = control != null ? Quaternion.Inverse(control.rotation) * model.rotation : Quaternion.identity;
        copy.transform.SetPositionAndRotation(portrait.holder.transform.position, facing);
        copy.SetActive(true);
        StripToMeshes(copy);

        Transform head = copy.transform.Find(HEAD);
        Vector3 aim = head != null ? head.position + Vector3.up * 0.15f : copy.transform.position + Vector3.up * 1.2f;

        portrait.Texture = new RenderTexture(SIZE, SIZE, 16, RenderTextureFormat.ARGB32) { name = "Ghost Portrait " + (seat + 1) };
        GameObject eye = new GameObject("Portrait Camera");
        eye.transform.SetParent(portrait.holder.transform, false);
        eye.transform.position = aim + new Vector3(0f, 0.25f, 2.1f);
        eye.transform.LookAt(aim);
        Camera camera = eye.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = BACKDROP;
        camera.fieldOfView = 34f;
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = SPACING / 2f;
        camera.allowHDR = false;
        camera.targetTexture = portrait.Texture;
        portrait.camera = camera;

        Light light = new GameObject("Portrait Light").AddComponent<Light>();
        light.transform.SetParent(portrait.holder.transform, false);
        light.transform.position = aim + new Vector3(0.4f, 0.6f, 0.9f);
        light.type = LightType.Point;
        light.range = 4f;
        light.intensity = 2f;

        portrait.holder.SetActive(true);
        return portrait;
    }

    // Everything but its meshes goes (scripts, physics, sound, decals, effects), and only the ghost and its hat show
    static void StripToMeshes(GameObject copy)
    {
        // Dependents first (RequireComponent): scripts, then effect renderers (a particle system's), then the rest
        for (int pass = 0; pass < 3; pass++)
        {
            foreach (Component part in copy.GetComponentsInChildren<Component>(true))
            {
                if (part == null || part is Transform || part is MeshFilter || part is MeshRenderer || part is SkinnedMeshRenderer)
                    continue;
                bool now = pass == 0 ? part is MonoBehaviour : pass == 1 ? part is Renderer : true;
                if (now)
                    Object.DestroyImmediate(part);
            }
        }
        foreach (Renderer renderer in copy.GetComponentsInChildren<Renderer>(true))
        {
            bool ghost = renderer.transform.IsChildOf(copy.transform.Find("Player Model") ?? copy.transform)
                || (copy.transform.Find("Ghost Model") != null && renderer.transform.IsChildOf(copy.transform.Find("Ghost Model")));
            renderer.enabled = ghost;
            if (renderer is SkinnedMeshRenderer skinned)
                skinned.updateWhenOffscreen = true;
        }
    }

    /// <summary>Each frame it's shown: the camera stops once the picture's taken</summary>
    public void Tick()
    {
        if (camera == null || !camera.enabled)
            return;
        if (--framesLeft <= 0)
            camera.enabled = false;
    }

    public void Destroy()
    {
        if (holder != null)
            Object.Destroy(holder);
        if (Texture != null)
        {
            Texture.Release();
            Object.Destroy(Texture);
        }
    }
}

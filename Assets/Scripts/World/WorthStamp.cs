using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The small "×2" over a stolen package, so anyone can read that it is worth more (StealFeedbackRules.StampText: it
/// doubles with each steal, and the golden order and the Mother Lode never have one). One per order, made the first time
/// it has something to say, hidden when the order starts afresh. It hangs off the order, not its mesh, so it doesn't
/// swell with it, floats just over the mesh, and turns to face each camera as that camera draws (every split-screen view
/// reads it straight on). Every machine counts the steals it replays, so they all show the same stamp
/// </summary>
public class WorthStamp : MonoBehaviour
{
    /// <summary>How far over the package's mesh it floats, in metres</summary>
    internal const float HEIGHT_OVER_MESH = 1.6f;
    internal const float WORLD_SIZE = 6f;
    const string BANGERS = "Fonts & Materials/Bangers SDF";
    const float OUTLINE = 0.3f;

    Transform mesh;
    TextMeshPro label;
    string text = "";

    /// <summary>What it reads now: "" while hidden</summary>
    internal string Text { get { return text; } }

    /// <summary>An order's steals changed: its stamp says so. An order with nothing to say and no stamp gets none</summary>
    internal static void Refresh(Order order, Transform mesh)
    {
        string wanted = StealFeedbackRules.StampText(order.Value, order.IsLode, order.Steals);
        WorthStamp stamp = order.GetComponentInChildren<WorthStamp>(true);
        if (stamp == null)
        {
            if (wanted == "")
                return;
            stamp = Make(order, mesh);
        }
        stamp.Set(wanted);
    }

    static WorthStamp Make(Order order, Transform mesh)
    {
        GameObject holder = new GameObject("Worth Stamp");
        holder.transform.SetParent(order.transform, false);
        holder.layer = mesh != null ? mesh.gameObject.layer : order.gameObject.layer;
        WorthStamp stamp = holder.AddComponent<WorthStamp>();
        stamp.mesh = mesh;
        stamp.label = holder.AddComponent<TextMeshPro>();
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(BANGERS);
        if (font != null)
            stamp.label.font = font;
        stamp.label.fontSize = WORLD_SIZE;
        stamp.label.alignment = TextAlignmentOptions.Center;
        stamp.label.enableWordWrapping = false;
        stamp.label.richText = false;
        stamp.label.color = GameCard.GOLD;
        // The outline gives the stamp a material of its own: only in play (edit mode would leak it into the scene)
        if (Application.isPlaying)
        {
            stamp.label.outlineWidth = OUTLINE;
            stamp.label.outlineColor = Color.black;
        }
        stamp.Place();
        return stamp;
    }

    void Set(string wanted)
    {
        text = wanted;
        // A copy of an order copies its stamp too, without these
        if (label == null)
            label = GetComponent<TextMeshPro>();
        if (label != null)
            label.text = wanted;
        if (gameObject.activeSelf != (wanted != ""))
            gameObject.SetActive(wanted != "");
    }

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += FaceCamera;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= FaceCamera;
    }

    // Just over the mesh, wherever the order has it (on a scooter's back, in the air on a drop)
    void Place()
    {
        Vector3 from = mesh != null ? mesh.position : transform.parent != null ? transform.parent.position : transform.position;
        transform.position = from + Vector3.up * HEIGHT_OVER_MESH;
    }

    // Each camera about to draw sees it straight on
    void FaceCamera(ScriptableRenderContext context, Camera camera)
    {
        Place();
        transform.rotation = camera.transform.rotation;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Another machine's player's small Steam profile picture, floating over their scooter (playtest, 2026-10-31). It's
/// part of the world, so buildings hide it like the scooter and it's only seen when the scooter is in view. It turns
/// to face each camera as that camera draws (split screen), stays off the minimap (a camera drawing to a texture),
/// and goes with the scooter when it's hidden (respawning). The winner of the match this one is a rematch of wears a
/// small crown on top of it (RematchCrown). No picture (no Steam, a CPU driver, Steam still loading it):
/// nothing shows. Its picture's renderers are the scooter's like any other, so the highlight reel hides them with it
/// </summary>
public class SteamIcon : MonoBehaviour
{
    /// <summary>How high over the scooter's base the picture's centre floats, in metres</summary>
    internal const float HEIGHT = 2.6f;

    /// <summary>The picture's width, in metres</summary>
    internal const float SIZE = 0.6f;

    // The white border round the picture, in metres each side
    const float BORDER = 0.04f;

    /// <summary>The winner's crown's width, in metres: it sits on the picture's top edge</summary>
    internal const float CROWN = 0.42f;

    static readonly List<SteamIcon> shown = new List<SteamIcon>();

    Func<ulong> steamId;  // the player's Steam ID: it can arrive after the scooter does
    Func<int> seat;       // the player's seat (null: never crowned)
    SpriteRenderer crown;
    Respawn respawn;      // hides the rider while they respawn: the picture goes with them
    GameObject picture;   // the picture and its border, on only while a camera that shows it draws
    SpriteRenderer face;
    SpriteRenderer border;
    Texture2D pictureOf;

    /// <summary>
    /// Puts the Steam picture of whoever steamId names over a remote scooter (null avatar: nothing)
    /// </summary>
    public static SteamIcon Attach(RemoteAvatar avatar, Func<ulong> steamId, Func<int> seat = null)
    {
        if (avatar == null || avatar.Driving == null)
            return null;
        GameObject holder = new GameObject("Steam Icon");
        holder.transform.SetParent(avatar.Driving.transform, false);
        holder.transform.localPosition = new Vector3(0, HEIGHT, 0);
        SteamIcon icon = holder.AddComponent<SteamIcon>();
        icon.steamId = steamId;
        icon.seat = seat;
        icon.respawn = avatar.GetComponentInChildren<Respawn>(true);
        return icon;
    }

    void Awake()
    {
        picture = new GameObject("Picture");
        picture.transform.SetParent(transform, false);
        picture.SetActive(false);

        border = new GameObject("Border").AddComponent<SpriteRenderer>();
        border.transform.SetParent(picture.transform, false);
        border.sprite = SquareOf(Texture2D.whiteTexture, SIZE + BORDER * 2);

        face = new GameObject("Face").AddComponent<SpriteRenderer>();
        face.transform.SetParent(picture.transform, false);
        face.sortingOrder = 1; // in front of its border

        crown = new GameObject("Winner Crown").AddComponent<SpriteRenderer>();
        crown.transform.SetParent(picture.transform, false);
        crown.sortingOrder = 2;
        crown.enabled = false;
    }

    /// <summary>Whether the crown shows on the picture now (tests)</summary>
    internal bool CrownShown => crown != null && crown.enabled;

    // The crown's art at CROWN metres across, its bottom just into the picture's top edge
    void ShowCrown(bool on)
    {
        if (on && crown.sprite == null)
        {
            Sprite art = RematchCrown.Art();
            crown.sprite = art;
            float across = art != null ? art.bounds.size.x : 0f;
            float scale = across > 0f ? CROWN / across : 1f;
            crown.transform.localScale = Vector3.one * scale;
            float tall = art != null ? art.bounds.size.y * scale : CROWN;
            crown.transform.localPosition = new Vector3(0f, SIZE / 2f + BORDER + tall * 0.38f, 0f);
        }
        crown.enabled = on;
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

    void OnDestroy()
    {
        if (face != null && face.sprite != null)
            Destroy(face.sprite);
        if (border != null && border.sprite != null)
            Destroy(border.sprite);
    }

    void Update()
    {
        bool crowned = seat != null && RematchCrown.Wears(seat());
        if (crown.enabled != crowned)
            ShowCrown(crowned);
        if (pictureOf != null || steamId == null)
            return;
        // Asked each frame until Steam has it: SteamAvatars makes the texture once
        Texture2D steam = SteamAvatars.Get(steamId());
        if (steam == null)
            return;
        pictureOf = steam;
        face.sprite = SquareOf(steam, SIZE);
    }

    /// <summary>Whether the picture shows for a camera: there's one, the scooter's there, and it's not the minimap</summary>
    internal static bool ShowsFor(bool hasPicture, bool scooterShown, bool drawsToTexture)
    {
        return hasPicture && scooterShown && !drawsToTexture;
    }

    static void FaceCamera(ScriptableRenderContext context, Camera camera)
    {
        for (int i = 0; i < shown.Count; i++)
            shown[i].Face(camera);
    }

    void Face(Camera camera)
    {
        bool on = ShowsFor(pictureOf != null, respawn == null || !respawn.RiderHidden, camera.targetTexture != null);
        if (picture.activeSelf != on)
            picture.SetActive(on);
        if (on)
            picture.transform.rotation = camera.transform.rotation; // flat to the camera, upright on screen
    }

    // A sprite of the whole texture, size metres across
    static Sprite SquareOf(Texture2D texture, float size)
    {
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), texture.width / size);
    }
}

using UnityEngine;

/// <summary>
/// Which of a player's cameras need to render: every camera costs a cull and a draw per frame per player, so cameras
/// whose picture isn't on screen are turned off (PlayerCameraResizer applies these every frame, after the game's own
/// camera switching). See docs/performance.md
/// - A player's view isn't drawn while a full-screen camera covers it: the cutscene camera (depth 20) and the results
///   camera (depth 100) draw over every player's view.
/// - The menu preview camera renders only in player select, the one place its picture (Player Select Canvas) is shown.
/// </summary>
public static class CameraBudget
{
    /// <summary>A covering camera draws above every player camera (theirs are depth 0-3)</summary>
    public const float COVER_DEPTH = 10f;

    /// <summary>
    /// Whether a camera covers every player's view: drawn to the screen, full-screen, clearing what was there, above the
    /// players' cameras
    /// </summary>
    public static bool Covers(bool hasTargetTexture, Rect rect, CameraClearFlags clear, float depth)
    {
        if (hasTargetTexture || depth < COVER_DEPTH)
            return false;
        if (clear != CameraClearFlags.Skybox && clear != CameraClearFlags.SolidColor)
            return false;

        return rect.x <= 0.001f && rect.y <= 0.001f && rect.width >= 0.999f && rect.height >= 0.999f;
    }

    static Camera[] cameras = new Camera[32];
    static int checkedFrame = -1;
    static bool covered;

    /// <summary>
    /// Whether a full-screen camera covers the players' views this frame (worked out once a frame, for every player)
    /// </summary>
    public static bool CoveredNow()
    {
        if (checkedFrame == Time.frameCount)
            return covered;

        checkedFrame = Time.frameCount;
        if (Camera.allCamerasCount > cameras.Length)
            cameras = new Camera[Camera.allCamerasCount * 2];
        int count = Camera.GetAllCameras(cameras);

        covered = false;
        for (int i = 0; i < count && !covered; i++)
        {
            Camera camera = cameras[i];
            covered = Covers(camera.targetTexture != null, camera.rect, camera.clearFlags, camera.depth);
        }
        for (int i = 0; i < count; i++)
            cameras[i] = null; // no reference kept to a camera a scene unloads
        return covered;
    }

    /// <summary>Whether the menu preview's picture is on screen in a state</summary>
    public static bool PreviewShown(GameState state)
    {
        return state == GameState.PlayerSelect;
    }
}

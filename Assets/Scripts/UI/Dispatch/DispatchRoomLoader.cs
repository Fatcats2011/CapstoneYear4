using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loading and unloading scenes alongside the one that's open (UnityRoomScenes; tests use a fake)
/// </summary>
public interface IRoomScenes
{
    /// <summary>Whether the scene is in the build, so it can load</summary>
    bool CanLoad(string scene);
    bool IsLoaded(string scene);
    void Load(string scene, Action done);
    void Unload(string scene, Action done);
}

/// <summary>
/// The Dispatch room's scene (Multiplayerlobby), loaded alongside the menu while the board is up and unloaded when it
/// goes. One load or unload at a time: a change of mind while one runs is acted on when it ends. A match's own scene
/// load (Single) takes the room with it, so there's then nothing to unload. Nothing is unloaded while the game is leaving
/// Play Mode or quitting: unloading a scene during that teardown crashes Unity (stopping Play online takes the game
/// offline, which hides the board for one last frame), and Unity unloads every scene then anyway. See docs/online.md
/// </summary>
public class DispatchRoomLoader
{
    public const string SCENE = "Multiplayerlobby";

    readonly IRoomScenes scenes;
    readonly Func<bool> leaving;
    readonly string scene = SCENE;
    bool wanted;
    bool warned;
    bool loading;  // the running operation is a load
    bool refused;  // a load ended without the room: not tried again until it's unwanted

    /// <summary>A load or unload is running</summary>
    public bool Busy { get; private set; }

    /// <param name="leaving">Whether the game is leaving Play Mode or quitting (UnityRoomScenes.Leaving); null: never</param>
    public DispatchRoomLoader(IRoomScenes scenes, Func<bool> leaving = null)
    {
        this.scenes = scenes;
        this.leaving = leaving;
    }

    /// <summary>Whether the room should be up</summary>
    public void SetWanted(bool on)
    {
        wanted = on;
        if (!on)
            refused = false;
        Step();
    }

    void Step()
    {
        if (Busy)
            return;

        bool loaded = scenes.IsLoaded(scene);
        if (wanted && !loaded && !refused)
        {
            if (!scenes.CanLoad(scene))
            {
                if (!warned)
                    Debug.LogWarning("The Dispatch room's scene (" + scene + ") isn't in the build: add it to File → Build Settings");
                warned = true;
                return;
            }

            Busy = true;
            loading = true;
            scenes.Load(scene, Done);
        }
        else if (!wanted && loaded && (leaving == null || !leaving()))
        {
            Busy = true;
            loading = false;
            scenes.Unload(scene, Done);
        }
    }

    void Done()
    {
        Busy = false;
        if (loading && !scenes.IsLoaded(scene))
            refused = true;
        Step();
    }
}

/// <summary>
/// Unity's scene loading, additive. Unity's SceneManager, not the game's class of the same name
/// </summary>
public class UnityRoomScenes : IRoomScenes
{
    static bool quitting;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetQuitting()
    {
        quitting = false;
        Application.quitting -= OnQuitting;
        Application.quitting += OnQuitting;
    }

    static void OnQuitting()
    {
        quitting = true;
    }

    /// <summary>
    /// Whether the game is leaving: quitting, or in the editor, Play Mode on its way out (still playing, but Stop was
    /// pressed). Never while editing: the editor raises Application.quitting each time Play stops
    /// </summary>
    public static bool Leaving()
    {
        if (!Application.isPlaying)
            return false;
#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return true;
#endif
        return quitting;
    }

    public bool CanLoad(string scene)
    {
        // Scenes load only while playing (edit mode tests reach the board too)
        return Application.isPlaying && Application.CanStreamedLevelBeLoaded(scene);
    }

    public bool IsLoaded(string scene)
    {
        return UnityEngine.SceneManagement.SceneManager.GetSceneByName(scene).isLoaded;
    }

    public void Load(string scene, Action done)
    {
        AsyncOperation load = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(scene, LoadSceneMode.Additive);
        Finish(load, done);
    }

    public void Unload(string scene, Action done)
    {
        AsyncOperation unload = UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
        Finish(unload, done);
    }

    // An operation Unity refused (null) ends at once
    static void Finish(AsyncOperation operation, Action done)
    {
        if (operation == null)
            done();
        else
            operation.completed += _ => done();
    }
}

using System.IO;
using UnityEditor;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

// TEMPORARY: starts one lighting bake of Design Scene(Main) when Temp/bake_lighting.request exists, and
// writes progress to Temp/BakeLighting/status.txt. Only bakes if that scene is the single open scene, so it
// never opens, closes or replaces a scene. Does not save the scene. Delete this file after use.
[InitializeOnLoad]
static class TempBakeLighting
{
    const string Request = "Temp/bake_lighting.request";
    const string OutDir = "Temp/BakeLighting";
    const string ScenePath = "Assets/Scenes/Design Scene(Main).unity";
    static double started, lastWrite;

    static TempBakeLighting()
    {
        if (!File.Exists(Request)) return;
        File.Delete(Request);
        EditorApplication.delayCall += Start;
    }

    static void Status(string s) => File.WriteAllText(Path.Combine(OutDir, "status.txt"), s);

    static void Start()
    {
        Directory.CreateDirectory(OutDir);
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Status("SKIPPED: editor is in play mode"); return; }
        var active = UnitySceneManager.GetActiveScene();
        if (UnitySceneManager.sceneCount != 1 || active.path != ScenePath)
        {
            Status($"SKIPPED: open only {ScenePath} first (open now: {active.path}, scenes loaded: {UnitySceneManager.sceneCount})");
            return;
        }
        if (Lightmapping.isRunning) { Status("SKIPPED: a bake is already running"); return; }
        Lightmapping.bakeCompleted += Done;
        started = EditorApplication.timeSinceStartup;
        if (!Lightmapping.BakeAsync())
        {
            Lightmapping.bakeCompleted -= Done;
            Status("FAILED: Unity refused to start the bake");
            return;
        }
        EditorApplication.update += Progress;
        Status("RUNNING 0%");
    }

    static void Progress()
    {
        if (!Lightmapping.isRunning || EditorApplication.timeSinceStartup - lastWrite < 10) return;
        lastWrite = EditorApplication.timeSinceStartup;
        Status($"RUNNING {Lightmapping.buildProgress:P0} after {(lastWrite - started) / 60:F1} min");
    }

    static void Done()
    {
        Lightmapping.bakeCompleted -= Done;
        EditorApplication.update -= Progress;
        Status($"DONE in {(EditorApplication.timeSinceStartup - started) / 60:F1} min, scene dirty: {UnitySceneManager.GetActiveScene().isDirty}");
    }
}

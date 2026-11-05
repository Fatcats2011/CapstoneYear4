// Trailer capture's build: copied into the private snapshot's Assets/Editor by tools/trailer/build.py
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class TrailerBuild
{
    public static void Windows()
    {
        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "TrailerBuild/DeadOnArrival.exe",
            target = BuildTarget.StandaloneWindows64,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
            // Direct sessions (a host alone with bots) without a real development build's on-screen watermark
            extraScriptingDefines = new[] { "DEVELOPMENT_BUILD" },
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log("TRAILER build " + report.summary.result + " errors " + report.summary.totalErrors);
        EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}

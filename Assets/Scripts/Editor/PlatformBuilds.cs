using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Tools → Dead on Arrival → Build: a release build for Windows, macOS or Linux into Builds/&lt;platform&gt;, where the
/// SteamPipe scripts in docs/steam/steampipe look for it. Batch mode: -executeMethod PlatformBuilds.BuildMac (or
/// BuildWindows, BuildLinux, BuildAll). The Mac build is Universal (Apple Silicon and Intel)
/// </summary>
public static class PlatformBuilds
{
    const string MENU = "Tools/Dead on Arrival/Build/";

    [MenuItem(MENU + "Windows", false, 60)]
    public static void BuildWindows()
    {
        Build(BuildTarget.StandaloneWindows64, "Builds/Windows/DeadOnArrival.exe");
    }

    [MenuItem(MENU + "macOS", false, 61)]
    public static void BuildMac()
    {
        // UserBuildSettings lives in the macOS Build Support module: reached by name so this file still compiles where
        // that module isn't installed (only the Mac build then fails)
        PropertyInfo architecture = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("UnityEditor.OSXStandalone.UserBuildSettings"))
            .FirstOrDefault(t => t != null)?.GetProperty("architecture", BindingFlags.Public | BindingFlags.Static);
        if (architecture == null)
        {
            Fail("Build StandaloneOSX: the macOS Build Support module isn't installed");
            return;
        }
        architecture.SetValue(null, UnityEditor.Build.OSArchitecture.x64ARM64);
        Build(BuildTarget.StandaloneOSX, "Builds/macOS/Dead on Arrival.app");
    }

    [MenuItem(MENU + "Linux", false, 62)]
    public static void BuildLinux()
    {
        Build(BuildTarget.StandaloneLinux64, "Builds/Linux/DeadOnArrival.x86_64");
    }

    [MenuItem(MENU + "All Three", false, 80)]
    public static void BuildAll()
    {
        // Stops at the first failure, so a broken build is never missed among the others' logs
        Action[] builds = { BuildLinux, BuildMac, BuildWindows };
        foreach (Action build in builds)
        {
            failed = false;
            build();
            if (failed)
                return;
        }
    }

    static bool failed;

    static void Fail(string message)
    {
        failed = true;
        Debug.LogError(message);
        // Batch mode: a failed build must fail the command
        if (Application.isBatchMode)
            EditorApplication.Exit(1);
    }

    static void Build(BuildTarget target, string path)
    {
        // A fresh folder, built beside the last good one and swapped in only on success: files left from an older build
        // would ship with this one, and a failed build must not leave nothing to upload
        string folder = Path.GetDirectoryName(path);
        string staging = folder + ".building";
        if (Directory.Exists(staging))
            Directory.Delete(staging, true);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = Path.Combine(staging, Path.GetFileName(path)),
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"Build {target}: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {path}");

        if (summary.result != BuildResult.Succeeded)
        {
            Fail($"Build {target} failed; the last good build in {folder} is kept");
            return;
        }
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);
        Directory.Move(staging, folder);
    }
}

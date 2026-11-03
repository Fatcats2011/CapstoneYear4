using System.IO;
using System.Linq;
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
        UnityEditor.OSXStandalone.UserBuildSettings.architecture = UnityEditor.Build.OSArchitecture.x64ARM64;
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
        BuildLinux();
        BuildMac();
        BuildWindows();
    }

    static void Build(BuildTarget target, string path)
    {
        // A fresh folder: files left from an older build would ship with this one
        string folder = Path.GetDirectoryName(path);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = path,
            target = target,
            targetGroup = BuildTargetGroup.Standalone,
            options = BuildOptions.None,
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"Build {target}: {summary.result}, {summary.totalErrors} errors, {summary.totalSize / (1024 * 1024)} MB, {path}");

        // Batch mode: a failed build must fail the command
        if (summary.result != BuildResult.Succeeded && Application.isBatchMode)
            EditorApplication.Exit(1);
    }
}

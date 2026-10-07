using UnityEngine;

/// <summary>
/// Measures the frame rate in every build: a "Perf:" line every 30 s of real time (in Player.log, and in the opt-in
/// session log), and, in the editor and development builds, a small box at the top left while toggled with F6
/// (HotKeys). Made once the first scene has loaded, so no scene needs to contain it. See docs/performance.md
/// </summary>
public class PerfOverlay : MonoBehaviour
{
    /// <summary>How often the "Perf:" line is logged, in seconds of real time</summary>
    public const float LOG_EVERY = 30f;

    public static PerfOverlay Instance { get; private set; }

    readonly FrameStats stats = new FrameStats();
    float nextLog;
    bool shown;
    GUIStyle style;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnLaunch()
    {
        if (Instance != null)
            return;

        GameObject holder = new GameObject(nameof(PerfOverlay));
        DontDestroyOnLoad(holder);
        holder.AddComponent<PerfOverlay>();
    }

    void Awake()
    {
        Instance = this;
        nextLog = Time.realtimeSinceStartup + LOG_EVERY;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>Shows or hides the box (F6, editor and development builds)</summary>
    public void Toggle()
    {
        shown = !shown;
    }

    static int Players
    {
        get { return PlayerInstantiate.Instance != null ? PlayerInstantiate.Instance.PlayerCount : 0; }
    }

    void Update()
    {
        stats.Add(Time.unscaledDeltaTime);

        if (Time.realtimeSinceStartup >= nextLog)
        {
            nextLog = Time.realtimeSinceStartup + LOG_EVERY;
            Debug.Log(FrameStats.Line(stats.AverageFps, stats.OnePercentLowFps, Players, Camera.allCamerasCount));
        }
    }

    void OnGUI()
    {
        if (!shown || !DevTools.Enabled)
            return;

        if (style == null)
            style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.UpperLeft };

        float average = stats.AverageFps;
        string text = string.Format("{0:0} fps ({1:0.0} ms)\n1% low {2:0} fps\n{3} players, {4} cameras",
            average, average > 0f ? 1000f / average : 0f, stats.OnePercentLowFps, Players, Camera.allCamerasCount);
        GUI.Box(new Rect(8, 8, 220, 74), text, style);
    }
}

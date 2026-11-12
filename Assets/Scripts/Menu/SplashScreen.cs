using System.Collections;
using System.IO;
using Udar.SceneManager;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SplashScreen : MonoBehaviour
{
    [SerializeField] float timer;
    Coroutine sceneLoadCoroutune;
    [SerializeField] SceneField PlayerSelectScene;
    [SerializeField] Animator splashScreenAnim;

    [Header("Engine sound")]
    [Tooltip("A scooter's engine over the boot intro (none: silent)")]
    [SerializeField] internal AudioClip engineClip;
    [Tooltip("Seconds into the intro the engine starts")]
    [SerializeField] internal float engineDelay;
    [Tooltip("Seconds into the intro the engine revs away: when the wiper starts across")]
    [SerializeField] internal float revAt;
    [Tooltip("How long the rev takes: the wiper's sweep")]
    [SerializeField] internal float revTime;

    /// <summary>The engine's loudness when nothing is saved yet</summary>
    internal const float DEFAULT_VOLUME = 0.6f;
    /// <summary>The engine at full Master and Effects: under the music's level, it's a flourish</summary>
    internal const float FULL_VOLUME = 0.8f;
    /// <summary>The engine's pitch at the end of its rev (1 = as recorded)</summary>
    internal const float REV_PITCH = 1.45f;

    /// <summary>The engine's source: it outlives this scene, so the menu loading doesn't cut it off</summary>
    internal AudioSource engine;

    public void Start()
    {
        LoadMenuFromSplash(timer);

        if (engineClip != null)
            StartEngine(Path.Combine(Application.persistentDataPath, "settings.cfg"));
    }

    /// <summary>
    /// How loud the intro's engine is (0 to 1) for the saved settings' text: Master times Effects, as the Options screen
    /// has them. The menu scene applies the saved volumes, and it hasn't loaded yet. Null (nothing saved): a modest level
    /// </summary>
    internal static float IntroVolume(string settingsText)
    {
        if (settingsText == null)
            return DEFAULT_VOLUME;

        int top = GameSettings.MAX_VOLUME_POSITION - 1;
        GameSettings defaults = new GameSettings(top, top, 0, 0);
        GameSettings settings = GameSettings.Parse(settingsText, defaults);
        float master = Mathf.Clamp01(settings.masterPosition / (float)GameSettings.MAX_MASTER_POSITION);
        float effects = Mathf.Clamp01(settings.sfxPosition / (float)top);
        return master * effects * FULL_VOLUME;
    }

    void StartEngine(string settingsPath)
    {
        string saved = null;
        try
        {
            if (File.Exists(settingsPath))
                saved = File.ReadAllText(settingsPath);
        }
        catch (IOException) { } // unreadable: the modest level

        GameObject holder = new GameObject("Intro Engine");
        DontDestroyOnLoad(holder);
        engine = holder.AddComponent<AudioSource>();
        engine.clip = engineClip;
        engine.playOnAwake = false;
        engine.loop = false;
        engine.spatialBlend = 0f;
        engine.volume = IntroVolume(saved);
        holder.AddComponent<IntroEngine>().Play(engine, engineDelay, revAt, revTime, REV_PITCH);
    }

    ///<summary>
    /// Main method that loads the menu from splashscreen
    ///</summary>
    public void LoadMenuFromSplash(float timer)
    {
        // Stops Corutine
        if (sceneLoadCoroutune != null)
        {
            StopCoroutine(sceneLoadCoroutune);
            sceneLoadCoroutune = null;
        }

        sceneLoadCoroutune = StartCoroutine(LoadSceneAsync(PlayerSelectScene.BuildIndex, timer));
    }

    ///<summary>
    /// Loads the scene async
    ///</summary>
    private IEnumerator LoadSceneAsync(int sceneToLoad, float delayTime)
    {
        // Sets gamestate to loading

        // Loads the first scene asynchronously
        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
        asyncLoad.allowSceneActivation = false;

        // Wait until the asynchronous scene is allowed to be activated
        while (!asyncLoad.allowSceneActivation)
        {
            if (asyncLoad.progress >= 0.90f && splashScreenAnim.GetCurrentAnimatorStateInfo(0).normalizedTime > 1)
            {
                yield return new WaitForSeconds(delayTime);

                asyncLoad.allowSceneActivation = true;

                break;
            }
            yield return null;
        }
    }

}

/// <summary>
/// Plays the boot intro's engine once and removes itself: it starts after a delay, and revs away (its pitch rising) as
/// the wiper crosses. On its own object, kept across the load of the menu scene
/// </summary>
public class IntroEngine : MonoBehaviour
{
    public void Play(AudioSource engine, float delay, float revAt, float revTime, float revPitch)
    {
        StartCoroutine(Run(engine, delay, revAt, revTime, revPitch));
    }

    IEnumerator Run(AudioSource engine, float delay, float revAt, float revTime, float revPitch)
    {
        float start = Time.unscaledTime;
        while (Time.unscaledTime - start < delay)
            yield return null;

        engine.Play();
        while (engine != null && engine.isPlaying)
        {
            float since = Time.unscaledTime - start - revAt;
            engine.pitch = revTime > 0f ? Mathf.Lerp(1f, revPitch, Mathf.Clamp01(since / revTime)) : 1f;
            yield return null;
        }

        Destroy(gameObject);
    }
}

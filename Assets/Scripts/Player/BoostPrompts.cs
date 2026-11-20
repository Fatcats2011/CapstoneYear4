using System;
using System.IO;
using UnityEngine;

/// <summary>
/// How many online matches this machine has played to the results, saved in settings.cfg: the boost ring's button prompt
/// shows at once for the first BoostRingRules.PROMPT_MATCHES of them. Offline matches, and solo ones with CPU drivers, are not counted.
/// BoostPromptWatcher counts a match when the results come
/// </summary>
public static class BoostPrompts
{
    static int onlineMatches;

    /// <summary>The saved count. Setting it (the settings load it) raises nothing</summary>
    public static int OnlineMatches
    {
        get { return onlineMatches; }
        set { onlineMatches = value; }
    }

    /// <summary>The count changed: to save it</summary>
    public static event Action<int> Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void Reset()
    {
        onlineMatches = 0;
        Changed = null;
    }

    /// <summary>A match reached its results: an online one is counted and the new count announced; an offline one is not</summary>
    internal static void OnResults(bool online)
    {
        if (!online)
            return;
        onlineMatches++;
        Changed?.Invoke(onlineMatches);
    }

    /// <summary>
    /// Whether this match follows the online prompt rule and counts: online with another machine's player in it. Solo
    /// with CPU drivers is hosted online but plays like local (BoostRingRules.WithPeopleOnline)
    /// </summary>
    public static bool WithPeopleOnline()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        return BoostRingRules.WithPeopleOnline(GameAuthority.IsOnline, players != null ? players.Roster.Players : null);
    }

    /// <summary>Writes the count into settings.cfg, leaving every other line as it is (as Hints.Save does)</summary>
    internal static void Save(int count)
    {
        string path = Path.Combine(Application.persistentDataPath, GameSettings.FILE_NAME);
        try
        {
            string text = File.Exists(path) ? File.ReadAllText(path) : "";
            File.WriteAllText(path, GameSettings.WithLine(text, "onlineMatches", count));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Could not save the online match count to {path}: {e.Message}");
        }
    }
}

/// <summary>
/// Counts an online match when it reaches the results, and saves the count. Made at launch and kept between scenes, as
/// HintWatcher is; the game manager may come and go with the scenes, so it is followed each frame
/// </summary>
public class BoostPromptWatcher : MonoBehaviour
{
    static BoostPromptWatcher instance;
    GameManager watched;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null)
            return;
        GameObject holder = new GameObject(nameof(BoostPromptWatcher));
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<BoostPromptWatcher>();
    }

    void OnEnable()
    {
        BoostPrompts.Changed += BoostPrompts.Save;
    }

    void OnDisable()
    {
        BoostPrompts.Changed -= BoostPrompts.Save;
        Watch(null);
    }

    void Update()
    {
        Watch(GameManager.Instance);
    }

    // Follows the current game manager (none until one exists, a new one after a scene load)
    void Watch(GameManager game)
    {
        if (game == watched)
            return;
        if (watched != null)
            watched.OnSwapResults -= OnResults;
        watched = game;
        if (watched != null)
            watched.OnSwapResults += OnResults;
    }

    void OnResults()
    {
        BoostPrompts.OnResults(BoostPrompts.WithPeopleOnline());
    }
}

using UnityEngine;

/// <summary>
/// Tells AudioVolume when the game's window gains or loses focus. Made once the first scene has loaded, so no scene
/// needs to contain it
/// </summary>
public class BackgroundAudio : MonoBehaviour
{
    static BackgroundAudio instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateOnLaunch()
    {
        if (instance != null)
            return;

        GameObject holder = new GameObject(nameof(BackgroundAudio));
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<BackgroundAudio>();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        AudioVolume.Focus(hasFocus);
    }
}

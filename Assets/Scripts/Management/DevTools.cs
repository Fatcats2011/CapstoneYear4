using UnityEngine;

/// <summary>
/// Gate for developer-only features: debug hotkeys and playtest data recording.
/// On in the editor and development builds, off in release builds.
/// </summary>
public static class DevTools
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool Enabled = true;
#else
    public static bool Enabled = false;
#endif

    /// <summary>
    /// Input.GetKeyDown that only reports presses while developer tools are enabled
    /// </summary>
    public static bool GetKeyDown(KeyCode key)
    {
        return Enabled && Input.GetKeyDown(key);
    }
}

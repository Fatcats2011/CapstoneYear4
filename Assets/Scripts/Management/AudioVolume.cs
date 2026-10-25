using UnityEngine;

/// <summary>
/// The game's overall loudness: the Master row (Options → Audio, in tenths) and, when In the background is Mute,
/// silence while the window isn't focused. It's the listener's volume, so it scales the Music and Effects mixer groups
/// alike, and the game keeps running in the background (online needs it). BackgroundAudio reports the focus
/// </summary>
public static class AudioVolume
{
    static int master = GameSettings.MAX_MASTER_POSITION;
    static bool muteInBackground = false;
    static bool focused = true;

    /// <summary>The listener's volume for a master position (0-10), the mute setting and whether the window is focused</summary>
    public static float ListenerVolume(int masterPosition, bool muteInBackground, bool focused)
    {
        if (muteInBackground && !focused)
            return 0f;
        return Mathf.Clamp(masterPosition, 0, GameSettings.MAX_MASTER_POSITION) / (float)GameSettings.MAX_MASTER_POSITION;
    }

    /// <summary>The Master and In the background rows changed (or were loaded)</summary>
    public static void Set(int masterPosition, bool mute)
    {
        master = masterPosition;
        muteInBackground = mute;
        Apply();
    }

    /// <summary>The window gained or lost focus</summary>
    public static void Focus(bool hasFocus)
    {
        focused = hasFocus;
        Apply();
    }

    static void Apply()
    {
        AudioListener.volume = ListenerVolume(master, muteInBackground, focused);
    }
}

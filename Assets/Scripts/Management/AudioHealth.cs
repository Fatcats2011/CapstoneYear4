using System.Globalization;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// One line of the sound's state for the log, written beside the Perf line every 30 s (PerfOverlay): playtesters lost all
/// sound after many rounds (2026-10-24), and an offline soak of 8 matches didn't (see the playtest notes). The next
/// Player.log shows when it goes, and which part went: the listener, the volume, the mixer or the voices
/// </summary>
public static class AudioHealth
{
    /// <summary>
    /// "Audio: 1 listener, volume 1, music -4 dB, effects -8 dB, music playing, 23 sources (3 playing), 32 real voices"
    /// </summary>
    public static string Line(int listeners, float volume, bool paused, float musicDb, float effectsDb, bool musicPlaying,
        int sources, int playing, int realVoices)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "Audio: {0} listener{1}, volume {2:0.##}{3}, music {4:0.#} dB, effects {5:0.#} dB, music {6}, {7} sources ({8} playing), {9} real voices",
            listeners, listeners == 1 ? "" : "s", volume, paused ? " (paused)" : "", musicDb, effectsDb,
            musicPlaying ? "playing" : "STOPPED", sources, playing, realVoices);
    }

    /// <summary>The line for the game as it is now</summary>
    public static string Now(AudioMixer mixer, AudioSource music)
    {
        int listeners = 0;
        foreach (AudioListener listener in Object.FindObjectsOfType<AudioListener>())
        {
            if (listener.enabled)
                listeners++;
        }

        AudioSource[] all = Object.FindObjectsOfType<AudioSource>();
        int playing = 0;
        foreach (AudioSource source in all)
        {
            if (source.isPlaying)
                playing++;
        }

        float musicDb = 0f, effectsDb = 0f;
        if (mixer != null)
        {
            mixer.GetFloat("Music", out musicDb);
            mixer.GetFloat("SFX", out effectsDb);
        }

        return Line(listeners, AudioListener.volume, AudioListener.pause, musicDb, effectsDb, music != null && music.isPlaying,
            all.Length, playing, AudioSettings.GetConfiguration().numRealVoices);
    }
}

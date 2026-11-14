using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The sounds that tell a player what just happened to them: a steal, being robbed, a bump that took nothing, the cursed
/// package landing on them, 30 seconds left. Their clips
/// load by name from Resources/Audio/Feedback, so no scene or prefab holds them (each file's source and licence is in
/// LICENSES.md). A sound whose file isn't there is no sound: the game plays without it. The steal has no file, so it
/// keeps the game's own whoosh (the user's call, 2026-11-14); a "steal" file dropped in the folder would replace it.
/// Each sound's volume is set against the music and the other pickups, since the files are mastered loud. SoundPool
/// plays them
/// </summary>
public static class FeedbackSounds
{
    /// <summary>A thief takes an order: no file today, so the whoosh plays</summary>
    public const string STEAL = "steal";
    /// <summary>A player loses an order to a thief: only they hear it</summary>
    public const string ROBBED = "robbed";
    /// <summary>A bump that took nothing: only the player who bumped hears it</summary>
    public const string THUD = "thud";
    /// <summary>The cursed package (Hot Potato) lands on the player: only they hear it</summary>
    public const string CURSED = "cursed";
    /// <summary>A timed round has 30 seconds left: every player hears it</summary>
    public const string WARNING = "warning";

    // The files peak at about -1 dB. The music plays at 0.1 to 0.3 and the pickup at 0.6 of a much quieter file, so
    // these are turned down to be heard over the music without standing over everything else. The thud is nearly all
    // bass, which sounds quieter than it measures, so it gets a little more. The warning is a bell: one sharp strike and
    // a quiet ring, 10 dB quieter on average than the robbed sound, so it gets more too
    const float ROBBED_VOLUME = 0.45f;
    const float THUD_VOLUME = 0.6f;
    const float CURSED_VOLUME = 0.5f;
    const float WARNING_VOLUME = 0.6f;

    /// <summary>How loud a sound plays, 0 to 1</summary>
    public static float Volume(string name)
    {
        switch (name)
        {
            case THUD: return THUD_VOLUME;
            case CURSED: return CURSED_VOLUME;
            case WARNING: return WARNING_VOLUME;
            default: return ROBBED_VOLUME;
        }
    }

    const string FOLDER = "Audio/Feedback/";

    static readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        clips.Clear();
    }

    /// <summary>A sound's clip, or null when the game has no such file</summary>
    public static AudioClip Clip(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (!clips.TryGetValue(name, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>(FOLDER + name);
            clips[name] = clip;
        }
        return clip;
    }
}

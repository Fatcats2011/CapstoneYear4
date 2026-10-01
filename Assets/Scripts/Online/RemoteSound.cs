using UnityEngine;

/// <summary>
/// How another machine's scooter sounds here, online. It plays its owner's one-shots (OnlineCues, SoundPool.PlayRemote):
/// in full close to this machine's player, fading to silence farther away. The game's sounds are all 2D and its listener
/// doesn't move, so the distance sets the volume. See docs/online.md
/// </summary>
public static class RemoteSound
{
    /// <summary>Up to this far from this machine's player (m), another machine's scooter is heard in full</summary>
    public const float NEAR = 10f;

    /// <summary>From this far on (m), it's silent</summary>
    public const float FAR = 60f;

    /// <summary>
    /// How loud another machine's scooter is at a distance (m) from this machine's player: 1 up to NEAR, fading to 0 at FAR
    /// </summary>
    public static float Volume(float distance)
    {
        return 1f - Mathf.Clamp01((distance - NEAR) / (FAR - NEAR));
    }

    /// <summary>
    /// The sound (a SoundManager key) a one-shot plays: its owner's. None for a phase's end (it stops the phasing sound)
    /// or a rise (a gravestone)
    /// </summary>
    public static string KeyFor(CueKind kind)
    {
        switch (kind)
        {
            case CueKind.Boost:
                return "boost_used";
            case CueKind.DriftBoost:
                return "mini";
            case CueKind.HornReady:
                return "boost_charged";
            case CueKind.Phase:
                return "phasing";
            case CueKind.Death:
                return "death";
            default:
                return null;
        }
    }

    /// <summary>
    /// How loud a sound from there is here: by its distance from this machine's online player's ball. In full when this
    /// machine has no such player
    /// </summary>
    public static float VolumeAt(Vector3 where)
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null || players.OnlineSeat < 0 || players.OnlineSeat >= Constants.MAX_PLAYERS)
            return 1f;

        PlayerSlot mine = players.Roster[players.OnlineSeat];
        BallDriving driving = mine != null && mine.Player != null ? mine.Player.GetComponentInChildren<BallDriving>(true) : null;
        if (driving == null || driving.Sphere == null)
            return 1f;

        return Volume(Vector3.Distance(where, driving.Sphere.transform.position));
    }
}

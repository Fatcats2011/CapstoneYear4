using UnityEngine;

/// <summary>
/// What online play spawns: its two network objects, the scooter shown for another machine's player, and the local
/// player's prefab (its player select lists the colours and hats). The one asset is Resources/Online/OnlinePrefabs
/// </summary>
[CreateAssetMenu(fileName = "OnlinePrefabs", menuName = "Dead on Arrival/Online Prefabs")]
public class OnlinePrefabs : ScriptableObject
{
    /// <summary>Where the asset is, under a Resources folder</summary>
    public const string RESOURCE = "Online/OnlinePrefabs";

    [SerializeField] GameObject playerPrefab;
    [SerializeField] GameObject matchPrefab;
    [SerializeField] GameObject remoteAvatarPrefab;
    [SerializeField] GameObject localPlayerPrefab;

    /// <summary>A NetworkPlayer: the host spawns one per machine</summary>
    public GameObject PlayerPrefab { get { return playerPrefab; } }

    /// <summary>The NetworkMatch: the host spawns one per session</summary>
    public GameObject MatchPrefab { get { return matchPrefab; } }

    /// <summary>PlayerAvatar.prefab: another machine's player's scooter</summary>
    public GameObject RemoteAvatarPrefab { get { return remoteAvatarPrefab; } }

    /// <summary>Player - Cinemachine.prefab: a player on this machine</summary>
    public GameObject LocalPlayerPrefab { get { return localPlayerPrefab; } }

    /// <summary>The asset (null if it's missing)</summary>
    public static OnlinePrefabs Load()
    {
        return Resources.Load<OnlinePrefabs>(RESOURCE);
    }
}

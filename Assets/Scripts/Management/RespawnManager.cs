using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnManager : SingletonMonobehaviour<RespawnManager>
{
    [Tooltip("Parent Game Object for the respawn points.")]
    [SerializeField] private GameObject RSPParent;

    [Tooltip("Will return any respawn point less than this distance, even if it's not technically the closest.")]
    [SerializeField] private float closeEnough = 10f;

    private RespawnPoint[] gathered;

    // The scene's points, gathered on first use: online the opening state can place players in the frame the scene
    // loads, before Start (a player started online with no tutorial found none)
    private RespawnPoint[] respawnPoints
    {
        get
        {
            if (gathered == null && RSPParent != null)
                InitRespawnPoints(RSPParent);
            return gathered;
        }
    }

    /// <summary>The scene's respawn points (all on the road), e.g. for where Kings Crown's crown lies</summary>
    internal RespawnPoint[] Points => respawnPoints;

    private void Start()
    {
        if (gathered == null && RSPParent != null)
            InitRespawnPoints(RSPParent);
    }

    /// <summary>
    /// Initializes the respawn point array based on the child GOs of the parent parameter.
    /// </summary>
    /// <param name="parent">Parent GO of the respawn points for the map.</param>
    internal void InitRespawnPoints(GameObject parent)
    {
        gathered = new RespawnPoint[parent.transform.childCount];
        for(int i=0;i<parent.transform.childCount;i++)
        {
            gathered[i] = parent.transform.GetChild(i).GetComponent<RespawnPoint>();
        }
    }

    /// <summary>
    /// A respawn point's place among this scene's points: the same on every machine (online the host names a point by
    /// it). -1 when it isn't one of them
    /// </summary>
    public int IndexOf(RespawnPoint point)
    {
        return point == null || respawnPoints == null ? -1 : System.Array.IndexOf(respawnPoints, point);
    }

    /// <summary>
    /// The respawn point in a place among this scene's points, or null outside the range
    /// </summary>
    public RespawnPoint PointAt(int index)
    {
        return respawnPoints != null && index >= 0 && index < respawnPoints.Length ? respawnPoints[index] : null;
    }

    /// <summary>
    /// Where a player who was last on the ground here rises: the nearest respawn point nobody is rising from (a free one
    /// closer than closeEnough will do). With every point in use, the nearest anyway. Null when the scene has none.
    /// Online the host picks for every player (OnlineRespawns)
    /// </summary>
    public RespawnPoint GetRespawnPoint(Vector3 lastGrounded)
    {
        if(respawnPoints == null || respawnPoints.Length == 0)
        {
            return null;
        }

        // The nearest point of all, and the nearest free one (2024: the first point was handed out even while in use)
        RespawnPoint nearest = null, nearestFree = null;
        float nearestDist = float.MaxValue, freeDist = float.MaxValue;
        foreach (RespawnPoint point in respawnPoints)
        {
            float dist = Vector3.Distance(lastGrounded, point.PlayerSpawn);
            if (dist < nearestDist)
            {
                nearest = point;
                nearestDist = dist;
            }
            if (point.InUse || dist >= freeDist)
                continue;
            if (dist < closeEnough)
                return point;
            nearestFree = point;
            freeDist = dist;
        }
        return nearestFree != null ? nearestFree : nearest;
    }
}

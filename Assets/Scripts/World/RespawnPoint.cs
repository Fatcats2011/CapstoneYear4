using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RespawnPoint : MonoBehaviour
{
    [SerializeField] internal Transform order1Spawn;
    [SerializeField] internal Transform order2Spawn;

    public Vector3 PlayerSpawn { get { return transform.position; } }
    public Vector3 Order1Spawn { get { return order1Spawn.position; } }
    public Vector3 Order2Spawn { get { return order2Spawn.position; } }
    /// <summary>Where a player here faces: a spot (the middle of the order spots), not a direction (FacingFrom)</summary>
    public Vector3 PlayerFacingDirection { get { return playerFacingDirection; } }
    /// <summary>How a player starting here faces: level, towards the middle of the order spots (set or not)</summary>
    public Quaternion PlayerFacing { get { return FacingFrom(PlayerSpawn, (Order1Spawn + Order2Spawn) / 2f); } }
    /// <summary>Whether a player is rising here: this machine's (Respawn), or on an online host another machine's (HoldFor)</summary>
    public bool InUse { get { return inUse || Time.time < heldUntil; } set { inUse = value; } }

    private Vector3 playerFacingDirection;
    private bool inUse = false;
    private float heldUntil = float.NegativeInfinity; // online host: another machine's player rises here until then

    /// <summary>A rotation at a spawn facing a spot, level (height ignored); no turn when the spot is right on it</summary>
    public static Quaternion FacingFrom(Vector3 spawn, Vector3 spot)
    {
        Vector3 flat = new Vector3(spot.x - spawn.x, 0f, spot.z - spawn.z);
        return flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat) : Quaternion.identity;
    }

    /// <summary>
    /// Online host: another machine's player rises here. Their own machine runs the respawn and the host never hears when
    /// it ends, so the point is in use for that long
    /// </summary>
    public void HoldFor(float seconds)
    {
        heldUntil = Time.time + seconds;
    }

    private void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += InitPoint;
        GameManager.Instance.OnSwapGoldenCutscene += InitPoint;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= InitPoint;
        GameManager.Instance.OnSwapGoldenCutscene -= InitPoint;
    }

    /// <summary>
    /// This method is for initializing the point, specifially the inUse variable in case the gamestate changes mid-respawn.
    /// </summary>
    public void InitPoint()
    {
        playerFacingDirection = (Order1Spawn + Order2Spawn) / 2f;
        inUse = false;
        heldUntil = float.NegativeInfinity;
    }
}

using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// This class will respawn the player if they fall into the water.
/// </summary>
public class Respawn : MonoBehaviour
{

    IEnumerator respawnCoroutine;

    [Header("Respawn Stats")]
    [Tooltip("The height to lift the player above the map.")]
    [SerializeField] private float liftHeight = 5.0f; // The height to lift the player above the map

    [Tooltip("The time it takes to lift the player between startingLiftHeight to liftHeight.")]
    [SerializeField] private float liftDuration = 5.0f; // The time it takes to lift the player above the ground

    [Tooltip("The height the player wisp will rise above the water.")]
    [SerializeField] private float wispHeight = 5.0f;

    [Tooltip("Time it takes for the wisp to rise above the water.")]
    [SerializeField] private float wispRiseTime = 1f;

    [Tooltip("Time it takes for the wisp to travel to the casket.")]
    [SerializeField] private float wispToCasketTime = 1f;

    [Tooltip("Trailtime of the wisp trail renderer component.")]
    [SerializeField] private float wispTrailTime = 1f;

    [Tooltip("Time it takes for the player to grow back to full size")]
    [SerializeField] private float growTime = 0.1f;

    [Tooltip("How far below the player death position the wisp spawns.")]
    [SerializeField] private float wispSpawnOffset = 5f;

    [Tooltip("Distance between tombstone spawn and player spawn.")]
    [SerializeField] internal int tombstoneOffset = 2;

    [Tooltip("How far below the tombstone the player spawns.")]
    [SerializeField] private float graveDepth = 2f;

    [Header("Game Object Refs")]
    [Tooltip("The prefab for the gravestone model.)")]
    [SerializeField] private GameObject respawnGravestone; // Gravestone model to spawn in during respawn

    [Tooltip("Reference to the control game object.")]
    [SerializeField] private GameObject control;

    [Tooltip("For enabling / disabling the mesh.")]
    [SerializeField] internal GameObject modelParent;

    [Tooltip("The VFX used to animate the respawn.")]
    [SerializeField] internal VisualEffect deathWisp;
    internal TrailRenderer wispTrail;

    [Tooltip("ParticleFX to play when you respawn")]
    [SerializeField] private ParticleSystem rebornParticles;

    private OrderHandler orderHandler;
    private BallDriving ballDriving;

    private Vector3 lastGroundedPos; // last position the player was grounded at
    public Vector3 LastGroundedPos { get { return lastGroundedPos; } set { lastGroundedPos = value; } }

    private Vector3 wispOrigin; // original pos of the wisp

    private Vector3 ogPlayerScale, ogWispScale; // for scaling up and down the player model

    // Sound stuff
    private SoundPool soundPool;

    private bool isRespawning;
    public bool IsRespawning { get { return isRespawning; } }

    /// <summary>Online client: how long its player waits for the host's respawn point before picking their own, in seconds</summary>
    public const float HOST_ANSWER_WAIT = 2f;

    private RespawnPoint risePoint; // online client: the host's point for the respawn waiting for one
    private bool awaitingPoint;     // online client: this respawn waits for the host's point

    /// <summary>Another machine's scooter: how long its reborn sparkle's last particles get after the lift, in seconds</summary>
    private const float SPARKLE_FADE = 1f;
    private Coroutine riseShow; // another machine's scooter: the reborn sparkle waiting for its owner's lift

    /// <summary>How long a respawn takes, from the wisp rising to the end of the lift (online, the host holds the point that long)</summary>
    public float Duration { get { return wispRiseTime + wispToCasketTime + liftDuration; } }

    /// <summary>Whether the rider is hidden: from falling in the water until it rises from its grave (its wisp shows)</summary>
    public bool RiderHidden { get { return modelParent != null && !modelParent.activeSelf; } }

    private Rigidbody rb;
    private SphereCollider sc;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += StopRespawnCoroutine;
        GameManager.Instance.OnSwapGoldenCutscene += StopRespawnCoroutine;
        GameManager.Instance.OnSwapResults += StopRespawnCoroutine;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= StopRespawnCoroutine;
        GameManager.Instance.OnSwapGoldenCutscene -= StopRespawnCoroutine;
        GameManager.Instance.OnSwapResults -= StopRespawnCoroutine;
    }

    private void Update()
    {
        if(DevTools.GetKeyDown(KeyCode.R))
        {
            StartRespawnCoroutine();
        }
    }

    private void Awake()
    {
        wispTrail = deathWisp.GetComponentInChildren<TrailRenderer>();
        orderHandler = control.GetComponent<OrderHandler>();
        ballDriving = control.GetComponent<BallDriving>();
        soundPool = control.GetComponent<SoundPool>();
        rb = GetComponent<Rigidbody>();
        sc = GetComponent<SphereCollider>();

        tombstoneOffset = Mathf.Abs(tombstoneOffset); // in case a stinky designer made this value negative
        wispOrigin = deathWisp.transform.localPosition;

        wispTrail.time = 0f;
        deathWisp.enabled = (false);
        deathWisp.SetFloat("Duration", liftDuration + wispRiseTime + wispToCasketTime);
        rebornParticles.Stop();

        ogPlayerScale = modelParent.transform.localScale;
        ogWispScale = deathWisp.transform.localScale;
    }

    /// <summary>
    /// The new respawning player coroutine. Simplified from the old version, rotates the player 180 degrees and positions them at their respawn point. Also creates a tombstone.
    /// </summary>
    /// <returns></returns>
    private IEnumerator RespawnPlayer()
    {
        // Where to rise. Online a client asks the host, which picks where every player rises (never two on one point) and
        // drops the orders there. It waits, hidden, a little at most
        RespawnPoint rsp = null;
        if (GameAuthority.IsOnline && !GameAuthority.IsAuthority)
        {
            risePoint = null;
            awaitingPoint = true;
            RespawnSync.Ask(OrderSync.SeatOf(this), lastGroundedPos);
            float giveUp = Time.time + HOST_ANSWER_WAIT;
            while (risePoint == null && !GameAuthority.IsAuthority && Time.time < giveUp) // offline again: the host left
                yield return null;
            awaitingPoint = false;
            rsp = risePoint;
        }
        if (rsp == null)
        {
            // Offline, on the host, or with no answer: this machine picks. A client with no answer asks the host to drop
            // the orders there
            rsp = RespawnManager.Instance.GetRespawnPoint(lastGroundedPos); // get the RSP
            rsp.InUse = true;
            orderHandler.DropEverything(rsp.Order1Spawn, rsp.Order2Spawn, false);
        }

        // init the wisp
        Vector3 wispStart = deathWisp.transform.position;
        deathWisp.transform.position -= Vector3.up * wispSpawnOffset;
        deathWisp.enabled = (true);
        deathWisp.Reinit();
        wispTrail.time = wispTrailTime;

        float elapsedTime = 0;
        Vector3 wispRise = wispStart + Vector3.up * wispHeight; // position of the wisp risen above water

        // rotate the control around the wisp while it's rising, to face the way the tombstone faces
        Pose grave = GraveAt(rsp, tombstoneOffset);
        Quaternion controlStart = control.transform.rotation;
        Quaternion controlEnd = grave.rotation;

        // create the tombstone. Online, the other machines put it up too, and show this player rising from it
        Instantiate(respawnGravestone, grave.position, grave.rotation);
        if (GameAuthority.IsOnline)
        {
            int point = RespawnManager.Instance.IndexOf(rsp);
            if (point >= 0)
                CueSync.Play(OrderSync.SeatOf(this), ScooterCue.Rise(point));
        }

        // raise the wisp above the water
        while (elapsedTime < wispRiseTime)
        {
            deathWisp.transform.position = Vector3.Slerp(wispStart, wispRise, elapsedTime / wispRiseTime); // lift wisp above water
            control.transform.rotation = Quaternion.Slerp(controlStart, controlEnd, elapsedTime / wispRiseTime); // rotate around wisp
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // reset vars for next movement
        elapsedTime = 0;
        Vector3 casketLocation = rsp.PlayerSpawn - Vector3.up * graveDepth;
        wispStart = deathWisp.transform.position;
        Vector3 playerStart = transform.position;

        // move the wisp to the casket under RSP
        while (elapsedTime < wispToCasketTime)
        {
            deathWisp.transform.position = Vector3.Slerp(wispStart, casketLocation, elapsedTime / wispToCasketTime); // move wisp to casket
            transform.position = Vector3.Lerp(playerStart, casketLocation, elapsedTime / wispToCasketTime); // for control to follow wisp
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // reset vars for next movement
        elapsedTime = 0;
        Vector3 liftTarget = new Vector3(rsp.PlayerSpawn.x, rsp.PlayerSpawn.y + liftHeight, rsp.PlayerSpawn.z);
        modelParent.SetActive(true);
        deathWisp.enabled = false;
        wispTrail.time = 0;

        rebornParticles.transform.parent = rsp.transform;
        rebornParticles.transform.localPosition = Vector3.zero;
        rebornParticles.Play();


        // lift player out of their tomb
        while (elapsedTime < liftDuration)
        {
            transform.position = Vector3.Slerp(casketLocation, liftTarget, elapsedTime / liftDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        rebornParticles.Stop();
        rebornParticles.transform.parent = deathWisp.transform;
        rebornParticles.transform.localPosition = Vector3.zero;

        rebornParticles.time = 0;
        deathWisp.enabled = false;
        deathWisp.transform.localScale = ogWispScale;
        modelParent.transform.localScale = ogPlayerScale;
        
        rsp.InitPoint();

        StopRespawnCoroutine();
    }

    /// <summary>
    /// Online client: where the host says this player rises, for the respawn waiting for it. Ignored when none waits: an
    /// answer that came too late finds the player already rising on their own point
    /// </summary>
    public void RiseAt(RespawnPoint point)
    {
        if (awaitingPoint)
            risePoint = point;
    }

    /// <summary>
    /// Another machine's scooter (this script is off there): hidden while its owner's rider is, with its ball's collider
    /// off so it bumps nobody, and its wisp showing. It shows again when its owner's does
    /// </summary>
    public void ShowRemote(bool hidden)
    {
        if (modelParent.activeSelf != hidden)
            return;

        modelParent.SetActive(!hidden);
        GetComponent<SphereCollider>().enabled = !hidden;

        // The wisp sits on the ball, which follows its owner's wisp to the grave
        deathWisp.enabled = hidden;
        if (hidden)
            deathWisp.Reinit();
        wispTrail.time = hidden ? wispTrailTime : 0f;
    }

    /// <summary>
    /// Where a respawn's gravestone stands: offset behind its point, facing where the risen player will face (the middle
    /// of the point's order spots)
    /// </summary>
    public static Pose GraveAt(RespawnPoint point, float offset)
    {
        Quaternion facing = Quaternion.LookRotation(point.PlayerFacingDirection - point.PlayerSpawn, Vector3.up);
        return new Pose(point.PlayerSpawn - facing * Vector3.forward * offset, facing);
    }

    /// <summary>
    /// Another machine's scooter (this script is off there): its owner's player rises at that point. Their gravestone
    /// stands there now, and the reborn sparkle plays there while they lift out of it, as on their own machine. A copy of
    /// the sparkle plays: this scooter's own stays where it is
    /// </summary>
    public void ShowRise(RespawnPoint point)
    {
        Pose grave = GraveAt(point, tombstoneOffset);
        Instantiate(respawnGravestone, grave.position, grave.rotation);

        // A new rise replaces one whose sparkle is still waiting
        if (riseShow != null)
            StopCoroutine(riseShow);
        riseShow = StartCoroutine(SparkleAt(point));
    }

    // Another machine's scooter: the reborn sparkle where its owner's player rises, when they lift out of the grave
    private IEnumerator SparkleAt(RespawnPoint point)
    {
        yield return new WaitForSeconds(wispRiseTime + wispToCasketTime);
        riseShow = null;
        if (point == null)
            yield break; // the scene changed

        ParticleSystem sparkle = Instantiate(rebornParticles, point.PlayerSpawn, Quaternion.identity);
        sparkle.Play();
        Destroy(sparkle.gameObject, liftDuration + SPARKLE_FADE);
        yield return new WaitForSeconds(liftDuration);
        if (sparkle != null)
            sparkle.Stop();
    }

    internal void OnTriggerEnter(Collider other)
    {
        // Another machine's scooter (online): its own machine respawns it (this script is off there, but triggers still arrive)
        if (RemoteAvatar.IsRemote(this))
            return;

        if (other.tag == "Water")
        {
            StartRespawnCoroutine();
        }
    }

    public void StartRespawnCoroutine()
    {
        isRespawning = true;
        soundPool.PlayDeathSound();

        ballDriving.FreezeBall(true, false);

        // Turning these off fixes camera jittering on respawn
        rb.velocity = Vector3.zero; // set velocity to 0 on respawn
        rb.useGravity = false;
        sc.enabled = false;

        if (respawnCoroutine == null)
        {
            respawnCoroutine = RespawnPlayer();
        }

        modelParent.transform.parent.parent.DOComplete();
        modelParent.transform.parent.parent.DOComplete();
        modelParent.transform.parent.parent.DOKill();
        modelParent.transform.parent.parent.localEulerAngles = Vector3.zero;
        modelParent.SetActive(false);

        StartCoroutine(respawnCoroutine);
    }

    private void StopRespawnCoroutine()
    {
        if (respawnCoroutine != null)
        {
            StopCoroutine(respawnCoroutine);
        }

        isRespawning = false;
        awaitingPoint = false;

        wispTrail.time = 0f;
        modelParent.SetActive(true);
        deathWisp.enabled = (false);
        deathWisp.transform.localPosition = wispOrigin;

        ballDriving.FreezeBall(false, false);
        modelParent.transform.parent.parent.localEulerAngles = Vector3.zero;

        respawnCoroutine = null;
        rb.useGravity = true;
        sc.enabled = true;

        ballDriving.DirtyTerrainRespawn = false;

        rebornParticles.time = 0;
        rebornParticles.transform.parent = deathWisp.transform;
        rebornParticles.transform.localPosition = Vector3.zero;

        deathWisp.enabled = false;
        deathWisp.transform.localScale = ogWispScale;
        modelParent.transform.localScale = ogPlayerScale;
    }
}

using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
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
    [SerializeField] private int tombstoneOffset = 2;

    [Tooltip("How far below the tombstone the player spawns.")]
    [SerializeField] private float graveDepth = 2f;

    [Header("Game Object Refs")]
    [Tooltip("The prefab for the gravestone model.)")]
    [SerializeField] private GameObject respawnGravestone; // Gravestone model to spawn in during respawn

    [Tooltip("Reference to the control game object.")]
    [SerializeField] private GameObject control;

    [Tooltip("For enabling / disabling the mesh.")]
    [SerializeField] private GameObject modelParent;

    [Tooltip("The VFX used to animate the respawn.")]
    [SerializeField] private VisualEffect deathWisp;
    private TrailRenderer wispTrail;

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
        RespawnPoint rsp = RespawnManager.Instance.GetRespawnPoint(lastGroundedPos); // get the RSP
        rsp.InUse = true;
        // init the wisp
        Vector3 wispStart = deathWisp.transform.position;
        deathWisp.transform.position -= Vector3.up * wispSpawnOffset;
        deathWisp.enabled = (true);
        deathWisp.Reinit();
        wispTrail.time = wispTrailTime;

        orderHandler.DropEverything(rsp.Order1Spawn, rsp.Order2Spawn, false);

        float elapsedTime = 0;
        Vector3 wispRise = wispStart + Vector3.up * wispHeight; // position of the wisp risen above water

        // rotate the control around the wisp while it's rising
        Quaternion controlStart = control.transform.rotation;
        Quaternion controlEnd = Quaternion.LookRotation(rsp.PlayerFacingDirection - rsp.PlayerSpawn, Vector3.up);

        // create the tombstone
        Vector3 graveForward = controlEnd * Vector3.forward;
        Instantiate(respawnGravestone, rsp.PlayerSpawn - graveForward * tombstoneOffset, controlEnd);

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

    private void OnTriggerEnter(Collider other)
    {
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

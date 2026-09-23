#if (UNITY_EDITOR)
#define ENABLEDEBUGLOG
#endif

using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEditor;
using UnityEngine.InputSystem;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;

/// <summary>
/// Version 3.0 of the vehicle controller. Drives by rolling a sphere collider around the world then simply matching the bike model to its position.
/// Drifting is slightly more complicated, and involves doing a bunch of math.
/// </summary>
public class BallDriving : MonoBehaviour
{
    private const float STEERING_MODEL_ROTATION = 15.0f; //How far the model rotates when steering normally
    private const float DRIFTING_MODEL_ROTATION = 30.0f; //How far the model rotates when drifting
    private const float MODEL_TILT_MULTIPLIER = 0.5f; //How much the model tilts compared to rotates
    private const float DRIFTING_MODEL_TILT_MULTIPLIER = 0.8f; //How much again the model tilts compared to rotates when drifting (compounded with the regular one; stops the scooter from just leaning all the way over)
    private const float MODEL_ROTATION_TIME = 0.2f; //How long it takes the model to rotate into full position
    private const float DRIFT_HOP_AMOUNT = 0.25f; //How high the little pre-drift hop is
    private const float DRIFT_HOP_TIME = 0.4f; //How fast the little hop is in seconds
    private const float WHEELIE_AMOUNT = 45f; //Degrees that the scooter rotates when doing a wheelie

    private const float GROUNDCHECK_DISTANCE = 1.3f; //How long the ray that checks for the ground is
    private const float CSV_RATIO = 0.35f; //Don't touch
    private const float WATERCHECK_DISTANCE = 2f; // length of ray checking for respawning

    private const float BRAKE_CHECK_TIME = 0.08f;
    private const float RESTING_ANGULAR_DRAG = 0.1f;
    private const float FULLBRAKE_ANGULAR_DRAG = 15.0f;
    private const float RESTING_DYNAMIC_FRICTION = 0.4f;
    private const float FULLBRAKE_DYNAMIC_FRICTION = 5.0f;
    private const float RESTING_STATIC_FRICTION = 0.4f;
    private const float FULLBRAKE_STATIC_FRICTION = 5.0f;

    private IEnumerator boostActiveCoroutine;
    private IEnumerator boostCooldownCoroutine;
    private IEnumerator brakeCheckCoroutine;
    private IEnumerator endBoostCoroutine;
    private IEnumerator spinOutTimeCoroutine;
    private IEnumerator slowdownImmunityCoroutine;
    private IEnumerator coyoteTimeCoroutine;

    public delegate void BoostDelegate(); // boost event stuff for the trail
    public BoostDelegate OnBoostStart;

    [Header("Setup")]
    [Tooltip("Reference to the scooter model (specifically whatever empty is right above the first object with any actual mesh)")]
    [SerializeField] private Transform scooterModel;
    [Tooltip("Reference to the empty used for checking the scooter's normal to the ground")]
    [SerializeField] private Transform scooterNormal;
    public Transform ScooterNormal { get { return scooterNormal; } }
    [Tooltip("Reference to the movement sphere")]
    [SerializeField] private GameObject sphere; 
    public GameObject Sphere { get { return sphere; } }
    [Tooltip("An input manager class, from the correlated InputReceiver object")]
    [SerializeField] private InputManager inp;
    private IDriveInput driveInput; // set when something other than this prefab's controller drives the scooter
    private bool listening; // from Start until the scooter is destroyed: drift and boost follow the driver's buttons
    [Tooltip("Reference to the order manager object")]
    [SerializeField] private OrderHandler orderHandler;
    [Tooltip("Reference to the left slipstream trail")]
    [SerializeField] private TrailRenderer leftSlipstreamTrail;
    [Tooltip("Reference to the right slipstream trail")]
    [SerializeField] private TrailRenderer rightSlipstreamTrail;
    [Tooltip("Reference to the particles basket")]
    [SerializeField] private Transform particleBasket;
    [Tooltip("Reference to the left-side sparks position")]
    [SerializeField] private Transform sparksPos1;
    [Tooltip("Reference to the right-side sparks position")]
    [SerializeField] private Transform sparksPos2;

    [Header("Speed Modifiers")]
    [Tooltip("An amorphous representation of how quickly the bike can accelerate")]
    [SerializeField] private float accelerationPower = 30.0f;
    [Tooltip("An amorphous representation of how quickly the bike can reverse")]
    [SerializeField] private float reversingPower = 10.0f;
    [Tooltip("The amount of drag while falling. Improves the feel of the physics")]
    [SerializeField] private float fallingDrag = 1.0f;
    [Tooltip("The amount of speed that a ground boost patch gives")]
    [SerializeField] private float groundBoostDefault = 160.0f;
    [Tooltip("The multiplier applied to speed when in a ground slow patch")]
    [SerializeField] private float slowPatchMultiplier = 0.75f;
    [Tooltip("The multiplier applied to speed when holding the golden order")]
    [SerializeField] private float goldenOrderMultiplier = 0.95f;

    [Header("Steering")]
    [Tooltip("The 'turning power'. A slightly abstract concept representing how well the scooter can turn. Higher values represent a tighter turning circle")]
    [SerializeField] private float steeringPower = 15.0f;
    [Tooltip("The 'turning power' when reversing.")]
    [SerializeField] private float reverseSteeringPower = 40.0f;

    [Header("Drifting")]
    [Tooltip("The multiplier applied to turning when drifting. Always above 1 or there'll be no difference. Use caution when messing with this")]
    [SerializeField] private float driftTurnScalar = 1.8f;
    [Tooltip("A scalar for how 'sidewaysey' the drifting is. Higher values are LESS sideways")]
    [SerializeField] private float driftSidewaysScalar = 3.0f;
    [Tooltip("The minimum multipler applied to drifting. It's hard to explain exactly what this is but you'll get a feel for it. ALWAYS KEEP IT LESS THAN DRIFTTURNSCALAR")]
    [SerializeField] private float driftTurnMinimum = 0.0f;
    [Tooltip("How many 'drift points' are needed to achieve the drift boost. This is a semi-arbitrary unit, though if the drift boost is being based entirely on time, 100 drift points equals 1 second")]
    [SerializeField] private float driftBoostThreshold = 100.0f;
    [Tooltip("How much time vs turning amount is factored into drift boost. 0 is full time, 1 is full turning amount")]
    [SerializeField] private float driftBoostMode = 0.0f;
    [Tooltip("The amount of speed granted by a first-tier successful drift")]
    [SerializeField] private float driftBoost1 = 5.0f;
    [Tooltip("The amount of speed granted by a second-tier successful drift")]
    [SerializeField] private float driftBoost2 = 10.0f;
    [Tooltip("The amount of speed granted by a second-tier successful drift")]
    [SerializeField] private float driftBoost3 = 15.0f;
    [Tooltip("Color for the sparks when at the first tier of drifting")]
    [SerializeField] private Color driftSparksTier1Color;
    [Tooltip("Color for the sparks when at the second tier of drifting")]
    [SerializeField] private Color driftSparksTier2Color;
    [Tooltip("Color for the sparks when at the third tier of drifting")]
    [SerializeField] private Color driftSparksTier3Color;
    [Tooltip("How long the player is immune to grass slowdowns after getting a drift boost")]
    [SerializeField] private float slowdownImmunityDuration;

    [Header("Boosting")]
    [Tooltip("The speed power of the boost")]
    [SerializeField] private float boostPower = 50.0f;
    [Tooltip("How long the boost lasts")]
    [SerializeField] private float boostDuration = 1.0f;
    [Tooltip("How long it takes to recharge the boost, starting after it finishes")]
    [SerializeField] private float boostRechargeTime = 3.0f;
    [Tooltip("How long it takes to recharge the boost when holding an order")]
    [SerializeField] private float handsFullBoostRechargeTime = 5.0f;
    [Tooltip("The amount of drag while boosting (Exercise caution when changing this; ask Will before playing with it too much)")]
    [SerializeField] private float boostingDrag = 1.0f;
    [Tooltip("A multipler applied to steering power while in a boost, which reduces your steering capability")]
    [SerializeField] private float boostingSteerModifier = 0.4f;
    [Tooltip("How much force is used when clashing")]
    [SerializeField] private float clashForce = 50.0f;
    [Tooltip("How many frames the scooter is prevented from falling when boosting")]
    [SerializeField] private int coyoteFrames = 10;
    public float ClashForce { get { return clashForce; } }

    [Header("Slipstream")]
    [Tooltip("Maximum distance that two vehicles can be from each other to get slipstream")]
    [SerializeField] private float slipstreamDistance = 10.0f;
    [Tooltip("Minimum speed that BOTH vehicles need to be moving at to get slipstream")]
    [SerializeField] private float minimumSlipstreamSpeed = 10.0f;
    [Tooltip("How long in seconds slipstream needs to be maintained before getting the full boost")]
    [SerializeField] private float slipstreamTime = 1.5f;
    [Tooltip("The most amount of speed that the pre-boost slipstream grants")]
    [SerializeField] private float preBoostSlipstreamMax = 50.0f;
    [Tooltip("How much speed is granted from the slipstream boost")]
    [SerializeField] private float slipstreamBoostAmount = 300.0f;

    [Header("Animator Information")]
    [SerializeField] Animator playerAnimator;

    [Header("Speed Lines")]
    [SerializeField] float speedLineValue = 1;
    Material speedLinesMain;
    [SerializeField] Material[] potentialPlayerSpeedLineMaterials;

    [Header("Phasing Information")]
    [Tooltip("The type of visual to happen when boosting")]
    public PhaseType phaseType = PhaseType.OnlyInBuilding;
    [SerializeField] PlayerCameraResizer cameraResizer;
    [Tooltip("The player index is what allows only the certain player to phase")]
    public int playerIndex;
    [Tooltip("This is the reference to the horn phase indicator")]
    public PhaseIndicator phaseIndicator;
    [Tooltip("Toggle to check phase status")]
    [SerializeField] bool checkPhaseStatus = false;
    [SerializeField] GameObject[] phaseRaycastPositions;
    [Tooltip("Whether the current map is set up for phase testing; Will uses this for things dwai")]
    [SerializeField] bool phaseSetMap = true;
    [SerializeField] OrbitalCamera orbitalCamera;

    public enum PhaseType
    {
        OnlyInBuilding,
        AtAllTimes
    }

    [Header("Debug")]
    [SerializeField] private bool debugSpeedometerEnable = false;
    [SerializeField] private TextMeshProUGUI debugSpeedText;
    [SerializeField] private bool debugDriftStateEnable = false;
    [SerializeField] private TextMeshProUGUI debugDriftStateText;
    [SerializeField] private bool debugDriftCompleteEnable = false;
    [SerializeField] private Image debugDriftComplete;
    [SerializeField] private bool debugBoostabilityEnable = false;
    [SerializeField] private Image debugBoostability;
    [SerializeField] private bool debugCSVEnable = false;
    [SerializeField] private TextMeshProUGUI debugCSV;

    private Gamepad pad;
    private Rumbler rumble;

    private Rigidbody sphereBody; //just reference to components of the sphere
    private Transform sphereTransform;
    private Collider sphereCollider;
    public Collider SphereCollider { get { return sphereCollider; } }
    private Respawn respawn; // used to update the respawn point when grounded
    public Respawn Respawn { get { return respawn; } }

    private float startingDrag;
    private PhysicMaterial pMat;

    private ParticleManipulator baseSpark, wideSpark, flare1Spark, flare2Spark, flare3Spark, longSpark;

    private float leftStick, leftTrig, rightTrig; //stick ranges from -1 to 1, triggers range from 0 to 1

    private float currentForce; //the amount of force to add to the speed on any given frame
    private float scaledVelocityMax; //a complicated variable derived from a bunch of testing and math which really just boils down to accelerationPower * 0.35
    private float currentVelocity; //just shorthand for the sphere's current velocity magnitude
    public float CurrentVelocity { get { return currentVelocity; } }

    private float rotationAmount; //the amount to turn on any given frame

    private bool reverseGear, forwardGear, grounded, airboost;
    public bool Grounded => grounded;
    private bool coyoteing = false;
    private bool hasCoyoted = false;
    private bool canDrive = true;
    private bool brakeChecking = false;
    private bool stopped = true;
    private float timeSpentChecking = 0.0f;

    private bool spinningOut = false;
    private bool wheelying = false;

    private Tween wheelie;
    private Tween wheelieEnd;
    private Sequence mySeq;

    private bool callToDrift = false; //whether the controller should attempt to drift. only used if drift is called while the left stick is neutral
    private bool drifting = false;
    public bool Drifting => drifting;
    private int driftDirection; //-1 is drifting leftward, 1 is drifting rightward
    private bool driftBoostAchieved, firstFrameDriftBoostFlag = false;
    private float driftPoints = 0.0f;
    private float driftBoost = 0.0f;
    private int driftTier = 0;
    private bool moveOnTransform = false;
    private bool isFrozen = false;

    private bool groundBoostFlag = false;
    private bool groundSlowFlag = false;
    private bool slowdownImmune = false;
    private float groundBoostAmount = 0;

    private bool onMovingPlatform = false; //tells whether the player is on a moving platform
    private MovingPlatform currentMovingPlatform;
    private int movingPlatformIndex; //a local copy of the index that the current moving platform recognizes this scooter as

    private bool boostInitialburst = false;
    private bool boosting = false;
    public bool Boosting => boosting;
    private bool boostAble = true;
    public bool BoostAble { set { boostAble = value; } }
    private bool phasing = false;
    public bool Phasing => phasing;
    private float boostElapsedTime = 0f;
    public float BoostElapsedTime => boostElapsedTime;
    private float boostTimerMaxTime = 0f;
    public float BoostTimerMaxTime => boostTimerMaxTime;
    private float boostRechargeTimeSet;

    private float slipstreamPortion = 0.0f;

    private float csv;

    private bool dirtyTerrainRespawn = false;
    public bool DirtyTerrainRespawn { get { return dirtyTerrainRespawn; } set { dirtyTerrainRespawn = value; } }

    private SoundPool soundPool; // for driving noises

    public bool insideBuilding = false;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapStartingCutscene += ResetBoost;
        GameManager.Instance.OnSwapGoldenCutscene += ResetBoost;
        GameManager.Instance.OnSwapMenu += ResetBoost;

        GameManager.Instance.OnSwapStartingCutscene += FreezeBallForGameState;
        GameManager.Instance.OnSwapGoldenCutscene += FreezeBallForGameState;
        GameManager.Instance.OnSwapResults += FreezeBallForGameState;

        GameManager.Instance.OnSwapTutorial += UnfreezeBallForGameState;
        GameManager.Instance.OnSwapFinalPackage += UnfreezeBallForGameState;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapStartingCutscene -= ResetBoost;
        GameManager.Instance.OnSwapGoldenCutscene -= ResetBoost;
        GameManager.Instance.OnSwapMenu -= ResetBoost;

        GameManager.Instance.OnSwapGoldenCutscene -= FreezeBallForGameState;
        GameManager.Instance.OnSwapStartingCutscene -= FreezeBallForGameState;
        GameManager.Instance.OnSwapResults -= FreezeBallForGameState;

        GameManager.Instance.OnSwapTutorial -= UnfreezeBallForGameState;
        GameManager.Instance.OnSwapFinalPackage -= UnfreezeBallForGameState;
    }

    /// <summary>
    /// Freezes the ball when the game state takes control away from the player (cutscenes, results)
    /// </summary>
    private void FreezeBallForGameState()
    {
        FreezeBall(true);
    }

    /// <summary>
    /// Unfreezes the ball when the game state hands control back to the player
    /// </summary>
    private void UnfreezeBallForGameState()
    {
        FreezeBall(false);
    }

    /// <summary>
    /// Sends this player's rumble to another controller (after it takes over from a disconnected one)
    /// </summary>
    public void SetGamepad(Gamepad gamepad)
    {
        pad = gamepad;
    }

    /// <summary>
    /// Who drives this scooter: the controller on this machine unless something else is set. Can change at any time
    /// </summary>
    public IDriveInput DriveInput
    {
        get { return driveInput ?? inp; }
        set
        {
            if (listening)
                StopListening();
            driveInput = value;
            if (listening)
                StartListening();
        }
    }

    private void StartListening()
    {
        IDriveInput driver = DriveInput;
        if (driver == null)
            return;

        driver.DriftButton += DriftFlag;
        driver.BoostButton += BoostFlag;
    }

    private void StopListening()
    {
        IDriveInput driver = DriveInput;
        if (driver == null)
            return;

        driver.DriftButton -= DriftFlag;
        driver.BoostButton -= BoostFlag;
    }

    private void OnDestroy()
    {
        if (listening)
            StopListening();
        listening = false;
    }

    /// <summary>
    /// Standard Start. Just used to get references, get initial values, and subscribe to events
    /// </summary>
    private void Start()
    {
        pad = PlayerInstantiate.Instance.PlayerGamepads[playerIndex - 1];
        rumble = gameObject.GetComponent<Rumbler>();

        boostTimerMaxTime = boostDuration;
        boostElapsedTime = boostTimerMaxTime;
        boostRechargeTimeSet = boostRechargeTime;

        sphereBody = sphere.GetComponent<Rigidbody>();
        sphereTransform = sphere.GetComponent<Transform>();
        sphereCollider = sphere.GetComponent<Collider>();

        pMat = new PhysicMaterial();
        pMat.bounciness = 0.3f;
        pMat.staticFriction = RESTING_STATIC_FRICTION;
        pMat.dynamicFriction = RESTING_DYNAMIC_FRICTION;
        pMat.bounceCombine = PhysicMaterialCombine.Maximum;
        sphereCollider.material = pMat;

        startingDrag = sphereBody.drag;
        //baseFriction = selfPhysicsMaterial.dynamicFriction;
        //frictionDifference = brakingFriction - baseFriction;

        listening = true;
        StartListening(); // drift on the drift button, boost on the boost button

        scaledVelocityMax = accelerationPower * CSV_RATIO;

        respawn = sphere.GetComponent<Respawn>(); // get respawn component
        soundPool = GetComponent<SoundPool>();

        baseSpark = particleBasket.GetChild(0).GetComponent<ParticleManipulator>();
        wideSpark = particleBasket.GetChild(1).GetComponent<ParticleManipulator>();
        flare1Spark = particleBasket.GetChild(2).GetComponent<ParticleManipulator>();
        flare2Spark = particleBasket.GetChild(3).GetComponent<ParticleManipulator>();
        flare3Spark = particleBasket.GetChild(4).GetComponent<ParticleManipulator>();
        longSpark = particleBasket.GetChild(5).GetComponent<ParticleManipulator>();

        orderHandler.GotHit += SpinOut;
        orderHandler.Clash += BallClash;
    }

    /// <summary>
    /// Standard Update. Gets controls, updates variables, and manages many aspects of steering and speed
    /// </summary>
    private void Update()
    {
        if(DevTools.GetKeyDown(KeyCode.B))
            ResetBoost();

        transform.position = sphere.transform.position - new Vector3(0, 0.97f, 0); //makes the scooter follow the sphere

        //Assigns drag
        sphereBody.drag = startingDrag;
        if (forwardGear)
            sphereBody.drag = startingDrag * (1 + Mathf.Clamp(RangeMutations.Map_Linear(currentVelocity, 0, 20, 1, 0), 0, 1));
        if (!grounded)
            sphereBody.drag = fallingDrag;
        if (boosting && grounded)
            sphereBody.drag = boostingDrag;

        if (!canDrive)
            return;

        IDriveInput driver = DriveInput;
        leftStick = driver.Steer;
        leftTrig = driver.Brake;
        rightTrig = driver.Accelerate;

        // checks for playing engine and brake sounds
        if (csv == 0)
            soundPool.StopDrivingSound();
        else
            soundPool.PlayDrivingSound();

        if (forwardGear && leftTrig == 1 && !drifting)
            soundPool.PlayBrakeSound();

        if (callToDrift && leftStick != 0)
            AssignDriftState();

        //Checks for whether the scooter has been still long enough to be considered stopped
        currentVelocity = sphereBody.velocity.magnitude;
        if (currentVelocity > 0.5f)
        {
            csv = currentForce / currentVelocity;
            stopped = false;
            StopBrakeCheck();
        }
        else if (!brakeChecking && !stopped)
        {
            brakeChecking = true;
            StartBrakeCheck();
        }

        //When stopped, changes gear based on trigger input
        if (stopped)
        {
            reverseGear = false;
            forwardGear = false;

            if (rightTrig > leftTrig)
            {
                forwardGear = true;
                reverseGear = false;
                stopped = false;
            }
            else if (leftTrig > rightTrig)
            {
                reverseGear = true;
                forwardGear = false;
                stopped = false;
            }
        }

        if (reverseGear)
        {
            currentForce = Mathf.Max((reversingPower * leftTrig) - (reversingPower * rightTrig), 0);
            sphereBody.angularDrag = RangeMutations.Map_Linear(rightTrig, 0, 1, RESTING_ANGULAR_DRAG, FULLBRAKE_ANGULAR_DRAG);
            pMat.dynamicFriction = RangeMutations.Map_Linear(rightTrig, 0, 1, RESTING_DYNAMIC_FRICTION, FULLBRAKE_DYNAMIC_FRICTION);
            pMat.staticFriction = RangeMutations.Map_Linear(rightTrig, 0, 1, RESTING_STATIC_FRICTION, FULLBRAKE_STATIC_FRICTION);
        }
        if (forwardGear) 
        {
            currentForce = Mathf.Max((accelerationPower * rightTrig) - (accelerationPower * leftTrig), 0);
            sphereBody.angularDrag = RangeMutations.Map_Linear(leftTrig, 0, 1, RESTING_ANGULAR_DRAG, FULLBRAKE_ANGULAR_DRAG);
            pMat.dynamicFriction = RangeMutations.Map_Linear(leftTrig, 0, 1, RESTING_DYNAMIC_FRICTION, FULLBRAKE_DYNAMIC_FRICTION);
            pMat.staticFriction = RangeMutations.Map_Linear(leftTrig, 0, 1, RESTING_STATIC_FRICTION, FULLBRAKE_STATIC_FRICTION);
        }

        if (boosting)
            currentForce = accelerationPower;

        float modelRotateAmount;
        if (drifting)
        {
            //Determines the actual rotation of the larger object
            rotationAmount = Drift();

            //Determines model rotation
            float driftTargetAmount = (driftDirection > 0) ? RangeMutations.Map_Linear(leftStick, -1, 1, 0.5f, driftTurnScalar) : RangeMutations.Map_Linear(leftStick, -1, 1, driftTurnScalar, 0.5f);
            modelRotateAmount = 90 + driftTargetAmount * driftDirection * DRIFTING_MODEL_ROTATION * RangeMutations.Map_SpeedToSteering(currentVelocity, scaledVelocityMax);
        }
        else if (reverseGear)
        {
            DriftDrop(); //only needed like 1% of the time but fixes a weird little collision behavior
            
            //Determines actual rotation
            rotationAmount = leftStick * reverseSteeringPower;
            rotationAmount *= -RangeMutations.Map_SpeedToSteering(currentVelocity, scaledVelocityMax);

            //Determines model rotation
            modelRotateAmount = 90 + (leftStick * STEERING_MODEL_ROTATION * RangeMutations.Map_SpeedToSteering(currentVelocity, scaledVelocityMax));
        }
        else
        {
            //Determines the actual rotation of the larger object
            rotationAmount = leftStick * steeringPower;
            rotationAmount *= RangeMutations.Map_SpeedToSteering(currentVelocity, scaledVelocityMax + (boosting ? boostPower * CSV_RATIO : 0)); //scales steering by speed (also prevents turning on the spot)
            rotationAmount *= boosting ? boostingSteerModifier : 1.0f; //reduces steering if boosting

            //Determines model rotation
            modelRotateAmount = 90 + (leftStick * STEERING_MODEL_ROTATION * RangeMutations.Map_SpeedToSteering(currentVelocity, scaledVelocityMax) * (boosting ? boostingSteerModifier : 1.0f));
        }

        //Applies model rotation
        Quaternion intendedRotation = Quaternion.Euler((modelRotateAmount - 90f) * MODEL_TILT_MULTIPLIER * (drifting ? DRIFTING_MODEL_TILT_MULTIPLIER : 1), modelRotateAmount, 0);
        Quaternion newRotation = Quaternion.Lerp(scooterModel.parent.localRotation, intendedRotation, MODEL_ROTATION_TIME);
        scooterModel.parent.localRotation = newRotation;

        //Rotates the control object
        transform.eulerAngles = Vector3.Lerp(transform.eulerAngles, new Vector3(0, transform.eulerAngles.y + rotationAmount, 0), Time.deltaTime);

        ControlSpeedLines();

        // Updates player driving animations

        float speedValue = RangeMutations.Map_Linear(currentVelocity, 0, 30, 0, 10);
        playerAnimator.SetFloat(HashReference._speedFloat, speedValue);
    }

    /// <summary>
    /// Standard FixedUpdate. Handles the actual movement.
    /// </summary>
    private void FixedUpdate()
    {
        if (!canDrive)
            return;

        float totalForce = currentForce;

        //Adds the boost from a successful drift
        if (driftBoostAchieved)
        {
            sphereBody.velocity *= 0.65f;
            soundPool.PlayMiniBoost();
            switch (driftTier)
            {
                case 1:
                    driftBoost = driftBoost1;
                    break;
                case 2:
                    driftBoost = driftBoost2;
                    break;
                case 3:
                    driftBoost = driftBoost3;
                    break;
            }
            totalForce += driftBoost;
            driftTier = 0;
            driftBoostAchieved = false;
            firstFrameDriftBoostFlag = true;
            StartSlowdownImmunity();
            rumble.EndSuspension(pad);
            rumble.RumblePulse(pad, 0.3f, 0.65f, 0.3f);
            moveOnTransform = true;
        }

        //Adds the boost from rocket boosting
        if (boostInitialburst)
        {
            totalForce += boostPower;
            boostInitialburst = false;
        }

        //Adds the boost from slipstream
        SetSlipstreamTrails(leftSlipstreamTrail.time - Time.fixedDeltaTime);
        totalForce += Slipstream(); //most of the time Slipstream just returns 0

        //Adds the boost from ground boosts
        if (groundBoostFlag)
        {
            totalForce += groundBoostAmount;
            groundBoostFlag = false;
        }

        //Applies slow from grass patches
        if (groundSlowFlag && !boosting && !slowdownImmune)
        {
            totalForce *= slowPatchMultiplier;
            groundSlowFlag = false;
        }

        //Applies slow from holding the golden order
        if (orderHandler.HasGoldenOrder)
            totalForce *= goldenOrderMultiplier;

        //force ranges from 0 to about ~45-46 at the highest

        //Adds the force to move forward
        if (grounded)
        {
            if (respawn != null)
                respawn.LastGroundedPos = sphere.transform.position;

            if (forwardGear)
            {
                if (drifting)
                    sphereBody.AddForce((driftDirection == 1 ? ((driftSidewaysScalar * transform.forward) - transform.right).normalized : ((driftSidewaysScalar * transform.forward) + transform.right).normalized) * totalForce, ForceMode.Acceleration);
                else if (wheelying || moveOnTransform)
                {
                    sphereBody.AddForce(transform.forward * totalForce, ForceMode.Acceleration);
                    moveOnTransform = false;
                }
                else
                    sphereBody.AddForce(-scooterModel.transform.right * totalForce, ForceMode.Acceleration);
            }
            else if (reverseGear)
                sphereBody.AddForce(scooterModel.transform.right * totalForce, ForceMode.Acceleration);
        }
        else if (wheelying || firstFrameDriftBoostFlag) //allows boosting in mid-air. bit of a weird implementation; possibly refactor in the future.
        {
            sphereBody.AddForce(transform.forward * totalForce, ForceMode.Acceleration);
            firstFrameDriftBoostFlag = false;
        }

        //Clamping to make it easier to come to a complete stop
        if (sphereBody.velocity.magnitude < 1 && currentForce < 2)
            sphereBody.velocity = new Vector3(0, sphereBody.velocity.y, 0);

        if (sphereBody.velocity.magnitude < 6 || rightTrig < 0.05f)
            DriftDrop();

        // Enables raycasting for boosting while in a phase
        if (phaseSetMap)
        {
            int layerMask = 1 << 9;
            RaycastHit hit1, hit2;

            // First raycast
            bool hit1Success = Physics.Raycast(phaseRaycastPositions[0].transform.position, transform.TransformDirection(Vector3.down), out hit1, Mathf.Infinity, layerMask);
            // Second raycast
            bool hit2Success = Physics.Raycast(phaseRaycastPositions[1].transform.position, transform.TransformDirection(Vector3.down), out hit2, Mathf.Infinity, layerMask);

            if (hit1Success == false && hit2Success == false && checkPhaseStatus)
            {
                #if ENABLEDEBUGLOG
                    Debug.DrawRay(phaseRaycastPositions[0].transform.position, transform.TransformDirection(Vector3.down) * 200, Color.white);
                    Debug.DrawRay(phaseRaycastPositions[1].transform.position, transform.TransformDirection(Vector3.down) * 200, Color.white);
                #endif

                phasing = false;
                ToggleCollision(false);
                checkPhaseStatus = false;
                insideBuilding = false;

                if(phaseType == PhaseType.OnlyInBuilding)
                    cameraResizer.SwapCameraRendering(false);

                soundPool.StopPhaseSound();
            }
            // Check if either raycast hit
            else if (hit1Success == false && hit2Success == false && insideBuilding == true)
            {
                insideBuilding = false;

                if (phaseType == PhaseType.OnlyInBuilding)
                    cameraResizer.SwapCameraRendering(false);
            }
            else if (hit1Success == true && hit2Success == true && insideBuilding == false && boosting)
            {
                if (phaseType == PhaseType.OnlyInBuilding)
                    cameraResizer.SwapCameraRendering(true);

                insideBuilding = true;

                Debug.DrawRay(phaseRaycastPositions[0].transform.position, transform.TransformDirection(Vector3.down) * 200, Color.red);
                Debug.DrawRay(phaseRaycastPositions[1].transform.position, transform.TransformDirection(Vector3.down) * 200, Color.red);

                phasing = true;
                soundPool.PlayPhaseSound();
            }
        }

        GroundCheck();
    }

    /// <summary>
    /// Standard LateUpdate. Corrects model stuff when boosting
    /// </summary>
    private void LateUpdate()
    {
        if (wheelying)
            scooterModel.parent.parent.localEulerAngles = new Vector3(scooterModel.parent.parent.localEulerAngles.x, 0, 0);
    }

    /// <summary>
    /// Times how long the player has stood still, and marks them as stopped after a certain threshold
    /// </summary>
    /// <returns>Boilerplate IEnumerator</returns>
    private IEnumerator BrakeCheck()
    {
        timeSpentChecking = 0.0f;

        while (timeSpentChecking < BRAKE_CHECK_TIME)
        {
            timeSpentChecking += Time.deltaTime;
            yield return null;
        }

        stopped = true;
        brakeChecking = false;
    }

    /// <summary>
    /// Used a short raycast to check whether there's a driveable surface beneath the scooter, as well as find its slope
    /// Flags grounded if there's a surface, and matches the scooter to its angle if so
    /// </summary>
    private void GroundCheck()
    {
        int lm = (1 << 0) | (1 << 9) | (1 << 29);//513; //layers 0 and 9
        RaycastHit hit, waterHit;

        if (Physics.Raycast(scooterNormal.transform.position, Vector3.down, out hit, GROUNDCHECK_DISTANCE, lm))
            grounded = true;
        else
            grounded = false;

        if (grounded)
        {
            StopCoyoteTime();
            hasCoyoted = false;

            scooterNormal.up = Vector3.Lerp(scooterNormal.up, hit.normal, Time.fixedDeltaTime * 10.0f);
            scooterNormal.Rotate(0, transform.eulerAngles.y, 0);

            switch (hit.collider.tag)
            {
                case "Speed":
                    groundBoostFlag = true;
                    try
                    {
                        groundBoostAmount = hit.collider.gameObject.GetComponent<Booster>().SpeedBoostAmount;
                    }
                    catch (System.NullReferenceException e) //indicates the booster has no Booster script, and should just use the default value
                    {
                        groundBoostAmount = groundBoostDefault;
                    }
                    break;

                case "TouchGrass":
                    groundSlowFlag = true;
                    break;

                case "MovingPlatform":
                    if (currentMovingPlatform == null)
                    {
                        onMovingPlatform = true;
                        currentMovingPlatform = hit.collider.gameObject.GetComponent<MovingPlatform>();
                        if ((movingPlatformIndex = currentMovingPlatform.AddToScooterList(this)) == -1)
                        {
                            #if ENABLEDEBUGLOG

                            #endif
                        }
                    }
                    break;

                default:
                    if (currentMovingPlatform != null)
                    {
                        onMovingPlatform = false;
                        currentMovingPlatform.RemoveFromScooterList(movingPlatformIndex);
                        currentMovingPlatform = null;
                    }
                    break;
            }
        }
        else if (!coyoteing && !hasCoyoted && boosting && coyoteTimeCoroutine == null)
        {
            StartCoyoteTime();
        }

        if (!dirtyTerrainRespawn)
        {
            if (Physics.Raycast(scooterNormal.transform.position, Vector3.down, out waterHit, WATERCHECK_DISTANCE))
            {
                if (waterHit.collider.tag == "Water")
                {
                    respawn.StartRespawnCoroutine();
                    dirtyTerrainRespawn = true;
                }
            }
        }
    }

    private IEnumerator CoyoteTime()
    {
        coyoteing = true;
        hasCoyoted = true;
        int framesElapsed = 0;

        while (framesElapsed < coyoteFrames)
        {
            sphereBody.velocity = new Vector3(sphereBody.velocity.x, Mathf.Max(0f, sphereBody.velocity.y), sphereBody.velocity.z);

            framesElapsed++;

            yield return null;
        }

        StopCoyoteTime();
    }

    /// <summary>
    /// Receives input as an event. Flags callToDrift and drifting depending on circumstance. Applies the boost if applicable
    /// </summary>
    /// <param name="WestFaceState">The state of the west face button, passed by the event</param>
    private void DriftFlag(bool WestFaceState)
    {
        if (WestFaceState)
        {
            if (leftStick != 0)
                AssignDriftState();
            else
                callToDrift = true; //can't drift without a selected direction, so it stows the request until a direction is selected
        }
        else
        {
            if (driftTier > 0)
                driftBoostAchieved = true;

            DriftDrop(false);
        }
    }

    /// <summary>
    /// Sets drifting to true and assings its direction based on the left stick.
    /// </summary>
    private void AssignDriftState()
    {
        if (!boosting && !reverseGear && grounded)
        {
            soundPool.PlayDriftSound();
            callToDrift = false;
            drifting = true;

            driftDirection = leftStick < 0 ? -1 : 1;

            //Does a little hop does a little jump does a little skip
            scooterModel.parent.DOComplete();
            scooterModel.parent.DOPunchPosition(transform.up * DRIFT_HOP_AMOUNT, DRIFT_HOP_TIME, 5, 0);
        }
        else
            callToDrift = true;
    }

    /// <summary>
    /// The function that actually handles drift. 
    /// Scales the user input to be from 0 to driftTurnScalar instead of -1 to 1, enhancing the turning circle but restricting its direction
    /// </summary>
    /// <returns>A rotation amount</returns>
    private float Drift()
    {
        float scaledInput = RangeMutations.Map_Linear(leftStick, -1, 1, driftDirection > 0 ? driftTurnMinimum : driftTurnScalar, driftDirection > 0 ? driftTurnScalar : driftTurnMinimum);
        driftPoints += (2 * Time.deltaTime * (1 - driftBoostMode)) + (Time.deltaTime * scaledInput * driftBoostMode) * 100.0f;

        rumble.SuspendedRumble(pad, 0.0f, 0.07f);

        if (driftPoints > driftBoostThreshold) 
        {
            driftTier = 1;
            DriftSparkSet(1);
            soundPool.PlayDriftSpark(0);
            rumble.SuspendedRumble(pad, 0.0f, 0.1f);
        }
        if (driftPoints > (driftBoostThreshold * 2))
        {
            driftTier = 2;
            DriftSparkSet(2);
            //soundPool.PlayDriftSpark(1);
            rumble.SuspendedRumble(pad, 0.02f, 0.13f);
        }
        if (driftPoints > (driftBoostThreshold * 3))
        {
            driftTier = 3;
            DriftSparkSet(3);
            //soundPool.PlayDriftSpark(2);
            rumble.SuspendedRumble(pad, 0.04f, 0.16f);
        }

        return steeringPower * driftDirection * scaledInput * RangeMutations.Map_SpeedToSteering(currentVelocity, scaledVelocityMax); //scales steering by speed (also prevents turning on the spot)
    }

    /// <summary>
    /// Ends drifting; can work with or without boost.
    /// </summary>
    /// <param name="dirty">Whether the drop is dirty</param>
    public void DriftDrop(bool dirty = true)
    {
        soundPool.StopDriftSound();
        soundPool.StopDriftSpark();

        drifting = false;
        callToDrift = false;
        driftPoints = 0;
        driftTier = dirty ? 0 : driftTier;

        DriftSparkSet(0);
        rumble.EndSuspension(pad);
    }

    /// <summary>
    /// Sets the sparks when drifting, aligns them to the correct angle
    /// </summary>
    /// <param name="tier">Which drift tier applies. 0 is no tier.</param>
    private void DriftSparkSet(int tier)
    {
        if (driftDirection > 0)
        {
            particleBasket.position = sparksPos2.position;
            particleBasket.localEulerAngles = new Vector3(0, 160, 0);
        }
        else
        {
            particleBasket.position = sparksPos1.position;
            particleBasket.localEulerAngles = new Vector3(0, 20, 0);
        }

        switch (tier) 
        {
            case 0:
                baseSpark.gameObject.SetActive(false);
                wideSpark.gameObject.SetActive(false);
                flare1Spark.gameObject.SetActive(false);
                longSpark.gameObject.SetActive(false);
                flare2Spark.gameObject.SetActive(false);
                flare3Spark.gameObject.SetActive(false);
                break;

            case 1:
                baseSpark.gameObject.SetActive(true);
                wideSpark.gameObject.SetActive(true);
                flare1Spark.gameObject.SetActive(true);

                baseSpark.StartColor = driftSparksTier1Color;
                wideSpark.StartColor = driftSparksTier1Color;
                flare1Spark.StartColor = driftSparksTier1Color;
                break;

            case 2:
                baseSpark.gameObject.SetActive(true);
                wideSpark.gameObject.SetActive(true);
                flare1Spark.gameObject.SetActive(true);
                longSpark.gameObject.SetActive(true);
                flare2Spark.gameObject.SetActive(true);

                baseSpark.StartColor = driftSparksTier2Color;
                wideSpark.StartColor = driftSparksTier2Color;
                flare1Spark.StartColor = driftSparksTier2Color;
                longSpark.StartColor = driftSparksTier2Color;
                flare2Spark.StartColor = driftSparksTier2Color;
                break;

            case 3:
                baseSpark.gameObject.SetActive(true);
                wideSpark.gameObject.SetActive(true);
                flare1Spark.gameObject.SetActive(true);
                longSpark.gameObject.SetActive(true);
                flare2Spark.gameObject.SetActive(true);
                flare3Spark.gameObject.SetActive(true);

                baseSpark.StartColor = driftSparksTier3Color;
                wideSpark.StartColor = driftSparksTier3Color;
                flare1Spark.StartColor = driftSparksTier3Color;
                longSpark.StartColor = driftSparksTier3Color;
                flare2Spark.StartColor = driftSparksTier3Color;
                flare3Spark.StartColor = driftSparksTier3Color; 
                break;
        }
    }

    /// <summary>
    /// Simple timer for how long the player is immune to grass slowdowns after getting a drift boost
    /// </summary>
    /// <returns>Boilerplate IEnumerator</returns>
    private IEnumerator SlowdownImmunity()
    {
        slowdownImmune = true;
        yield return new WaitForSeconds(slowdownImmunityDuration);
        slowdownImmune = false;
    }

    /// <summary>
    /// Receives input as an event. Calls for a boost to be activated if possible
    /// </summary>
    /// <param name="WestFaceState">The state of the south face button, passed by the event</param>
    private void BoostFlag(bool SouthFaceState)
    {
        if (boostAble && !callToDrift && !drifting && !reverseGear && !spinningOut && !isFrozen) //& by Tally Hall
        {
            StartBoostActive();
            OnBoostStart?.Invoke();

            rumble.RumblePulse(pad, 0.45f, 0.9f, 1f);
            soundPool.PlayBoostActivate();
        }
    }

    /// <summary>
    /// Initializes all aspects of boost, starts the wheelie, calls on recharge, then calls on the end steps
    /// </summary>
    /// <returns>Boilerplate IEnumerator</returns>
    private IEnumerator BoostActive()
    {
        boosting = true;
        boostAble = false;
        boostInitialburst = true;
        DriftDrop();

        stopped = false;
        reverseGear = false;
        forwardGear = true;

        ToggleCollision(true);

        if (phaseType == PhaseType.AtAllTimes)
            cameraResizer.SwapCameraRendering(true);

        //~~~~~~~~~Wheelie~~~~~~~~~~~
        scooterModel.parent.parent.DOComplete();
        scooterModel.parent.parent.localEulerAngles = Vector3.zero;
        wheelie = scooterModel.parent.parent.DORotate(new Vector3(-WHEELIE_AMOUNT, 0, 0), 0.8f * 1.6f, RotateMode.LocalAxisAdd);
        wheelie.SetEase(Ease.OutQuint);
        wheelie.SetRelative(true);
        wheelieEnd = scooterModel.parent.parent.DORotate(new Vector3(WHEELIE_AMOUNT, 0, 0), 0.8f * 1.6f, RotateMode.LocalAxisAdd);
        wheelieEnd.SetEase(Ease.OutBounce);
        wheelieEnd.SetRelative(true);
        mySeq = DOTween.Sequence();
        mySeq.Append(wheelie);
        mySeq.Append(wheelieEnd);
        wheelying = true;
        //~~~~~~~~EndWheelie~~~~~~~~~~

        boostElapsedTime = boostDuration;
        boostTimerMaxTime = boostDuration;
        while (boostElapsedTime > 0)
        {
            boostElapsedTime -= Time.deltaTime;
            boostElapsedTime = Mathf.Max(boostElapsedTime, 0f);
            yield return null;
        }


        // After glow depletion is complete, proceed with the rest of the boost logic
        StartEndBoost(wheelie, wheelieEnd);
        StopBoostActive();
    }

    /// <summary>
    /// Ends a boost. Stops phasing, finishes the wheelie, corrects all positions, starts the cooldown
    /// </summary>
    /// <param name="wheelie">Reference to the tween for the wheelie</param>
    /// <param name="wheelieEnd">Reference to the tween for the end of the wheelie</param>
    /// <returns>Boilerplate IEnumerator</returns>
    private IEnumerator EndBoost(Tween wheelie = null, Tween wheelieEnd = null)
    {
        // Toggles to check phase status
        checkPhaseStatus = true;

        yield return new WaitForFixedUpdate();

        while (phasing)
            yield return new WaitForSeconds(0.1f);

        boosting = false;

        if (phaseType == PhaseType.AtAllTimes)
            cameraResizer.SwapCameraRendering(false);

        if (wheelie != null && wheelieEnd != null) 
        {
            wheelie.Complete();

            yield return wheelieEnd.WaitForCompletion();

            scooterModel.parent.parent.localEulerAngles = Vector3.zero;
            wheelying = false;
        }

        StartBoostCooldown();
        StopEndBoost();
    }

    private void StartBoostCooldown()
    {
        boostCooldownCoroutine = BoostCooldown();
        StartCoroutine(boostCooldownCoroutine);
    }

    /// <summary>
    /// Waits for the recharge duration then enables boosting again
    /// </summary>
    /// <returns>IEnumerator boilerplate</returns>
    private IEnumerator BoostCooldown()
    {
        KillWheelie();

        boostElapsedTime = 0f;
        boostTimerMaxTime = boostRechargeTime;

        while (boostElapsedTime < boostTimerMaxTime)
        {
            boostElapsedTime += Time.deltaTime;
            boostElapsedTime = Mathf.Min(boostElapsedTime, boostTimerMaxTime);
            yield return null;
        }

        boostAble = true;
        StopBoostCooldown();
    }

    /// <summary>
    /// Instantly recharges boost; used for scene transition or debug
    /// </summary>
    private void ResetBoost()
    {
        StopBoostCooldown();
        StopBoostActive();
        StopEndBoost();

        KillWheelie();

        boostElapsedTime = boostTimerMaxTime;

        if (phaseType == PhaseType.AtAllTimes)
            cameraResizer.SwapCameraRendering(false);

        boostAble = true;
        boosting = false;
        wheelying = false;
        checkPhaseStatus = true;
    }

    private void KillWheelie()
    {
        wheelie.Complete();
        wheelieEnd.Complete();
        mySeq.Complete();
        scooterModel.parent.parent.localEulerAngles = Vector3.zero;
    }

    /// <summary>
    /// Sets collision layers for boost phasing
    /// </summary>
    /// <param name="toggle">Whether to phase or not</param>
    private void ToggleCollision(bool toggle)
    {
        switch (playerIndex)
        {
            case 1:
                Physics.IgnoreLayerCollision(9, 10, toggle);
                break;
            case 2:
                Physics.IgnoreLayerCollision(9, 11, toggle);
                break;
            case 3:
                Physics.IgnoreLayerCollision(9, 12, toggle);
                break;
            case 4:
                Physics.IgnoreLayerCollision(9, 13, toggle);
                break;
        }
    }

    /// <summary>
    /// Increases speed slowly while behind another vehicle and facing in approximately the same direction. 
    /// After a certain amount of time tailing them like that, grants a further burst of speed
    /// </summary>
    /// <returns>How much speed should be added based on current slipstream status</returns>
    private float Slipstream()
    {
        BallDriving caddy = null;
        bool slipstreamRaysAligned = false;
        bool caddySpeedMet = false;
        bool selfSpeedMet = currentVelocity > minimumSlipstreamSpeed;

        int lm = 128; //layer 7
        RaycastHit hit;

        Debug.DrawRay(transform.position + Vector3.up, scooterNormal.forward * slipstreamDistance, Color.green);
        if (Physics.Raycast(transform.position + Vector3.up, scooterNormal.forward, out hit, slipstreamDistance, lm)) //checks forward ray
        {
            caddy = hit.collider.gameObject.GetComponent<BallDriving>();
            caddySpeedMet = caddy.CurrentVelocity > minimumSlipstreamSpeed;

            RaycastHit secondHit;

            if (Physics.Raycast(caddy.transform.position + Vector3.up, -caddy.ScooterNormal.forward, out secondHit, slipstreamDistance, lm)) //checks reciprocal ray
                slipstreamRaysAligned = true;
        }

        //Updates slipstream time. Decreases at twice the speed it increases
        if (slipstreamRaysAligned && caddySpeedMet && selfSpeedMet)
            slipstreamPortion += Time.fixedDeltaTime;
        else
            slipstreamPortion -= (Time.fixedDeltaTime * 2.0f);
        slipstreamPortion = Mathf.Clamp(slipstreamPortion, 0.0f, slipstreamTime);

        float slipStreamScalar = RangeMutations.Map_Linear(slipstreamPortion, 0.0f, slipstreamTime, 0.0f, 1.0f);

        //Returns a certain amount of speed based on the current amount of slipstream
        if (slipstreamPortion == slipstreamTime)
        {
            slipstreamPortion = 0.0f;
            if (caddy != null) caddy.SetSlipstreamTrails(0.0f);
            return slipstreamBoostAmount;
        }
        else
        {
            if (caddy != null) caddy.SetSlipstreamTrails(slipStreamScalar - 0.2f);
            return slipStreamScalar * preBoostSlipstreamMax;
        }
    }

    /// <summary>
    /// Sets the length of the slipstream indication trails. Called by a *different* player
    /// </summary>
    /// <param name="trailAmount">How long the trail should go</param>
    public void SetSlipstreamTrails(float trailAmount)
    {
        leftSlipstreamTrail.time = trailAmount;
        rightSlipstreamTrail.time = trailAmount;
    }

    /// <summary>
    /// Starts a spinout when called. Meant to be invoked by events from OrderHandler
    /// </summary>
    private void SpinOut()
    {
        if (!spinningOut)
        {
            canDrive = false;
            spinningOut = true;
            DriftDrop();
            StartSpinOutTime();
        }
    }

    /// <summary>
    /// Spins the model. Uses the outBack easing function to overshoot slightly, then correct.
    /// Simultaneously rocks the model side to side. Once done, returns the ability to drive.
    /// </summary>
    /// <returns>IEnumerator boilerplate</returns>
    private IEnumerator SpinOutTime()
    {
        scooterModel.parent.DOComplete(); //make sure nothing's in the wrong place
        float tweenTime = 1.0f;

        Tween spinning = scooterModel.parent.DORotate(new Vector3(scooterModel.parent.rotation.x, 360, scooterModel.parent.rotation.z), tweenTime, RotateMode.LocalAxisAdd);
        spinning.SetEase(Ease.OutBack); //an easing function which dictates a steep climb, slight overshoot, then gradual correction

        Tween rocking = scooterModel.DOShakeRotation(tweenTime, new Vector3(10, 0, 0), 10, 90, true, ShakeRandomnessMode.Harmonic); //rocks the scooter around its long axis

        yield return spinning.WaitForCompletion();

        canDrive = true;
        spinningOut = false;
        scooterModel.parent.localEulerAngles = new Vector3(scooterModel.rotation.x, 90, scooterModel.rotation.z); //prevents the model from misaligning
    }

    /// <summary>
    /// Slams balls together
    /// </summary>
    /// <param name="opponent">The other person in the collision</param>
    private void BallClash(OrderHandler opponent)
    {
        if (phasing)
            return;

        Rigidbody opponentBall = opponent.gameObject.GetComponent<BallDriving>().Sphere.GetComponent<Rigidbody>(); //woof
        BounceOff(opponentBall.position, clashForce);
    }

    /// <summary>
    /// Bounces the player off in the opposite direction of a given point
    /// </summary>
    /// <param name="oppositePoint">The point to move away from</param>
    /// <param name="force">The force of the bounce</param>
    /// <param name="spark">Optional parameter for whether the bounce generates sparks. Defaults to true</param>
    public void BounceOff(Vector3 oppositePoint, float force, bool spark = true)
    {
        Vector3 difference = (sphereBody.position - oppositePoint).normalized;
        difference.y = 0.15f;
        difference.Normalize();

        sphereBody.AddForce(difference * clashForce, ForceMode.Impulse);
        PeterSparker.Instance.CreateImpactFromCollider(sphereCollider, oppositePoint);
    }

    /// <summary>
    /// Changes speed line value based on speed and boosting state
    /// </summary>
    private void ControlSpeedLines()
    {
        if (boosting)
        {
            speedLineValue = 0.8f;
            speedLinesMain.SetFloat("_SpeedLinesRemap", speedLineValue);

            StartCoroutine(orbitalCamera.SetFOVAfterTime(orbitalCamera.maxFOV, 0.3f));
        }
        else
        {
            float clampedSpeedLineValue = RangeMutations.Map_Linear(currentVelocity, 23, 30, 1, 0.85f);
            clampedSpeedLineValue = Mathf.Clamp(clampedSpeedLineValue, 0.85f, 1f);

            float clampedFOVValue = RangeMutations.Map_Linear(currentVelocity, 30, 40, 60f, orbitalCamera.maxFOV);
            clampedFOVValue = Mathf.Clamp(clampedFOVValue, 60f, orbitalCamera.maxFOV);

            orbitalCamera.passInFOV = clampedFOVValue;

            speedLineValue = clampedSpeedLineValue;
            speedLinesMain.SetFloat("_SpeedLinesRemap", speedLineValue);
        }
    }

    /// <summary>
    /// Freezes the ball's rigidbody and prevents it from driving
    /// </summary>
    /// <param name="toFreeze">True for freeze, False for unfreeze</param>
    /// <param name="resetBoost">True to reset boost as part of freezing/unfreezing</param>
    /// <param name="freezeY">True to freeze Y for the results freezeframe</param>
    public void FreezeBall(bool toFreeze, bool resetBoost = true, bool freezeY = false)
    {


        if (sphereBody == null)
            return;

        DriftDrop(true);
        driftTier = 0;
        sphereBody.velocity = Vector3.zero;
        sphereBody.angularVelocity = Vector3.zero;

        canDrive = !toFreeze;

        sphereBody.constraints = toFreeze ? RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ : RigidbodyConstraints.None;

        if (freezeY && toFreeze)
            sphereBody.constraints = RigidbodyConstraints.FreezePositionY;

        isFrozen = toFreeze;

        if (resetBoost)
        {
            ResetBoost();
        }
        else if (boostActiveCoroutine != null || endBoostCoroutine != null)
        {
            StopBoostActive();
            StopEndBoost();

            KillWheelie();
            checkPhaseStatus = true;
            boosting = false;
            wheelying = false;
            if (phaseType == PhaseType.AtAllTimes)
                cameraResizer.SwapCameraRendering(false);
            
            StartBoostCooldown();
        }


    }

    /// <summary>
    /// Automatically starts a boost. Used for results freezeframe.
    /// </summary>
    public void AutoBoost()
    {
        FreezeBall(false);
        StartBoostActive();
    }

    /// <summary>
    /// Sets the speed lines material for the player, which allows us to change during gameplay
    /// </summary>
    public void SetSpeedLinesMaterial(int playerIndex)
    {
        speedLinesMain = potentialPlayerSpeedLineMaterials[playerIndex - 1];
        speedLinesMain.SetFloat("_SpeedLinesRemap", speedLineValue);
    }

    /// <summary>
    /// Sets the boost modifier based on whether or not the player is holding something.
    /// </summary>
    /// <param name="holdingPackage">Status of the player's inventory</param>
    public void SetBoostModifier(bool holdingPackage)
    {
        float currentElapsedTimeRatio = boostElapsedTime / boostTimerMaxTime;
        boostRechargeTime = holdingPackage ? handsFullBoostRechargeTime : boostRechargeTimeSet;
        SetBoostRatio(currentElapsedTimeRatio);
    }

    /// <summary>
    /// Sets the boost modifier to a set value. Will add that value if add is true.
    /// </summary>
    /// <param name="mod">Modifier value</param>
    public void HardCodeBoostModifier(float mod)
    {
        float currentElapsedTimeRatio = boostElapsedTime / boostTimerMaxTime;
        boostRechargeTime = mod;
        SetBoostRatio(currentElapsedTimeRatio);
    }

    private void SetBoostRatio(float ratio)
    {
        if (!boosting)
        {
            boostTimerMaxTime = boostRechargeTime;
            boostElapsedTime = ratio * boostTimerMaxTime;
        }
    }

    private void StartBoostActive()
    {
        boostActiveCoroutine = BoostActive();
        StartCoroutine(boostActiveCoroutine);
    }
    private void StopBoostActive()
    {
        if (boostActiveCoroutine != null)
        {
            StopCoroutine(boostActiveCoroutine);
            boostActiveCoroutine = null;
        }
    }

    private void StopBoostCooldown()
    {
        if (boostCooldownCoroutine != null)
        {
            StopCoroutine(boostCooldownCoroutine);
            boostCooldownCoroutine = null;
        }
    }

    private void StartEndBoost(Tween wheelie = null, Tween wheelieEnd = null)
    {
        Tween w = wheelie == null ? null : wheelie;
        Tween wE = wheelieEnd == null ? null : wheelieEnd;

        endBoostCoroutine = EndBoost(w, wE);
        StartCoroutine(endBoostCoroutine);
    }
    private void StopEndBoost()
    {
        if (endBoostCoroutine != null)
        {
            StopCoroutine(endBoostCoroutine);
            endBoostCoroutine = null;
        }
    }

    private void StartSpinOutTime()
    {
        spinOutTimeCoroutine = SpinOutTime();
        StartCoroutine(spinOutTimeCoroutine);
    }
    private void StopSpinOutTime()
    {
        if (spinOutTimeCoroutine != null)
        {
            StopCoroutine(spinOutTimeCoroutine);
            spinOutTimeCoroutine = null;
        }
    }

    private void StartBrakeCheck()
    {
        brakeCheckCoroutine = BrakeCheck();
        StartCoroutine(brakeCheckCoroutine);
    }
    private void StopBrakeCheck()
    {
        if (brakeCheckCoroutine != null)
        {
            StopCoroutine(brakeCheckCoroutine);
            brakeCheckCoroutine = null;
        }
        brakeChecking = false;
    }

    private void StartSlowdownImmunity()
    {
        StopSlowdownImmunity();
        slowdownImmunityCoroutine = SlowdownImmunity();
        StartCoroutine(slowdownImmunityCoroutine);
    }
    private void StopSlowdownImmunity()
    {
        if (slowdownImmunityCoroutine != null)
        {
            StopCoroutine(slowdownImmunityCoroutine);
            slowdownImmunityCoroutine = null;
        }
    }

    private void StartCoyoteTime()
    {
        StopCoyoteTime();
        coyoteTimeCoroutine = CoyoteTime();
        StartCoroutine(coyoteTimeCoroutine);
    }
    private void StopCoyoteTime()
    {
        if (coyoteTimeCoroutine != null)
        {
            StopCoroutine(coyoteTimeCoroutine);
            coyoteTimeCoroutine = null;
            coyoteing = false;
        }
    }
}

using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OrbitalCamera : MonoBehaviour
{
    [SerializeField] InputManager inputManager;
    IDriveInput driveInput; // set when something other than this prefab's controller turns the camera

    /// <summary>
    /// Whose right stick turns this camera: the controller on this machine unless something else is set
    /// </summary>
    public IDriveInput DriveInput { get { return driveInput ?? inputManager; } set { driveInput = value; } }

    [Header("Horizontal Movement")]
    [SerializeField] internal CinemachineVirtualCamera virtualCameraMain;
    [SerializeField] internal CinemachineVirtualCamera virtualCameraIcon;
    [SerializeField] internal CinemachineOrbitalTransposer mainOrb;
    [SerializeField] internal CinemachineOrbitalTransposer iconOrb;
    [SerializeField] float maxXAngle = 180;
    [SerializeField] float smoothSpeedValue = 0.1f;
    [SerializeField] float realXAxis;
    public float smoothXAxis;

    [Header("Vertical Movement")]
    [SerializeField] internal GameObject CameraFocus;
    [SerializeField] Vector2 yAngleMinMax;
    [SerializeField] float realYAxis;
    public float smoothYAxis;

    [Header("FOV")]
    [SerializeField] float fovValue = 60f;
    public float passInFOV;
    public float maxFOV = 80;
    [SerializeField] float changeSpeed;

    bool reverseCamera;

    // Start is called before the first frame update
    void Start()
    {
        mainOrb = virtualCameraMain.GetCinemachineComponent<CinemachineOrbitalTransposer>();
        iconOrb = virtualCameraIcon.GetCinemachineComponent<CinemachineOrbitalTransposer>();
    }

    // Update is called once per frame
    internal void Update()
    {
        IDriveInput driver = DriveInput;
        // While boosting, the view goes wide after a moment, and stays wide a moment after (BoostFOV); never with
        // Reduce motion on (Comfort)
        float targetFOV = Comfort.TargetFov(boostHold.Active(Time.time), boostFOV, passInFOV);
        fovValue = Mathf.Lerp(fovValue, targetFOV, changeSpeed);

        // Custom joystick camera aim
        if (!driver.LookBehind && reverseCamera == false)
        {
            realXAxis = RangeMutations.Map_Linear(driver.CameraX, -1, 1, -maxXAngle, maxXAngle);
            realYAxis = RangeMutations.Map_Linear(driver.CameraY, -1, 1, yAngleMinMax.x, yAngleMinMax.y);

            smoothXAxis = Mathf.Lerp(smoothXAxis, realXAxis, smoothSpeedValue);
            smoothYAxis = Mathf.Lerp(smoothYAxis, realYAxis, smoothSpeedValue);

            mainOrb.m_XAxis.Value = smoothXAxis;
            iconOrb.m_XAxis.Value = smoothXAxis;

            CameraFocus.transform.localPosition = new Vector3(CameraFocus.transform.localPosition.x, smoothYAxis, CameraFocus.transform.localPosition.z);

            float kick = KickAt(kickDegrees, Time.time - kickedAt);
            virtualCameraMain.m_Lens.FieldOfView = fovValue + kick;
            virtualCameraIcon.m_Lens.FieldOfView = fovValue + kick;

        }
        // Reset look behind
        if (!driver.LookBehind && reverseCamera == true)
        {
            reverseCamera = false;
            smoothXAxis = 0f;
        }
        // Static look behind
        else if(driver.LookBehind)
        {
            reverseCamera = true;

            mainOrb.m_XAxis.Value = -180;
            iconOrb.m_XAxis.Value = -180;

            realXAxis = 180;
            realYAxis = 0;
            
            smoothYAxis = 0;
            smoothXAxis = 180f;

            virtualCameraMain.m_Lens.FieldOfView = 60;
            virtualCameraIcon.m_Lens.FieldOfView = 60;

            CameraFocus.transform.localPosition = new Vector3(CameraFocus.transform.localPosition.x, smoothYAxis, CameraFocus.transform.localPosition.z);
        }
    }

    // A steal's kick: the view jumps wider and eases back (BallDriving.StealJolt)
    internal const float KICK_SECONDS = 0.25f;
    float kickDegrees;
    float kickedAt = -KICK_SECONDS;

    /// <summary>Widens the view by some degrees at once, easing back over KICK_SECONDS. Comfort decides whether it kicks at all</summary>
    public void Kick(float degrees)
    {
        kickDegrees = degrees;
        kickedAt = Time.time;
    }

    /// <summary>How much of a kick is left a time after it: all of it at first, none from KICK_SECONDS</summary>
    internal static float KickAt(float degrees, float age)
    {
        if (age < 0f || age >= KICK_SECONDS)
            return 0f;
        return degrees * (1f - age / KICK_SECONDS);
    }

    // The boost's wide view (FovHold)
    const float BOOST_FOV_DELAY = 0.3f;
    readonly FovHold boostHold = new FovHold(BOOST_FOV_DELAY);
    float boostFOV;

    /// <summary>
    /// Asked every frame while boosting: the view widens to this field of view a moment after the boost starts, and
    /// stays wide a moment after it ends
    /// </summary>
    public void BoostFOV(float fov)
    {
        boostFOV = fov;
        boostHold.Request(Time.time);
    }
}

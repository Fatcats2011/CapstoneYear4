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
        // While boosting, the view goes wide after a moment, and stays wide a moment after (BoostFOV)
        float targetFOV = boostHold.Active(Time.time) ? boostFOV : passInFOV;
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

            virtualCameraMain.m_Lens.FieldOfView = fovValue;
            virtualCameraIcon.m_Lens.FieldOfView = fovValue;

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

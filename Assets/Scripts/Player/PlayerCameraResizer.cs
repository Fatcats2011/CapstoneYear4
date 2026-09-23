using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// This class resizes the rect of all cameras in array, based on a reference cam. 
/// This is needed due to the player input script only allowing one camera to be resized on instantiation. 
/// This acts to allow all cameras needed to be resized, after the main is resized.
/// /// </summary>
public class PlayerCameraResizer : MonoBehaviour
{
    [Header("Main Camera Details")]
    [Tooltip("This is the camera in which if changed, other cameras will also change to match")]
    [SerializeField] Camera referenceCam;
    public Camera PlayerReferenceCamera { get { return referenceCam; } }
    [SerializeField] UniversalAdditionalCameraData referenceCamData;

    [Tooltip("This is the camera array in which all will resize to the main upon main being changed")]
    [SerializeField] Camera[] camerasToFollow;

    [Tooltip("This is a vector4 value indicating the default rect of a camera. the four values are xPos, yPos, Width and Height")]
    [SerializeField] Vector4 viewPortRectDefault;

    [Tooltip("This reference is to the player camera outputing to the render textures")]
    [SerializeField] Camera playerRenderCamera;
    public Camera PlayerRenderCamera { get { return playerRenderCamera; } }

    [Space(10)]
    [Header("Cinemachine Info")]
    [SerializeField] GameObject virtualCameraMain;
    [SerializeField] GameObject virtualCameraIcon;
    [SerializeField] CinemachineCollider cameraCollidder;

    [Space(10)]
    [Header("Camera References")]
    [SerializeField] LayerMask drivingMask;
    [SerializeField] LayerMask phasingMask;

    [SerializeField] Camera drivingUICamera;
    [SerializeField] Camera iconCamera;
    [SerializeField] Camera playerCamera;
    [SerializeField] Camera menuUICamera;

    [Space(10)]
    [Header("Phase Camera")]
    [SerializeField] float phaseTransitionSpeed = 0.15f;
    [SerializeField] float shaderPassIncrements = 10f;
    [SerializeField] Material phaseTransitionMaterial;
    [SerializeField] GameObject phaseRender;
    [SerializeField] Camera phaseCamera;
    [SerializeField] UniversalAdditionalCameraData phaseCamData;
    Material phaseTransitionMaterialMain;
    RenderTexture phaseCameraRT;
    bool phaseCameraUpdate;
    bool currentPhaseStatus;
    Coroutine phaseCameraUpdateCoroutine;

    [Space(10)]
    [Header("Scooter Information")]
    [SerializeField] MeshRenderer scooterModel;
    [SerializeField] DecalProjector[] logoDecal;

    [SerializeField] CompanyInformation companyInfo;
    public CompanyInformation CompanyInfo { get { return companyInfo; } }

    [Space(10)]
    [Header("Other")]
    public Animator playerAnimator;
    [SerializeField] GameObject customizationSelector;
    [SerializeField] BallDriving ballDriving;
    [SerializeField] DrivingIndicators drivingIndicators;
    bool initalized = false;
    Rect followedRect;

    public int cameraLayer = 0;
    public int playerWorldLayer = 0;
    int iconLayer = 0;
    [SerializeField] int nextFillSlot = 0;

    private void Start()
    {
        phaseTransitionMaterialMain = new Material(phaseTransitionMaterial);

        ResizePhaseTexture();

        phaseRender.GetComponent<Image>().material = phaseTransitionMaterialMain;
    }

    // Update is called once per frame
    void Update()
    {
        // Updates the phase camera to follow the movement of the main camera
        if (phaseCameraUpdate)
        {
            // Updates position and rotation
            phaseCamera.transform.rotation = referenceCam.transform.rotation;
            phaseCamera.transform.position = referenceCam.transform.position;
        }

        // Keeps the phase transition texture the size of this player's view (players joining or leaving, window resized)
        if (phaseCameraRT != null)
            ResizePhaseTexture();

        // Waits until the player is given their first split-screen viewport
        if (!initalized && referenceCam.rect.x == viewPortRectDefault.x && referenceCam.rect.y == viewPortRectDefault.y
            && referenceCam.rect.width == viewPortRectDefault.z && referenceCam.rect.height == viewPortRectDefault.w)
            return;

        // Returns if the other cameras already match
        if (initalized && referenceCam.rect == followedRect)
            return;

        // Players joining or leaving resize the reference cam, so the other cameras follow every change
        foreach (Camera cam in camerasToFollow)
        {
            cam.rect = referenceCam.rect;
        }

        followedRect = referenceCam.rect;
        initalized = true;
    }

    ///<summary>
    /// Renders the phase camera at this player's view size in pixels, so the transition isn't stretched or drawn bigger than the view
    ///</summary>
    private void ResizePhaseTexture()
    {
        int width = Mathf.Max(1, referenceCam.pixelWidth);
        int height = Mathf.Max(1, referenceCam.pixelHeight);

        if (phaseCameraRT != null && phaseCameraRT.width == width && phaseCameraRT.height == height)
            return;

        ReleasePhaseTexture();

        phaseCameraRT = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        phaseCamera.targetTexture = phaseCameraRT;
        phaseTransitionMaterialMain.SetTexture(HashReference._mainTexProperty, phaseCameraRT);
    }

    ///<summary>
    /// Frees the phase camera's texture
    ///</summary>
    private void ReleasePhaseTexture()
    {
        if (phaseCameraRT == null)
            return;

        if (phaseCamera != null)
            phaseCamera.targetTexture = null;

        phaseCameraRT.Release();

        if (Application.isPlaying)
            Destroy(phaseCameraRT);
        else
            DestroyImmediate(phaseCameraRT);

        phaseCameraRT = null;
    }

    private void OnDestroy()
    {
        ReleasePhaseTexture();
    }

    ///<summary>
    /// Updates the cameras to render certain layers based on the player
    ///</summary>
    public void UpdateMainVirtualCameras(int nextFillSlotPass)
    {
        nextFillSlot = nextFillSlotPass;

        // Gets camera layer based on player
        if (nextFillSlot == 1)
        {
            cameraLayer = 17;
            playerWorldLayer = 10;
        }
        else if (nextFillSlot == 2)
        {
            cameraLayer = 18;
            playerWorldLayer = 11;
        }
        else if (nextFillSlot == 3)
        {
            cameraLayer = 19;
            playerWorldLayer = 12;
        }
        else if (nextFillSlot == 4)
        {
            cameraLayer = 20;
            playerWorldLayer = 13;
        }

        virtualCameraMain.layer = cameraLayer;

        // Add via bitwise to include the camera layer
        referenceCam.cullingMask |= (1 << playerWorldLayer);
        referenceCam.cullingMask |= (1 << cameraLayer);

        // Add via bitwise to include player world layer to phase cam
        phaseCamera.cullingMask |= (1 << playerWorldLayer);

        // Updates camera layers to be spesific player renderers
        referenceCamData.SetRenderer(nextFillSlot);
        phaseCamData.SetRenderer(0);

        // Sets the speed lines material for the player
        ballDriving.SetSpeedLinesMaterial(nextFillSlot);
    }

    ///<summary>
    /// Updates the cameras to render certain layers based on the player
    ///</summary>
    public void UpdateIconVirtualCameras(int nextFillSlot)
    {
        // Gets camera layer based on player
        if (nextFillSlot == 1)
            iconLayer = 24;
        else if (nextFillSlot == 2)
            iconLayer = 25;
        else if (nextFillSlot == 3)
            iconLayer = 26;
        else if (nextFillSlot == 4)
            iconLayer = 27;

        virtualCameraIcon.layer = iconLayer;

        //// Add via bitwise to include the camera layer
        iconCamera.cullingMask |= (1 << iconLayer);
    }

    public static void UpdatePlayerObjectLayer(List<GameObject> objectsToChange, int playerPos, Camera iconCamera)
    {
        int layer = 0;

        // Gets camera layer based on player
        if (playerPos == 0)
            layer = 24;// 10;
        else if (playerPos == 1)
            layer = 25;// 11;
        else if (playerPos == 2)
            layer = 26;// 12;
        else if (playerPos == 3)
            layer = 27;// 13;

        foreach (GameObject obj in objectsToChange)
        {
            obj.layer = layer;
        }

        // Update icon camera to not render self layer
        iconCamera.cullingMask &= ~(1 << layer);
    }

    ///<summary>
    /// Toggles to swap canvas on player. True sets menu canvas on
    ///</summary>
    public void SwapCanvas(bool menuCanvasOn)
    {
        if (menuCanvasOn)
        {
            menuUICamera.enabled = true;
            drivingUICamera.enabled = false;
            //drivingCanvas.SetActive(false);
        }
        else 
        {
            menuUICamera.enabled = false;
            drivingUICamera.enabled = true;
            //drivingCanvas.SetActive(true);
        }
    }

    ///<summary>
    /// Toggles to swap player camera rendering to and from phase camera
    ///</summary>
    public void SwapCameraRendering(bool mainCameraOn)
    {
        // handles cases where rendering may be set twice
        if(currentPhaseStatus == mainCameraOn)
            return;
        
        // Sets current phase status to be what camera mode is
        currentPhaseStatus = mainCameraOn;

        if (phaseCameraUpdateCoroutine != null)
            StopCoroutine(phaseCameraUpdateCoroutine);

        phaseCameraUpdateCoroutine = StartCoroutine(RenderOutPhaseCamera(mainCameraOn));

        // Phasing
        if (mainCameraOn)
        {
            referenceCamData.SetRenderer(nextFillSlot + 4);
            referenceCam.cullingMask = phasingMask;

            // Add via bitwise to include the phase layer
            referenceCam.cullingMask |= (1 << 8);
            cameraCollidder.m_AvoidObstacles = false;
        }
        // Normal
        else
        {
            referenceCamData.SetRenderer(nextFillSlot);
            referenceCam.cullingMask = drivingMask;
            cameraCollidder.m_AvoidObstacles = true;
        }

        // Add via bitwise to include the camera layer
        referenceCam.cullingMask |= (1 << playerWorldLayer);
        referenceCam.cullingMask |= (1 << cameraLayer);

        // Add via bitwise to include player world layer to phase cam
        phaseCamera.cullingMask |= (1 << playerWorldLayer);
    }

    ///<summary>
    /// Relocates the character customization ui to the left if player is even number (2 or 4)
    ///</summary>
    public void RelocateCustomizationMenu(int playerNumber)
    {
        // Even
        if(playerNumber % 2 == 0)
        {
            customizationSelector.transform.position = new Vector3(-customizationSelector.transform.position.x, customizationSelector.transform.position.y,
                customizationSelector.transform.position.z);
        }
    }

    /// <summary>
    /// Reparents the ui camera on the two cameras that can use it in a stack. (true is mainCamera, false is playerCamera)
    /// </summary>
    public void ReparentMenuCameraStack(bool posOfMenuCamera)
    {
        // Reparent to main camera
        if(posOfMenuCamera)
        {
            drivingUICamera.enabled = true;
            playerCamera.enabled = false;
            menuUICamera.enabled = false;

            try
            {
                playerCamera.GetUniversalAdditionalCameraData().cameraStack.RemoveAt(0);
                referenceCam.GetUniversalAdditionalCameraData().cameraStack.Add(menuUICamera);
            }
            catch
            {
            }
        }
        // Reparent to player camera
        else
        {
            drivingUICamera.enabled = false;
            playerCamera.enabled = true;
            menuUICamera.enabled = true;

            try
            {
                if (referenceCam.GetUniversalAdditionalCameraData().cameraStack.Count > 0)
                {
                    referenceCam.GetUniversalAdditionalCameraData().cameraStack.RemoveAt(2);
                }
            }
            catch
            {
            }

            playerCamera.GetUniversalAdditionalCameraData().cameraStack.Add(menuUICamera);
        }
    }

    /// <summary>
    /// Renders and calculates phase camera transition
    /// </summary>
    public IEnumerator RenderOutPhaseCamera(bool renderType)
    {
        // Reset Tex
        phaseTransitionMaterialMain.SetFloat("_Value", 0);

        phaseCameraUpdate = true;
        phaseCamera.enabled = true;
        phaseRender.SetActive(true);

        float waitTimeIncrement = phaseTransitionSpeed / shaderPassIncrements;
        for (float i = 0f; i <= shaderPassIncrements; i += 1f)
        {
            phaseTransitionMaterialMain.SetFloat("_Value", i / shaderPassIncrements);
            yield return new WaitForSeconds(waitTimeIncrement);
        }

        phaseRender.SetActive(false);
        phaseCamera.enabled = false;
        phaseCameraUpdate = false;

        // Phasing
        if (renderType)
        {
            phaseCamData.SetRenderer(nextFillSlot + 4);
            phaseCamera.cullingMask = phasingMask;
            // Add via bitwise to include the phase layer
            phaseCamera.cullingMask |= (1 << 8);
        }
        // Normal
        else
        {
            phaseCamData.SetRenderer(0);
            phaseCamera.cullingMask = drivingMask;
        }

        // Add via bitwise to include the camera layer
        phaseCamera.cullingMask |= (1 << cameraLayer);
    }

    /// <summary>
    /// Initalizes the company scooter rendering features
    /// </summary>
    public void InitalizeCompanyScooter(CompanyInformation companyInformation)
    {
        companyInfo = companyInformation;
        scooterModel.material = companyInformation.scooterColorMaterial;

        foreach(DecalProjector decal in logoDecal)
        {
            decal.material = companyInformation.scooterDecalMaterial;
        }

        drivingIndicators.SetDrivingIndicatorsToCompany(companyInfo.playerIndicatorSprites);
    }
}

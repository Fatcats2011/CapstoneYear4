using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

public class PlayerUIHandler : MonoBehaviour
{
    /// <summary>The UI map's Y (triangle) action, which reaches the menus through NorthFaceTrigger</summary>
    public const string NORTH_ACTION = "UI/North Face";

    public GameObject MenuCanvas;
    public MenuInteractions menuInteractions;
    public CustomizationSelector customizationSelector;
    public float scrollSpeed = 0.2f;

    public float uiDelayTime = 0.1f;
    [SerializeField] bool canInput = false;

    public UnityEvent<bool> NorthFaceEvent;
    private bool northFaceValue; //a bool representing the pushed state of the west face button (true for pushed, false for loose)
    public bool NorthFaceValue { get { return northFaceValue; } }

    public UnityEvent<bool> EastFaceEvent;
    private bool eastFaceValue; //a bool representing the pushed state of the west face button (true for pushed, false for loose)
    public bool EastFaceValue { get { return eastFaceValue; } }

    public UnityEvent<bool> SouthFaceEvent;
    private bool southFaceValue; //a bool representing the pushed state of the west face button (true for pushed, false for loose)
    public bool SouthFaceValue { get { return southFaceValue; } }

    public UnityEvent<bool> WestFaceEvent;
    private bool westFaceValue; //a bool representing the pushed state of the west face button (true for pushed, false for loose)
    public bool WestFaceValue { get { return westFaceValue; } }

    public UnityEvent<bool> LeftPadEvent;
    private bool leftPadValue; //a bool representing the pushed state of the left d-pad button (true for pushed, false for loose)
    public bool LeftPadValue { get { return leftPadValue; } }
    Coroutine leftPadCoroutine;

    public UnityEvent<bool> RightPadEvent;
    private bool rightPadValue; //a bool representing the pushed state of the right d-pad button (true for pushed, false for loose)
    public bool RightPadValue { get { return rightPadValue; } }
    Coroutine rightPadCoroutine;

    public UnityEvent<bool> UpPadEvent;
    private bool upPadValue; //a bool representing the pushed state of the up d-pad button (true for pushed, false for loose)
    public bool UpPadValue { get { return upPadValue; } }
    Coroutine upPadCoroutine;

    public UnityEvent<bool> DownPadEvent;
    private bool downPadValue; //a bool representing the pushed state of the down d-pad button (true for pushed, false for loose)
    public bool DownPadValue { get { return downPadValue; } }
    Coroutine downPadCoroutine;

    public UnityEvent<bool> StartPadEvent;
    private bool startPadValue; // A bool representing the pushed stage of the start button (true for pushed, false for loose)

    Coroutine disableInteraction;
    InputAction northFace; // wired here: the player prefab's event list has no Y

    private void OnEnable()
    {
        GameManager.Instance.OnSwapPlayerSelect += TriggerDisableInteraction;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapPlayerSelect -= TriggerDisableInteraction;
    }

    private void Start()
    {
        TriggerDisableInteraction();

        // Y (triangle) reaches the menus like the other buttons. By now every player has their own copy of the actions
        northFace = GetComponentInParent<PlayerInput>().actions.FindAction(NORTH_ACTION);
        if (northFace != null)
            northFace.performed += NorthFaceTrigger;
    }

    private void OnDestroy()
    {
        if (northFace != null)
            northFace.performed -= NorthFaceTrigger;
    }

    public void TriggerDisableInteraction()
    {
        if(disableInteraction != null)
            StopCoroutine(disableInteraction);

        disableInteraction = StartCoroutine(DisableInteraction());
    }

    /// <summary>
    /// Coroutine to disable interaction on main menu
    /// </summary>
    public IEnumerator DisableInteraction()
    {
        canInput = false;
        yield return new WaitForSeconds(uiDelayTime);
        canInput = true;
    }

    /// <summary>
    /// Takes input from the north face button (Y on Xbox)
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void NorthFaceTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        if (context.performed)
        {
            northFaceValue = context.ReadValueAsButton();
            NorthFaceEvent?.Invoke(northFaceValue);
        }
    }

    /// <summary>
    /// Takes input from the east face button (B on Xbox)
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void EastFaceTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        if (context.performed)
        {
            eastFaceValue = context.ReadValueAsButton();
            EastFaceEvent?.Invoke(eastFaceValue);
        }
    }

    /// <summary>
    /// Takes input from the south face button (A on Xbox)
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void SouthFaceTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        if (context.performed)
        {
            southFaceValue = context.ReadValueAsButton();
            SouthFaceEvent?.Invoke(southFaceValue);
        }
    }

    /// <summary>
    /// Takes input from the west face button (X on Xbox)
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void WestFaceTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        if (context.performed)
        {
            westFaceValue = context.ReadValueAsButton();
            WestFaceEvent?.Invoke(westFaceValue);
        }
    }

    /// <summary>
    /// Takes input from the left d-pad button
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void LeftPadTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        leftPadValue = context.ReadValueAsButton();

        if (context.performed)
        {
            LeftPadEvent?.Invoke(leftPadValue);

            if (leftPadCoroutine != null)
            {
                StopCoroutine(leftPadCoroutine);
                leftPadCoroutine = null;
            }

            leftPadCoroutine = StartCoroutine(ScrollPress(context, 0));
        }
    }

    /// <summary>
    /// Takes input from the right d-pad button
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void RightPadTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        rightPadValue = context.ReadValueAsButton();

        if (context.performed)
        {
            RightPadEvent?.Invoke(rightPadValue);

            if (rightPadCoroutine != null)
            {
                StopCoroutine(rightPadCoroutine);
                rightPadCoroutine = null;
            }

            rightPadCoroutine = StartCoroutine(ScrollPress(context, 1));
        }
    }

    /// <summary>
    /// Takes input from the up d-pad button
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void UpPadTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        upPadValue = context.ReadValueAsButton();

        if (context.performed)
        {
            UpPadEvent?.Invoke(upPadValue);

            if (upPadCoroutine != null)
            {
                StopCoroutine(upPadCoroutine);
                upPadCoroutine = null;
            }

            upPadCoroutine = StartCoroutine(ScrollPress(context, 2));
        }
    }

    /// <summary>
    /// Takes input from the down d-pad button
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void DownPadTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        downPadValue = context.ReadValueAsButton();

        if (context.performed)
        {
            DownPadEvent?.Invoke(downPadValue);

            if (downPadCoroutine != null)
            {
                StopCoroutine(downPadCoroutine);
                downPadCoroutine = null;
            }

            downPadCoroutine = StartCoroutine(ScrollPress(context, 3));
        }
    }

    // Enables the ability to scoll selection
    private IEnumerator ScrollPress(CallbackContext context, int buttonType)
    {
        yield return new WaitForSeconds(scrollSpeed);

        switch (buttonType)
        {
            case 0: // Left D-Pad
                if (leftPadValue == true)
                {
                    LeftPadTrigger(context);
                }
                break;
            case 1: // Right D-Pad
                if (rightPadValue == true)
                {
                    RightPadTrigger(context);
                }
                break;
            case 2: // Up D-Pad
                if (upPadValue == true)
                {
                    UpPadTrigger(context);
                }
                break;
            case 3: // Down D-Pad
                if (downPadValue == true)
                {
                    DownPadTrigger(context);
                }
                break;
        }
    }

    /// <summary>
    /// Takes input from the start button
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void StartPadTrigger(CallbackContext context)
    {
        // Disables input untill can input is true
        if (canInput == false)
            return;

        startPadValue = context.ReadValueAsButton();

        if (context.performed)
        {
            StartPadEvent?.Invoke(startPadValue);
        }
    }
}

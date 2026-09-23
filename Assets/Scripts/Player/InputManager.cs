using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using static UnityEngine.InputSystem.InputAction;

/// <summary>
/// Gets input from the controller and stores it in accessible variables.
/// Some of these are accessed constantly, some push events. Depends on the purpose of the control.
/// </summary>
public class InputManager : MonoBehaviour, IDriveInput
{
    // North Face Press
    public event Action<bool> NorthFaceEvent;
    private bool northFaceValue; //a bool representing the pushed state of the west face button (true for pushed, false for loose)
    public bool NorthFaceValue { get { return northFaceValue; } }

    // East Face Press
    public event Action<bool> EastFaceEvent;
    private bool eastFaceValue; //a bool representing the pushed state of the west face button (true for pushed, false for loose)
    public bool EastFaceValue { get { return eastFaceValue; } }

    // South Face Press
    public event Action<bool> SouthFaceEvent;
    private bool southFaceValue; //a bool representing the pushed state of the west face button (true for pushed, false for loose)
    public bool SouthFaceValue { get { return southFaceValue; } }

    // West Face Press
    public event Action<bool> WestFaceEvent;
    private bool westFaceValue; //a bool representing the pushed state of the west face button (true for pushed, false for loose)
    public bool WestFaceValue { get { return westFaceValue; } }

    // Right Stick Press
    private bool rightStickValue; //a bool representing the pushed state of the right stick button (true for pushed, false for loose)
    public bool RightStickValue { get { return rightStickValue; } }

    // Left Stick Movement
    private float leftStickValue; //a value from -1 to 1, which represents the horizontal position of the left stick
    public float LeftStickValue { get { return leftStickValue; } }

    // Right Stick X Movement
    private float rightStickXValue; //a value from -1 to 1, which represents the horizontal position of the right stick
    public float RightStickXValue { get { return rightStickXValue; } }

    // Right Stick Y Movement
    private float rightStickYValue; //a value from -1 to 1, which represents the horizontal position of the right stick
    public float RightStickYValue { get { return rightStickYValue; } }

    // Left Trigger
    private float leftTriggerValue; //a value from 0 to 1, which represents the pull of the left trigger
    public float LeftTriggerValue { get { return leftTriggerValue; } }

    // Right Trigger
    private float rightTriggerValue; //a value from 0 to 1, which represents the pull of the right trigger
    public float RightTriggerValue { get { return rightTriggerValue; } }

    // D-Pad for emotes
    public event Action<Vector2> DPadEvent;
    private Vector2 dpadValue; // a vector2 with x representing horizontal (1 right, -1 left) and y representing vertical (1 up, -1 down). returns (+-0.71, +-0.71) when 2 directions are pressed

    public UnityEvent<bool> StartPadEvent;
    private bool startPadValue; // A bool representing the pushed stage of the start button (true for pushed, false for loose)


    /// <summary>
    /// Takes input from the left stick's horizontal position, driven by Input Controller
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void LeftStickControl(CallbackContext context)
    {
        leftStickValue = context.ReadValue<float>();
    }

    /// <summary>
    /// Takes input from the right stick's horizontal position, driven by Input Controller
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void RightStickXControl(CallbackContext context)
    {
        rightStickXValue = context.ReadValue<float>();
    }

    /// <summary>
    /// Takes input from the right stick's horizontal position, driven by Input Controller
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void RightStickYControl(CallbackContext context)
    {
        rightStickYValue = context.ReadValue<float>();
    }

    /// <summary>
    /// Takes input from the north face button (Y on Xbox)
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void RightStickPress(CallbackContext context)
    {
        rightStickValue = context.ReadValueAsButton();
    }

    /// <summary>
    /// Takes input from the right trigger, driven by Input Controller
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void RightTriggerControl(CallbackContext context)
    {
        rightTriggerValue = context.ReadValue<float>();
    }

    /// <summary>
    /// Takes input from the left trigger, driven by Input Controller
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void LeftTriggerControl(CallbackContext context) 
    {
        leftTriggerValue = context.ReadValue<float>();
    }

    /// <summary>
    /// Takes input from the north face button (Y on Xbox)
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void NorthFaceTrigger(CallbackContext context)
    {
        northFaceValue = context.ReadValueAsButton();
        NorthFaceEvent(northFaceValue);
    }

    /// <summary>
    /// Takes input from the west face button (X on Xbox)
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void WestFaceTrigger(CallbackContext context) 
    {
        westFaceValue = context.ReadValueAsButton();
        WestFaceEvent(westFaceValue);
    }

    /// <summary>
    /// Takes input from the south face button (A on Xbox)
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void SouthFaceTrigger(CallbackContext context)
    {
        southFaceValue = context.ReadValueAsButton();
        SouthFaceEvent(southFaceValue);
    }

    /// <summary>
    /// Reads input from the DPad as a Vector2 and invokes the DPad event.
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void DPadPress(CallbackContext context)
    {
        dpadValue = context.ReadValue<Vector2>();
        if(dpadValue.x * dpadValue.x == 1f || dpadValue.y * dpadValue.y == 1f) // ensure only one direction is being pressed
        {
            DPadEvent(dpadValue);
        }
    }

    /// <summary>
    /// Takes input from the start button
    /// </summary>
    /// <param name="context">boilerplate for Input Controller</param>
    public void StartPadTrigger(CallbackContext context)
    {
        startPadValue = context.ReadValueAsButton();

        if (context.performed)
        {
            StartPadEvent?.Invoke(startPadValue);
        }
    }

    // What the scooter, emotes and camera read (IDriveInput). The callbacks above keep their names: the player prefab's
    // PlayerInput events call them
    float IDriveInput.Steer { get { return leftStickValue; } }
    float IDriveInput.Accelerate { get { return rightTriggerValue; } }
    float IDriveInput.Brake { get { return leftTriggerValue; } }
    float IDriveInput.CameraX { get { return rightStickXValue; } }
    float IDriveInput.CameraY { get { return rightStickYValue; } }
    bool IDriveInput.LookBehind { get { return rightStickValue; } }

    event Action<bool> IDriveInput.DriftButton { add { WestFaceEvent += value; } remove { WestFaceEvent -= value; } }
    event Action<bool> IDriveInput.BoostButton { add { SouthFaceEvent += value; } remove { SouthFaceEvent -= value; } }
    event Action<Vector2> IDriveInput.EmotePad { add { DPadEvent += value; } remove { DPadEvent -= value; } }
}
